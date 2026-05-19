namespace Finance.Core.Banking;

public interface IBankingQueries
{
    Task<IReadOnlyList<AccountDto>> GetAccounts(CancellationToken cancellationToken);

    Task<AccountDto?> GetAccount(Guid accountId, CancellationToken cancellationToken);

    Task<AccountDto?> UpdateAccount(Guid accountId, UpdateAccountRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BalanceDto>> GetBalances(CancellationToken cancellationToken);

    Task<OverviewDto> GetOverview(Guid? accountId, bool? includeInternalTransfers, CancellationToken cancellationToken);

    Task<IReadOnlyList<OverviewDailyCashFlowDto>> GetDailyCashFlow(Guid? accountId, bool? includeInternalTransfers, string? range, CancellationToken cancellationToken);

    Task<IReadOnlyList<OverviewMetricSnapshotDto>> GetAverageDailySpendHistory(Guid? accountId, bool? includeInternalTransfers, CancellationToken cancellationToken);

    Task<SavingsTrajectoryDto?> GetSavingsTrajectory(Guid accountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionDto>> GetTransactions(TransactionQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<PayBreakdownProfileDto>> GetPayBreakdownProfiles(CancellationToken cancellationToken);

    Task<PayBreakdownProfileDto?> GetPayBreakdownProfile(Guid profileId, CancellationToken cancellationToken);

    Task<PayBreakdownProfileDto> CreatePayBreakdownProfile(CreatePayBreakdownProfileRequest request, CancellationToken cancellationToken);

    Task<PayBreakdownProfileDto?> UpdatePayBreakdownProfile(Guid profileId, UpdatePayBreakdownProfileRequest request, CancellationToken cancellationToken);

    Task<bool> DeletePayBreakdownProfile(Guid profileId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BudgetProfileDto>> GetBudgetProfiles(CancellationToken cancellationToken);

    Task<BudgetProfileDto> CreateBudgetProfile(CreateBudgetProfileRequest request, CancellationToken cancellationToken);

    Task<BudgetProfileDto?> UpdateBudgetProfile(Guid profileId, UpdateBudgetProfileRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteBudgetProfile(Guid profileId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionTagDto>> GetTags(CancellationToken cancellationToken);

    Task<TransactionTagDto> CreateTag(CreateTransactionTagRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteTag(Guid tagId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionTagDto>> SetTransactionTags(Guid transactionId, UpdateTransactionTagsRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MerchantTagRuleDto>> GetMerchantTagRules(CancellationToken cancellationToken);

    Task<MerchantTagRuleDto> CreateMerchantTagRule(CreateMerchantTagRuleRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteMerchantTagRule(Guid ruleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportRunDto>> GetImportRuns(CancellationToken cancellationToken);

    Task<OperationsStatusDto> GetOperationsStatus(CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionDto>> GetSubscriptions(CancellationToken cancellationToken);

    Task<SubscriptionDetailDto?> GetSubscription(Guid subscriptionId, CancellationToken cancellationToken);

    Task<SubscriptionDto> CreateSubscription(CreateSubscriptionRequest request, CancellationToken cancellationToken);

    Task<SubscriptionDto?> UpdateSubscription(Guid subscriptionId, UpdateSubscriptionRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteSubscription(Guid subscriptionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSuggestionDto>> GetSubscriptionSuggestions(CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSuggestionDto>> RefreshSubscriptionSuggestions(CancellationToken cancellationToken);

    Task<SubscriptionDto?> AcceptSubscriptionSuggestion(Guid suggestionId, CancellationToken cancellationToken);

    Task<bool> DismissSubscriptionSuggestion(Guid suggestionId, CancellationToken cancellationToken);
}
