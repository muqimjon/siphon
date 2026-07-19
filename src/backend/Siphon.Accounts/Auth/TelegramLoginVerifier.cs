using System.Security.Cryptography;
using System.Text;

namespace Siphon.Accounts.Auth;

public static class TelegramLoginVerifier
{
    public static bool Verify(IReadOnlyDictionary<string, string> fields, string hash, string botToken, TimeSpan maxAge)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(botToken))
            return false;

        var checkString = string.Join('\n', fields
            .Where(kv => kv.Key != "hash" && kv.Value is not null)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var computed = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(checkString)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(hash.ToLowerInvariant())))
            return false;

        if (fields.TryGetValue("auth_date", out var authDate) && long.TryParse(authDate, out var seconds))
        {
            var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(seconds);
            if (age > maxAge || age < -maxAge) return false;
        }

        return true;
    }
}
