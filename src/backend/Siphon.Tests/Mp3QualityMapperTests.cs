using Siphon.Media.Policy;

namespace Siphon.Tests;

public class Mp3QualityMapperTests
{
    [Theory]
    [InlineData(320, 0)]
    [InlineData(192, 0)]
    [InlineData(191, 1)]
    [InlineData(160, 1)]
    [InlineData(159, 2)]
    [InlineData(128, 2)]
    [InlineData(127, 3)]
    [InlineData(112, 3)]
    [InlineData(111, 4)]
    [InlineData(96, 4)]
    [InlineData(95, 5)]
    [InlineData(64, 5)]
    [InlineData(63, 6)]
    [InlineData(1, 6)]
    public void Maps_source_bitrate_to_vbr_quality(int abr, int expectedQscale) =>
        Assert.Equal(expectedQscale, Mp3QualityMapper.Map(abr).Qscale);

    [Fact]
    public void Unknown_bitrate_falls_back_to_q2() =>
        Assert.Equal(2, Mp3QualityMapper.Map(null).Qscale);

    [Fact]
    public void Never_promises_320k()
    {
        for (var abr = 1; abr <= 512; abr++)
            Assert.True(Mp3QualityMapper.Map(abr).TypicalKbps < 320);
    }
}
