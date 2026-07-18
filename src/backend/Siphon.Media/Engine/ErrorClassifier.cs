using System.Text.RegularExpressions;

namespace Siphon.Media.Engine;

public static partial class ErrorClassifier
{
    private static readonly (Regex Pattern, string Code)[] Rules =
    [
        (UnsupportedUrl(), ErrorCodes.UnsupportedSite),
        (InvalidUrl(), ErrorCodes.InvalidUrl),
        (AgeRestricted(), ErrorCodes.AgeRestricted),
        (LoginRequired(), ErrorCodes.LoginRequired),
        (GeoBlocked(), ErrorCodes.GeoBlocked),
        (Unavailable(), ErrorCodes.Unavailable),
        (TooLarge(), ErrorCodes.TooLarge),
        (Live(), ErrorCodes.LiveNotSupported),
    ];

    public static string Classify(string stderr)
    {
        foreach (var (pattern, code) in Rules)
            if (pattern.IsMatch(stderr)) return code;
        return ErrorCodes.ExtractorBroken;
    }

    public static string FirstErrorLine(string stderr)
    {
        foreach (var line in stderr.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("ERROR:", StringComparison.Ordinal))
                return trimmed["ERROR:".Length..].Trim();
        }
        var fallback = stderr.Trim();
        return fallback.Length > 300 ? fallback[..300] : fallback;
    }

    [GeneratedRegex(@"Unsupported URL", RegexOptions.IgnoreCase)]
    private static partial Regex UnsupportedUrl();
    [GeneratedRegex(@"is not a valid URL", RegexOptions.IgnoreCase)]
    private static partial Regex InvalidUrl();
    [GeneratedRegex(@"Sign in to confirm your age|age.?restricted|inappropriate for some users", RegexOptions.IgnoreCase)]
    private static partial Regex AgeRestricted();
    [GeneratedRegex(@"Sign in to confirm|login required|log.?in or sign.?up|use --cookies|account cookies|rate-limit reached|requested content is not available", RegexOptions.IgnoreCase)]
    private static partial Regex LoginRequired();
    [GeneratedRegex(@"available in your country|geo.?restrict|blocked it in your|unavailable in your", RegexOptions.IgnoreCase)]
    private static partial Regex GeoBlocked();
    [GeneratedRegex(@"Video unavailable|Private video|no longer available|has been removed|This post is unavailable|content isn't available|404", RegexOptions.IgnoreCase)]
    private static partial Regex Unavailable();
    [GeneratedRegex(@"larger than max-filesize", RegexOptions.IgnoreCase)]
    private static partial Regex TooLarge();
    [GeneratedRegex(@"live event|live stream|premieres in|does not pass filter", RegexOptions.IgnoreCase)]
    private static partial Regex Live();
}
