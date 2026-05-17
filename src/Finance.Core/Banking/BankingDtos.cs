namespace Finance.Core.Banking;

public sealed record AccountDto(Guid Id, string Name, string InstitutionName, string Currency, long? CurrentBalanceMinorUnits);

public sealed record BalanceDto(Guid AccountId, long? CurrentMinorUnits, string Currency, DateTimeOffset AsOf);

public sealed record TransactionDto(Guid Id, Guid AccountId, string Description, long AmountMinorUnits, string Currency, DateOnly PostedDate);

public sealed record ImportRunDto(Guid Id, string Source, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int ImportedCount, string? Error);

public sealed record OperationsStatusDto(int RedbarkRequestsToday, int RedbarkRequestsThisMonth, int RedbarkRequestsTotal, DateTimeOffset? LastRedbarkRequestAt);

public sealed record TransactionQuery(Guid? AccountId, DateOnly? From, DateOnly? To, string? Search, int Page, int PageSize, string? Sort);
