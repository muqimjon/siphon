namespace Siphon.Media.Policy;

public sealed record ConvertActionSpec(string Id, string Extension, string ContentType);

public static class ConvertAction
{
    private static readonly Dictionary<string, ConvertActionSpec> Specs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mp3"] = new("mp3", ".mp3", "audio/mpeg"),
        ["m4a"] = new("m4a", ".m4a", "audio/mp4"),
        ["mp4"] = new("mp4", ".mp4", "video/mp4"),
        ["videonote"] = new("videonote", ".mp4", "video/mp4"),
        ["gif"] = new("gif", ".gif", "image/gif"),
    };

    public static bool IsValid(string action) => Specs.ContainsKey(action);

    public static ConvertActionSpec? Get(string action) => Specs.GetValueOrDefault(action);
}
