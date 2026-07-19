namespace Siphon.Media.Policy;

public static class FormatSelector
{
    public static string Video(string? formatId, string format)
    {
        var v = formatId ?? "bestvideo";
        var tail = formatId is null ? "best" : $"{formatId}/best";
        return format == "webm"
            ? $"{v}+bestaudio[acodec=opus]/{v}+bestaudio[acodec=vorbis]/{v}+bestaudio/{tail}"
            : $"{v}+bestaudio[acodec^=mp4a]/{v}+bestaudio/{tail}";
    }

    public static string Audio(string? formatId) => formatId is null
        ? "bestaudio/best"
        : $"{formatId}/bestaudio/best";
}
