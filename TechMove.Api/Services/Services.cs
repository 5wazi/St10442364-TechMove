using TechMove.Api.Models;
using TechMove.Api.Patterns.Factory;
using TechMove.Api.Patterns.Observer;
using TechMove.Api.Patterns.Repository;

namespace TechMove.Api.Services
{
    // ── Currency Service ──────────────────────────────────────────────────────

    public interface ICurrencyService
    {
        Task<decimal> GetUsdToZarRateAsync();
        Task<(decimal zarAmount, decimal rateUsed)> ConvertUsdToZarAsync(decimal usdAmount);
    }

    public class CurrencyService : ICurrencyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CurrencyService> _logger;
        private const decimal FallbackRate = 18.50m;
        private const string ApiUrl = "https://open.er-api.com/v6/latest/USD";

        public CurrencyService(HttpClient httpClient, ILogger<CurrencyService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<decimal> GetUsdToZarRateAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(ApiUrl);
                if (response?.Rates != null && response.Rates.TryGetValue("ZAR", out var rate))
                    return rate;

                _logger.LogWarning("ZAR rate not found in API response. Using fallback rate {Rate}.", FallbackRate);
                return FallbackRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Currency API unreachable. Using fallback rate {Rate}.", FallbackRate);
                return FallbackRate;
            }
        }

        public async Task<(decimal zarAmount, decimal rateUsed)> ConvertUsdToZarAsync(decimal usdAmount)
        {
            var rate = await GetUsdToZarRateAsync();
            return (Math.Round(usdAmount * rate, 2), rate);
        }

        private class ExchangeRateResponse
        {
            public Dictionary<string, decimal>? Rates { get; set; }
        }
    }

    // ── File Service ──────────────────────────────────────────────────────────

    public interface IFileService
    {
        Task<(string filePath, string fileName)> SaveSignedAgreementAsync(IFormFile file);
        string GetPhysicalPath(string storedPath);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileService> _logger;
        private const string UploadFolder = "uploads/agreements";
        private static readonly string[] AllowedExtensions = { ".pdf" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024;

        public FileService(IWebHostEnvironment env, ILogger<FileService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<(string filePath, string fileName)> SaveSignedAgreementAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("No file was uploaded.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException($"Only PDF files are allowed. You uploaded: '{extension}'.");

            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException("File size exceeds the 10 MB limit.");

            var uploadDir = Path.Combine(_env.WebRootPath ?? "wwwroot", UploadFolder);
            Directory.CreateDirectory(uploadDir);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var physicalPath = Path.Combine(uploadDir, uniqueFileName);

            await using var stream = new FileStream(physicalPath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation("Saved signed agreement: {FileName} → {Path}", file.FileName, physicalPath);
            return ($"/{UploadFolder}/{uniqueFileName}", file.FileName);
        }

        public string GetPhysicalPath(string storedPath)
            => Path.Combine(_env.WebRootPath ?? "wwwroot", storedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    // ── Contract Service ──────────────────────────────────────────────────────

    public interface IContractService
    {
        Task<Contract> CreateContractAsync(int clientId, DateTime startDate, DateTime endDate,
            string serviceLevel, IFormFile? signedAgreement);
        Task ChangeStatusAsync(int contractId, ContractStatus newStatus);
        Task<IEnumerable<Contract>> SearchContractsAsync(DateTime? from, DateTime? to, ContractStatus? status);
    }

    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepo;
        private readonly IContractFactory _factory;
        private readonly IFileService _fileService;
        private readonly IEnumerable<IContractObserver> _observers;
        private readonly ILogger<ContractService> _logger;

        public ContractService(
            IContractRepository contractRepo,
            IContractFactory factory,
            IFileService fileService,
            IEnumerable<IContractObserver> observers,
            ILogger<ContractService> logger)
        {
            _contractRepo = contractRepo;
            _factory = factory;
            _fileService = fileService;
            _observers = observers;
            _logger = logger;
        }

        public async Task<Contract> CreateContractAsync(
            int clientId, DateTime startDate, DateTime endDate,
            string serviceLevel, IFormFile? signedAgreement)
        {
            var contract = _factory.CreateContract(clientId, startDate, endDate, serviceLevel);

            if (signedAgreement != null && signedAgreement.Length > 0)
            {
                var (path, name) = await _fileService.SaveSignedAgreementAsync(signedAgreement);
                contract.SignedAgreementPath = path;
                contract.SignedAgreementFileName = name;
            }

            await _contractRepo.AddAsync(contract);
            _logger.LogInformation("Contract {Id} created for ClientId {ClientId}.", contract.Id, clientId);
            return contract;
        }

        public async Task ChangeStatusAsync(int contractId, ContractStatus newStatus)
        {
            var contract = await _contractRepo.GetByIdAsync(contractId)
                ?? throw new KeyNotFoundException($"Contract {contractId} not found.");

            var oldStatus = contract.Status;
            contract.Status = newStatus;
            await _contractRepo.UpdateAsync(contract);

            var evt = new ContractStatusChangedEvent
            {
                ContractId = contractId,
                OldStatus = oldStatus,
                NewStatus = newStatus
            };

            foreach (var observer in _observers)
                await observer.OnContractStatusChangedAsync(evt);
        }

        public async Task<IEnumerable<Contract>> SearchContractsAsync(
            DateTime? from, DateTime? to, ContractStatus? status)
            => await _contractRepo.SearchAsync(from, to, status);
    }

    // ── Service Request Service ───────────────────────────────────────────────

    public interface IServiceRequestService
    {
        Task<ServiceRequest> CreateAsync(int contractId, string description, decimal costUsd);
        Task<IEnumerable<ServiceRequest>> GetByContractAsync(int contractId);
        Task<ServiceRequest?> GetByIdAsync(int id);
    }

    public class ServiceRequestService : IServiceRequestService
    {
        private readonly IServiceRequestRepository _srRepo;
        private readonly IContractRepository _contractRepo;
        private readonly ICurrencyService _currencyService;
        private readonly ILogger<ServiceRequestService> _logger;

        public ServiceRequestService(
            IServiceRequestRepository srRepo,
            IContractRepository contractRepo,
            ICurrencyService currencyService,
            ILogger<ServiceRequestService> logger)
        {
            _srRepo = srRepo;
            _contractRepo = contractRepo;
            _currencyService = currencyService;
            _logger = logger;
        }

        public async Task<ServiceRequest> CreateAsync(int contractId, string description, decimal costUsd)
        {
            var contract = await _contractRepo.GetByIdAsync(contractId)
                ?? throw new KeyNotFoundException($"Contract {contractId} not found.");

            if (contract.Status == ContractStatus.Expired || contract.Status == ContractStatus.OnHold)
                throw new InvalidOperationException(
                    $"Service requests cannot be created for a contract with status '{contract.Status}'. " +
                    "Only Active or Draft contracts are allowed.");

            var (zarAmount, rate) = await _currencyService.ConvertUsdToZarAsync(costUsd);

            var sr = new ServiceRequest
            {
                ContractId = contractId,
                Description = description,
                CostUsd = costUsd,
                CostZar = zarAmount,
                ExchangeRateUsed = rate,
                Status = ServiceRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _srRepo.AddAsync(sr);
            _logger.LogInformation(
                "ServiceRequest {Id} created. USD {Usd} → ZAR {Zar} (rate: {Rate}).",
                sr.Id, costUsd, zarAmount, rate);

            return sr;
        }

        public async Task<IEnumerable<ServiceRequest>> GetByContractAsync(int contractId)
            => await _srRepo.GetByContractIdAsync(contractId);

        public async Task<ServiceRequest?> GetByIdAsync(int id)
            => await _srRepo.GetByIdAsync(id);
    }
}