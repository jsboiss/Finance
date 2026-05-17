namespace Finance.Api.Endpoints;

using Finance.Api.Auth;
using Finance.Core.Banking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = DevDashboardAuthenticationHandler.SchemeName });

        group.MapGet("/accounts", GetAccounts);
        group.MapGet("/accounts/{accountId:guid}", GetAccount);
        group.MapGet("/balances", GetBalances);
        group.MapGet("/transactions", GetTransactions);
        group.MapGet("/tags", GetTags);
        group.MapPost("/tags", CreateTag);
        group.MapDelete("/tags/{tagId:guid}", DeleteTag);
        group.MapPut("/transactions/{transactionId:guid}/tags", SetTransactionTags);
        group.MapGet("/merchant-tags", GetMerchantTagRules);
        group.MapPost("/merchant-tags", CreateMerchantTagRule);
        group.MapDelete("/merchant-tags/{ruleId:guid}", DeleteMerchantTagRule);
        group.MapGet("/imports", GetImports);
        group.MapGet("/operations/status", GetOperationsStatus);

        return app;
    }

    private static Task<IReadOnlyList<AccountDto>> GetAccounts(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetAccounts(cancellationToken);
    }

    private static async Task<Results<Ok<AccountDto>, NotFound>> GetAccount(Guid accountId, IBankingQueries queries, CancellationToken cancellationToken)
    {
        var account = await queries.GetAccount(accountId, cancellationToken);
        return account is null ? TypedResults.NotFound() : TypedResults.Ok(account);
    }

    private static Task<IReadOnlyList<BalanceDto>> GetBalances(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetBalances(cancellationToken);
    }

    private static Task<IReadOnlyList<TransactionDto>> GetTransactions(
        Guid? accountId,
        DateOnly? from,
        DateOnly? to,
        string? search,
        int? page,
        int? pageSize,
        string? sort,
        IBankingQueries queries,
        CancellationToken cancellationToken)
    {
        return queries.GetTransactions(new TransactionQuery(accountId, from, to, search, page ?? 1, pageSize ?? 50, sort), cancellationToken);
    }

    private static Task<IReadOnlyList<TransactionTagDto>> GetTags(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetTags(cancellationToken);
    }

    private static Task<TransactionTagDto> CreateTag(CreateTransactionTagRequest request, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.CreateTag(request, cancellationToken);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteTag(Guid tagId, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return await queries.DeleteTag(tagId, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static Task<IReadOnlyList<TransactionTagDto>> SetTransactionTags(Guid transactionId, UpdateTransactionTagsRequest request, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.SetTransactionTags(transactionId, request, cancellationToken);
    }

    private static Task<IReadOnlyList<MerchantTagRuleDto>> GetMerchantTagRules(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetMerchantTagRules(cancellationToken);
    }

    private static Task<MerchantTagRuleDto> CreateMerchantTagRule(CreateMerchantTagRuleRequest request, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.CreateMerchantTagRule(request, cancellationToken);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteMerchantTagRule(Guid ruleId, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return await queries.DeleteMerchantTagRule(ruleId, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static Task<IReadOnlyList<ImportRunDto>> GetImports(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetImportRuns(cancellationToken);
    }

    private static Task<OperationsStatusDto> GetOperationsStatus(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetOperationsStatus(cancellationToken);
    }
}
