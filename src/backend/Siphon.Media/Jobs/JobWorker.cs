using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Siphon.Media.Engine;

namespace Siphon.Media.Jobs;

public sealed class JobWorker(
    JobEngine engine,
    YtDlpEngine ytDlp,
    GalleryDlEngine galleryDl,
    IOptions<SiphonOptions> options,
    IServiceScopeFactory scopes,
    ILogger<JobWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, options.Value.MaxConcurrentJobs).Select(_ => RunLoopAsync(stoppingToken)));

    private async Task RecordBytesAsync(Job job)
    {
        if (job.Caller is not { } caller || job.FileSizeBytes is not { } bytes) return;
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<Http.IUsageSink>().RecordBytesAsync(caller, bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record usage bytes for job {Job}", job.Id);
        }
    }

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in engine.Reader.ReadAllAsync(stoppingToken))
        {
            if (job.Cts.IsCancellationRequested)
            {
                job.State = JobState.Canceled;
                JobEngine.TryDeleteDir(job.Dir);
                continue;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, job.Cts.Token);
            linked.CancelAfter(TimeSpan.FromMinutes(options.Value.DownloadTimeoutMinutes));
            job.State = JobState.Running;
            job.Phase = "downloading";
            try
            {
                var outcome = job.Request.Output == "gallery"
                    ? await RunGalleryAsync(job, linked.Token)
                    : await ytDlp.DownloadAsync(job, (pct, eta, phase) =>
                    {
                        job.ProgressPct = pct;
                        job.EtaSec = eta;
                        job.Phase = phase;
                    }, linked.Token);

                var produced = new FileInfo(outcome.Path).Length;
                var cap = (long)job.Request.MaxFileSizeMb * 1024 * 1024;
                if (produced > cap)
                    throw new MediaEngineException(ErrorCodes.TooLarge,
                        $"File is larger than the {job.Request.MaxFileSizeMb} MB limit.");

                job.FilePath = outcome.Path;
                job.FileName = outcome.FileName;
                job.ContentType = outcome.ContentType;
                job.FileSizeBytes = produced;
                job.ProgressPct = 100;
                job.EtaSec = 0;
                job.Phase = null;
                job.State = JobState.Completed;
                await RecordBytesAsync(job);
                job.CompletedAt = DateTimeOffset.UtcNow;
            }
            catch (OperationCanceledException) when (job.Cts.IsCancellationRequested)
            {
                job.State = JobState.Canceled;
                JobEngine.TryDeleteDir(job.Dir);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                job.State = JobState.Canceled;
                return;
            }
            catch (OperationCanceledException)
            {
                Fail(job, new MediaEngineException(ErrorCodes.ExtractorBroken, "Download timed out."));
            }
            catch (MediaEngineException ex)
            {
                Fail(job, ex);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "job {JobId} crashed", job.Id);
                Fail(job, new MediaEngineException(ErrorCodes.ExtractorBroken, "Unexpected failure."));
            }
        }
    }

    private async Task<DownloadOutcome> RunGalleryAsync(Job job, CancellationToken ct)
    {
        job.Phase = "downloading";
        var outcome = await galleryDl.DownloadAsync(job, ct);
        if (outcome.ContentType == "application/zip") job.Phase = "packaging";
        return outcome;
    }

    private static void Fail(Job job, MediaEngineException ex)
    {
        job.State = JobState.Failed;
        job.Error = new JobError(ex.Code, ex.Message);
        job.CompletedAt = DateTimeOffset.UtcNow;
        JobEngine.TryDeleteDir(job.Dir);
    }
}
