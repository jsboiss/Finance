namespace Finance.Data.Data;

using System.Text.Json;

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class TenantMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "member";
}

public sealed class ApiClient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class BankConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ExternalConnectionId { get; set; } = "";
    public string InstitutionName { get; set; } = "";
    public JsonDocument RawJson { get; set; } = JsonDocument.Parse("{}");
}

public sealed class RedbarkConnectionAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ExternalConnectionId { get; set; } = "";
    public string InstitutionName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BankAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BankConnectionId { get; set; }
    public string ExternalAccountId { get; set; } = "";
    public string Name { get; set; } = "";
    public string CustomName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string Currency { get; set; } = "AUD";
    public JsonDocument RawJson { get; set; } = JsonDocument.Parse("{}");
}

public sealed class Balance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BankAccountId { get; set; }
    public long? CurrentMinorUnits { get; set; }
    public string Currency { get; set; } = "AUD";
    public DateTimeOffset AsOf { get; set; }
    public JsonDocument RawJson { get; set; } = JsonDocument.Parse("{}");
}

public sealed class BankTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BankAccountId { get; set; }
    public string ExternalTransactionId { get; set; } = "";
    public string ExternalAccountName { get; set; } = "";
    public string Description { get; set; } = "";
    public string? MerchantName { get; set; }
    public string? MerchantCategoryCode { get; set; }
    public string Category { get; set; } = "Uncategorized";
    public long AmountMinorUnits { get; set; }
    public string Currency { get; set; } = "AUD";
    public DateOnly PostedDate { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public string Direction { get; set; } = "";
    public string Status { get; set; } = "posted";
    public JsonDocument RawJson { get; set; } = JsonDocument.Parse("{}");
}

public sealed class TransactionTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#64748b";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BankTransactionTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BankTransactionId { get; set; }
    public Guid TransactionTagId { get; set; }
    public string Source { get; set; } = "manual";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OverviewMetricSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? BankAccountId { get; set; }
    public string ScopeKey { get; set; } = "";
    public DateOnly SnapshotDate { get; set; }
    public long AverageDailySpendMinorUnits { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PayBreakdownProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public Guid MainAccountId { get; set; }
    public Guid? SavingsAccountId { get; set; }
    public long FortnightlyPayMinorUnits { get; set; }
    public string Currency { get; set; } = "AUD";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BudgetProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public long WeeklyLimitMinorUnits { get; set; }
    public string Currency { get; set; } = "AUD";
    public string CategoryMatchers { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BudgetProfileTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BudgetProfileId { get; set; }
    public Guid TransactionTagId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MerchantTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string MerchantName { get; set; } = "";
    public string MerchantKey { get; set; } = "";
    public Guid TransactionTagId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string MerchantName { get; set; } = "";
    public string MerchantKey { get; set; } = "";
    public string PaymentManager { get; set; } = "direct";
    public string Cadence { get; set; } = "monthly";
    public long ExpectedAmountMinorUnits { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? StatusOverride { get; set; }
    public bool IsCancelled { get; set; }
    public DateOnly? FirstPaymentDate { get; set; }
    public DateOnly? LastPaymentDate { get; set; }
    public DateOnly? NextExpectedPaymentDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SubscriptionTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid BankTransactionId { get; set; }
    public int MatchConfidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SubscriptionSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string MerchantName { get; set; } = "";
    public string MerchantKey { get; set; } = "";
    public string PaymentManager { get; set; } = "direct";
    public string Cadence { get; set; } = "monthly";
    public long ExpectedAmountMinorUnits { get; set; }
    public string Currency { get; set; } = "AUD";
    public int Confidence { get; set; }
    public string Status { get; set; } = "pending";
    public string SampleTransactionIds { get; set; } = "[]";
    public DateOnly FirstPaymentDate { get; set; }
    public DateOnly LastPaymentDate { get; set; }
    public DateOnly NextExpectedPaymentDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ExternalEventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string RawJson { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public sealed class ImportRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Source { get; set; } = "";
    public string Status { get; set; } = "running";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int ImportedCount { get; set; }
    public string? Error { get; set; }
}

public sealed class RedbarkRequestLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public int? StatusCode { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
}
