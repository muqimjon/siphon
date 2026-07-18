using Siphon.Media.Policy;

namespace Siphon.Tests;

public class FormatSelectorTests
{
    [Fact]
    public void Video_without_choice_prefers_aac_merge() =>
        Assert.Equal("bestvideo+bestaudio[acodec^=mp4a]/bestvideo+bestaudio/best", FormatSelector.Video(null));

    [Fact]
    public void Video_with_choice_keeps_fallback_chain() =>
        Assert.Equal("248+bestaudio[acodec^=mp4a]/248+bestaudio/248/best", FormatSelector.Video("248"));

    [Fact]
    public void Audio_without_choice() =>
        Assert.Equal("bestaudio/best", FormatSelector.Audio(null));

    [Fact]
    public void Audio_with_choice() =>
        Assert.Equal("251/bestaudio/best", FormatSelector.Audio("251"));
}
