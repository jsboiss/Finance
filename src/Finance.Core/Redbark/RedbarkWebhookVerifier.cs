namespace Finance.Core.Redbark;

using System.Security.Cryptography;
using System.Text;

public sealed class RedbarkWebhookVerifier
{
    public bool Verify(ReadOnlySpan<byte> rawBody, string secret, string signature)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var normalizedSignature = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature["sha256=".Length..]
            : signature;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(rawBody.ToArray());
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(normalizedSignature.ToLowerInvariant()));
    }
}
