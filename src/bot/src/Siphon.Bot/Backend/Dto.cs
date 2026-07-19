namespace Siphon.Bot.Backend;

public sealed record ProbeResult
{
    public string Kind { get; init; } = "";
    public string? Title { get; init; }
    public string? Uploader { get; init; }
    public string? ThumbnailUrl { get; init; }
    public double? DurationSec { get; init; }
    public bool IsLive { get; init; }
    public IReadOnlyList<VideoVariant> VideoVariants { get; init; } = [];
    public IReadOnlyList<AudioVariant> AudioVariants { get; init; } = [];
    public IReadOnlyList<ImageInfo> Images { get; init; } = [];
}

public sealed record VideoVariant
{
    public string FormatId { get; init; } = "";
    public string? Codec { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public double? Fps { get; init; }
    public double? VbrKbps { get; init; }
    public long? ApproxSizeBytes { get; init; }
    public bool NeedsMerge { get; init; }
    public string? Delivery { get; init; }
}

public sealed record AudioVariant
{
    public string FormatId { get; init; } = "";
    public string? Codec { get; init; }
    public double? AbrKbps { get; init; }
    public long? ApproxSizeBytes { get; init; }
    public PlannedMp3? PlannedMp3 { get; init; }
}

public sealed record PlannedMp3
{
    public int Qscale { get; init; }
    public int TypicalKbps { get; init; }
}

public sealed record ImageInfo
{
    public int Index { get; init; }
}

public sealed record JobCreated
{
    public string JobId { get; init; } = "";
}

public sealed record JobStatus
{
    public string State { get; init; } = "";
    public string? Phase { get; init; }
    public double? ProgressPct { get; init; }
    public double? EtaSec { get; init; }
    public JobError? Error { get; init; }
    public JobFile? File { get; init; }
}

public sealed record JobError
{
    public string Code { get; init; } = "";
    public string? Message { get; init; }
}

public sealed record JobFile
{
    public string Url { get; init; } = "";
    public string FileName { get; init; } = "";
    public long SizeBytes { get; init; }
    public string ContentType { get; init; } = "";
    public DateTime ExpiresAtUtc { get; init; }
}
