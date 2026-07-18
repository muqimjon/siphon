using Siphon.Media.Probing;

namespace Siphon.Tests;

public class YtDlpJsonParserTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Parses_youtube_video()
    {
        var probe = YtDlpJsonParser.Parse("https://youtu.be/ei1qAsLJePE", Fixture("youtube-video.json"));

        Assert.Equal("video", probe.Kind);
        Assert.StartsWith("Jahongir Otajonov", probe.Title);
        Assert.Equal(216, probe.DurationSec);
        Assert.False(probe.IsLive);

        Assert.Contains(probe.AudioVariants, a => a.Codec == "opus");
        Assert.Contains(probe.AudioVariants, a => a.Codec == "aac");
        var opus = probe.AudioVariants.First(a => a.Codec == "opus");
        Assert.Equal("251", opus.FormatId);
        Assert.Equal(2, opus.PlannedMp3.Qscale);

        Assert.Equal(1080, probe.VideoVariants[0].Height);
        Assert.All(probe.VideoVariants.Where(v => v.Codec is "h264" or "vp9" or "av1"), v => Assert.Equal("remux", v.Delivery));
        Assert.Contains(probe.VideoVariants, v => v.Codec == "h264" && v.Height == 1080);
        Assert.Contains(probe.VideoVariants, v => v.NeedsMerge);
    }

    [Fact]
    public void Audio_variants_keep_best_per_codec()
    {
        var probe = YtDlpJsonParser.Parse("u", Fixture("youtube-video.json"));
        Assert.Equal(probe.AudioVariants.Count, probe.AudioVariants.Select(a => a.Codec).Distinct().Count());
        Assert.True(probe.AudioVariants.First(a => a.Codec == "opus").AbrKbps > 100);
    }

    [Fact]
    public void Parses_instagram_carousel_as_gallery()
    {
        var probe = YtDlpJsonParser.Parse("https://www.instagram.com/p/DEMO123/", Fixture("instagram-carousel.json"));
        Assert.Equal("gallery", probe.Kind);
        Assert.Equal(3, probe.Images.Count);
        Assert.Equal("Instagram", probe.Extractor);
        Assert.Empty(probe.VideoVariants);
    }

    [Fact]
    public void Video_playlist_marked_as_playlist()
    {
        var probe = YtDlpJsonParser.Parse("https://youtube.com/playlist?list=PL123", Fixture("playlist-videos.json"));
        Assert.Equal("playlist", probe.Kind);
    }
}
