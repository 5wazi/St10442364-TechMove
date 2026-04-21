using PROG7313_TechMove.Models;
using PROG7313_TechMove.Patterns.Factory;
using PROG7313_TechMove.Patterns.Observer;
using PROG7313_TechMove.Patterns.Repository;

namespace PROG7313_TechMove.Services
{
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
            // Use Factory to create the contract with correct defaults
            var contract = _factory.CreateContract(clientId, startDate, endDate, serviceLevel);

            // Handle file upload if provided
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

            // Notify all observers (Observer Pattern)
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
}
