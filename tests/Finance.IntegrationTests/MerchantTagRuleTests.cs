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
                ExternalTransactionId = "tenant-a-coles-description",
                Description = "COLES BRISBANE",
                MerchantName = null,
                AmountMinorUnits = -4200,
                PostedDate = new DateOnly(2026, 6, 1)
            },
            new BankTransaction
            {
                TenantId = tenantA,
                BankAccountId = tenantAAccountId,
                ExternalTransactionId = "tenant-a-coles-store",
                Description = "Coles 4568 Ascot AU",
                MerchantName = "COLES 4568 ASCOT AU",
                AmountMinorUnits = -1625,
                PostedDate = new DateOnly(2026, 6, 3)
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

        var tenantATransaction = await dbContext.BankTransactions.FirstAsync(x => x.TenantId == tenantA && x.ExternalTransactionId == "tenant-a-coles-description");
        var tenantAStoreTransaction = await dbContext.BankTransactions.FirstAsync(x => x.TenantId == tenantA && x.ExternalTransactionId == "tenant-a-coles-store");
        var tenantBTransaction = await dbContext.BankTransactions.FirstAsync(x => x.TenantId == tenantB && x.ExternalTransactionId == "tenant-b-coles");
        Assert.True(await dbContext.BankTransactionTags.AnyAsync(x => x.TenantId == tenantA && x.BankTransactionId == tenantATransaction.Id && x.TransactionTagId == tag.Id && x.Source == "merchant"));
        Assert.True(await dbContext.BankTransactionTags.AnyAsync(x => x.TenantId == tenantA && x.BankTransactionId == tenantAStoreTransaction.Id && x.TransactionTagId == tag.Id && x.Source == "merchant"));
        Assert.False(await dbContext.BankTransactionTags.AnyAsync(x => x.TenantId == tenantB && x.BankTransactionId == tenantBTransaction.Id));
    }

    [Fact]
    public async Task Internal_tag_is_system_managed_and_only_applies_to_matched_account_transfers()
    {
        if (Postgres is null)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<FinanceDbContext>().UseNpgsql(Postgres.GetConnectionString()).Options;
        await using var dbContext = new FinanceDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        var everydayAccountId = Guid.NewGuid();
        var savingsAccountId = Guid.NewGuid();
        var internalTag = new TransactionTag { TenantId = tenantId, Name = DefaultBankingData.InternalTransferTagName, Color = DefaultBankingData.InternalTransferTagColor };
        dbContext.Tenants.Add(new Tenant { Id = tenantId, Name = "Internal tenant" });
        dbContext.BankAccounts.AddRange(
            new BankAccount { Id = everydayAccountId, TenantId = tenantId, ExternalAccountId = "everyday", Name = "Everyday" },
            new BankAccount { Id = savingsAccountId, TenantId = tenantId, ExternalAccountId = "savings", Name = "Savings" });
        dbContext.TransactionTags.Add(internalTag);
        dbContext.BankTransactions.AddRange(
            new BankTransaction
            {
                TenantId = tenantId,
                BankAccountId = everydayAccountId,
                ExternalTransactionId = "matched-debit",
                Description = "Transfer to savings",
                AmountMinorUnits = -5000,
                PostedDate = new DateOnly(2026, 6, 5),
                Status = "posted"
            },
            new BankTransaction
            {
                TenantId = tenantId,
                BankAccountId = savingsAccountId,
                ExternalTransactionId = "matched-credit",
                Description = "Transfer from everyday",
                AmountMinorUnits = 5000,
                PostedDate = new DateOnly(2026, 6, 6),
                Status = "posted"
            },
            new BankTransaction
            {
                TenantId = tenantId,
                BankAccountId = everydayAccountId,
                ExternalTransactionId = "external-transfer",
                Description = "Transfer to partner",
                AmountMinorUnits = -12500,
                PostedDate = new DateOnly(2026, 6, 7),
                Status = "posted"
            });
        var oldInternalRule = new MerchantTag { TenantId = tenantId, MerchantName = "Transfer", MerchantKey = DefaultBankingData.GetMerchantKey("Transfer"), TransactionTagId = internalTag.Id };
        dbContext.MerchantTags.Add(oldInternalRule);
        await dbContext.SaveChangesAsync();

        var queries = CreateQueries(dbContext, tenantId);
        Assert.False(await queries.DeleteMerchantTagRule(oldInternalRule.Id, CancellationToken.None));

        var tags = await queries.GetTags(CancellationToken.None);

        var systemInternalTag = Assert.Single(tags, x => x.Name == DefaultBankingData.InternalTransferTagName);
        Assert.True(systemInternalTag.IsSystem);
        Assert.Empty(await dbContext.MerchantTags.Where(x => x.TenantId == tenantId && x.TransactionTagId == internalTag.Id).ToListAsync());
        Assert.False(await queries.DeleteTag(internalTag.Id, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.CreateMerchantTagRule(new CreateMerchantTagRuleRequest("Transfer", internalTag.Id), CancellationToken.None));

        var matchedDebit = await dbContext.BankTransactions.FirstAsync(x => x.ExternalTransactionId == "matched-debit");
        var matchedCredit = await dbContext.BankTransactions.FirstAsync(x => x.ExternalTransactionId == "matched-credit");
        var externalTransfer = await dbContext.BankTransactions.FirstAsync(x => x.ExternalTransactionId == "external-transfer");
        Assert.True(await dbContext.BankTransactionTags.AnyAsync(x => x.TenantId == tenantId && x.BankTransactionId == matchedDebit.Id && x.TransactionTagId == internalTag.Id));
        Assert.True(await dbContext.BankTransactionTags.AnyAsync(x => x.TenantId == tenantId && x.BankTransactionId == matchedCredit.Id && x.TransactionTagId == internalTag.Id));
        Assert.False(await dbContext.BankTransactionTags.AnyAsync(x => x.TenantId == tenantId && x.BankTransactionId == externalTransfer.Id && x.TransactionTagId == internalTag.Id));
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
