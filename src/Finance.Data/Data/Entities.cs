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

public sealed class BankAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BankConnectionId { get; set; }
    public string ExternalAccountId { get; set; } = "";
    public string Name { get; set; } = "";
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
    public string Description { get; set; } = "";
    public long AmountMinorUnits { get; set; }
    public string Currency { get; set; } = "AUD";
    public DateOnly PostedDate { get; set; }
    public JsonDocument RawJson { get; set; } = JsonDocument.Parse("{}");
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
