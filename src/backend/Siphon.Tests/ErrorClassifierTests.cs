using Siphon.Media;
using Siphon.Media.Engine;

namespace Siphon.Tests;

public class ErrorClassifierTests
{
    [Theory]
    [InlineData("ERROR: Unsupported URL: https://example.com/page", ErrorCodes.UnsupportedSite)]
    [InlineData("ERROR: 'not-a-url' is not a valid URL.", ErrorCodes.InvalidUrl)]
    [InlineData("ERROR: [youtube] abc: Sign in to confirm your age. This video may be inappropriate for some users.", ErrorCodes.AgeRestricted)]
    [InlineData("ERROR: [youtube] abc: Sign in to confirm you're not a bot. Use --cookies for authentication.", ErrorCodes.LoginRequired)]
    [InlineData("ERROR: [instagram] abc: rate-limit reached or login required.", ErrorCodes.LoginRequired)]
    [InlineData("ERROR: [youtube] abc: The uploader has not made this video available in your country", ErrorCodes.GeoBlocked)]
    [InlineData("ERROR: [youtube] abc: Video unavailable", ErrorCodes.Unavailable)]
    [InlineData("ERROR: [youtube] abc: Private video. Sign in if you've been granted access", ErrorCodes.AgeRestricted, ErrorCodes.LoginRequired, ErrorCodes.Unavailable)]
    [InlineData("File is larger than max-filesize (3000000000 bytes > 2147483648 bytes)", ErrorCodes.TooLarge)]
    [InlineData("Traceback (most recent call last):\n  File \"yt_dlp\\extractor\\youtube.py\", line 100", ErrorCodes.ExtractorBroken)]
    public void Classifies_stderr(string stderr, params string[] acceptable) =>
        Assert.Contains(ErrorClassifier.Classify(stderr), acceptable);

    [Fact]
    public void Extracts_first_error_line()
    {
        var stderr = "WARNING: something minor\nERROR: Video unavailable\nmore noise";
        Assert.Equal("Video unavailable", ErrorClassifier.FirstErrorLine(stderr));
    }
}
