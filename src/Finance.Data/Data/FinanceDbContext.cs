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
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(x => x.HasIndex(y => y.Name).IsUnique());
        modelBuilder.Entity<AppUser>(x => x.HasIndex(y => y.Email).IsUnique());
        modelBuilder.Entity<TenantMember>(x => x.HasIndex(y => new { y.TenantId, y.UserId }).IsUnique());
        modelBuilder.Entity<ApiClient>(x => x.HasIndex(y => y.KeyHash).IsUnique());
        modelBuilder.Entity<BankConnection>(x => x.HasIndex(y => new { y.TenantId, y.ExternalConnectionId }).IsUnique());
        modelBuilder.Entity<BankAccount>(x => x.HasIndex(y => new { y.TenantId, y.ExternalAccountId }).IsUnique());
        modelBuilder.Entity<Balance>(x => x.HasIndex(y => new { y.TenantId, y.BankAccountId, y.AsOf }).IsUnique());
        modelBuilder.Entity<BankTransaction>(x => x.HasIndex(y => new { y.TenantId, y.ExternalTransactionId }).IsUnique());
        modelBuilder.Entity<WebhookEvent>(x => x.HasIndex(y => new { y.TenantId, y.ExternalEventId }).IsUnique());
    }
}
