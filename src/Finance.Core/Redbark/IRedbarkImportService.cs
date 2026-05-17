namespace Finance.Core.Redbark;

public interface IRedbarkImportService
{
    Task Backfill(Guid tenantId, CancellationToken cancellationToken);

    Task ReconcileRecent(Guid tenantId, CancellationToken cancellationToken);

    Task ReconcileFull(Guid tenantId, CancellationToken cancellationToken);

    Task ProcessWebhook(Guid tenantId, string eventId, string eventType, string rawJson, CancellationToken cancellationToken);
}
