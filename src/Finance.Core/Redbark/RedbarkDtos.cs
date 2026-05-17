namespace Finance.Core.Redbark;

using System.Text.Json;

public sealed record RedbarkConnectionDto(string Id, string InstitutionName, JsonDocument Raw);

public sealed record RedbarkAccountDto(string Id, string ConnectionId, string Name, string Currency, JsonDocument Raw);

public sealed record RedbarkBalanceDto(string AccountId, long? CurrentMinorUnits, string Currency, DateTimeOffset AsOf, JsonDocument Raw);

public sealed record RedbarkTransactionDto(string Id, string AccountId, string Description, long AmountMinorUnits, string Currency, DateOnly PostedDate, string Status, JsonDocument Raw);

public sealed record RedbarkTransactionPage(IReadOnlyList<RedbarkTransactionDto> Transactions, string? NextCursor);
