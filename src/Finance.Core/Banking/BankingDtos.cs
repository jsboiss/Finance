namespace Finance.Core.Banking;

public sealed record AccountDto(Guid Id, string Name, string AccountNumber, string DisplayName, string InstitutionName, string Currency, long? CurrentBalanceMinorUnits);

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
    string Status);

public sealed record ImportRunDto(Guid Id, string Source, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int ImportedCount, string? Error);

public sealed record OperationsStatusDto(int RedbarkRequestsToday, int RedbarkRequestsThisMonth, int RedbarkRequestsTotal, DateTimeOffset? LastRedbarkRequestAt);

public sealed record TransactionQuery(Guid? AccountId, DateOnly? From, DateOnly? To, string? Search, int Page, int PageSize, string? Sort);
