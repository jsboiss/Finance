namespace Finance.Data.Data;

using Microsoft.EntityFrameworkCore;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<RedbarkConnectionAssignment> RedbarkConnectionAssignments => Set<RedbarkConnectionAssignment>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Balance> Balances => Set<Balance>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();
    public DbSet<BankTransactionTag> BankTransactionTags => Set<BankTransactionTag>();
    public DbSet<OverviewMetricSnapshot> OverviewMetricSnapshots => Set<OverviewMetricSnapshot>();
    public DbSet<PayBreakdownProfile> PayBreakdownProfiles => Set<PayBreakdownProfile>();
    public DbSet<BudgetProfile> BudgetProfiles => Set<BudgetProfile>();
    public DbSet<BudgetProfileTag> BudgetProfileTags => Set<BudgetProfileTag>();
    public DbSet<SpendingPlannerItem> SpendingPlannerItems => Set<SpendingPlannerItem>();
    public DbSet<MerchantTag> MerchantTags => Set<MerchantTag>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionTransaction> SubscriptionTransactions => Set<SubscriptionTransaction>();
    public DbSet<SubscriptionSuggestion> SubscriptionSuggestions => Set<SubscriptionSuggestion>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<RedbarkRequestLog> RedbarkRequestLogs => Set<RedbarkRequestLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(x => x.HasIndex(y => y.Name).IsUnique());
        modelBuilder.Entity<AppUser>(x => x.HasIndex(y => y.Email).IsUnique());
        modelBuilder.Entity<TenantMember>(x => x.HasIndex(y => new { y.TenantId, y.UserId }).IsUnique());
        modelBuilder.Entity<ApiClient>(x => x.HasIndex(y => y.KeyHash).IsUnique());
        modelBuilder.Entity<RedbarkConnectionAssignment>(x =>
        {
            x.HasIndex(y => y.ExternalConnectionId).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.ExternalConnectionId }).IsUnique();
        });
        modelBuilder.Entity<BankConnection>(x => x.HasIndex(y => new { y.TenantId, y.ExternalConnectionId }).IsUnique());
        modelBuilder.Entity<BankAccount>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.ExternalAccountId }).IsUnique();
            x.Property(y => y.AccountType).HasDefaultValue("Everyday");
        });
        modelBuilder.Entity<Balance>(x => x.HasIndex(y => new { y.TenantId, y.BankAccountId, y.AsOf }).IsUnique());
        modelBuilder.Entity<BankTransaction>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.ExternalTransactionId }).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.PostedDate });
            x.HasIndex(y => new { y.TenantId, y.BankAccountId, y.PostedDate });
            x.HasIndex(y => new { y.TenantId, y.Status, y.PostedDate });
            x.HasIndex(y => new { y.TenantId, y.BankAccountId, y.Status, y.PostedDate });
            x.Property(y => y.Category).HasDefaultValue("Uncategorized");
            x.Property(y => y.Status).HasDefaultValue("posted");
        });
        modelBuilder.Entity<TransactionTag>(x => x.HasIndex(y => new { y.TenantId, y.Name }).IsUnique());
        modelBuilder.Entity<BankTransactionTag>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.BankTransactionId, y.TransactionTagId }).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.TransactionTagId });
        });
        modelBuilder.Entity<OverviewMetricSnapshot>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.ScopeKey, y.SnapshotDate }).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.BankAccountId, y.SnapshotDate });
        });
        modelBuilder.Entity<PayBreakdownProfile>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.Name }).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.MainAccountId });
        });
        modelBuilder.Entity<BudgetProfile>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.Name }).IsUnique();
            x.Property(y => y.Currency).HasDefaultValue("AUD");
            x.Property(y => y.WeekStartsOn).HasDefaultValue(1);
            x.Property(y => y.CategoryMatchers).HasDefaultValue("[]");
        });
        modelBuilder.Entity<BudgetProfileTag>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.BudgetProfileId, y.TransactionTagId }).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.TransactionTagId });
        });
        modelBuilder.Entity<SpendingPlannerItem>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.IsPurchased });
            x.HasIndex(y => new { y.TenantId, y.TargetDate });
            x.Property(y => y.Currency).HasDefaultValue("AUD");
        });
        modelBuilder.Entity<MerchantTag>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.MerchantKey, y.TransactionTagId }).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.TransactionTagId });
        });
        modelBuilder.Entity<Subscription>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.MerchantKey });
            x.HasIndex(y => new { y.TenantId, y.StatusOverride });
        });
        modelBuilder.Entity<SubscriptionTransaction>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.SubscriptionId, y.BankTransactionId }).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.BankTransactionId });
        });
        modelBuilder.Entity<SubscriptionSuggestion>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.Status });
            x.HasIndex(y => new { y.TenantId, y.MerchantKey, y.Cadence, y.ExpectedAmountMinorUnits }).IsUnique();
            x.Property(y => y.Status).HasDefaultValue("pending");
            x.Property(y => y.PaymentManager).HasDefaultValue("direct");
        });
        modelBuilder.Entity<WebhookEvent>(x => x.HasIndex(y => new { y.TenantId, y.ExternalEventId }).IsUnique());
        modelBuilder.Entity<RedbarkRequestLog>(x => x.HasIndex(y => new { y.TenantId, y.RequestedAt }));
    }
}
