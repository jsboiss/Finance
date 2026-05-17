namespace Finance.Core.Redbark;

public interface IRedbarkImportService
{
    Task DiscoverAccounts(Guid tenantId, CancellationToken cancellationToken);

    Task Backfill(Guid tenantId, CancellationToken cancellationToken);

    Task BackfillAccount(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    Task ReconcileRecent(Guid tenantId, CancellationToken cancellationToken);

    Task ReconcileFull(Guid tenantId, CancellationToken cancellationToken);

    Task ProcessWebhook(Guid tenantId, string eventId, string eventType, string rawJson, CancellationToken cancellationToken);
}
