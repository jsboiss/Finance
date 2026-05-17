namespace Finance.Data.Tenancy;

using Finance.Core.Abstractions;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    public Guid? TenantId { get; set; }
}
