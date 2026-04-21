using PROG7313_TechMove.Models;

namespace PROG7313_TechMove.Patterns.Observer
{
    public interface IContractObserver
    {
        Task OnContractStatusChangedAsync(ContractStatusChangedEvent contractEvent);
    }

    public class ContractStatusChangedEvent
    {
        public int ContractId { get; init; }
        public ContractStatus OldStatus { get; init; }
        public ContractStatus NewStatus { get; init; }
        public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
    }
}
