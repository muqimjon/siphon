using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Siphon.Bot.Modules.Convert;

public sealed record CachedFileJob(IncomingFile File, int SourceMessageId);

public sealed class ConvertCache
{
    private readonly record struct Entry(CachedFileJob Job, DateTime ExpiresUtc);

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, Entry> _items = new();

    public string Put(CachedFileJob job)
    {
        var token = System.Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        _items[token] = new Entry(job, DateTime.UtcNow.Add(Ttl));
        return token;
    }

    public CachedFileJob? Get(string token) =>
        _items.TryGetValue(token, out var e) && e.ExpiresUtc > DateTime.UtcNow ? e.Job : null;
}
