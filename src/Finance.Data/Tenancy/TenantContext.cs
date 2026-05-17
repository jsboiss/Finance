namespace Finance.Data.Tenancy;

using Finance.Core.Abstractions;

public sealed class TenantContext(ITenantContextAccessor accessor) : ITenantContext
{
    public Guid TenantId => accessor.TenantId ?? throw new InvalidOperationException("No tenant is available for the current request.");
}
