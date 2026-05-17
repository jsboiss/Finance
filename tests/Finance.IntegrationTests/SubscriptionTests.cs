namespace Finance.IntegrationTests;

using Finance.Core.Banking;
using Finance.Data.Banking;
using Finance.Data.Data;
using Finance.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

public sealed class SubscriptionTests : IAsyncLifetime
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
    public async Task Refresh_detects_monthly_subscriptions_and_ignores_one_offs()
    {
        if (Postgres is null)
        {
            return;
        }

        await using var dbContext = await CreateDbContext();
        var tenantId = await SeedTenant(dbContext);
        var accountId = await SeedAccount(dbContext, tenantId);
        AddTransaction(dbContext, tenantId, accountId, "netflix-1", "NETFLIX", -2299, new DateOnly(2026, 1, 4));
        AddTransaction(dbContext, tenantId, accountId, "netflix-2", "NETFLIX", -2299, new DateOnly(2026, 2, 4));
        AddTransaction(dbContext, tenantId, accountId, "netflix-3", "NETFLIX", -2299, new DateOnly(2026, 3, 4));
        AddTransaction(dbContext, tenantId, accountId, "one-off", "JB HI FI", -12900, new DateOnly(2026, 3, 9));
        await dbContext.SaveChangesAsync();

        var queries = CreateQueries(dbContext, tenantId);
        var suggestions = await queries.RefreshSubscriptionSuggestions(CancellationToken.None);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("NETFLIX", suggestion.MerchantName);
        Assert.Equal("monthly", suggestion.Cadence);
        Assert.Equal(2299, suggestion.ExpectedAmountMinorUnits);
    }

    [Fact]
    public async Task Refresh_detects_subscription_when_card_merchant_suffix_changes()
    {
        if (Postgres is null)
        {
            return;
        }

        await using var dbContext = await CreateDbContext();
        var tenantId = await SeedTenant(dbContext);
        var accountId = await SeedAccount(dbContext, tenantId);
        AddTransaction(dbContext, tenantId, accountId, "netflix-1", "NETFLIX.COM Melbourne AU AUS", -1899, new DateOnly(2026, 1, 18));
        AddTransaction(dbContext, tenantId, accountId, "netflix-2", "Netflix.com Melbourne VI AUS", -1899, new DateOnly(2026, 2, 18));
        AddTransaction(dbContext, tenantId, accountId, "netflix-3", "NETFLIX AUSTRALIA PTY Melbourne VI AUS", -1899, new DateOnly(2026, 3, 18));
        AddTransaction(dbContext, tenantId, accountId, "netflix-4", "NETFLIX.COM Melbourne AU AUS", -1899, new DateOnly(2026, 4, 18));
        await dbContext.SaveChangesAsync();

        var queries = CreateQueries(dbContext, tenantId);
        var suggestions = await queries.RefreshSubscriptionSuggestions(CancellationToken.None);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("netflix", suggestion.MerchantKey);
        Assert.Equal("monthly", suggestion.Cadence);
        Assert.Equal(new DateOnly(2026, 4, 18), suggestion.LastPaymentDate);
    }

    [Fact]
    public async Task Dismissed_suggestions_are_not_returned_after_refresh()
    {
        if (Postgres is null)
        {
            return;
        }

        await using var dbContext = await CreateDbContext();
        var tenantId = await SeedTenant(dbContext);
        var accountId = await SeedAccount(dbContext, tenantId);
        AddMonthlyPayments(dbContext, tenantId, accountId, "spotify", "SPOTIFY", 1399);
        await dbContext.SaveChangesAsync();

        var queries = CreateQueries(dbContext, tenantId);
        var suggestion = Assert.Single(await queries.RefreshSubscriptionSuggestions(CancellationToken.None));
        Assert.True(await queries.DismissSubscriptionSuggestion(suggestion.Id, CancellationToken.None));

        var refreshedSuggestions = await queries.RefreshSubscriptionSuggestions(CancellationToken.None);

        Assert.Empty(refreshedSuggestions);
    }

    [Fact]
    public async Task Accepting_suggestion_creates_subscription_links_transactions_and_calculates_totals()
    {
        if (Postgres is null)
        {
            return;
        }

        await using var dbContext = await CreateDbContext();
        var tenantId = await SeedTenant(dbContext);
        var accountId = await SeedAccount(dbContext, tenantId);
        AddMonthlyPayments(dbContext, tenantId, accountId, "youtube", "YOUTUBE", 1699);
        await dbContext.SaveChangesAsync();

        var queries = CreateQueries(dbContext, tenantId);
        var suggestion = Assert.Single(await queries.RefreshSubscriptionSuggestions(CancellationToken.None));
        var subscription = await queries.AcceptSubscriptionSuggestion(suggestion.Id, CancellationToken.None);
        var detail = await queries.GetSubscription(subscription!.Id, CancellationToken.None);

        Assert.NotNull(subscription);
        Assert.NotNull(detail);
        Assert.Equal(3, detail.Payments.Count);
        Assert.Equal(5097, detail.Subscription.TotalPaidMinorUnits);
    }

    [Fact]
    public async Task Subscription_detail_reports_possible_and_confirmed_price_increases()
    {
        if (Postgres is null)
        {
            return;
        }

        await using var dbContext = await CreateDbContext();
        var tenantId = await SeedTenant(dbContext);
        var accountId = await SeedAccount(dbContext, tenantId);
        AddTransaction(dbContext, tenantId, accountId, "stan-1", "STAN", -1200, new DateOnly(2026, 1, 1));
        AddTransaction(dbContext, tenantId, accountId, "stan-2", "STAN", -1200, new DateOnly(2026, 2, 1));
        AddTransaction(dbContext, tenantId, accountId, "stan-3", "STAN", -1500, new DateOnly(2026, 3, 1));
        AddTransaction(dbContext, tenantId, accountId, "stan-4", "STAN", -1500, new DateOnly(2026, 4, 1));
        AddTransaction(dbContext, tenantId, accountId, "stan-5", "STAN", -1800, new DateOnly(2026, 5, 1));
        await dbContext.SaveChangesAsync();

        var queries = CreateQueries(dbContext, tenantId);
        var subscription = await queries.CreateSubscription(new CreateSubscriptionRequest("Stan", "STAN", "direct", "monthly", 1500, "AUD", null, false), CancellationToken.None);
        var detail = await queries.GetSubscription(subscription.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Contains(detail.Subscription.PriceChanges, x => x.NewAmountMinorUnits == 1500 && x.Status == "confirmed");
        Assert.Contains(detail.Subscription.PriceChanges, x => x.NewAmountMinorUnits == 1800 && x.Status == "possible");
    }

    [Fact]
    public async Task Subscriptions_are_scoped_to_tenant()
    {
        if (Postgres is null)
        {
            return;
        }

        await using var dbContext = await CreateDbContext();
        var tenantA = await SeedTenant(dbContext, "A");
        var tenantB = await SeedTenant(dbContext, "B");
        var accountA = await SeedAccount(dbContext, tenantA);
        var accountB = await SeedAccount(dbContext, tenantB);
        AddMonthlyPayments(dbContext, tenantA, accountA, "netflix-a", "NETFLIX", 2299);
        AddMonthlyPayments(dbContext, tenantB, accountB, "spotify-b", "SPOTIFY", 1399);
        await dbContext.SaveChangesAsync();

        var tenantAQueries = CreateQueries(dbContext, tenantA);
        var tenantBQueries = CreateQueries(dbContext, tenantB);

        Assert.Equal("NETFLIX", Assert.Single(await tenantAQueries.RefreshSubscriptionSuggestions(CancellationToken.None)).MerchantName);
        Assert.Equal("SPOTIFY", Assert.Single(await tenantBQueries.RefreshSubscriptionSuggestions(CancellationToken.None)).MerchantName);
    }

    private async Task<FinanceDbContext> CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>().UseNpgsql(Postgres!.GetConnectionString()).Options;
        var dbContext = new FinanceDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static async Task<Guid> SeedTenant(FinanceDbContext dbContext, string name = "Tenant")
    {
        var tenant = new Tenant { Name = name };
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        return tenant.Id;
    }

    private static async Task<Guid> SeedAccount(FinanceDbContext dbContext, Guid tenantId)
    {
        var connection = new BankConnection { TenantId = tenantId, ExternalConnectionId = Guid.NewGuid().ToString(), InstitutionName = "Bank" };
        var account = new BankAccount { TenantId = tenantId, BankConnectionId = connection.Id, ExternalAccountId = Guid.NewGuid().ToString(), Name = "Everyday" };
        dbContext.BankConnections.Add(connection);
        dbContext.BankAccounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account.Id;
    }

    private static EfBankingQueries CreateQueries(FinanceDbContext dbContext, Guid tenantId)
    {
        var accessor = new TenantContextAccessor { TenantId = tenantId };
        return new EfBankingQueries(dbContext, new TenantContext(accessor));
    }

    private static void AddMonthlyPayments(FinanceDbContext dbContext, Guid tenantId, Guid accountId, string idPrefix, string merchantName, long amountMinorUnits)
    {
        AddTransaction(dbContext, tenantId, accountId, $"{idPrefix}-1", merchantName, -amountMinorUnits, new DateOnly(2026, 1, 5));
        AddTransaction(dbContext, tenantId, accountId, $"{idPrefix}-2", merchantName, -amountMinorUnits, new DateOnly(2026, 2, 5));
        AddTransaction(dbContext, tenantId, accountId, $"{idPrefix}-3", merchantName, -amountMinorUnits, new DateOnly(2026, 3, 5));
    }

    private static void AddTransaction(FinanceDbContext dbContext, Guid tenantId, Guid accountId, string externalTransactionId, string merchantName, long amountMinorUnits, DateOnly postedDate)
    {
        dbContext.BankTransactions.Add(new BankTransaction
        {
            TenantId = tenantId,
            BankAccountId = accountId,
            ExternalTransactionId = externalTransactionId,
            ExternalAccountName = "Everyday",
            Description = merchantName,
            MerchantName = merchantName,
            AmountMinorUnits = amountMinorUnits,
            PostedDate = postedDate,
            Direction = "debit",
            Status = "posted"
        });
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
