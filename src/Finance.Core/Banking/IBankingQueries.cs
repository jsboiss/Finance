namespace Finance.Core.Banking;

public interface IBankingQueries
{
    Task<IReadOnlyList<AccountDto>> GetAccounts(CancellationToken cancellationToken);

    Task<AccountDto?> GetAccount(Guid accountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BalanceDto>> GetBalances(CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionDto>> GetTransactions(TransactionQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportRunDto>> GetImportRuns(CancellationToken cancellationToken);
}
