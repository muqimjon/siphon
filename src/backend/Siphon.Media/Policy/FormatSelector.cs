namespace Siphon.Media.Policy;

public static class FormatSelector
{
    public static string Video(string? formatId) => formatId is null
        ? "bestvideo+bestaudio[acodec^=mp4a]/bestvideo+bestaudio/best"
        : $"{formatId}+bestaudio[acodec^=mp4a]/{formatId}+bestaudio/{formatId}/best";

    public static string Audio(string? formatId) => formatId is null
        ? "bestaudio/best"
        : $"{formatId}/bestaudio/best";
}
