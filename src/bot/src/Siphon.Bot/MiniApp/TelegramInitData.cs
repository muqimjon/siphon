using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Siphon.Bot.MiniApp;

public sealed record TelegramUser(long Id, string? FirstName, string? LastName, string? Username, string? PhotoUrl, string? LanguageCode)
{
    public static TelegramUser? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number)
                return null;
            return new TelegramUser(
                id.GetInt64(),
                root.TryGetProperty("first_name", out var f) ? f.GetString() : null,
                root.TryGetProperty("last_name", out var ln) ? ln.GetString() : null,
                root.TryGetProperty("username", out var u) ? u.GetString() : null,
                root.TryGetProperty("photo_url", out var ph) ? ph.GetString() : null,
                root.TryGetProperty("language_code", out var l) ? l.GetString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public static class TelegramInitData
{
    public static TelegramUser? Validate(string? initData, string botToken, TimeSpan ttl, DateTimeOffset? now = null)
    {
        if (string.IsNullOrEmpty(initData) || string.IsNullOrEmpty(botToken))
            return null;

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        string? hash = null;
        foreach (var pair in initData.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;
            var key = pair[..eq];
            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            if (key == "hash") hash = value;
            else fields[key] = value;
        }
        if (hash is null || !fields.TryGetValue("auth_date", out var authRaw) || !long.TryParse(authRaw, out var authUnix))
            return null;

        if ((now ?? DateTimeOffset.UtcNow) - DateTimeOffset.FromUnixTimeSeconds(authUnix) > ttl)
            return null;

        var checkString = string.Join('\n', fields.Select(f => $"{f.Key}={f.Value}"));
        var secret = HMACSHA256.HashData("WebAppData"u8, Encoding.UTF8.GetBytes(botToken));
        var computed = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(checkString)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(hash)))
            return null;

        return fields.TryGetValue("user", out var userJson) ? TelegramUser.Parse(userJson) : null;
    }
}
