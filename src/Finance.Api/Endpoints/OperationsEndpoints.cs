namespace Finance.Api.Endpoints;

using Finance.Api.Auth;
using Finance.Core.Redbark;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/operations")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = DevDashboardAuthenticationHandler.SchemeName });

        group.MapPost("/backfill", Backfill);
        group.MapPost("/reconcile", ReconcileRecent);
        group.MapPost("/reconcile/full", ReconcileFull);

        return app;
    }

    private static async Task<Accepted> Backfill(HttpContext httpContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.Backfill(GetTenantId(httpContext), cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Accepted> ReconcileRecent(HttpContext httpContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.ReconcileRecent(GetTenantId(httpContext), cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Accepted> ReconcileFull(HttpContext httpContext, IRedbarkImportService imports, CancellationToken cancellationToken)
    {
        await imports.ReconcileFull(GetTenantId(httpContext), cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static Guid GetTenantId(HttpContext httpContext)
    {
        return Guid.Parse(httpContext.User.FindFirst("tenant_id")!.Value);
    }
}
