namespace Finance.Api.Endpoints;

using Finance.Api.Auth;
using Finance.Core.Abstractions;
using Finance.Core.Redbark;
using Finance.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/operations")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OwnerDashboardAuthenticationHandler.SchemeName });

        group.MapPost("/backfill", Backfill);
        group.MapPost("/accounts/discover", DiscoverAccounts);
        group.MapPost("/accounts/{accountId:guid}/backfill", BackfillAccount);
        group.MapPost("/reconcile", ReconcileRecent);
        group.MapPost("/reconcile/full", ReconcileFull);
        group.MapDelete("/data", ClearData);
        group.MapDelete("/accounts/{accountId:guid}/data", ClearAccountData);
        group.MapDelete("/tenants/{tenantId:guid}/data", ClearTenantData);

        return app;
    }

    private static async Task<Accepted> DiscoverAccounts(ITenantContext tenantContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.DiscoverAccounts(tenantContext.TenantId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Accepted> Backfill(ITenantContext tenantContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.Backfill(tenantContext.TenantId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Accepted> BackfillAccount(Guid accountId, ITenantContext tenantContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.BackfillAccount(tenantContext.TenantId, accountId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Accepted> ReconcileRecent(ITenantContext tenantContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.ReconcileRecent(tenantContext.TenantId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Accepted> ReconcileFull(ITenantContext tenantContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.ReconcileFull(tenantContext.TenantId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<NoContent> ClearData(ITenantContext tenantContext, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        await DeleteTenantData(tenantContext.TenantId, dbContext, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> ClearAccountData(Guid accountId, ITenantContext tenantContext, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var exists = await dbContext.BankAccounts.AnyAsync(x => x.TenantId == tenantId && x.Id == accountId, cancellationToken);
        if (!exists)
        {
            return TypedResults.NotFound();
        }

        var transactionIds = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId && x.BankAccountId == accountId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await dbContext.BankTransactionTags.Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.SubscriptionTransactions.Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BankTransactions.Where(x => x.TenantId == tenantId && x.BankAccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Balances.Where(x => x.TenantId == tenantId && x.BankAccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> ClearTenantData(Guid tenantId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!exists)
        {
            return TypedResults.NotFound();
        }

        await DeleteTenantData(tenantId, dbContext, cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task DeleteTenantData(Guid tenantId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.BankTransactionTags.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.SubscriptionTransactions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.SubscriptionSuggestions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Balances.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BankTransactions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BankAccounts.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BankConnections.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.WebhookEvents.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ImportRuns.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.RedbarkRequestLogs.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
    }
}
