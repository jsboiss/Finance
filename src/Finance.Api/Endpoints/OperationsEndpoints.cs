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
        group.MapPost("/reconcile", ReconcileRecent);
        group.MapPost("/reconcile/full", ReconcileFull);
        group.MapDelete("/data", ClearData);

        return app;
    }

    private static async Task<Accepted> Backfill(ITenantContext tenantContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.Backfill(tenantContext.TenantId, cancellationToken);
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
        var tenantId = tenantContext.TenantId;
        await dbContext.Balances.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BankTransactions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BankAccounts.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BankConnections.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.WebhookEvents.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ImportRuns.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.RedbarkRequestLogs.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
