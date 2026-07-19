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
        Assert.Equal("bestaudio/best", FormatSelector.Audio(null, "mp3"));

    [Fact]
    public void Audio_with_choice() =>
        Assert.Equal("251/bestaudio/best", FormatSelector.Audio("251", "mp3"));

    [Fact]
    public void Audio_m4a_prefers_aac_source_so_it_is_copied() =>
        Assert.Equal("bestaudio[acodec^=mp4a]/bestaudio/best", FormatSelector.Audio(null, "m4a"));

    [Fact]
    public void Audio_opus_prefers_opus_source_so_it_is_copied() =>
        Assert.Equal("bestaudio[acodec=opus]/bestaudio/best", FormatSelector.Audio(null, "opus"));
}
