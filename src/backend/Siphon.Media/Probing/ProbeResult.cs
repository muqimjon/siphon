namespace Siphon.Media.Probing;

public sealed record ProbeResult(
    string Url,
    string Kind,
    string Title,
    string? Uploader,
    string? ThumbnailUrl,
    double? DurationSec,
    bool IsLive,
    IReadOnlyList<VideoVariant> VideoVariants,
    IReadOnlyList<AudioVariant> AudioVariants,
    IReadOnlyList<ImageEntry> Images)
{
    public string? Extractor { get; init; }
    public IReadOnlyList<string> AudioFormats { get; init; } = [];
    public IReadOnlyList<string> VideoFormats { get; init; } = [];
}

public sealed record VideoVariant(
    string FormatId,
    string Codec,
    int? Width,
    int? Height,
    double? Fps,
    int? VbrKbps,
    long? ApproxSizeBytes,
    bool NeedsMerge,
    string Delivery);

public sealed record AudioVariant(
    string FormatId,
    string Codec,
    int? AbrKbps,
    long? ApproxSizeBytes,
    PlannedMp3 PlannedMp3);

public sealed record PlannedMp3(int Qscale, int TypicalKbps);

public sealed record ImageEntry(int Index, string? ThumbnailUrl, int? Width, int? Height);
