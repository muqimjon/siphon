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
        var cb = ctx.Callback;
        var chatSlot = _chatSlots.GetOrAdd(ctx.ChatId, _ => new SemaphoreSlim(limits.Value.MaxActiveJobsPerChat));
        if (!await chatSlot.WaitAsync(0, ct))
        {
            await NotifyAsync(ctx, cb, messageId, ctx.L.Busy, ct);
            return;
        }
        try
        {
            if (!await _globalSlots.WaitAsync(0, ct))
            {
                await NotifyAsync(ctx, cb, messageId, ctx.L.ServerBusy, ct);
                return;
            }
            try
            {
                if (cb is not null)
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

    static async Task NotifyAsync(UpdateContext ctx, CallbackQuery? cb, int messageId, string text, CancellationToken ct)
    {
        if (cb is not null)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, text, showAlert: true, cancellationToken: ct);
            return;
        }
        if (messageId == 0) return;
        try
        {
            await ctx.Bot.EditMessageText(ctx.ChatId, messageId, text, cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not modified"))
        {
        }
    }

    public async Task RunConvertAsync(UpdateContext ctx, string sourceUrl, string action, int sourceMessageId, int messageId, CancellationToken ct)
    {
        var probe = new ProbeResult { Title = "" };
        var entry = new CachedProbe(sourceUrl, probe, sourceMessageId, new UserPref());
        await RunAsync(ctx, entry, "convert", action, null, messageId, ct);
    }

    async Task ExecuteAsync(UpdateContext ctx, CachedProbe entry, string output, string format, string? formatId, int messageId, CancellationToken ct)
    {
        var api = ctx.Services.GetRequiredService<SiphonApi>();
        var lim = limits.Value;
        var lastText = "";
        var lastEdit = DateTime.MinValue;

        async Task EditAsync(string text, bool force = true)
        {
            if (messageId == 0 || text == lastText) return;
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

        var db = ctx.Services.GetRequiredService<BotDb>();
        var cacheKey = $"{entry.Url}|{output}|{format}|{formatId}";
        if (await db.Files.FindAsync([cacheKey], ct) is { } hit && await ResendAsync(ctx, entry.SourceMessageId, hit, messageId, ct))
        {
            await CountDownloadAsync(ctx, entry, db);
            await DeleteSourceAsync(ctx, entry.SourceMessageId, ct);
            return;
        }

        await EditAsync(ctx.L.Queued);
        string jobId;
        try
        {
            jobId = await api.CreateJobAsync(entry.Url, output, format, formatId, OwnerFor(ctx), ct);
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
        var wait = TimeSpan.FromMilliseconds(400);
        var maxWait = TimeSpan.FromSeconds(lim.PollSeconds);
        while (true)
        {
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
                    await DeliverAsync(ctx, api, entry, formatId, status.File, messageId, cacheKey, text => EditAsync(text), ct);
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
            await Task.Delay(wait, ct);
            if (wait < maxWait) wait += TimeSpan.FromMilliseconds(300);
        }
    }

    async Task DeliverAsync(UpdateContext ctx, SiphonApi api, CachedProbe entry, string? formatId, JobFile file, int messageId, string cacheKey, Func<string, Task> edit, CancellationToken ct)
    {
        var lim = limits.Value;
        if (file.SizeBytes > lim.MaxUploadMb * 1024L * 1024)
        {
            await edit(ctx.L.FinalTooLarge(lim.MaxUploadMb));
            return;
        }
        await edit(ctx.L.Uploading);
        var probe = entry.Probe;
        var reply = ctx.State.ReplyToSource && !ctx.State.DeleteSourceLink
            ? new ReplyParameters { MessageId = entry.SourceMessageId, AllowSendingWithoutReply = true }
            : null;
        var duration = probe.DurationSec is double d ? (int?)d : null;
        var caption = SourceCaption(ctx, entry.Url);
        await ctx.Bot.SendChatAction(ctx.ChatId, ActionFor(file.ContentType), cancellationToken: ct);
        await using var stream = await api.OpenFileAsync(file.Url, ct);
        var input = InputFile.FromStream(stream, file.FileName);
        Message sent;
        if (file.ContentType.StartsWith("audio"))
        {
            var (performer, title) = SplitTitle(probe);
            var thumbnail = await GetThumbnailAsync(probe.ThumbnailUrl, ct);
            sent = await ctx.Bot.SendAudio(ctx.ChatId, input, caption: caption, replyParameters: reply, duration: duration, performer: performer, title: title, thumbnail: thumbnail, cancellationToken: ct);
        }
        else if (file.ContentType.StartsWith("video"))
        {
            var variant = probe.VideoVariants.FirstOrDefault(v => v.FormatId == formatId);
            sent = await ctx.Bot.SendVideo(ctx.ChatId, input, caption: caption, replyParameters: reply, duration: duration, width: variant?.Width, height: variant?.Height, supportsStreaming: true, cancellationToken: ct);
        }
        else if (file.ContentType.StartsWith("image"))
        {
            sent = await ctx.Bot.SendPhoto(ctx.ChatId, input, caption: caption, replyParameters: reply, cancellationToken: ct);
        }
        else
        {
            sent = await ctx.Bot.SendDocument(ctx.ChatId, input, caption: caption, replyParameters: reply, cancellationToken: ct);
        }
        if (messageId != 0)
        {
            try
            {
                await ctx.Bot.DeleteMessage(ctx.ChatId, messageId, ct);
            }
            catch (ApiRequestException)
            {
            }
        }
        var db = ctx.Services.GetRequiredService<BotDb>();
        Remember(db, cacheKey, sent);
        await DeleteSourceAsync(ctx, entry.SourceMessageId, ct);
        if (IsDecided(entry.Pref)) Remember(db, PrefKey(entry.Url, entry.Pref), sent);
        await CountDownloadAsync(ctx, entry, db);
    }

    static string? SourceCaption(UpdateContext ctx, string url)
    {
        var parts = new List<string>(2);
        if (ctx.State.ShowPlatform) parts.Add(Platforms.Label(Platforms.Detect(url)));
        if (ctx.State.ShowRequester)
        {
            var from = ctx.Callback?.From ?? ctx.Message?.From;
            if (from?.Username is { Length: > 0 } u) parts.Add("@" + u);
            else if (from?.FirstName is { Length: > 0 } n) parts.Add(n);
        }
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    static async Task DeleteSourceAsync(UpdateContext ctx, int sourceMessageId, CancellationToken ct)
    {
        if (!ctx.State.DeleteSourceLink) return;
        try
        {
            await ctx.Bot.DeleteMessage(ctx.ChatId, sourceMessageId, ct);
        }
        catch (ApiRequestException)
        {
        }
    }

    static void Remember(BotDb db, string cacheKey, Message sent)
    {
        var (fileId, kind) = sent switch
        {
            { Audio: { } a } => (a.FileId, "audio"),
            { Video: { } v } => (v.FileId, "video"),
            { Document: { } d } => (d.FileId, "document"),
            _ => (null, "")
        };
        if (fileId is null) return;
        if (db.Files.Local.FirstOrDefault(f => f.Key == cacheKey) is { } stale)
        {
            stale.FileId = fileId;
            stale.Kind = kind;
            stale.CreatedUtc = DateTime.UtcNow;
            return;
        }
        db.Files.Add(new CachedFile { Key = cacheKey, FileId = fileId, Kind = kind, CreatedUtc = DateTime.UtcNow });
    }

    static Task CountDownloadAsync(UpdateContext ctx, CachedProbe entry, BotDb db)
    {
        ctx.State.DownloadsToday++;
        db.Events.Add(new UsageEvent
        {
            ChatId = ctx.ChatId,
            Kind = "download",
            Site = Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) ? uri.Host : null,
            Utc = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    static long OwnerFor(UpdateContext ctx) => ctx.OwnerUserId ?? ctx.UserId;

    public static string PrefKey(string url, UserPref pref) =>
        $"{url}|pref|{pref.Kind}|{pref.AudioFormat}|{pref.VideoFormat}|{pref.Quality}";

    public static bool IsDecided(UserPref pref) =>
        pref.Kind is "audio" or "video"
        && pref.Quality != "ask"
        && (pref.Kind == "video" ? pref.VideoFormat : pref.AudioFormat) != "ask";

    public async Task<bool> TryInstantAsync(UpdateContext ctx, string url, UserPref pref, int sourceMessageId, CancellationToken ct)
    {
        var db = ctx.Services.GetRequiredService<BotDb>();
        if (await db.Files.FindAsync([PrefKey(url, pref)], ct) is not { } hit) return false;
        if (!await ResendAsync(ctx, sourceMessageId, hit, null, ct)) return false;
        await DeleteSourceAsync(ctx, sourceMessageId, ct);
        ctx.State.DownloadsToday++;
        db.Events.Add(new UsageEvent
        {
            ChatId = ctx.ChatId,
            Kind = "download",
            Site = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null,
            Utc = DateTime.UtcNow
        });
        return true;
    }

    static async Task<bool> ResendAsync(UpdateContext ctx, int sourceMessageId, CachedFile hit, int? placeholderId, CancellationToken ct)
    {
        var reply = ctx.State.ReplyToSource && !ctx.State.DeleteSourceLink
            ? new ReplyParameters { MessageId = sourceMessageId, AllowSendingWithoutReply = true }
            : null;
        var input = InputFile.FromFileId(hit.FileId);
        try
        {
            switch (hit.Kind)
            {
                case "audio":
                    await ctx.Bot.SendAudio(ctx.ChatId, input, replyParameters: reply, cancellationToken: ct);
                    break;
                case "video":
                    await ctx.Bot.SendVideo(ctx.ChatId, input, replyParameters: reply, supportsStreaming: true, cancellationToken: ct);
                    break;
                default:
                    await ctx.Bot.SendDocument(ctx.ChatId, input, replyParameters: reply, cancellationToken: ct);
                    break;
            }
        }
        catch (ApiRequestException)
        {
            return false;
        }
        if (placeholderId is int id)
        {
            try
            {
                await ctx.Bot.DeleteMessage(ctx.ChatId, id, ct);
            }
            catch (ApiRequestException)
            {
            }
        }
        return true;
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
