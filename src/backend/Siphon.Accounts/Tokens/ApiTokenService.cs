using System.Security.Cryptography;

namespace Siphon.Accounts.Tokens;

public static class ApiTokenService
{
    private const string Scheme = "sk_live_";
    private const int SecretBytes = 24;

    public static (string Secret, string Hash, string Prefix) Generate()
    {
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var secret = Scheme + random;
        return (secret, Hash(secret), secret[..12]);
    }

    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(secret)));

    public static bool IsApiToken(string? value) => value is not null && value.StartsWith(Scheme, StringComparison.Ordinal);
}
