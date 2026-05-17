namespace Finance.Core.Abstractions;

using Finance.Core.Redbark;

public interface IRedbarkClient
{
    Task<IReadOnlyList<RedbarkConnectionDto>> GetConnections(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RedbarkAccountDto>> GetAccounts(Guid tenantId, string connectionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RedbarkBalanceDto>> GetBalances(Guid tenantId, IReadOnlyList<string> accountIds, CancellationToken cancellationToken);

    Task<RedbarkTransactionPage> GetTransactions(Guid tenantId, string connectionId, string accountId, DateOnly from, DateOnly to, string? cursor, CancellationToken cancellationToken);
}
