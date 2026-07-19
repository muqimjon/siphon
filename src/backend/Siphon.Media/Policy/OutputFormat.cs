namespace Siphon.Media.Policy;

public static class OutputFormat
{
    public static readonly string[] Audio = ["mp3", "m4a", "opus"];
    public static readonly string[] Video = ["mp4", "webm"];

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
        sourceCodecs.Any(c => c == "opus") ? ["mp3", "m4a", "opus"] : ["mp3", "m4a"];

    public static IReadOnlyList<string> AvailableVideo(IEnumerable<string> sourceCodecs) =>
        sourceCodecs.Any(c => c is "vp9" or "av1") ? ["mp4", "webm"] : ["mp4"];
}
