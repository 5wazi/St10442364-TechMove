using Microsoft.EntityFrameworkCore;
using PROG7313_TechMove.Data;
using PROG7313_TechMove.Models;

namespace PROG7313_TechMove.Patterns.Repository
{
    // Contract Repository
    public class ContractRepository : IContractRepository
    {
        private readonly ApplicationDbContext _db;
        public ContractRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<Contract>> GetAllAsync()
            => await _db.Contracts
                .Include(c => c.Client)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

        /// <summary>
        /// Search/Filter with LINQ (rubric criterion 2): filter by date range and status.
        /// </summary>
        public async Task<IEnumerable<Contract>> SearchAsync(
            DateTime? fromDate,
            DateTime? toDate,
            ContractStatus? status)
        {
            var query = _db.Contracts.Include(c => c.Client).AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(c => c.StartDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(c => c.EndDate <= toDate.Value);

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            return await query.OrderByDescending(c => c.StartDate).ToListAsync();
        }

        public async Task<Contract?> GetByIdAsync(int id)
            => await _db.Contracts
                .Include(c => c.Client)
                .Include(c => c.ServiceRequests)
                .FirstOrDefaultAsync(c => c.Id == id);

        public async Task AddAsync(Contract contract)
        {
            await _db.Contracts.AddAsync(contract);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Contract contract)
        {
            _db.Contracts.Update(contract);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(int id)
            => await _db.Contracts.AnyAsync(c => c.Id == id);
    }

    // Client Repository 
    public class ClientRepository : IClientRepository
    {
        private readonly ApplicationDbContext _db;
        public ClientRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<Client>> GetAllAsync()
            => await _db.Clients
                .Include(c => c.Contracts)
                .OrderBy(c => c.Name)
                .ToListAsync();

        public async Task<Client?> GetByIdAsync(int id)
            => await _db.Clients
                .Include(c => c.Contracts)
                .FirstOrDefaultAsync(c => c.Id == id);

        public async Task AddAsync(Client client)
        {
            await _db.Clients.AddAsync(client);
            await _db.SaveChangesAsync();
        }
    }

    // ServiceRequest Repository
    public class ServiceRequestRepository : IServiceRequestRepository
    {
        private readonly ApplicationDbContext _db;
        public ServiceRequestRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<ServiceRequest>> GetByContractIdAsync(int contractId)
            => await _db.ServiceRequests
                .Where(sr => sr.ContractId == contractId)
                .Include(sr => sr.Contract)
                .OrderByDescending(sr => sr.CreatedAt)
                .ToListAsync();

        public async Task<ServiceRequest?> GetByIdAsync(int id)
            => await _db.ServiceRequests
                .Include(sr => sr.Contract)
                .FirstOrDefaultAsync(sr => sr.Id == id);

        public async Task AddAsync(ServiceRequest request)
        {
            await _db.ServiceRequests.AddAsync(request);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(ServiceRequest request)
        {
            _db.ServiceRequests.Update(request);
            await _db.SaveChangesAsync();
        }
    }
}
