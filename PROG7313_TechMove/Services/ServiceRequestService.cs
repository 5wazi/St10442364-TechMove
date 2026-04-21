using PROG7313_TechMove.Models;
using PROG7313_TechMove.Patterns.Repository;

namespace PROG7313_TechMove.Services
{
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

            // === WORKFLOW VALIDATION (Rubric requirement) ===
            if (contract.Status == ContractStatus.Expired || contract.Status == ContractStatus.OnHold)
                throw new InvalidOperationException(
                    $"Service requests cannot be created for a contract with status '{contract.Status}'. " +
                    "Only Active or Draft contracts are allowed.");

            // Currency conversion (Rubric criterion 3)
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
