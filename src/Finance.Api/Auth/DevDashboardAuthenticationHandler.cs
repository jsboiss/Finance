namespace Finance.Api.Auth;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Finance.Core.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public sealed class DevDashboardAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    ITenantContextAccessor tenantContextAccessor) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dashboard";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredTenantId = configuration["DevTenantId"];
        if (!Guid.TryParse(configuredTenantId, out var tenantId))
        {
            tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        tenantContextAccessor.TenantId = tenantId;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev-user"),
            new Claim("tenant_id", tenantId.ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
