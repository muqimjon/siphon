namespace Siphon.Media.Jobs;

public enum JobState { Queued, Running, Completed, Failed, Canceled, Expired }

public sealed record JobRequest(string Url, string Output, string Format, string? FormatId, string? Cookies);

public sealed record JobError(string Code, string Message);

public sealed class Job
{
    public required string Id { get; init; }
    public required JobRequest Request { get; init; }
    public required string Dir { get; init; }
    public JobState State { get; set; } = JobState.Queued;
    public string? Phase { get; set; }
    public double ProgressPct { get; set; }
    public int? EtaSec { get; set; }
    public JobError? Error { get; set; }
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public CancellationTokenSource Cts { get; } = new();

    public string? CookiesPath => Request.Cookies is null ? null : Path.Combine(Dir, "cookies.txt");
}
