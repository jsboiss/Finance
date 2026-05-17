namespace Finance.Api.Endpoints;

using System.Text.Json;
using Finance.Core.Redbark;
using Finance.Data.Redbark;
using Microsoft.Extensions.Options;

public static class RedbarkWebhookEndpoints
{
    public static IEndpointRouteBuilder MapRedbarkWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/redbark", ProcessWebhook);
        return app;
    }

    private static async Task<IResult> ProcessWebhook(
        HttpRequest request,
        IOptions<RedbarkOptions> options,
        RedbarkWebhookVerifier verifier,
        IRedbarkImportService imports,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var rawJson = await reader.ReadToEndAsync(cancellationToken);
        var body = System.Text.Encoding.UTF8.GetBytes(rawJson);
        var signature = request.Headers["X-Redbark-Signature"].FirstOrDefault() ?? "";
        if (!verifier.Verify(body, options.Value.WebhookSecret, signature))
        {
            return Results.Unauthorized();
        }

        using var document = JsonDocument.Parse(rawJson);
        var tenantId = document.RootElement.GetProperty("tenantId").GetGuid();
        var eventId = document.RootElement.GetProperty("id").GetString()!;
        var eventType = document.RootElement.GetProperty("type").GetString()!;
        await imports.ProcessWebhook(tenantId, eventId, eventType, rawJson, cancellationToken);
        return Results.Accepted();
    }
}
