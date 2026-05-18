namespace Finance.Api.Endpoints;

using Finance.Api.Auth;
using Finance.Core.Abstractions;
using Finance.Core.Banking;
using Finance.Core.Redbark;
using Finance.Data.Data;
using Finance.Data.Redbark;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public static class AdminTenantEndpoints
{
    public static IEndpointRouteBuilder MapAdminTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/tenants")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OwnerDashboardAuthenticationHandler.SchemeName });

        group.MapGet("/{tenantId:guid}/redbark/connections", GetConnections);
        group.MapPost("/{tenantId:guid}/redbark/connections", AssignConnection);
        group.MapDelete("/{tenantId:guid}/redbark/connections/{externalConnectionId}", UnassignConnection);
        group.MapGet("/{tenantId:guid}/redbark/accounts", GetAccounts);
        group.MapGet("/{tenantId:guid}/imports", GetImports);
        group.MapPost("/{tenantId:guid}/operations/accounts/discover", DiscoverAccounts);
        group.MapPost("/{tenantId:guid}/operations/backfill", Backfill);
        group.MapPost("/{tenantId:guid}/operations/reconcile", ReconcileRecent);
        group.MapPost("/{tenantId:guid}/operations/reconcile/full", ReconcileFull);
        group.MapPost("/{tenantId:guid}/operations/accounts/{accountId:guid}/backfill", BackfillAccount);
        group.MapDelete("/{tenantId:guid}/operations/data", ClearTenantData);

        return app;
    }

    private static async Task<Results<Ok<TenantConnectionsDto>, NotFound>> GetConnections(Guid tenantId, IRedbarkClient redbarkClient, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var assignments = await dbContext.RedbarkConnectionAssignments
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var assignedIds = assignments.Select(x => x.ExternalConnectionId).ToHashSet(StringComparer.Ordinal);
        var assignmentTenantNames = await dbContext.RedbarkConnectionAssignments
            .Join(dbContext.Tenants,
                x => x.TenantId,
                y => y.Id,
                (x, y) => new { x.ExternalConnectionId, TenantName = y.Name })
            .ToDictionaryAsync(x => x.ExternalConnectionId, x => x.TenantName, cancellationToken);

        var connections = await redbarkClient.GetConnections(tenantId, cancellationToken);
        var available = connections
            .OrderBy(x => x.InstitutionName)
            .Select(x => new RedbarkConnectionOptionDto(x.Id, x.InstitutionName, assignedIds.Contains(x.Id), assignmentTenantNames.GetValueOrDefault(x.Id)))
            .ToList();
        var assigned = assignments
            .OrderBy(x => x.InstitutionName)
            .Select(x => new RedbarkConnectionAssignmentDto(x.ExternalConnectionId, x.InstitutionName, x.CreatedAt))
            .ToList();

        return TypedResults.Ok(new TenantConnectionsDto(tenant.Id, tenant.Name, available, assigned));
    }

    private static async Task<Results<Ok<RedbarkConnectionAssignmentDto>, NotFound, Conflict>> AssignConnection(Guid tenantId, AssignConnectionRequest request, IRedbarkClient redbarkClient, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var existing = await dbContext.RedbarkConnectionAssignments.FirstOrDefaultAsync(x => x.ExternalConnectionId == request.ExternalConnectionId, cancellationToken);
        if (existing is not null && existing.TenantId != tenantId)
        {
            return TypedResults.Conflict();
        }

        var connections = await redbarkClient.GetConnections(tenantId, cancellationToken);
        var connection = connections.FirstOrDefault(x => x.Id == request.ExternalConnectionId);
        if (connection is null)
        {
            return TypedResults.NotFound();
        }

        var assignment = existing ?? new RedbarkConnectionAssignment { TenantId = tenantId, ExternalConnectionId = connection.Id };
        if (existing is null)
        {
            dbContext.RedbarkConnectionAssignments.Add(assignment);
        }

        assignment.InstitutionName = string.IsNullOrWhiteSpace(request.InstitutionName) ? connection.InstitutionName : request.InstitutionName.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new RedbarkConnectionAssignmentDto(assignment.ExternalConnectionId, assignment.InstitutionName, assignment.CreatedAt));
    }

    private static async Task<Results<NoContent, NotFound>> UnassignConnection(Guid tenantId, string externalConnectionId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var assignment = await dbContext.RedbarkConnectionAssignments.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalConnectionId == externalConnectionId, cancellationToken);
        if (assignment is null)
        {
            return TypedResults.NotFound();
        }

        dbContext.RedbarkConnectionAssignments.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<List<TenantAccountAdminDto>>, NotFound>> GetAccounts(Guid tenantId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!exists)
        {
            return TypedResults.NotFound();
        }

        var accounts = await dbContext.BankAccounts
            .Where(x => x.TenantId == tenantId)
            .Join(dbContext.BankConnections.Where(x => x.TenantId == tenantId),
                x => x.BankConnectionId,
                y => y.Id,
                (x, y) => new TenantAccountAdminDto(x.Id, x.Name, x.CustomName, x.AccountNumber, y.InstitutionName, x.Currency))
            .OrderBy(x => x.InstitutionName)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(accounts);
    }

    private static async Task<Results<Ok<List<ImportRunDto>>, NotFound>> GetImports(Guid tenantId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!exists)
        {
            return TypedResults.NotFound();
        }

        var imports = await dbContext.ImportRuns
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.StartedAt)
            .Take(10)
            .Select(x => new ImportRunDto(x.Id, x.Source, x.Status, x.StartedAt, x.CompletedAt, x.ImportedCount, x.Error))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(imports);
    }

    private static async Task<Results<Accepted, NotFound>> DiscoverAccounts(Guid tenantId, IRedbarkImportService imports, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await TenantExists(tenantId, dbContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await imports.DiscoverAccounts(tenantId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Accepted, NotFound>> Backfill(Guid tenantId, IRedbarkImportService imports, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await TenantExists(tenantId, dbContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await imports.Backfill(tenantId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Accepted, NotFound>> ReconcileRecent(Guid tenantId, IRedbarkImportService imports, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await TenantExists(tenantId, dbContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await imports.ReconcileRecent(tenantId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Accepted, NotFound>> ReconcileFull(Guid tenantId, IRedbarkImportService imports, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await TenantExists(tenantId, dbContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await imports.ReconcileFull(tenantId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Accepted, NotFound>> BackfillAccount(Guid tenantId, Guid accountId, IRedbarkImportService imports, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var accountExists = await dbContext.BankAccounts.AnyAsync(x => x.TenantId == tenantId && x.Id == accountId, cancellationToken);
        if (!accountExists)
        {
            return TypedResults.NotFound();
        }

        await imports.BackfillAccount(tenantId, accountId, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, NotFound>> ClearTenantData(Guid tenantId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await TenantExists(tenantId, dbContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        await OperationsEndpoints.DeleteTenantData(tenantId, dbContext, cancellationToken);
        return TypedResults.NoContent();
    }

    private static Task<bool> TenantExists(Guid tenantId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
    }

    private sealed record TenantConnectionsDto(Guid TenantId, string TenantName, IReadOnlyList<RedbarkConnectionOptionDto> Available, IReadOnlyList<RedbarkConnectionAssignmentDto> Assigned);

    private sealed record RedbarkConnectionOptionDto(string ExternalConnectionId, string InstitutionName, bool IsAssignedToTenant, string? AssignedTenantName);

    private sealed record RedbarkConnectionAssignmentDto(string ExternalConnectionId, string InstitutionName, DateTimeOffset CreatedAt);

    private sealed record AssignConnectionRequest(string ExternalConnectionId, string? InstitutionName);

    private sealed record TenantAccountAdminDto(Guid Id, string Name, string CustomName, string AccountNumber, string InstitutionName, string Currency);
}
