using System.Text;

namespace Siphon.Media.Delivery;

public static class FileNameSanitizer
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string Sanitize(string title, int maxBytes = 180)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var ch in title)
            builder.Append(ch is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' || char.IsControl(ch) ? ' ' : ch);

        var name = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.', ' ');
        if (name.Length == 0) return "media";
        if (Reserved.Contains(name)) return "_" + name;

        while (Encoding.UTF8.GetByteCount(name) > maxBytes)
            name = name[..^1];
        return name.TrimEnd('.', ' ') is { Length: > 0 } trimmed ? trimmed : "media";
    }
}
