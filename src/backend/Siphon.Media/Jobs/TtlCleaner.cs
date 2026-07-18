using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Siphon.Media.Jobs;

public sealed class TtlCleaner(JobStore store, IOptions<SiphonOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ttl = TimeSpan.FromMinutes(options.Value.JobTtlMinutes);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var job in store.All)
            {
                var age = now - (job.CompletedAt ?? job.CreatedAt);
                if (job.State is JobState.Completed or JobState.Failed or JobState.Canceled && age > ttl)
                {
                    job.State = JobState.Expired;
                    JobEngine.TryDeleteDir(job.Dir);
                }
                else if (job.State is JobState.Expired && age > ttl * 2)
                {
                    store.Remove(job.Id);
                }
            }

            var tempRoot = Path.GetFullPath(options.Value.TempRoot);
            if (!Directory.Exists(tempRoot)) continue;
            var live = store.All.Select(j => j.Dir).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var graceCutoff = DateTime.UtcNow.AddMinutes(-10);
            foreach (var dir in Directory.EnumerateDirectories(tempRoot))
                if (!live.Contains(dir) && Directory.GetCreationTimeUtc(dir) < graceCutoff)
                    JobEngine.TryDeleteDir(dir);
        }
    }
}
