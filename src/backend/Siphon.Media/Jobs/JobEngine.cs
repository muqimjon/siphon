using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Siphon.Media.Jobs;

public sealed class JobEngine
{
    private readonly SiphonOptions _options;
    private readonly JobStore _store;
    private readonly Channel<Job> _queue;

    public JobEngine(IOptions<SiphonOptions> options, JobStore store)
    {
        _options = options.Value;
        _store = store;
        _queue = Channel.CreateBounded<Job>(new BoundedChannelOptions(_options.MaxQueuedJobs)
        {
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ChannelReader<Job> Reader => _queue.Reader;

    public Job Create(JobRequest request)
    {
        var tempRoot = Path.GetFullPath(_options.TempRoot);
        Directory.CreateDirectory(tempRoot);
        var drive = new DriveInfo(Path.GetPathRoot(tempRoot)!);
        if (drive.AvailableFreeSpace < (long)_options.MinFreeDiskMb * 1024 * 1024)
            throw new MediaEngineException(ErrorCodes.DiskFull, "Server disk space is low.");

        var id = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(tempRoot, id);
        Directory.CreateDirectory(dir);
        var job = new Job { Id = id, Request = request, Dir = dir };
        if (request.Cookies is not null)
            File.WriteAllText(job.CookiesPath!, request.Cookies);

        _store.TryAdd(job);
        if (!_queue.Writer.TryWrite(job))
        {
            _store.Remove(id);
            TryDeleteDir(dir);
            throw new MediaEngineException(ErrorCodes.QueueFull, "Download queue is full, try again shortly.");
        }
        return job;
    }

    public void Cancel(Job job)
    {
        job.Cts.Cancel();
        if (job.State is JobState.Queued)
        {
            job.State = JobState.Canceled;
            TryDeleteDir(job.Dir);
        }
    }

    public static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
