using System.Security.Cryptography;
using System.Text;
using Siphon.Accounts.Auth;

namespace Siphon.Tests;

public class TelegramLoginVerifierTests
{
    private const string BotToken = "123456:test-bot-token";

    private static string SignHash(IReadOnlyDictionary<string, string> fields)
    {
        var checkString = string.Join('\n', fields
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(BotToken));
        return Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(checkString)));
    }

    private static Dictionary<string, string> Fresh() => new()
    {
        ["id"] = "42",
        ["first_name"] = "Alice",
        ["username"] = "alice",
        ["auth_date"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
    };

    [Fact]
    public void Valid_hash_accepted()
    {
        var fields = Fresh();
        var hash = SignHash(fields);
        Assert.True(TelegramLoginVerifier.Verify(fields, hash, BotToken, TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Tampered_field_rejected()
    {
        var fields = Fresh();
        var hash = SignHash(fields);
        fields["id"] = "43";
        Assert.False(TelegramLoginVerifier.Verify(fields, hash, BotToken, TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Wrong_bot_token_rejected()
    {
        var fields = Fresh();
        var hash = SignHash(fields);
        Assert.False(TelegramLoginVerifier.Verify(fields, hash, "999999:other-token", TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Stale_auth_date_rejected()
    {
        var fields = Fresh();
        fields["auth_date"] = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeSeconds().ToString();
        var hash = SignHash(fields);
        Assert.False(TelegramLoginVerifier.Verify(fields, hash, BotToken, TimeSpan.FromDays(1)));
    }
}
