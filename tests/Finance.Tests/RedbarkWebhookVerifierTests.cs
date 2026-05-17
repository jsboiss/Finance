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
        var signature = Sign(payload, "secret");

        Assert.True(new RedbarkWebhookVerifier().Verify(payload, "secret", $"sha256={signature}"));
    }

    [Fact]
    public void Verify_rejects_tampered_payload()
    {
        var payload = Encoding.UTF8.GetBytes("""{"id":"evt_1"}""");
        var signature = Sign(payload, "secret");

        Assert.False(new RedbarkWebhookVerifier().Verify(Encoding.UTF8.GetBytes("""{"id":"evt_2"}"""), "secret", signature));
    }

    private static string Sign(byte[] payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }
}
