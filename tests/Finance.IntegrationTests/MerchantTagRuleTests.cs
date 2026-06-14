namespace Finance.IntegrationTests;

using Finance.Core.Banking;
using Finance.Data.Banking;
using Finance.Data.Data;
using Finance.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

public sealed class MerchantTagRuleTests : IAsyncLifetime
{
    public PostgreSqlContainer? Postgres { get; set; }

    public async Task InitializeAsync()
    {
        if (!await DockerIsAvailable())
        {
            return;
        }

        Postgres = new PostgreSqlBuilder().WithImage("postgres:18").Build();
        await Postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (Postgres is not null)
        {
            await Postgres.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreateMerchantTagRule_applies_to_existing_matching_transactions()
    {
        if (Postgres is null)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<FinanceDbContext>().UseNpgsql(Postgres.GetConnectionString()).Options;
        await using var dbContext = new FinanceDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantAAccountId = Guid.NewGuid();
        var tenantBAccountId = Guid.NewGuid();
        var tag = new TransactionTag { TenantId = tenantA, Name = "Groceries", Color = "#22c55e" };
        dbContext.Tenants.AddRange(new Tenant { Id = tenantA, Name = "A" }, new Tenant { Id = tenantB, Name = "B" });
        dbContext.BankConnections.AddRange(
            new BankConnection { TenantId = tenantA, ExternalConnectionId = "connection-a", InstitutionName = "Bank" },
            new BankConnection { TenantId = tenantB, ExternalConnectionId = "connection-b", InstitutionName = "Bank" });
        dbContext.BankAccounts.AddRange(
            new BankAccount { Id = tenantAAccountId, TenantId = tenantA, ExternalAccountId = "account-a", Name = "Everyday" },
            new BankAccount { Id = tenantBAccountId, TenantId = tenantB, ExternalAccountId = "account-b", Name = "Everyday" });
        dbContext.TransactionTags.Add(tag);
        dbContext.BankTransactions.AddRange(
            new BankTransaction
            {
                TenantId = tenantA,
                BankAccountId = tenantAAccountId,
                ExternalTransactionId = "tenant-a-coles",
                Description = "COLES BRISBANE",
                MerchantName = null,
                AmountMinorUnits = -4200,
                PostedDate = new DateOnly(2026, 6, 1)
            },
            new BankTransaction
            {
                TenantId = tenantB,
                BankAccountId = tenantBAccountId,
                ExternalTransactionId = "tenant-b-coles",
                Description = "COLES BRISBANE",
                MerchantName = null,
                AmountMinorUnits = -3900,
                PostedDate = new DateOnly(2026, 6, 1)
            });
        await dbContext.SaveChangesAsync();

        var queries = CreateQueries(dbContext, tenantA);
        await queries.CreateMerchantTagRule(new CreateMerchantTagRuleRequest("Coles", tag.Id), CancellationToken.None);

        var tenantATransaction = await dbContext.BankTransactions.FirstAsync(x => x.TenantId == tenantA && x.ExternalTransactionId == "tenant-a-coles");
        var tenantBTransaction = await dbContext.BankTransactions.FirstAsync(x => x.TenantId == tenantB && x.ExternalTransactionId == "tenant-b-coles");
        Assert.True(await dbContext.BankTransactionTags.AnyAsync(x => x.TenantId == tenantA && x.BankTransactionId == tenantATransaction.Id && x.TransactionTagId == tag.Id && x.Source == "merchant"));
        Assert.False(await dbContext.BankTransactionTags.AnyAsync(x => x.TenantId == tenantB && x.BankTransactionId == tenantBTransaction.Id));
    }

    private static EfBankingQueries CreateQueries(FinanceDbContext dbContext, Guid tenantId)
    {
        var accessor = new TenantContextAccessor { TenantId = tenantId };
        return new EfBankingQueries(dbContext, new TenantContext(accessor));
    }

    private static async Task<bool> DockerIsAvailable()
    {
        try
        {
            using var client = new System.IO.Pipes.NamedPipeClientStream(".", "docker_engine", System.IO.Pipes.PipeDirection.InOut);
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(cancellationTokenSource.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
