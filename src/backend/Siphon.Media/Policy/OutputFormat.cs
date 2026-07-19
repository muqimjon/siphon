namespace Siphon.Media.Policy;

public static class OutputFormat
{
    public const string Best = "best";

    public static readonly string[] Audio = [Best, "mp3", "m4a", "opus"];
    public static readonly string[] Video = [Best, "mp4", "webm"];

    public static string Default(string output) => output == "audio" ? "mp3" : "mp4";

    public static bool IsValid(string output, string format) => output switch
    {
        "audio" => Audio.Contains(format),
        "video" => Video.Contains(format),
        _ => false,
    };

    public static string Extension(string format) => "." + format;

    public static string ContentType(string format) => format switch
    {
        "mp3" => "audio/mpeg",
        "m4a" => "audio/mp4",
        "opus" => "audio/ogg",
        "mp4" => "video/mp4",
        "webm" => "video/webm",
        _ => "application/octet-stream",
    };

    public static IReadOnlyList<string> AvailableAudio(IEnumerable<string> sourceCodecs) =>
        sourceCodecs.Any(c => c == "opus") ? [Best, "mp3", "m4a", "opus"] : [Best, "mp3", "m4a"];

    public static string ForCodec(string? codec) => codec is not null && codec.StartsWith("opus") ? "opus" : "m4a";

    public static IReadOnlyList<string> AvailableVideo(IEnumerable<string> videoCodecs, IEnumerable<string> audioCodecs) =>
        videoCodecs.Any(c => c is "vp9" or "av1") && WebmAudio(audioCodecs) ? [Best, "mp4", "webm"] : [Best, "mp4"];

    public static string ForVideoCodec(string? videoCodec, string? audioCodec) =>
        videoCodec is "vp9" or "av1" && WebmAudio(audioCodec is null ? [] : [audioCodec]) ? "webm" : "mp4";

    static bool WebmAudio(IEnumerable<string> audioCodecs)
    {
        var list = audioCodecs.ToList();
        return list.Count == 0 || list.Any(c => c is "opus" or "vorbis");
    }
}
