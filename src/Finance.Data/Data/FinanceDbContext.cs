namespace Finance.Data.Data;

using Microsoft.EntityFrameworkCore;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Balance> Balances => Set<Balance>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();
    public DbSet<BankTransactionTag> BankTransactionTags => Set<BankTransactionTag>();
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
        modelBuilder.Entity<BankConnection>(x => x.HasIndex(y => new { y.TenantId, y.ExternalConnectionId }).IsUnique());
        modelBuilder.Entity<BankAccount>(x => x.HasIndex(y => new { y.TenantId, y.ExternalAccountId }).IsUnique());
        modelBuilder.Entity<Balance>(x => x.HasIndex(y => new { y.TenantId, y.BankAccountId, y.AsOf }).IsUnique());
        modelBuilder.Entity<BankTransaction>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.ExternalTransactionId }).IsUnique();
            x.Property(y => y.Category).HasDefaultValue("Uncategorized");
            x.Property(y => y.Status).HasDefaultValue("posted");
        });
        modelBuilder.Entity<TransactionTag>(x => x.HasIndex(y => new { y.TenantId, y.Name }).IsUnique());
        modelBuilder.Entity<BankTransactionTag>(x =>
        {
            x.HasIndex(y => new { y.TenantId, y.BankTransactionId, y.TransactionTagId }).IsUnique();
            x.HasIndex(y => new { y.TenantId, y.TransactionTagId });
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
