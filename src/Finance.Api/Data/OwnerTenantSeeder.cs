namespace Finance.Api.Data;

using Finance.Data.Data;
using Microsoft.EntityFrameworkCore;

public static class OwnerTenantSeeder
{
    public static async Task SeedOwnerTenant(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        if (dbContext.Database.ProviderName is not null)
        {
            await dbContext.Database.MigrateAsync();
        }

        var configuredTenantId = configuration["OwnerTenantId"];
        if (!Guid.TryParse(configuredTenantId, out var tenantId))
        {
            tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        var tenantName = configuration["OwnerTenantName"] ?? "Owner";
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null)
        {
            dbContext.Tenants.Add(new Tenant { Id = tenantId, Name = tenantName });
            await dbContext.SaveChangesAsync();
            return;
        }

        if (tenant.Name != tenantName)
        {
            tenant.Name = tenantName;
            await dbContext.SaveChangesAsync();
        }
    }
}
