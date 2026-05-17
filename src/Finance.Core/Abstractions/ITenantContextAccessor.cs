namespace Finance.Core.Abstractions;

public interface ITenantContextAccessor
{
    Guid? TenantId { get; set; }
}
