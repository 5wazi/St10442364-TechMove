using PROG7313_TechMove.Models;

namespace PROG7313_TechMove.Patterns.Repository
{
    public interface IContractRepository
    {
        Task<IEnumerable<Contract>> GetAllAsync();
        Task<IEnumerable<Contract>> SearchAsync(DateTime? fromDate, DateTime? toDate, ContractStatus? status);
        Task<Contract?> GetByIdAsync(int id);
        Task AddAsync(Contract contract);
        Task UpdateAsync(Contract contract);
        Task<bool> ExistsAsync(int id);
    }

    public interface IClientRepository
    {
        Task<IEnumerable<Client>> GetAllAsync();
        Task<Client?> GetByIdAsync(int id);
        Task AddAsync(Client client);
    }

    public interface IServiceRequestRepository
    {
        Task<IEnumerable<ServiceRequest>> GetByContractIdAsync(int contractId);
        Task<ServiceRequest?> GetByIdAsync(int id);
        Task AddAsync(ServiceRequest request);
        Task UpdateAsync(ServiceRequest request);
    }
}
