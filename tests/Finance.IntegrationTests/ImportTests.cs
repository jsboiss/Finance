namespace Finance.IntegrationTests;

using Finance.Core.Abstractions;
using Finance.Core.Banking;
using Finance.Core.Redbark;
using Finance.Data.Banking;
using Finance.Data.Data;
using Finance.Data.Redbark;
using Finance.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

public sealed class ImportTests : IAsyncLifetime
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
    public async Task Backfill_skips_pending_transactions_and_filters_by_tenant()
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
        dbContext.Tenants.AddRange(new Tenant { Id = tenantA, Name = "A" }, new Tenant { Id = tenantB, Name = "B" });
        await dbContext.SaveChangesAsync();

        await new RedbarkImportService(dbContext, new FakeRedbarkClient()).Backfill(tenantA, CancellationToken.None);
        await new RedbarkImportService(dbContext, new FakeRedbarkClient()).Backfill(tenantB, CancellationToken.None);

        Assert.Equal(2, await dbContext.BankTransactions.CountAsync());
        Assert.DoesNotContain(await dbContext.BankTransactions.ToListAsync(), x => x.ExternalTransactionId == "txn-pending");

        var accessor = new TenantContextAccessor { TenantId = tenantA };
        var queries = new EfBankingQueries(dbContext, new TenantContext(accessor));
        var transactions = await queries.GetTransactions(new TransactionQuery(null, null, null, null, 1, 50, null), CancellationToken.None);
        Assert.Single(transactions);
        Assert.All(transactions, x => Assert.Equal(-550, x.AmountMinorUnits));
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
