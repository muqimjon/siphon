using System.Collections.Concurrent;

namespace Siphon.Media.Probing;

public sealed class ProbeJsonCache
{
    private readonly record struct Entry(string Json, DateTime ExpiresUtc);

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private const int MaxItems = 200;

    private readonly ConcurrentDictionary<string, Entry> _items = new();

    public void Put(string url, string json)
    {
        if (_items.Count >= MaxItems)
        {
            foreach (var (key, entry) in _items)
                if (entry.ExpiresUtc <= DateTime.UtcNow)
                    _items.TryRemove(key, out _);
            if (_items.Count >= MaxItems) _items.Clear();
        }
        _items[url] = new Entry(json, DateTime.UtcNow.Add(Ttl));
    }

    public string? Get(string url) =>
        _items.TryGetValue(url, out var entry) && entry.ExpiresUtc > DateTime.UtcNow ? entry.Json : null;
}
