using Siphon.Media.Engine;

namespace Siphon.Tests;

public class ProgressParserTests
{
    [Fact]
    public void Parses_percent_and_eta()
    {
        var update = ProgressParser.Parse("PROG| 42.1%|00:18");
        Assert.NotNull(update);
        Assert.Equal(42.1, update.Pct, 3);
        Assert.Equal(18, update.EtaSec);
    }

    [Fact]
    public void Parses_hours_eta()
    {
        var update = ProgressParser.Parse("PROG|  5.0%|01:02:03");
        Assert.Equal(3723, update!.EtaSec);
    }

    [Fact]
    public void Unknown_eta_is_null()
    {
        var update = ProgressParser.Parse("PROG|100.0%|NA");
        Assert.Equal(100, update!.Pct);
        Assert.Null(update.EtaSec);
    }

    [Theory]
    [InlineData("[download] 42.1% of ~113MiB")]
    [InlineData("PROG|NA|NA")]
    [InlineData("random noise")]
    [InlineData("PROG|")]
    public void Rejects_non_progress_lines(string line) =>
        Assert.Null(ProgressParser.Parse(line));
}
