using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Siphon.Media.Engine;

public sealed class SelfUpdater(IServiceProvider services, IOptions<SiphonOptions> options, ILogger<SelfUpdater> logger) : BackgroundService
{
    private readonly Lock _gate = new();
    private readonly Queue<DateTimeOffset> _failures = new();
    private readonly Channel<bool> _trigger = Channel.CreateBounded<bool>(1);
    private DateTimeOffset _lastRun = DateTimeOffset.MinValue;

    public void ReportExtractorFailure()
    {
        if (!options.Value.SelfUpdate.Enabled) return;
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            _failures.Enqueue(now);
            while (_failures.Count > 0 && now - _failures.Peek() > TimeSpan.FromMinutes(10))
                _failures.Dequeue();
            if (_failures.Count >= 3 && now - _lastRun > TimeSpan.FromHours(6))
            {
                _failures.Clear();
                _trigger.Writer.TryWrite(true);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.SelfUpdate.Enabled) return;
        var interval = TimeSpan.FromHours(options.Value.SelfUpdate.IntervalHours);
        var lastScheduled = DateTimeOffset.UtcNow;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var due = DateTimeOffset.UtcNow - lastScheduled >= interval || _trigger.Reader.TryRead(out _);
            if (!due) continue;
            lastScheduled = DateTimeOffset.UtcNow;
            lock (_gate) _lastRun = lastScheduled;
            try
            {
                await services.GetRequiredService<YtDlpEngine>().RunSelfUpdateAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "yt-dlp self-update failed");
            }
        }
    }
}
