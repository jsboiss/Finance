namespace Finance.Api.Data;

using Finance.Data.Data;
using Microsoft.EntityFrameworkCore;

public static class DevTenantSeeder
{
    public static async Task SeedDevTenant(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        if (dbContext.Database.ProviderName is not null)
        {
            await dbContext.Database.MigrateAsync();
        }

        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        if (!await dbContext.Tenants.AnyAsync(x => x.Id == tenantId))
        {
            dbContext.Tenants.Add(new Tenant { Id = tenantId, Name = "Development" });
            await dbContext.SaveChangesAsync();
        }
    }
}
