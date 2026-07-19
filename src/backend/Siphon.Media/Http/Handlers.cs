using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Siphon.Media.Delivery;
using Siphon.Media.Engine;
using Siphon.Media.Jobs;
using Siphon.Media.Policy;
using Siphon.Media.Probing;

namespace Siphon.Media.Http;

public sealed class ProbeHandler(YtDlpEngine ytDlp, GalleryDlEngine galleryDl, ProbeJsonCache probeCache, IOptions<SiphonOptions> options)
{
    public async Task<IResult> Handle(ProbeRequest request, CancellationToken ct)
    {
        if (!IsHttpUrl(request.Url, out var uri))
            return Problems.Create(ErrorCodes.InvalidUrl, "Provide a valid http(s) URL.");

        var cookiesDir = request.Cookies is null
            ? null
            : Path.Combine(Path.GetFullPath(options.Value.TempRoot), "probe-" + Guid.NewGuid().ToString("N"));
        string? cookiesPath = null;
        if (cookiesDir is not null)
        {
            Directory.CreateDirectory(cookiesDir);
            cookiesPath = Path.Combine(cookiesDir, "cookies.txt");
            await File.WriteAllTextAsync(cookiesPath, request.Cookies, ct);
        }

        try
        {
            var json = await ytDlp.ProbeJsonAsync(request.Url, cookiesPath, ct);
            var probe = YtDlpJsonParser.Parse(request.Url, json);
            if (cookiesPath is null) probeCache.Put(request.Url, json);
            if (probe.Kind != "playlist") return Results.Ok(probe);
            if (IsGalleryCandidate(uri) && galleryDl.Enabled)
                return Results.Ok(probe with { Kind = "gallery" });
            return Problems.Create(ErrorCodes.PlaylistNotSupported, "Playlists are not supported yet.");
        }
        catch (MediaEngineException ex) when (galleryDl.Enabled && ex.Code != ErrorCodes.LoginRequired)
        {
            try
            {
                var count = await galleryDl.CountAsync(request.Url, cookiesPath, ct);
                if (count == 0) return Problems.Create(ex.Code, ex.Message);
                var images = Enumerable.Range(1, Math.Min(count, options.Value.MaxGalleryImages))
                    .Select(i => new ImageEntry(i, null, null, null)).ToList();
                return Results.Ok(new ProbeResult(request.Url, "gallery", uri.Segments.Length > 1 ? uri.Segments[^1].Trim('/') : "gallery",
                    null, null, null, false, [], [], images));
            }
            catch (MediaEngineException gallery)
            {
                return gallery.Code == ErrorCodes.LoginRequired
                    ? Problems.Create(gallery.Code, gallery.Message)
                    : Problems.Create(ex.Code, ex.Message);
            }
        }
        catch (MediaEngineException ex)
        {
            return Problems.Create(ex.Code, ex.Message);
        }
        finally
        {
            if (cookiesDir is not null) JobEngine.TryDeleteDir(cookiesDir);
        }
    }

    private static bool IsGalleryCandidate(Uri uri) =>
        uri.Host.Contains("instagram.", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Contains("pinterest.", StringComparison.OrdinalIgnoreCase);

    internal static bool IsHttpUrl(string url, out Uri uri)
    {
        uri = null!;
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && parsed.Scheme is "http" or "https"
            && (uri = parsed) == parsed;
    }
}

public sealed class JobHandlers(JobEngine engine, JobStore store, GalleryDlEngine galleryDl, FileTokenService tokens, IHttpContextAccessor http, IOptions<SiphonOptions> options, IUsageSink usage)
{
    private static readonly HashSet<string> Outputs = ["audio", "video", "gallery", "convert"];

    public async Task<IResult> Create(CreateJobRequest request)
    {
        if (!ProbeHandler.IsHttpUrl(request.Url, out _))
            return Problems.Create(ErrorCodes.InvalidUrl, "Provide a valid http(s) URL.");
        if (!Outputs.Contains(request.Output))
            return Problems.Create(ErrorCodes.InvalidUrl, "Output must be audio, video or gallery.");
        if (request.Output == "gallery" && !galleryDl.Enabled)
            return Problems.Create(ErrorCodes.UnsupportedSite, "Image galleries are disabled on this server.");

        string format;
        if (request.Output == "gallery")
        {
            format = "";
        }
        else if (request.Output == "convert")
        {
            format = request.Format ?? "";
            if (!ConvertAction.IsValid(format))
                return Problems.Create(ErrorCodes.InvalidUrl, "Unsupported conversion.");
        }
        else
        {
            format = request.Format ?? OutputFormat.Default(request.Output);
            if (!OutputFormat.IsValid(request.Output, format))
                return Problems.Create(ErrorCodes.InvalidUrl, "Unsupported format for this output.");
        }

        var caller = http.HttpContext?.Items["caller"] as ApiCaller;
        if (caller?.Limits.MaxConcurrent is int maxActive && store.ActiveFor(caller.Id) >= maxActive)
            return Problems.Create(ErrorCodes.QuotaExceeded, $"You already have {maxActive} download(s) running.");
        if (caller is not null && await usage.TryConsumeAsync(caller, "download") is { } denied)
            return Problems.Create(ErrorCodes.QuotaExceeded, denied);

        var maxFileSizeMb = caller?.Limits.MaxFileSizeMb ?? options.Value.MaxFileSizeMb;

        try
        {
            var job = engine.Create(new JobRequest(request.Url, request.Output, format, request.FormatId, request.Cookies, maxFileSizeMb), caller);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", new CreateJobResponse(job.Id, $"/api/v1/jobs/{job.Id}"));
        }
        catch (MediaEngineException ex)
        {
            return Problems.Create(ex.Code, ex.Message);
        }
    }

    public IResult Status(string id)
    {
        if (store.Get(id) is not { } job)
            return Problems.Create(ErrorCodes.JobNotFound, "Job not found.");

        JobFileInfo? file = null;
        if (job is { State: JobState.Completed, FilePath: not null })
        {
            var expiresAt = (job.CompletedAt ?? DateTimeOffset.UtcNow).AddMinutes(options.Value.JobTtlMinutes);
            var token = tokens.Issue(job.Id, expiresAt);
            file = new JobFileInfo($"/api/v1/files/{job.Id}?token={token}", job.FileName!, job.FileSizeBytes ?? 0, job.ContentType!, expiresAt);
        }

        return Results.Ok(new JobStatusResponse(job.Id, job.State.ToString().ToLowerInvariant(), job.Phase,
            job.ProgressPct, job.EtaSec, job.Error, file));
    }

    public IResult Delete(string id)
    {
        if (store.Get(id) is not { } job)
            return Problems.Create(ErrorCodes.JobNotFound, "Job not found.");
        engine.Cancel(job);
        if (job.State is JobState.Completed or JobState.Failed)
        {
            job.State = JobState.Expired;
            JobEngine.TryDeleteDir(job.Dir);
        }
        return Results.NoContent();
    }
}

public sealed class FileHandler(JobStore store, FileTokenService tokens)
{
    public IResult Handle(string id, string? token)
    {
        if (store.Get(id) is not { } job)
            return Problems.Create(ErrorCodes.JobNotFound, "Job not found.");
        switch (tokens.Validate(id, token ?? ""))
        {
            case TokenVerdict.Expired:
                return Problems.Create(ErrorCodes.TokenExpired, "Download link has expired.");
            case TokenVerdict.Invalid:
                return Problems.Create(ErrorCodes.TokenInvalid, "Invalid download token.");
        }
        if (job is not { State: JobState.Completed, FilePath: not null } || !File.Exists(job.FilePath))
            return Problems.Create(ErrorCodes.JobNotFound, "File is no longer available.");
        return Results.File(job.FilePath, job.ContentType, job.FileName, enableRangeProcessing: true);
    }
}

public sealed class HealthHandler(YtDlpEngine ytDlp)
{
    private string? _ytDlpVersion;
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;

    public async Task<IResult> Handle(CancellationToken ct)
    {
        if (_ytDlpVersion is null || DateTimeOffset.UtcNow - _checkedAt > TimeSpan.FromHours(1))
        {
            try
            {
                _ytDlpVersion = await ytDlp.VersionAsync(ct);
            }
            catch
            {
                _ytDlpVersion = null;
            }
            _checkedAt = DateTimeOffset.UtcNow;
        }
        var version = typeof(HealthHandler).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        return Results.Ok(new { ok = _ytDlpVersion is not null, version, ytdlp = _ytDlpVersion });
    }
}
