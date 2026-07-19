using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Siphon.Bot.Backend;
using Siphon.Bot.Data;

namespace Siphon.Bot.Modules.Download;

public sealed record CachedProbe(string Url, ProbeResult Probe, int SourceMessageId, UserPref Pref);

public sealed class ProbeCache(IMemoryCache cache, IOptions<LimitsOptions> limits)
{
    public string Put(CachedProbe entry)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        cache.Set("probe:" + token, entry, TimeSpan.FromMinutes(limits.Value.ProbeCacheMinutes));
        return token;
    }

    public CachedProbe? Get(string token) =>
        cache.TryGetValue("probe:" + token, out CachedProbe? entry) ? entry : null;
}
