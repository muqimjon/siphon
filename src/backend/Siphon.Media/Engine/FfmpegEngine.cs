using System.Text;
using System.Text.Json;
using CliWrap;
using Microsoft.Extensions.Options;
using Siphon.Media.Delivery;
using Siphon.Media.Jobs;
using Siphon.Media.Policy;

namespace Siphon.Media.Engine;

public sealed class FfmpegEngine(IOptions<SiphonOptions> options, IHttpClientFactory http)
{
    private const int VideoNoteSeconds = 60;
    private const int GifSeconds = 12;
    private readonly SiphonOptions _options = options.Value;

    public async Task<DownloadOutcome> ConvertAsync(Job job, Action<double, int?, string> onProgress, CancellationToken ct)
    {
        var spec = ConvertAction.Get(job.Request.Format)
            ?? throw new MediaEngineException(ErrorCodes.InvalidUrl, "Unknown conversion.");

        onProgress(5, null, "downloading");
        var source = Path.Combine(job.Dir, "source");
        await DownloadAsync(job.Request.Url, source, job.Request.MaxFileSizeMb, ct);

        onProgress(60, null, "converting");
        var target = Path.Combine(job.Dir, "out" + spec.Extension);
        var args = await BuildArgsAsync(spec.Id, source, target, ct);

        var stderr = new StringBuilder();
        var result = await Cli.Wrap(_options.Tools.FfmpegPath)
            .WithArguments(args)
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .RunGuardedAsync(ct);

        if (result.ExitCode != 0 || !File.Exists(target))
            throw new MediaEngineException(ErrorCodes.ExtractorBroken, "Could not convert this file.");

        return new DownloadOutcome(target, "siphon" + spec.Extension, spec.ContentType);
    }

    private async Task DownloadAsync(string url, string path, int maxMb, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Siphon/1.0");
        using var response = await http.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new MediaEngineException(ErrorCodes.Unavailable, "Could not fetch the file.");

        var cap = (long)maxMb * 1024 * 1024;
        if (response.Content.Headers.ContentLength is > 0 and var length && length > cap)
            throw new MediaEngineException(ErrorCodes.TooLarge, $"File is larger than the {maxMb} MB limit.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(path);
        await stream.CopyToAsync(file, ct);
    }

    private async Task<List<string>> BuildArgsAsync(string action, string source, string target, CancellationToken ct)
    {
        var args = new List<string> { "-y", "-i", source };
        switch (action)
        {
            case "mp3":
                var quality = Mp3QualityMapper.Map(await AudioKbpsAsync(source, ct));
                args.AddRange(["-vn", "-c:a", "libmp3lame", "-q:a", quality.Qscale.ToString()]);
                break;
            case "m4a":
                args.AddRange(["-vn", "-c:a", "copy"]);
                break;
            case "mp4":
                args.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-c:a", "aac"]);
                break;
            case "videonote":
                args.AddRange([
                    "-t", VideoNoteSeconds.ToString(),
                    "-vf", @"crop='min(iw,ih)':'min(iw,ih)',scale=480:480",
                    "-c:v", "libx264", "-preset", "veryfast", "-crf", "26", "-c:a", "aac", "-b:a", "96k"
                ]);
                break;
            case "gif":
                args.AddRange([
                    "-t", GifSeconds.ToString(),
                    "-vf", "fps=12,scale=480:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse",
                    "-loop", "0"
                ]);
                break;
        }
        args.Add(target);
        return args;
    }

    private async Task<int?> AudioKbpsAsync(string path, CancellationToken ct)
    {
        var probe = Path.Combine(Path.GetDirectoryName(_options.Tools.FfmpegPath)!, "ffprobe.exe");
        if (!File.Exists(probe)) probe = "ffprobe";

        var stdout = new StringBuilder();
        try
        {
            await Cli.Wrap(probe)
                .WithArguments(["-v", "quiet", "-select_streams", "a:0", "-show_entries", "stream=bit_rate", "-of", "json", path])
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(ct);

            using var doc = JsonDocument.Parse(stdout.ToString());
            var streams = doc.RootElement.GetProperty("streams");
            if (streams.GetArrayLength() == 0) return null;
            return streams[0].TryGetProperty("bit_rate", out var br) && double.TryParse(br.GetString(), out var bits)
                ? (int)Math.Round(bits / 1000)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
