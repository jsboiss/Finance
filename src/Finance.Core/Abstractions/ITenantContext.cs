namespace Finance.Core.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; }
}
