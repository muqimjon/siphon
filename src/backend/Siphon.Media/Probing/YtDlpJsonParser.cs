using System.Text.Json;
using Siphon.Media.Policy;

namespace Siphon.Media.Probing;

public static class YtDlpJsonParser
{
    private static readonly HashSet<string> RemuxFamilies = ["h264", "hevc", "vp9", "av1"];
    private static readonly HashSet<string> ImageExts = ["jpg", "jpeg", "png", "webp"];

    public static ProbeResult Parse(string url, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return Str(root, "_type") == "playlist" ? ParsePlaylist(url, root) : ParseSingle(url, root);
    }

    private static ProbeResult ParseSingle(string url, JsonElement root)
    {
        var title = Str(root, "title") ?? Str(root, "id") ?? "media";
        var uploader = Str(root, "uploader") ?? Str(root, "channel");
        var thumbnail = Str(root, "thumbnail");
        var duration = Dbl(root, "duration");
        var liveStatus = Str(root, "live_status");
        var isLive = Bool(root, "is_live") || liveStatus is "is_live" or "is_upcoming";
        var extractor = Str(root, "extractor_key") ?? Str(root, "extractor");

        var audio = new List<AudioVariant>();
        var video = new List<VideoVariant>();
        var images = new List<ImageEntry>();

        if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in formats.EnumerateArray())
            {
                var vcodec = Str(f, "vcodec") ?? "none";
                var acodec = Str(f, "acodec") ?? "none";
                var formatId = Str(f, "format_id");
                if (formatId is null || Str(f, "format_note") == "storyboard") continue;

                var size = Long(f, "filesize") ?? Long(f, "filesize_approx");
                if (vcodec == "none" && acodec != "none")
                {
                    var abr = ToKbps(Dbl(f, "abr") ?? Dbl(f, "tbr"));
                    audio.Add(new AudioVariant(formatId, CodecFamily(acodec), abr, size, Mp3QualityMapper.Map(abr)));
                }
                else if (vcodec != "none")
                {
                    var family = CodecFamily(vcodec);
                    var needsMerge = acodec == "none";
                    var vbr = ToKbps(Dbl(f, "vbr") ?? Dbl(f, "tbr"));
                    video.Add(new VideoVariant(formatId, family, Int(f, "width"), Int(f, "height"), Dbl(f, "fps"),
                        vbr, size, needsMerge, RemuxFamilies.Contains(family) ? "remux" : "reencode"));
                }
            }
        }

        if (audio.Count == 0 && video.Count == 0 && ImageExts.Contains(Str(root, "ext") ?? ""))
            images.Add(new ImageEntry(1, Str(root, "url") ?? thumbnail, Int(root, "width"), Int(root, "height")));

        audio = [.. audio.GroupBy(a => a.Codec).Select(g => g.OrderByDescending(a => a.AbrKbps ?? 0).First())
            .OrderByDescending(a => a.AbrKbps ?? 0)];
        video = [.. video.GroupBy(v => (v.Codec, v.Height, Fps: (int?)(v.Fps ?? 0)))
            .Select(g => g.OrderByDescending(v => v.VbrKbps ?? 0).First())
            .OrderByDescending(v => v.Height ?? 0).ThenByDescending(v => v.Fps ?? 0).ThenByDescending(v => v.VbrKbps ?? 0)];

        var kind = video.Count > 0 ? "video" : audio.Count > 0 ? "audio" : images.Count > 0 ? "image" : "video";
        if (kind == "video" && video.Count == 0 && !isLive)
            throw new MediaEngineException(ErrorCodes.Unavailable, "No downloadable formats found.");

        return new ProbeResult(url, kind, title, uploader, thumbnail, duration, isLive, video, audio, images)
        { Extractor = extractor };
    }

    private static ProbeResult ParsePlaylist(string url, JsonElement root)
    {
        var title = Str(root, "title") ?? Str(root, "id") ?? "gallery";
        var extractor = Str(root, "extractor_key") ?? Str(root, "extractor");
        var images = new List<ImageEntry>();
        var hasVideoEntries = false;

        if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var e in entries.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                index++;
                var ext = Str(e, "ext");
                var hasFormats = e.TryGetProperty("formats", out var ef) && ef.ValueKind == JsonValueKind.Array && ef.GetArrayLength() > 0;
                if (!hasFormats && (ext is null || ImageExts.Contains(ext)))
                    images.Add(new ImageEntry(index, Str(e, "url") ?? Str(e, "thumbnail"), Int(e, "width"), Int(e, "height")));
                else
                    hasVideoEntries = true;
            }
        }

        var kind = hasVideoEntries ? "playlist" : "gallery";
        return new ProbeResult(url, kind, title, Str(root, "uploader"), Str(root, "thumbnail"), null, false, [], [], images)
        { Extractor = extractor };
    }

    private static string CodecFamily(string codec)
    {
        var token = codec.Split('.')[0].ToLowerInvariant();
        return token switch
        {
            "avc1" or "avc3" or "h264" => "h264",
            "hev1" or "hvc1" or "h265" => "hevc",
            "vp9" or "vp09" => "vp9",
            "av01" => "av1",
            "mp4a" => "aac",
            _ => token,
        };
    }

    private static int? ToKbps(double? v) => v is null ? null : (int)Math.Round(v.Value);
    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static double? Dbl(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;
    private static int? Int(JsonElement e, string name) => ToKbps(Dbl(e, name));
    private static long? Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? (long)p.GetDouble() : null;
    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
}
