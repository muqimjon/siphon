namespace Siphon.Bot.Data;

public static class Platforms
{
    public const string YouTube = "youtube";
    public const string Instagram = "instagram";
    public const string TikTok = "tiktok";
    public const string X = "x";
    public const string Facebook = "facebook";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [YouTube, Instagram, TikTok, X, Facebook, Other];

    public static string Label(string platform) => platform switch
    {
        YouTube => "▶️ YouTube",
        Instagram => "📸 Instagram",
        TikTok => "🎵 TikTok",
        X => "✖️ X",
        Facebook => "📘 Facebook",
        _ => "🌐 Havola"
    };

    public static string Detect(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return Other;
        var h = uri.Host.ToLowerInvariant();
        return Is(h, "youtube.com") || Is(h, "youtu.be") ? YouTube
            : Is(h, "instagram.com") ? Instagram
            : Is(h, "tiktok.com") ? TikTok
            : Is(h, "x.com") || Is(h, "twitter.com") ? X
            : Is(h, "facebook.com") || Is(h, "fb.watch") || Is(h, "fb.com") ? Facebook
            : Other;
    }

    static bool Is(string host, string domain) =>
        host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);
}
