using Microsoft.AspNetCore.Http;
using Siphon.Media.Jobs;

namespace Siphon.Media.Http;

public sealed record ProbeRequest(string Url, string? Cookies);

public sealed record CreateJobRequest(string Url, string Output, string? FormatId, string? Cookies);

public sealed record CreateJobResponse(string JobId, string StatusUrl);

public sealed record JobFileInfo(string Url, string FileName, long SizeBytes, string ContentType, DateTimeOffset ExpiresAtUtc);

public sealed record JobStatusResponse(string JobId, string State, string? Phase, double ProgressPct, int? EtaSec, JobError? Error, JobFileInfo? File);

public static class Problems
{
    public static IResult Create(string code, string detail) =>
        Results.Problem(detail: detail, statusCode: ErrorCodes.HttpStatus(code), title: code,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
