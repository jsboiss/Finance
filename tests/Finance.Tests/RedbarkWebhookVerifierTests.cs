namespace Finance.Tests;

using System.Security.Cryptography;
using System.Text;
using Finance.Core.Redbark;

public sealed class RedbarkWebhookVerifierTests
{
    [Fact]
    public void Verify_accepts_valid_payload()
    {
        var payload = Encoding.UTF8.GetBytes("""{"id":"evt_1"}""");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Sign(payload, "secret", timestamp);

        Assert.True(new RedbarkWebhookVerifier().Verify(payload, "secret", $"sha256={signature}", timestamp));
    }

    [Fact]
    public void Verify_rejects_tampered_payload()
    {
        var payload = Encoding.UTF8.GetBytes("""{"id":"evt_1"}""");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Sign(payload, "secret", timestamp);

        Assert.False(new RedbarkWebhookVerifier().Verify(Encoding.UTF8.GetBytes("""{"id":"evt_2"}"""), "secret", signature, timestamp));
    }

    private static string Sign(byte[] payload, string secret, string timestamp)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signedContent = Encoding.UTF8.GetBytes($"{timestamp}.{Encoding.UTF8.GetString(payload)}");
        return Convert.ToHexString(hmac.ComputeHash(signedContent)).ToLowerInvariant();
    }
}
