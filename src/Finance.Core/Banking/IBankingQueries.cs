namespace Finance.Core.Banking;

public interface IBankingQueries
{
    Task<IReadOnlyList<AccountDto>> GetAccounts(CancellationToken cancellationToken);

    Task<AccountDto?> GetAccount(Guid accountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BalanceDto>> GetBalances(CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionDto>> GetTransactions(TransactionQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionTagDto>> GetTags(CancellationToken cancellationToken);

    Task<TransactionTagDto> CreateTag(CreateTransactionTagRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteTag(Guid tagId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionTagDto>> SetTransactionTags(Guid transactionId, UpdateTransactionTagsRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MerchantTagRuleDto>> GetMerchantTagRules(CancellationToken cancellationToken);

    Task<MerchantTagRuleDto> CreateMerchantTagRule(CreateMerchantTagRuleRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteMerchantTagRule(Guid ruleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportRunDto>> GetImportRuns(CancellationToken cancellationToken);

    Task<OperationsStatusDto> GetOperationsStatus(CancellationToken cancellationToken);
}
