using System.Globalization;

namespace Siphon.Media.Engine;

public sealed record ProgressUpdate(double Pct, int? EtaSec);

public static class ProgressParser
{
    public const string Template = "download:PROG|%(progress._percent_str)s|%(progress._eta_str)s";

    public static ProgressUpdate? Parse(string line)
    {
        if (!line.StartsWith("PROG|", StringComparison.Ordinal)) return null;
        var parts = line.Split('|');
        if (parts.Length < 3) return null;
        var pctText = parts[1].Trim().TrimEnd('%');
        if (!double.TryParse(pctText, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)) return null;
        return new ProgressUpdate(Math.Clamp(pct, 0, 100), ParseEta(parts[2].Trim()));
    }

    private static int? ParseEta(string text)
    {
        if (text.Length == 0 || text == "NA" || text == "Unknown") return null;
        var segments = text.Split(':');
        var total = 0;
        foreach (var segment in segments)
        {
            if (!int.TryParse(segment, out var value)) return null;
            total = total * 60 + value;
        }
        return total;
    }
}
