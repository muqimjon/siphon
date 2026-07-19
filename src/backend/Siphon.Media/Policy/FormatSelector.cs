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

    public static string Audio(string? formatId, string format)
    {
        var head = formatId is null ? "" : $"{formatId}/";
        var preferred = format switch
        {
            "m4a" => "bestaudio[acodec^=mp4a]/",
            "opus" => "bestaudio[acodec=opus]/",
            _ => ""
        };
        return $"{head}{preferred}bestaudio/best";
    }
}
