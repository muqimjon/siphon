using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Siphon.Accounts.Auth;

public sealed class EmailCodeService(IMemoryCache cache)
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private sealed class Entry(string code)
    {
        public string Code { get; } = code;
        public int Attempts;
    }

    public string Issue(string email)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        cache.Set(KeyFor(email), new Entry(code), Ttl);
        return code;
    }

    public bool Verify(string email, string code)
    {
        var key = KeyFor(email);
        if (!cache.TryGetValue(key, out Entry? entry) || entry is null)
            return false;
        if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.Code), Encoding.ASCII.GetBytes(code)))
        {
            cache.Remove(key);
            return true;
        }
        if (Interlocked.Increment(ref entry.Attempts) >= MaxAttempts)
            cache.Remove(key);
        return false;
    }

    private static string KeyFor(string email) => $"otp:{email.ToLowerInvariant()}";
}
