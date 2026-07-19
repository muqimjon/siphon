using Siphon.Accounts.Tokens;

namespace Siphon.Tests;

public class ApiTokenServiceTests
{
    [Fact]
    public void Generated_secret_has_scheme_and_prefix()
    {
        var (secret, _, prefix) = ApiTokenService.Generate();
        Assert.StartsWith("sk_live_", secret);
        Assert.Equal(secret[..12], prefix);
        Assert.True(ApiTokenService.IsApiToken(secret));
    }

    [Fact]
    public void Stored_hash_verifies_the_secret()
    {
        var (secret, hash, _) = ApiTokenService.Generate();
        Assert.Equal(hash, ApiTokenService.Hash(secret));
    }

    [Fact]
    public void Wrong_secret_does_not_match_hash()
    {
        var (secret, hash, _) = ApiTokenService.Generate();
        Assert.NotEqual(hash, ApiTokenService.Hash(secret + "x"));
    }

    [Fact]
    public void Each_token_is_unique()
    {
        var (a, _, _) = ApiTokenService.Generate();
        var (b, _, _) = ApiTokenService.Generate();
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer eyJ...")]
    public void Non_api_tokens_rejected(string? value) => Assert.False(ApiTokenService.IsApiToken(value));
}
