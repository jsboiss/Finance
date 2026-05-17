namespace Finance.IntegrationTests;

using Finance.Core.Abstractions;
using Finance.Core.Redbark;

public sealed class FakeRedbarkClient : IRedbarkClient
{
    public Task<IReadOnlyList<RedbarkConnectionDto>> GetConnections(Guid tenantId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<RedbarkConnectionDto>>([new("conn-1", "Redbark Bank", System.Text.Json.JsonDocument.Parse("{}"))]);
    }

    public Task<IReadOnlyList<RedbarkAccountDto>> GetAccounts(Guid tenantId, string connectionId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<RedbarkAccountDto>>([new("acct-1", connectionId, "Everyday", "AUD", System.Text.Json.JsonDocument.Parse("{}"))]);
    }

    public Task<IReadOnlyList<RedbarkBalanceDto>> GetBalances(Guid tenantId, IReadOnlyList<string> accountIds, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<RedbarkBalanceDto>>(accountIds.Select(x => new RedbarkBalanceDto(x, 12345, "AUD", DateTimeOffset.UtcNow, System.Text.Json.JsonDocument.Parse("{}"))).ToList());
    }

    public Task<RedbarkTransactionPage> GetTransactions(Guid tenantId, string connectionId, string accountId, DateOnly from, DateOnly to, string? cursor, CancellationToken cancellationToken)
    {
        var transactions = new[]
        {
            new RedbarkTransactionDto("txn-posted", accountId, "Coffee", -550, "AUD", DateOnly.FromDateTime(DateTime.UtcNow), "posted", System.Text.Json.JsonDocument.Parse("{}")),
            new RedbarkTransactionDto("txn-pending", accountId, "Pending", -1000, "AUD", DateOnly.FromDateTime(DateTime.UtcNow), "pending", System.Text.Json.JsonDocument.Parse("{}"))
        };
        return Task.FromResult(new RedbarkTransactionPage(transactions, null));
    }
}
