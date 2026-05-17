namespace Finance.Api.Auth;

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Finance.Core.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public sealed class OwnerDashboardAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    ITenantContextAccessor tenantContextAccessor) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dashboard";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredUsername = configuration["OwnerDashboardUsername"];
        var configuredPassword = configuration["OwnerDashboardPassword"];
        if (string.IsNullOrWhiteSpace(configuredUsername) || string.IsNullOrWhiteSpace(configuredPassword))
        {
            return Task.FromResult(AuthenticateResult.Fail("Owner dashboard credentials are not configured."));
        }

        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var authorizationHeader) ||
            !string.Equals(authorizationHeader.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorizationHeader.Parameter))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string decodedCredentials;
        try
        {
            decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader.Parameter));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid owner dashboard credentials."));
        }

        var separatorIndex = decodedCredentials.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid owner dashboard credentials."));
        }

        var username = decodedCredentials[..separatorIndex];
        var password = decodedCredentials[(separatorIndex + 1)..];
        if (!SecureEquals(username, configuredUsername) || !SecureEquals(password, configuredPassword))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid owner dashboard credentials."));
        }

        var configuredTenantId = configuration["OwnerTenantId"];
        if (!Guid.TryParse(configuredTenantId, out var tenantId))
        {
            tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        tenantContextAccessor.TenantId = tenantId;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "owner"),
            new Claim("tenant_id", tenantId.ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"Finance Dashboard\", charset=\"UTF-8\"";
        return base.HandleChallengeAsync(properties);
    }

    private static bool SecureEquals(string value, string expectedValue)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedValue);
        return valueBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(valueBytes, expectedBytes);
    }
}
