namespace Siphon.Media;

public static class ErrorCodes
{
    public const string InvalidUrl = "invalid-url";
    public const string UnsupportedSite = "unsupported-site";
    public const string Unavailable = "unavailable";
    public const string GeoBlocked = "geo-blocked";
    public const string AgeRestricted = "age-restricted";
    public const string LoginRequired = "login-required";
    public const string LiveNotSupported = "live-not-supported";
    public const string PlaylistNotSupported = "playlist-not-supported";
    public const string TooLarge = "too-large";
    public const string ExtractorBroken = "extractor-broken";
    public const string QueueFull = "queue-full";
    public const string DiskFull = "disk-full";
    public const string JobNotFound = "job-not-found";
    public const string TokenInvalid = "token-invalid";
    public const string TokenExpired = "token-expired";
    public const string Unauthorized = "unauthorized";
    public const string QuotaExceeded = "quota-exceeded";

    public static int HttpStatus(string code) => code switch
    {
        InvalidUrl => 400,
        Unauthorized => 401,
        TooLarge => 413,
        QueueFull => 429,
        QuotaExceeded => 429,
        DiskFull => 503,
        ExtractorBroken => 502,
        JobNotFound => 404,
        TokenInvalid => 403,
        TokenExpired => 410,
        _ => 422,
    };
}

public sealed class MediaEngineException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
