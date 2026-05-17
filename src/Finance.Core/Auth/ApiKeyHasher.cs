namespace Finance.Core.Auth;

using System.Security.Cryptography;
using System.Text;

public static class ApiKeyHasher
{
    public static string Hash(string plaintext)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool Verify(string plaintext, string hash)
    {
        var candidate = Hash(plaintext);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(hash));
    }
}
