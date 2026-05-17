namespace Finance.Core.Redbark;

using System.Security.Cryptography;
using System.Text;

public sealed class RedbarkWebhookVerifier
{
    public bool Verify(ReadOnlySpan<byte> rawBody, string secret, string signature, string timestamp)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestamp))
        {
            return false;
        }

        if (!long.TryParse(timestamp, out var unixTimestamp))
        {
            return false;
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        if (DateTimeOffset.UtcNow - signedAt > TimeSpan.FromMinutes(5) || signedAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(5))
        {
            return false;
        }

        var normalizedSignature = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature["sha256=".Length..]
            : signature;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var timestampBytes = Encoding.UTF8.GetBytes(timestamp);
        var signedContent = new byte[timestampBytes.Length + 1 + rawBody.Length];
        timestampBytes.CopyTo(signedContent, 0);
        signedContent[timestampBytes.Length] = (byte)'.';
        rawBody.CopyTo(signedContent.AsSpan(timestampBytes.Length + 1));
        var hash = hmac.ComputeHash(signedContent);
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(normalizedSignature.ToLowerInvariant()));
    }
}
