using Siphon.Media.Policy;

namespace Siphon.Tests;

public class OutputFormatTests
{
    [Theory]
    [InlineData("audio", "mp3")]
    [InlineData("video", "mp4")]
    public void Default_matches_policy(string output, string expected) =>
        Assert.Equal(expected, OutputFormat.Default(output));

    [Theory]
    [InlineData("audio", "mp3", true)]
    [InlineData("audio", "m4a", true)]
    [InlineData("audio", "opus", true)]
    [InlineData("audio", "mp4", false)]
    [InlineData("video", "mp4", true)]
    [InlineData("video", "webm", true)]
    [InlineData("video", "mp3", false)]
    [InlineData("gallery", "mp3", false)]
    public void IsValid_enforces_output_format_pairing(string output, string format, bool expected) =>
        Assert.Equal(expected, OutputFormat.IsValid(output, format));

    [Theory]
    [InlineData("mp3", "audio/mpeg")]
    [InlineData("m4a", "audio/mp4")]
    [InlineData("opus", "audio/ogg")]
    [InlineData("mp4", "video/mp4")]
    [InlineData("webm", "video/webm")]
    public void ContentType_and_extension(string format, string contentType)
    {
        Assert.Equal(contentType, OutputFormat.ContentType(format));
        Assert.Equal("." + format, OutputFormat.Extension(format));
    }

    [Fact]
    public void AvailableAudio_adds_opus_only_when_source_has_opus()
    {
        Assert.Equal(["mp3", "m4a"], OutputFormat.AvailableAudio(["aac"]));
        Assert.Equal(["mp3", "m4a", "opus"], OutputFormat.AvailableAudio(["aac", "opus"]));
    }

    [Fact]
    public void AvailableVideo_adds_webm_only_for_vp9_or_av1()
    {
        Assert.Equal(["mp4"], OutputFormat.AvailableVideo(["h264"]));
        Assert.Equal(["mp4", "webm"], OutputFormat.AvailableVideo(["h264", "vp9"]));
        Assert.Equal(["mp4", "webm"], OutputFormat.AvailableVideo(["av1"]));
    }
}
