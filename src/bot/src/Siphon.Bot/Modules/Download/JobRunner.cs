using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Polly.Timeout;
using Siphon.Bot.Backend;
using Siphon.Bot.Core;
using Siphon.Bot.Data;
using Siphon.Bot.I18n;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Siphon.Bot.Modules.Download;

public sealed class JobRunner(IOptions<LimitsOptions> limits, IHttpClientFactory httpFactory)
{
    readonly ConcurrentDictionary<long, SemaphoreSlim> _chatSlots = new();
    readonly SemaphoreSlim _globalSlots = new(limits.Value.GlobalMaxActiveJobs);

    public async Task RunAsync(UpdateContext ctx, CachedProbe entry, string output, string format, string? formatId, int messageId, CancellationToken ct)
    {
        var cb = ctx.Callback!;
        var chatSlot = _chatSlots.GetOrAdd(ctx.ChatId, _ => new SemaphoreSlim(limits.Value.MaxActiveJobsPerChat));
        if (!await chatSlot.WaitAsync(0, ct))
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Busy, showAlert: true, cancellationToken: ct);
            return;
        }
        try
        {
            if (!await _globalSlots.WaitAsync(0, ct))
            {
                await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.ServerBusy, showAlert: true, cancellationToken: ct);
                return;
            }
            try
            {
                await ctx.Bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                await ExecuteAsync(ctx, entry, output, format, formatId, messageId, ct);
            }
            finally
            {
                _globalSlots.Release();
            }
        }
        finally
        {
            chatSlot.Release();
        }
    }

    async Task ExecuteAsync(UpdateContext ctx, CachedProbe entry, string output, string format, string? formatId, int messageId, CancellationToken ct)
    {
        var api = ctx.Services.GetRequiredService<SiphonApi>();
        var lim = limits.Value;
        var lastText = "";
        var lastEdit = DateTime.MinValue;

        async Task EditAsync(string text, bool force = true)
        {
            if (text == lastText) return;
            if (!force && (DateTime.UtcNow - lastEdit).TotalSeconds < lim.EditThrottleSeconds) return;
            try
            {
                await ctx.Bot.EditMessageText(ctx.ChatId, messageId, text, cancellationToken: ct);
                lastText = text;
                lastEdit = DateTime.UtcNow;
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("not modified"))
            {
                lastText = text;
            }
        }

        await EditAsync(ctx.L.Queued);
        string jobId;
        try
        {
            jobId = await api.CreateJobAsync(entry.Url, output, format, formatId, ct);
        }
        catch (BackendException ex)
        {
            await EditAsync(ctx.L.ErrorFor(ex.Code));
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException && !ct.IsCancellationRequested)
        {
            await EditAsync(ctx.L.ServerDown);
            return;
        }

        var deadline = DateTime.UtcNow.AddMinutes(lim.JobTimeoutMinutes);
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(lim.PollSeconds), ct);
            if (DateTime.UtcNow > deadline)
            {
                await EditAsync(ctx.L.TimedOut);
                return;
            }
            JobStatus status;
            try
            {
                status = await api.GetJobAsync(jobId, ct);
            }
            catch (BackendException ex) when (ex.Code == "rate-limited")
            {
                continue;
            }
            catch (BackendException)
            {
                await EditAsync(ctx.L.Oops);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException && !ct.IsCancellationRequested)
            {
                continue;
            }
            switch (status.State)
            {
                case "completed" when status.File is not null:
                    await DeliverAsync(ctx, api, entry, formatId, status.File, messageId, text => EditAsync(text), ct);
                    return;
                case "failed":
                    await EditAsync(ctx.L.ErrorFor(status.Error?.Code ?? ""));
                    return;
                case "completed" or "canceled" or "expired":
                    await EditAsync(ctx.L.Oops);
                    return;
                default:
                    await EditAsync(ProgressText(ctx.L, status), force: false);
                    break;
            }
        }
    }

    async Task DeliverAsync(UpdateContext ctx, SiphonApi api, CachedProbe entry, string? formatId, JobFile file, int messageId, Func<string, Task> edit, CancellationToken ct)
    {
        var lim = limits.Value;
        if (file.SizeBytes > lim.MaxUploadMb * 1024L * 1024)
        {
            await edit(ctx.L.FinalTooLarge(lim.MaxUploadMb));
            return;
        }
        await edit(ctx.L.Uploading);
        var probe = entry.Probe;
        var reply = new ReplyParameters { MessageId = entry.SourceMessageId, AllowSendingWithoutReply = true };
        var duration = probe.DurationSec is double d ? (int?)d : null;
        await ctx.Bot.SendChatAction(ctx.ChatId, ActionFor(file.ContentType), cancellationToken: ct);
        await using var stream = await api.OpenFileAsync(file.Url, ct);
        var input = InputFile.FromStream(stream, file.FileName);
        if (file.ContentType.StartsWith("audio"))
        {
            var (performer, title) = SplitTitle(probe);
            var thumbnail = await GetThumbnailAsync(probe.ThumbnailUrl, ct);
            await ctx.Bot.SendAudio(ctx.ChatId, input, replyParameters: reply, duration: duration, performer: performer, title: title, thumbnail: thumbnail, cancellationToken: ct);
        }
        else if (file.ContentType.StartsWith("video"))
        {
            var variant = probe.VideoVariants.FirstOrDefault(v => v.FormatId == formatId);
            await ctx.Bot.SendVideo(ctx.ChatId, input, replyParameters: reply, duration: duration, width: variant?.Width, height: variant?.Height, supportsStreaming: true, cancellationToken: ct);
        }
        else if (file.ContentType.StartsWith("image"))
        {
            await ctx.Bot.SendPhoto(ctx.ChatId, input, replyParameters: reply, cancellationToken: ct);
        }
        else
        {
            await ctx.Bot.SendDocument(ctx.ChatId, input, replyParameters: reply, cancellationToken: ct);
        }
        try
        {
            await ctx.Bot.DeleteMessage(ctx.ChatId, messageId, ct);
        }
        catch (ApiRequestException)
        {
        }
        ctx.State.DownloadsToday++;
        ctx.Services.GetRequiredService<BotDb>().Events.Add(new UsageEvent
        {
            ChatId = ctx.ChatId,
            Kind = "download",
            Site = Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) ? uri.Host : null,
            Utc = DateTime.UtcNow
        });
    }

    async Task<InputFile?> GetThumbnailAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(url)) return null;
        try
        {
            var bytes = await httpFactory.CreateClient().GetByteArrayAsync(url, ct);
            return bytes.Length < 200 * 1024 ? InputFile.FromStream(new MemoryStream(bytes), "thumb.jpg") : null;
        }
        catch
        {
            return null;
        }
    }

    static string ProgressText(Msg l, JobStatus status)
    {
        var line = status.State == "queued" ? l.Queued : l.PhaseFor(status.Phase);
        if (status.ProgressPct is not double pct) return line;
        var filled = Math.Clamp((int)Math.Round(pct / 12.5), 0, 8);
        var bar = new string('▰', filled) + new string('▱', 8 - filled);
        var eta = status.EtaSec is double e && e > 0
            ? e < 90 ? $" · ~{e:0} s" : $" · ~{e / 60:0} min"
            : "";
        return $"{line}\n{bar} {pct:0}%{eta}";
    }

    static (string? Performer, string? Title) SplitTitle(ProbeResult probe)
    {
        if (!string.IsNullOrWhiteSpace(probe.Uploader)) return (probe.Uploader, probe.Title);
        var i = probe.Title?.IndexOf(" - ", StringComparison.Ordinal) ?? -1;
        return i > 0
            ? (probe.Title![..i].Trim(), probe.Title[(i + 3)..].Trim())
            : (null, probe.Title);
    }

    static ChatAction ActionFor(string contentType) =>
        contentType.StartsWith("video") ? ChatAction.UploadVideo
        : contentType.StartsWith("image") ? ChatAction.UploadPhoto
        : ChatAction.UploadDocument;
}
