namespace Finance.Tests;

using Finance.Core.Auth;

public sealed class ApiKeyHasherTests
{
    [Fact]
    public void Verify_accepts_matching_plaintext_without_storing_plaintext()
    {
        var hash = ApiKeyHasher.Hash("secret-key");

        Assert.NotEqual("secret-key", hash);
        Assert.True(ApiKeyHasher.Verify("secret-key", hash));
        Assert.False(ApiKeyHasher.Verify("other-key", hash));
    }
}
