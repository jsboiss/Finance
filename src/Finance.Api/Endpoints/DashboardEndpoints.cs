namespace Finance.Api.Endpoints;

using System.Security.Cryptography;
using Finance.Api.Auth;
using Finance.Core.Abstractions;
using Finance.Core.Auth;
using Finance.Core.Banking;
using Finance.Data.Banking;
using Finance.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OwnerDashboardAuthenticationHandler.SchemeName });

        group.MapGet("/accounts", GetAccounts);
        group.MapGet("/accounts/{accountId:guid}", GetAccount);
        group.MapPut("/accounts/{accountId:guid}", UpdateAccount);
        group.MapGet("/balances", GetBalances);
        group.MapGet("/overview", GetOverview);
        group.MapGet("/overview/daily-cash-flow", GetDailyCashFlow);
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
        group.MapGet("/subscriptions", GetSubscriptions);
        group.MapGet("/subscriptions/{subscriptionId:guid}", GetSubscription);
        group.MapPost("/subscriptions", CreateSubscription);
        group.MapPut("/subscriptions/{subscriptionId:guid}", UpdateSubscription);
        group.MapDelete("/subscriptions/{subscriptionId:guid}", DeleteSubscription);
        group.MapGet("/subscription-suggestions", GetSubscriptionSuggestions);
        group.MapPost("/subscription-suggestions/refresh", RefreshSubscriptionSuggestions);
        group.MapPost("/subscription-suggestions/{suggestionId:guid}/accept", AcceptSubscriptionSuggestion);
        group.MapPost("/subscription-suggestions/{suggestionId:guid}/dismiss", DismissSubscriptionSuggestion);
        group.MapGet("/tenants", GetTenants);
        group.MapPost("/tenants", CreateTenant);
        group.MapGet("/api-clients", GetApiClients);
        group.MapPost("/api-clients", CreateApiClient);
        group.MapDelete("/api-clients/{apiClientId:guid}", RevokeApiClient);

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

    private static async Task<Results<Ok<AccountDto>, NotFound>> UpdateAccount(Guid accountId, UpdateAccountRequest request, IBankingQueries queries, CancellationToken cancellationToken)
    {
        var account = await queries.UpdateAccount(accountId, request, cancellationToken);
        return account is null ? TypedResults.NotFound() : TypedResults.Ok(account);
    }

    private static Task<IReadOnlyList<BalanceDto>> GetBalances(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetBalances(cancellationToken);
    }

    private static Task<OverviewDto> GetOverview(Guid? accountId, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetOverview(accountId, cancellationToken);
    }

    private static Task<IReadOnlyList<OverviewDailyCashFlowDto>> GetDailyCashFlow(Guid? accountId, string? range, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetDailyCashFlow(accountId, range, cancellationToken);
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

    private static Task<IReadOnlyList<SubscriptionDto>> GetSubscriptions(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetSubscriptions(cancellationToken);
    }

    private static async Task<Results<Ok<SubscriptionDetailDto>, NotFound>> GetSubscription(Guid subscriptionId, IBankingQueries queries, CancellationToken cancellationToken)
    {
        var subscription = await queries.GetSubscription(subscriptionId, cancellationToken);
        return subscription is null ? TypedResults.NotFound() : TypedResults.Ok(subscription);
    }

    private static Task<SubscriptionDto> CreateSubscription(CreateSubscriptionRequest request, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.CreateSubscription(request, cancellationToken);
    }

    private static async Task<Results<Ok<SubscriptionDto>, NotFound>> UpdateSubscription(Guid subscriptionId, UpdateSubscriptionRequest request, IBankingQueries queries, CancellationToken cancellationToken)
    {
        var subscription = await queries.UpdateSubscription(subscriptionId, request, cancellationToken);
        return subscription is null ? TypedResults.NotFound() : TypedResults.Ok(subscription);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSubscription(Guid subscriptionId, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return await queries.DeleteSubscription(subscriptionId, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static Task<IReadOnlyList<SubscriptionSuggestionDto>> GetSubscriptionSuggestions(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.GetSubscriptionSuggestions(cancellationToken);
    }

    private static Task<IReadOnlyList<SubscriptionSuggestionDto>> RefreshSubscriptionSuggestions(IBankingQueries queries, CancellationToken cancellationToken)
    {
        return queries.RefreshSubscriptionSuggestions(cancellationToken);
    }

    private static async Task<Results<Ok<SubscriptionDto>, NotFound>> AcceptSubscriptionSuggestion(Guid suggestionId, IBankingQueries queries, CancellationToken cancellationToken)
    {
        var subscription = await queries.AcceptSubscriptionSuggestion(suggestionId, cancellationToken);
        return subscription is null ? TypedResults.NotFound() : TypedResults.Ok(subscription);
    }

    private static async Task<Results<NoContent, NotFound>> DismissSubscriptionSuggestion(Guid suggestionId, IBankingQueries queries, CancellationToken cancellationToken)
    {
        return await queries.DismissSubscriptionSuggestion(suggestionId, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<IReadOnlyList<TenantDto>> GetTenants(FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Tenants
            .OrderBy(x => x.Name)
            .Select(x => new TenantDto(x.Id, x.Name, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static async Task<TenantDto> CreateTenant(CreateTenantRequest request, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(request.Name) ? "External tenant" : request.Name.Trim();
        var tenant = new Tenant { Name = name };
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);
        await DefaultBankingData.EnsureDefaultTags(tenant.Id, dbContext, cancellationToken);
        return new TenantDto(tenant.Id, tenant.Name, tenant.CreatedAt);
    }

    private static async Task<IReadOnlyList<ApiClientDto>> GetApiClients(FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.ApiClients
            .Join(dbContext.Tenants,
                x => x.TenantId,
                y => y.Id,
                (x, y) => new { Client = x, Tenant = y })
            .OrderByDescending(x => x.Client.CreatedAt)
            .Select(x => new ApiClientDto(x.Client.Id, x.Client.TenantId, x.Tenant.Name, x.Client.Name, x.Client.CreatedAt, x.Client.RevokedAt))
            .ToListAsync(cancellationToken);
    }

    private static async Task<Results<Ok<CreateApiClientResponse>, NotFound>> CreateApiClient(CreateApiClientRequest request, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? "External UI" : request.Name.Trim();
        var apiKey = GenerateApiKey();
        var apiClient = new ApiClient
        {
            TenantId = tenant.Id,
            Name = name,
            KeyHash = ApiKeyHasher.Hash(apiKey)
        };

        dbContext.ApiClients.Add(apiClient);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new CreateApiClientResponse(new ApiClientDto(apiClient.Id, apiClient.TenantId, tenant.Name, apiClient.Name, apiClient.CreatedAt, apiClient.RevokedAt), apiKey));
    }

    private static async Task<Results<NoContent, NotFound>> RevokeApiClient(Guid apiClientId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var apiClient = await dbContext.ApiClients.FirstOrDefaultAsync(x => x.Id == apiClientId, cancellationToken);
        if (apiClient is null)
        {
            return TypedResults.NotFound();
        }

        apiClient.RevokedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static string GenerateApiKey()
    {
        return $"fin_{Base64UrlEncode(RandomNumberGenerator.GetBytes(32))}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed record TenantDto(Guid Id, string Name, DateTimeOffset CreatedAt);

    private sealed record CreateTenantRequest(string? Name);

    private sealed record ApiClientDto(Guid Id, Guid TenantId, string TenantName, string Name, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

    private sealed record CreateApiClientRequest(Guid TenantId, string? Name);

    private sealed record CreateApiClientResponse(ApiClientDto Client, string ApiKey);
}
