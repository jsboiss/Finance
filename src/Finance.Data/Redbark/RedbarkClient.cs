namespace Finance.Data.Redbark;

using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Core.Abstractions;
using Finance.Core.Redbark;
using Finance.Data.Data;
using Microsoft.Extensions.Options;

public sealed class RedbarkClient(HttpClient httpClient, IOptions<RedbarkOptions> options, FinanceDbContext dbContext) : IRedbarkClient
{
    private readonly RedbarkOptions options = options.Value;

    public async Task<IReadOnlyList<RedbarkConnectionDto>> GetConnections(Guid tenantId, CancellationToken cancellationToken)
    {
        return await GetDataList(tenantId, "/v1/connections", x => new RedbarkConnectionDto(x.GetProperty("id").GetString()!, x.GetProperty("institutionName").GetString() ?? "", JsonDocument.Parse(x.GetRawText())), cancellationToken);
    }

    public async Task<IReadOnlyList<RedbarkAccountDto>> GetAccounts(Guid tenantId, string connectionId, CancellationToken cancellationToken)
    {
        var accounts = await GetPaginatedDataList(tenantId, "/v1/accounts?limit=200", x => new RedbarkAccountDto(x.GetProperty("id").GetString()!, x.GetProperty("connectionId").GetString()!, x.GetProperty("name").GetString()!, GetNullableString(x, "accountNumber") ?? "", x.GetProperty("currency").GetString() ?? "AUD", JsonDocument.Parse(x.GetRawText())), cancellationToken);
        return accounts.Where(x => x.ConnectionId == connectionId).ToList();
    }

    public async Task<IReadOnlyList<RedbarkBalanceDto>> GetBalances(Guid tenantId, IReadOnlyList<string> accountIds, CancellationToken cancellationToken)
    {
        var balances = new List<RedbarkBalanceDto>();
        foreach (var accountIdChunk in accountIds.Chunk(100))
        {
            var accountIdQuery = string.Join(",", accountIdChunk);
            var chunk = await GetDataList(tenantId, $"/v1/balances?accountIds={Uri.EscapeDataString(accountIdQuery)}", x => new RedbarkBalanceDto(x.GetProperty("accountId").GetString()!, ParseMinorUnits(GetNullableString(x, "currentBalance")), GetNullableString(x, "currency") ?? "AUD", DateTimeOffset.UtcNow, JsonDocument.Parse(x.GetRawText())), cancellationToken);
            balances.AddRange(chunk);
        }

        return balances;
    }

    public async Task<RedbarkTransactionPage> GetTransactions(Guid tenantId, string connectionId, string accountId, DateOnly from, DateOnly to, string? cursor, CancellationToken cancellationToken)
    {
        var limit = 500;
        var offset = int.TryParse(cursor, out var parsedOffset) ? parsedOffset : 0;
        var uri = $"/v1/transactions?connectionId={Uri.EscapeDataString(connectionId)}&accountId={Uri.EscapeDataString(accountId)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&limit={limit}&offset={offset}";

        using var request = CreateRequest(uri);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await LogRequest(tenantId, request, response, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var transactions = document.RootElement.GetProperty("data").EnumerateArray()
            .Select(x => new RedbarkTransactionDto(
                x.GetProperty("id").GetString()!,
                x.GetProperty("accountId").GetString()!,
                GetNullableString(x, "accountName") ?? "",
                x.GetProperty("description").GetString() ?? "",
                GetNullableString(x, "merchantName"),
                GetNullableString(x, "merchantCategoryCode"),
                GetNullableString(x, "category") ?? "Uncategorized",
                ParseRequiredMinorUnits(x.GetProperty("amount").GetString()),
                GetNullableString(x, "currency") ?? "AUD",
                DateOnly.Parse(x.GetProperty("date").GetString()!),
                ParseNullableDateTimeOffset(GetNullableString(x, "datetime")),
                GetNullableString(x, "direction") ?? "",
                GetNullableString(x, "status") ?? "posted",
                JsonDocument.Parse(x.GetRawText())))
            .ToList();
        var pagination = document.RootElement.GetProperty("pagination");
        var nextCursor = pagination.GetProperty("hasMore").GetBoolean() ? (offset + limit).ToString(CultureInfo.InvariantCulture) : null;
        return new RedbarkTransactionPage(transactions, nextCursor);
    }

    private async Task<IReadOnlyList<T>> GetDataList<T>(Guid tenantId, string uri, Func<JsonElement, T> map, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(uri);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await LogRequest(tenantId, request, response, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
        var root = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return root.GetProperty("data").EnumerateArray().Select(map).ToList();
    }

    private async Task<IReadOnlyList<T>> GetPaginatedDataList<T>(Guid tenantId, string uri, Func<JsonElement, T> map, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        var offset = 0;
        const int limit = 200;

        while (true)
        {
            var separator = uri.Contains('?') ? '&' : '?';
            using var request = CreateRequest($"{uri}{separator}offset={offset}");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            await LogRequest(tenantId, request, response, cancellationToken);
            await EnsureSuccess(response, cancellationToken);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            results.AddRange(document.RootElement.GetProperty("data").EnumerateArray().Select(map));

            var pagination = document.RootElement.GetProperty("pagination");
            if (!pagination.GetProperty("hasMore").GetBoolean())
            {
                return results;
            }

            offset += limit;
        }
    }

    private HttpRequestMessage CreateRequest(string uri)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Redbark API key is not configured.");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Authorization", $"Bearer {options.ApiKey}");
        return request;
    }

    private async Task LogRequest(Guid tenantId, HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        dbContext.RedbarkRequestLogs.Add(new RedbarkRequestLog
        {
            TenantId = tenantId,
            Method = request.Method.Method,
            Path = request.RequestUri?.PathAndQuery ?? "",
            StatusCode = (int)response.StatusCode
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Redbark request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }

    private static string? GetNullableString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind is not JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static long? ParseMinorUnits(string? amount)
    {
        if (string.IsNullOrWhiteSpace(amount))
        {
            return null;
        }

        var value = decimal.Parse(amount, NumberStyles.Number, CultureInfo.InvariantCulture);
        return decimal.ToInt64(decimal.Round(value * 100, 0, MidpointRounding.AwayFromZero));
    }

    private static long ParseRequiredMinorUnits(string? amount)
    {
        return ParseMinorUnits(amount) ?? throw new InvalidOperationException("Redbark transaction amount was missing.");
    }

    private static DateTimeOffset? ParseNullableDateTimeOffset(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }
}
