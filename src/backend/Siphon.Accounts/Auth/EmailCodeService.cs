using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace Siphon.Accounts.Auth;

public sealed class EmailCodeService(IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public string Issue(string email)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        cache.Set(KeyFor(email), code, Ttl);
        return code;
    }

    public bool Verify(string email, string code)
    {
        if (!cache.TryGetValue(KeyFor(email), out string? stored) || stored is null)
            return false;
        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(stored), System.Text.Encoding.ASCII.GetBytes(code)))
            return false;
        cache.Remove(KeyFor(email));
        return true;
    }

    private static string KeyFor(string email) => $"otp:{email.ToLowerInvariant()}";
}
