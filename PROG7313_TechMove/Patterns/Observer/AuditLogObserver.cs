namespace PROG7313_TechMove.Patterns.Observer
{
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
