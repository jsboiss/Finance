namespace Finance.Api.Endpoints;

using Finance.Api.Auth;
using Finance.Core.Banking;
using Microsoft.AspNetCore.Authorization;

public static class ExternalApiEndpoints
{
    public static IEndpointRouteBuilder MapExternalApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName });

        group.MapGet("/accounts", GetAccounts);
        group.MapGet("/transactions", GetTransactions);

        return app;
    }

    private static Task<IReadOnlyList<AccountDto>> GetAccounts(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetAccounts(cancellationToken);
    }

    private static Task<IReadOnlyList<TransactionDto>> GetTransactions(
        Guid? accountId,
        DateOnly? from,
        DateOnly? to,
        int? page,
        int? pageSize,
        IBankingQueries queries,
        CancellationToken cancellationToken)
    {
        return queries.GetTransactions(new TransactionQuery(accountId, from, to, null, page ?? 1, pageSize ?? 100, "-date"), cancellationToken);
    }
}
