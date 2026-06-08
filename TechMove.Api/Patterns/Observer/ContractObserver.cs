using TechMove.Api.Models;

namespace TechMove.Api.Patterns.Observer
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

    public class AuditLogObserver : IContractObserver
    {
        private readonly ILogger<AuditLogObserver> _logger;

        public AuditLogObserver(ILogger<AuditLogObserver> logger)
        {
            _logger = logger;
        }

        public Task OnContractStatusChangedAsync(ContractStatusChangedEvent contractEvent)
        {
            _logger.LogInformation(
                "[AUDIT] Contract {ContractId} status changed from {OldStatus} to {NewStatus} at {ChangedAt}",
                contractEvent.ContractId,
                contractEvent.OldStatus,
                contractEvent.NewStatus,
                contractEvent.ChangedAt);

            return Task.CompletedTask;
        }
    }
}