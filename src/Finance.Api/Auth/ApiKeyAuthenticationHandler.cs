namespace Finance.Api.Auth;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Finance.Core.Abstractions;
using Finance.Core.Auth;
using Finance.Data.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FinanceDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var values))
        {
            return AuthenticateResult.NoResult();
        }

        var plaintext = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return AuthenticateResult.Fail("Missing API key.");
        }

        var hash = ApiKeyHasher.Hash(plaintext);
        var client = await dbContext.ApiClients.FirstOrDefaultAsync(x => x.KeyHash == hash && x.RevokedAt == null);
        if (client is null || !ApiKeyHasher.Verify(plaintext, client.KeyHash))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        tenantContextAccessor.TenantId = client.TenantId;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, client.Id.ToString()),
            new Claim("tenant_id", client.TenantId.ToString()),
            new Claim("client_id", client.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
