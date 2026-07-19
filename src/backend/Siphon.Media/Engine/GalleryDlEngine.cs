using System.Text;
using System.Text.Json;
using CliWrap;
using Microsoft.Extensions.Options;
using Siphon.Media.Delivery;
using Siphon.Media.Jobs;

namespace Siphon.Media.Engine;

public sealed class GalleryDlEngine(IOptions<SiphonOptions> options)
{
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };

    private readonly SiphonOptions _options = options.Value;

    public bool Enabled => _options.Tools.GalleryDlPath is not null;

    public async Task<int> CountAsync(string url, string? cookiesPath, CancellationToken ct)
    {
        if (!Enabled) return 0;
        var stdout = new StringBuilder();
        var result = await Run(["--dump-json", url], cookiesPath)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .RunGuardedAsync(ct);
        if (stdout.ToString().Contains("login", StringComparison.OrdinalIgnoreCase))
            throw new MediaEngineException(ErrorCodes.LoginRequired, "This link is behind a login.");
        if (result.ExitCode != 0) throw new MediaEngineException(ErrorCodes.Unavailable, "No downloadable media found at this link.");
        try
        {
            using var doc = JsonDocument.Parse(stdout.ToString());
            return doc.RootElement.ValueKind != JsonValueKind.Array
                ? 0
                : doc.RootElement.EnumerateArray().Count(e =>
                    e.ValueKind == JsonValueKind.Array
                    && e.GetArrayLength() > 0
                    && e[0].ValueKind == JsonValueKind.Number
                    && e[0].GetInt32() == 3);
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    public async Task<DownloadOutcome> DownloadAsync(Job job, CancellationToken ct)
    {
        if (!Enabled) throw new MediaEngineException(ErrorCodes.UnsupportedSite, "Image galleries are disabled on this server.");

        var stderr = new StringBuilder();
        var result = await Run(["-D", job.Dir, "--range", $"1-{_options.MaxGalleryImages}", job.Request.Url], job.CookiesPath)
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .RunGuardedAsync(ct);

        var files = Directory.EnumerateFiles(job.Dir)
            .Where(f => !f.EndsWith("cookies.txt", StringComparison.OrdinalIgnoreCase)
                && !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();
        if (result.ExitCode != 0 && files.Count == 0)
            throw new MediaEngineException(ErrorClassifier.Classify(stderr.ToString()), ErrorClassifier.FirstErrorLine(stderr.ToString()));
        if (files.Count == 0)
            throw new MediaEngineException(ErrorCodes.Unavailable, "No media found in this post.");

        var baseName = FileNameSanitizer.Sanitize(LastUrlSegment(job.Request.Url));
        if (files.Count == 1)
        {
            var single = files[0];
            var ext = Path.GetExtension(single);
            var stem = baseName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                ? baseName[..^ext.Length]
                : baseName;
            return new DownloadOutcome(single, stem + ext.ToLowerInvariant(),
                ImageTypes.GetValueOrDefault(ext, "application/octet-stream"));
        }

        var zip = ZipPackager.Pack(job.Dir, files, baseName);
        return new DownloadOutcome(zip, baseName + ".zip", "application/zip");
    }

    private Command Run(IEnumerable<string> tail, string? cookiesPath)
    {
        var args = new List<string>();
        if (cookiesPath is not null && File.Exists(cookiesPath)) args.AddRange(["--cookies", cookiesPath]);
        if (_options.ProxyUrl is not null) args.AddRange(["--proxy", _options.ProxyUrl]);
        args.AddRange(tail);
        return Cli.Wrap(_options.Tools.GalleryDlPath!).WithArguments(args);
    }

    private static string LastUrlSegment(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "gallery";
        var segment = uri.Segments.Reverse().Select(s => s.Trim('/')).FirstOrDefault(s => s.Length > 0);
        return segment is null or "" ? "gallery" : segment;
    }
}
