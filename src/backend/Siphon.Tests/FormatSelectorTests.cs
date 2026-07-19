using Siphon.Media.Policy;

namespace Siphon.Tests;

public class FormatSelectorTests
{
    [Fact]
    public void Video_mp4_without_choice_prefers_aac_merge() =>
        Assert.Equal("bestvideo+bestaudio[acodec^=mp4a]/bestvideo+bestaudio/best", FormatSelector.Video(null, "mp4"));

    [Fact]
    public void Video_mp4_with_choice_keeps_fallback_chain() =>
        Assert.Equal("248+bestaudio[acodec^=mp4a]/248+bestaudio/248/best", FormatSelector.Video("248", "mp4"));

    [Fact]
    public void Video_webm_prefers_opus_audio() =>
        Assert.Equal("248+bestaudio[acodec=opus]/248+bestaudio[acodec=vorbis]/248+bestaudio/248/best", FormatSelector.Video("248", "webm"));

    [Fact]
    public void Audio_without_choice() =>
        Assert.Equal("bestaudio/best", FormatSelector.Audio(null));

    [Fact]
    public void Audio_with_choice() =>
        Assert.Equal("251/bestaudio/best", FormatSelector.Audio("251"));
}
