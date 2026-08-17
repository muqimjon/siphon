using System.Text;
using CliWrap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Siphon.Media.Delivery;
using Siphon.Media.Jobs;
using Siphon.Media.Policy;
using Siphon.Media.Probing;

namespace Siphon.Media.Engine;

public sealed record DownloadOutcome(string Path, string FileName, string ContentType);

public sealed class YtDlpEngine(IOptions<SiphonOptions> options, SelfUpdater updater, ProbeJsonCache probeCache, ILogger<YtDlpEngine> logger)
{
    private static readonly string[] PostprocessMarkers = ["[Merger]", "[ExtractAudio]", "[VideoRemuxer]", "[VideoConvertor]", "[Fixup"];
    private readonly SiphonOptions _options = options.Value;

    public async Task<string> ProbeJsonAsync(string url, string? cookiesPath, CancellationToken ct)
    {
        try
        {
            return await ProbeOnceAsync(url, cookiesPath, ct);
        }
        catch (MediaEngineException ex) when (ex.Code == ErrorCodes.ExtractorBroken)
        {
            logger.LogWarning("probe failed for {Url}, retrying once: {Message}", url, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            return await ProbeOnceAsync(url, cookiesPath, ct);
        }
    }

    private async Task<string> ProbeOnceAsync(string url, string? cookiesPath, CancellationToken ct)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var args = new List<string> { "-J", "--no-playlist", "--no-warnings", "--socket-timeout", "15", url };
        AddCommon(args, cookiesPath);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ProbeTimeoutSeconds));
        CliWrap.CommandResult result;
        try
        {
            result = await Cli.Wrap(_options.Tools.YtDlpPath)
                .WithArguments(args)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
                .WithValidation(CommandResultValidation.None)
                .RunGuardedAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MediaEngineException(ErrorCodes.ExtractorBroken, "Probe timed out.");
        }

        if (result.ExitCode == 0) return stdout.ToString();
        logger.LogWarning("yt-dlp probe exit {Code} for {Url}: {Stderr}", result.ExitCode, url, stderr.ToString().Trim());
        throw Classified(stderr.ToString());
    }

    public async Task<DownloadOutcome> DownloadAsync(Job job, Action<double, int?, string> onProgress, CancellationToken ct)
    {
        var cached = job.CookiesPath is null ? probeCache.Get(job.Request.Url) : null;
        var probe = YtDlpJsonParser.Parse(job.Request.Url, cached ?? await ProbeJsonAsync(job.Request.Url, job.CookiesPath, ct));
        if (probe.IsLive)
            throw new MediaEngineException(ErrorCodes.LiveNotSupported, "Live streams cannot be downloaded.");
        if (probe.DurationSec is { } duration && duration > _options.MaxDurationMinutes * 60)
            throw new MediaEngineException(ErrorCodes.TooLarge, $"Media is longer than {_options.MaxDurationMinutes} minutes.");
        if (probe.Kind == "playlist")
            throw new MediaEngineException(ErrorCodes.PlaylistNotSupported, "Playlists are not supported yet.");

        var audio = job.Request.Output == "audio";
        var format = ResolveFormat(job, probe, audio);
        var infoPath = cached is null ? null : Path.Combine(job.Dir, "info.json");
        if (infoPath is not null) await File.WriteAllTextAsync(infoPath, cached!, ct);

        var exit = await RunDownloadAsync(job, BuildDownloadArgs(job, probe, audio, format, infoPath, recode: false), onProgress, ct);
        if (exit.Code != 0 && infoPath is not null)
        {
            logger.LogWarning("cached info download failed for {Url}, retrying with a fresh extraction", job.Request.Url);
            infoPath = null;
            exit = await RunDownloadAsync(job, BuildDownloadArgs(job, probe, audio, format, null, recode: false), onProgress, ct);
        }
        if (exit.Code != 0 && !audio && LooksLikeMuxFailure(exit.Stderr))
        {
            logger.LogWarning("remux failed for {Url}, retrying with recode", job.Request.Url);
            exit = await RunDownloadAsync(job, BuildDownloadArgs(job, probe, audio, format, infoPath, recode: true), onProgress, ct);
        }
        if (exit.Code != 0) throw Classified(exit.Stderr);

        var ext = OutputFormat.Extension(format);
        var produced = Directory.EnumerateFiles(job.Dir)
            .Where(f => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (produced is null)
        {
            if (exit.Stdout.Contains("larger than max-filesize"))
                throw new MediaEngineException(ErrorCodes.TooLarge, $"File is larger than the {job.Request.MaxFileSizeMb} MB limit.");
            throw new MediaEngineException(
                exit.Stdout.Contains("does not pass filter") ? ErrorCodes.LiveNotSupported : ErrorCodes.ExtractorBroken,
                "Download produced no output file.");
        }

        var name = FileNameSanitizer.Sanitize(probe.Title) + ext;
        return new DownloadOutcome(produced, name, OutputFormat.ContentType(format));
    }

    private static string ResolveFormat(Job job, ProbeResult probe, bool audio)
    {
        if (job.Request.Format != OutputFormat.Best) return job.Request.Format;
        var id = job.Request.FormatId;
        if (audio)
        {
            var track = id is null
                ? probe.AudioVariants.MaxBy(a => a.AbrKbps ?? 0)
                : probe.AudioVariants.FirstOrDefault(a => a.FormatId == id);
            return OutputFormat.ForCodec(track?.Codec);
        }
        var clip = id is null
            ? probe.VideoVariants.MaxBy(v => v.Height ?? 0)
            : probe.VideoVariants.FirstOrDefault(v => v.FormatId == id);
        var sound = probe.AudioVariants.MaxBy(a => a.AbrKbps ?? 0);
        return OutputFormat.ForVideoCodec(clip?.Codec, sound?.Codec);
    }

    public async Task RunSelfUpdateAsync(CancellationToken ct)
    {
        var output = new StringBuilder();
        await Cli.Wrap(_options.Tools.YtDlpPath)
            .WithArguments(["--update-to", "stable@latest"])
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(output))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(ct);
        logger.LogInformation("yt-dlp self-update: {Output}", output.ToString().Trim());
    }

    public async Task<string> VersionAsync(CancellationToken ct)
    {
        var stdout = new StringBuilder();
        await Cli.Wrap(_options.Tools.YtDlpPath)
            .WithArguments(["--version"])
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(ct);
        return stdout.ToString().Trim();
    }

    private List<string> BuildDownloadArgs(Job job, ProbeResult probe, bool audio, string format, string? infoPath, bool recode)
    {
        var args = new List<string>
        {
            "--no-playlist", "--no-warnings", "--newline",
            "--socket-timeout", "15", "-N", "4",
            "--max-filesize", $"{job.Request.MaxFileSizeMb}M",
            "--match-filter", "!is_live & !is_upcoming",
            "--progress-template", ProgressParser.Template,
            "-o", Path.Combine(job.Dir, "%(title).120B [%(id)s].%(ext)s"),
        };

        if (audio)
        {
            args.AddRange(["-f", FormatSelector.Audio(job.Request.FormatId, format), "-x", "--audio-format", format]);
            if (format == "mp3")
            {
                var abr = job.Request.FormatId is { } id
                    ? probe.AudioVariants.FirstOrDefault(a => a.FormatId == id)?.AbrKbps
                    : probe.AudioVariants.Select(a => a.AbrKbps).Max();
                args.AddRange(["--audio-quality", Mp3QualityMapper.Map(abr).Qscale.ToString()]);
            }
        }
        else
        {
            args.AddRange(["-f", FormatSelector.Video(job.Request.FormatId, format), "--merge-output-format", format]);
            args.AddRange(recode ? ["--recode-video", format] : ["--remux-video", format]);
        }

        AddCommon(args, job.CookiesPath);
        if (infoPath is not null) args.AddRange(["--load-info-json", infoPath]);
        else args.Add(job.Request.Url);
        return args;
    }

    private void AddCommon(List<string> args, string? cookiesPath)
    {
        args.AddRange(["--ffmpeg-location", _options.Tools.FfmpegPath]);
        var cookies = cookiesPath is not null
            ? (File.Exists(cookiesPath) ? cookiesPath : null)
            : WritableCookies(_options.CookiesFile);
        if (cookies is not null) args.AddRange(["--cookies", cookies]);
        if (_options.ProxyUrl is not null) args.AddRange(["--proxy", _options.ProxyUrl]);
        if (_options.PotProviderUrl is not null)
        {
            args.AddRange(["--extractor-args", $"youtubepot-bgutilhttp:base_url={_options.PotProviderUrl}"]);
            args.AddRange(["--extractor-args", "youtube:player_client=default,-web_safari"]);
            args.AddRange(["--remote-components", "ejs:github"]);
        }
    }

    private string? _writableCookies;
    private readonly Lock _cookieLock = new();

    private string? WritableCookies(string? source)
    {
        if (source is null || !File.Exists(source)) return null;
        if (_writableCookies is not null && File.Exists(_writableCookies)) return _writableCookies;
        lock (_cookieLock)
        {
            if (_writableCookies is not null && File.Exists(_writableCookies)) return _writableCookies;
            try
            {
                var dir = string.IsNullOrEmpty(_options.TempRoot) ? Path.GetTempPath() : _options.TempRoot;
                Directory.CreateDirectory(dir);
                var dest = Path.Combine(dir, "yt-cookies.txt");
                File.Copy(source, dest, overwrite: true);
                return _writableCookies = dest;
            }
            catch
            {
                return source;
            }
        }
    }

    private async Task<(int Code, string Stdout, string Stderr)> RunDownloadAsync(
        Job job, List<string> args, Action<double, int?, string> onProgress, CancellationToken ct)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var result = await Cli.Wrap(_options.Tools.YtDlpPath)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.Merge(
                PipeTarget.ToStringBuilder(stdout),
                PipeTarget.ToDelegate(line =>
                {
                    if (ProgressParser.Parse(line) is { } update)
                        onProgress(Math.Min(update.Pct, 99), update.EtaSec, "downloading");
                    else if (PostprocessMarkers.Any(line.StartsWith))
                        onProgress(99, null, "converting");
                })))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .RunGuardedAsync(ct);
        return (result.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static bool LooksLikeMuxFailure(string stderr) =>
        stderr.Contains("Postprocessing", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("remux", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("merg", StringComparison.OrdinalIgnoreCase);

    private MediaEngineException Classified(string stderr)
    {
        var code = ErrorClassifier.Classify(stderr);
        if (code == ErrorCodes.ExtractorBroken) updater.ReportExtractorFailure();
        return new MediaEngineException(code, ErrorClassifier.FirstErrorLine(stderr));
    }
}
