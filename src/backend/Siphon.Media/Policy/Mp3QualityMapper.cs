using Siphon.Media.Probing;

namespace Siphon.Media.Policy;

public static class Mp3QualityMapper
{
    public static PlannedMp3 Map(int? sourceAbrKbps) => sourceAbrKbps switch
    {
        null => new(2, 190),
        >= 192 => new(0, 245),
        >= 160 => new(1, 225),
        >= 128 => new(2, 190),
        >= 112 => new(3, 175),
        >= 96 => new(4, 165),
        >= 64 => new(5, 130),
        _ => new(6, 115),
    };
}
