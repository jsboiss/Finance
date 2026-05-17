namespace Finance.Core.Banking;

public sealed record AccountDto(Guid Id, string Name, string CustomName, string AccountNumber, string DisplayName, string InstitutionName, string Currency, long? CurrentBalanceMinorUnits);

public sealed record BalanceDto(Guid AccountId, long? CurrentMinorUnits, string Currency, DateTimeOffset AsOf);

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    string ExternalTransactionId,
    string AccountName,
    string AccountNumber,
    string AccountDisplayName,
    string Description,
    string? MerchantName,
    string? MerchantCategoryCode,
    string Category,
    long AmountMinorUnits,
    string Currency,
    DateOnly PostedDate,
    DateTimeOffset? PostedAt,
    string Direction,
    string Status,
    IReadOnlyList<TransactionTagDto> Tags);

public sealed record TransactionTagDto(Guid Id, string Name, string Color);

public sealed record MerchantTagRuleDto(Guid Id, string MerchantName, TransactionTagDto Tag);

public sealed record CreateTransactionTagRequest(string Name, string? Color);

public sealed record UpdateTransactionTagsRequest(IReadOnlyList<Guid> TagIds);

public sealed record CreateMerchantTagRuleRequest(string MerchantName, Guid TagId);

public sealed record UpdateAccountRequest(string? CustomName);

public sealed record ImportRunDto(Guid Id, string Source, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int ImportedCount, string? Error);

public sealed record OperationsStatusDto(int RedbarkRequestsToday, int RedbarkRequestsThisMonth, int RedbarkRequestsTotal, DateTimeOffset? LastRedbarkRequestAt);

public sealed record TransactionQuery(Guid? AccountId, DateOnly? From, DateOnly? To, string? Search, int Page, int PageSize, string? Sort);

public sealed record SubscriptionDto(
    Guid Id,
    string Name,
    string MerchantName,
    string MerchantKey,
    string PaymentManager,
    string Cadence,
    long ExpectedAmountMinorUnits,
    string Currency,
    string Status,
    string? StatusOverride,
    bool IsCancelled,
    DateOnly? FirstPaymentDate,
    DateOnly? LastPaymentDate,
    DateOnly? NextExpectedPaymentDate,
    long TotalPaidMinorUnits,
    long MonthlyEstimateMinorUnits,
    long YearlyEstimateMinorUnits,
    IReadOnlyList<SubscriptionPriceChangeDto> PriceChanges);

public sealed record SubscriptionDetailDto(SubscriptionDto Subscription, IReadOnlyList<SubscriptionPaymentDto> Payments);

public sealed record SubscriptionPaymentDto(Guid TransactionId, string Description, string? MerchantName, long AmountMinorUnits, string Currency, DateOnly PostedDate);

public sealed record SubscriptionPriceChangeDto(DateOnly EffectiveDate, long PreviousAmountMinorUnits, long NewAmountMinorUnits, string Status);

public sealed record SubscriptionSuggestionDto(
    Guid Id,
    string MerchantName,
    string MerchantKey,
    string PaymentManager,
    string Cadence,
    long ExpectedAmountMinorUnits,
    string Currency,
    int Confidence,
    string Status,
    DateOnly FirstPaymentDate,
    DateOnly LastPaymentDate,
    DateOnly NextExpectedPaymentDate,
    IReadOnlyList<Guid> SampleTransactionIds);

public sealed record CreateSubscriptionRequest(string Name, string MerchantName, string? PaymentManager, string Cadence, long ExpectedAmountMinorUnits, string Currency, string? StatusOverride, bool IsCancelled);

public sealed record UpdateSubscriptionRequest(string Name, string MerchantName, string? PaymentManager, string Cadence, long ExpectedAmountMinorUnits, string Currency, string? StatusOverride, bool IsCancelled);
