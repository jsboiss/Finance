namespace Finance.Data.Redbark;

using Finance.Core.Abstractions;
using Finance.Core.Redbark;
using Finance.Data.Data;
using Microsoft.EntityFrameworkCore;

public sealed class RedbarkImportService(FinanceDbContext dbContext, IRedbarkClient redbarkClient) : IRedbarkImportService
{
    public Task Backfill(Guid tenantId, CancellationToken cancellationToken)
    {
        return ImportRange(tenantId, DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24)), DateOnly.FromDateTime(DateTime.UtcNow), "backfill", cancellationToken);
    }

    public Task ReconcileRecent(Guid tenantId, CancellationToken cancellationToken)
    {
        return ImportRange(tenantId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow), "recent-reconciliation", cancellationToken);
    }

    public Task ReconcileFull(Guid tenantId, CancellationToken cancellationToken)
    {
        return ImportRange(tenantId, DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24)), DateOnly.FromDateTime(DateTime.UtcNow), "full-reconciliation", cancellationToken);
    }

    public async Task ProcessWebhook(Guid tenantId, string eventId, string eventType, string rawJson, CancellationToken cancellationToken)
    {
        var exists = await dbContext.WebhookEvents.AnyAsync(x => x.TenantId == tenantId && x.ExternalEventId == eventId, cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.WebhookEvents.Add(new WebhookEvent { TenantId = tenantId, ExternalEventId = eventId, EventType = eventType, RawJson = rawJson });
        await dbContext.SaveChangesAsync(cancellationToken);
        await ReconcileRecent(tenantId, cancellationToken);
        await dbContext.WebhookEvents.Where(x => x.TenantId == tenantId && x.ExternalEventId == eventId).ExecuteUpdateAsync(x => x.SetProperty(y => y.ProcessedAt, DateTimeOffset.UtcNow), cancellationToken);
    }

    private async Task ImportRange(Guid tenantId, DateOnly from, DateOnly to, string source, CancellationToken cancellationToken)
    {
        var run = new ImportRun { TenantId = tenantId, Source = source };
        dbContext.ImportRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var importedCount = 0;
            var connections = await redbarkClient.GetConnections(tenantId, cancellationToken);
            foreach (var connection in connections)
            {
                var bankConnection = await UpsertConnection(tenantId, connection, cancellationToken);
                var accounts = await redbarkClient.GetAccounts(tenantId, connection.Id, cancellationToken);
                var bankAccountsByExternalId = new Dictionary<string, BankAccount>();
                foreach (var account in accounts)
                {
                    var bankAccount = await UpsertAccount(tenantId, bankConnection.Id, account, cancellationToken);
                    bankAccountsByExternalId[account.Id] = bankAccount;

                    string? cursor = null;
                    do
                    {
                        var page = await redbarkClient.GetTransactions(tenantId, connection.Id, account.Id, from, to, cursor, cancellationToken);
                        foreach (var transaction in page.Transactions.Where(x => x.Status.Equals("posted", StringComparison.OrdinalIgnoreCase)))
                        {
                            importedCount += await UpsertTransaction(tenantId, bankAccount.Id, transaction, cancellationToken) ? 1 : 0;
                        }

                        cursor = page.NextCursor;
                    }
                    while (!string.IsNullOrWhiteSpace(cursor));
                }

                var balances = await redbarkClient.GetBalances(tenantId, accounts.Select(x => x.Id).ToList(), cancellationToken);
                foreach (var balance in balances)
                {
                    if (bankAccountsByExternalId.TryGetValue(balance.AccountId, out var bankAccount))
                    {
                        await UpsertBalance(tenantId, bankAccount.Id, balance, cancellationToken);
                    }
                }
            }

            run.ImportedCount = importedCount;
            run.Status = "completed";
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.Error = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<BankConnection> UpsertConnection(Guid tenantId, RedbarkConnectionDto connection, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BankConnections.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalConnectionId == connection.Id, cancellationToken);
        if (entity is null)
        {
            entity = new BankConnection { TenantId = tenantId, ExternalConnectionId = connection.Id };
            dbContext.BankConnections.Add(entity);
        }

        entity.InstitutionName = connection.InstitutionName;
        entity.RawJson = connection.Raw;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<BankAccount> UpsertAccount(Guid tenantId, Guid connectionId, RedbarkAccountDto account, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BankAccounts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalAccountId == account.Id, cancellationToken);
        if (entity is null)
        {
            entity = new BankAccount { TenantId = tenantId, ExternalAccountId = account.Id };
            dbContext.BankAccounts.Add(entity);
        }

        entity.BankConnectionId = connectionId;
        entity.Name = account.Name;
        entity.Currency = account.Currency;
        entity.RawJson = account.Raw;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task UpsertBalance(Guid tenantId, Guid accountId, RedbarkBalanceDto balance, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Balances.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BankAccountId == accountId && x.AsOf == balance.AsOf, cancellationToken);
        if (entity is null)
        {
            entity = new Balance { TenantId = tenantId, BankAccountId = accountId, AsOf = balance.AsOf };
            dbContext.Balances.Add(entity);
        }

        entity.CurrentMinorUnits = balance.CurrentMinorUnits;
        entity.Currency = balance.Currency;
        entity.RawJson = balance.Raw;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> UpsertTransaction(Guid tenantId, Guid accountId, RedbarkTransactionDto transaction, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BankTransactions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalTransactionId == transaction.Id, cancellationToken);
        var isNew = entity is null;
        if (entity is null)
        {
            entity = new BankTransaction { TenantId = tenantId, ExternalTransactionId = transaction.Id };
            dbContext.BankTransactions.Add(entity);
        }

        entity.BankAccountId = accountId;
        entity.Description = transaction.Description;
        entity.AmountMinorUnits = transaction.AmountMinorUnits;
        entity.Currency = transaction.Currency;
        entity.PostedDate = transaction.PostedDate;
        entity.RawJson = transaction.Raw;
        await dbContext.SaveChangesAsync(cancellationToken);
        return isNew;
    }
}
