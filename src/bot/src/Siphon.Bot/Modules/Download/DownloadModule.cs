using Microsoft.Extensions.Options;
using Polly.Timeout;
using Siphon.Bot.Backend;
using Siphon.Bot.Core;
using Siphon.Bot.Data;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.Modules.Download;

public sealed class DownloadModule(SiphonApi api, ProbeCache probes, JobRunner runner, BotDb db, IOptions<LimitsOptions> limits) : IFeatureModule
{
    public IReadOnlyList<BotCommand> Commands { get; } = [];

    public bool CanHandle(UpdateContext ctx) =>
        ctx.Callback?.Data?.StartsWith("d:") == true || ExtractUrl(ctx.Message) is not null;

    public Task HandleAsync(UpdateContext ctx, CancellationToken ct) =>
        ctx.Callback is null ? HandleLinkAsync(ctx, ct) : HandleCallbackAsync(ctx, ct);

    async Task HandleLinkAsync(UpdateContext ctx, CancellationToken ct)
    {
        var message = ctx.Message!;
        var url = ExtractUrl(message)!;
        var platform = Platforms.Detect(url);
        var pref = await db.Prefs.FindAsync([ctx.ChatId, platform], ct) ?? new UserPref { ChatId = ctx.ChatId, Platform = platform };

        if (!ctx.IsGroup && JobRunner.IsDecided(pref)
            && ctx.State.DownloadsToday < limits.Value.DailyDownloadsPerChat
            && await runner.TryInstantAsync(ctx, url, pref, message.MessageId, ct))
            return;

        var msgId = 0;
        if (!ctx.State.QuietMode)
        {
            var placeholder = await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.Probing, replyParameters: new ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true }, cancellationToken: ct);
            msgId = placeholder.MessageId;
            await ctx.Bot.SendChatAction(ctx.ChatId, ChatAction.Typing, cancellationToken: ct);
        }
        ProbeResult? probe;
        try
        {
            probe = await api.ProbeAsync(url, ctx.UserId, ct);
        }
        catch (BackendException ex)
        {
            await EditPlaceholderAsync(ctx, msgId, ctx.L.ErrorFor(ex.Code), ct);
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException && !ct.IsCancellationRequested)
        {
            await EditPlaceholderAsync(ctx, msgId, ctx.L.ServerDown, ct);
            return;
        }
        if (probe.IsLive)
        {
            await EditPlaceholderAsync(ctx, msgId, ctx.L.ErrorFor("live-not-supported"), ct);
            return;
        }

        var mb = limits.Value.MaxUploadMb;
        var entry = new CachedProbe(url, probe, message.MessageId, pref);
        var token = probes.Put(entry);
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            db.Events.Add(new UsageEvent { ChatId = ctx.ChatId, Kind = "probe", Site = uri.Host, Utc = DateTime.UtcNow });

        if (ctx.IsGroup)
        {
            ctx.OwnerUserId = (await db.Groups.FindAsync([ctx.ChatId], ct))?.OwnerUserId;
            await RunGroupAsync(ctx, entry, probe, pref, mb, msgId, ct);
            return;
        }

        var plan = PrefResolver.Resolve(probe, pref, mb);
        if (plan.Step == PrefStep.Run)
        {
            await RunVariantAsync(ctx, entry, plan.Kind, plan.Format, plan.Index, msgId, null, ct);
            return;
        }

        InlineKeyboardMarkup? markup;
        string prompt;
        switch (plan.Step)
        {
            case PrefStep.TooLarge:
                markup = null;
                prompt = ctx.L.AllTooLarge(mb);
                break;
            case PrefStep.Quality:
                var (qm, autoRun) = VariantKeyboard.BuildQuality(probe, token, plan.Kind, plan.Format, 0, mb, ctx.L, formatLocked: true);
                if (autoRun is int index)
                {
                    await RunVariantAsync(ctx, entry, plan.Kind, plan.Format, index, msgId, null, ct);
                    return;
                }
                markup = qm;
                prompt = ctx.L.ChooseQuality;
                break;
            case PrefStep.Format:
                markup = VariantKeyboard.BuildFormats(probe, token, plan.Kind, ctx.L);
                prompt = ctx.L.ChooseFormat;
                break;
            default:
                markup = VariantKeyboard.BuildTypes(probe, token, mb, ctx.L);
                prompt = markup is null ? ctx.L.AllTooLarge(mb) : ctx.L.ChooseType;
                break;
        }
        var title = string.IsNullOrWhiteSpace(probe.Title) ? url : probe.Title!;
        var screen = $"{title}\n\n{prompt}";
        if (msgId == 0)
            await ctx.Bot.SendMessage(ctx.ChatId, screen, replyParameters: new ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true }, replyMarkup: markup, cancellationToken: ct);
        else
            await ctx.Bot.EditMessageText(ctx.ChatId, msgId, screen, replyMarkup: markup, cancellationToken: ct);
    }

    static Task EditPlaceholderAsync(UpdateContext ctx, int messageId, string text, CancellationToken ct) =>
        messageId == 0 ? Task.CompletedTask : ctx.Bot.EditMessageText(ctx.ChatId, messageId, text, cancellationToken: ct);

    async Task RunGroupAsync(UpdateContext ctx, CachedProbe entry, ProbeResult probe, UserPref pref, int mb, int messageId, CancellationToken ct)
    {
        var plan = PrefResolver.ResolveGroup(probe, pref, mb);
        switch (plan.Step)
        {
            case PrefStep.Run:
                await RunVariantAsync(ctx, entry, plan.Kind, plan.Format, plan.Index, messageId, null, ct);
                return;
            case PrefStep.TooLarge:
                await EditPlaceholderAsync(ctx, messageId, ctx.L.AllTooLarge(mb), ct);
                return;
            default:
                if (probe.Images.Count == 0)
                {
                    await EditPlaceholderAsync(ctx, messageId, ctx.L.ErrorFor("unsupported-site"), ct);
                    return;
                }
                if (ctx.State.DownloadsToday >= limits.Value.DailyDownloadsPerChat)
                {
                    await EditPlaceholderAsync(ctx, messageId, ctx.L.DailyLimit(limits.Value.DailyDownloadsPerChat), ct);
                    return;
                }
                await runner.RunAsync(ctx, entry, "gallery", "", null, messageId, ct);
                return;
        }
    }

    async Task HandleCallbackAsync(UpdateContext ctx, CancellationToken ct)
    {
        var cb = ctx.Callback!;
        var parts = cb.Data!.Split(':');
        if (parts.Length < 3 || cb.Message is null)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
            return;
        }
        var token = parts[1];
        var kind = parts[2];
        if (kind == "x")
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.TooLarge(limits.Value.MaxUploadMb), showAlert: true, cancellationToken: ct);
            return;
        }
        if (kind == "i")
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
            return;
        }
        var entry = probes.Get(token);
        if (entry is null)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
            return;
        }
        switch (kind)
        {
            case "t":
                if (parts.Length == 3)
                    await ShowTypesAsync(ctx, entry, token, cb, ct);
                else
                    await ShowFormatAsync(ctx, entry, token, parts[3], cb, ct);
                break;
            case "f" when parts.Length >= 5:
                await ChooseFormatAsync(ctx, entry, token, parts[3], parts[4], cb, ct);
                break;
            case "pq" when parts.Length >= 6:
                await ShowQualityAsync(ctx, entry, token, parts[3], parts[4], int.TryParse(parts[5], out var page) ? page : 0, cb, ct);
                break;
            case "d" when parts.Length >= 6:
                await StartAsync(ctx, entry, parts[3], parts[4], parts[5], cb, ct);
                break;
            case "g":
                await StartGalleryAsync(ctx, entry, cb, ct);
                break;
            default:
                await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
                break;
        }
    }

    async Task ShowTypesAsync(UpdateContext ctx, CachedProbe entry, string token, CallbackQuery cb, CancellationToken ct)
    {
        var markup = VariantKeyboard.BuildTypes(entry.Probe, token, limits.Value.MaxUploadMb, ctx.L);
        var prompt = markup is null ? ctx.L.AllTooLarge(limits.Value.MaxUploadMb) : ctx.L.ChooseType;
        await EditScreenAsync(ctx, entry, cb, prompt, markup, ct);
    }

    async Task ShowFormatAsync(UpdateContext ctx, CachedProbe entry, string token, string kind, CallbackQuery cb, CancellationToken ct)
    {
        if (kind is not ("a" or "v"))
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
            return;
        }
        var plan = PrefResolver.Resolve(entry.Probe, entry.Pref, limits.Value.MaxUploadMb, kind);
        switch (plan.Step)
        {
            case PrefStep.Format:
                await EditScreenAsync(ctx, entry, cb, ctx.L.ChooseFormat, VariantKeyboard.BuildFormats(entry.Probe, token, kind, ctx.L), ct);
                break;
            case PrefStep.Quality:
                await ShowQualityAsync(ctx, entry, token, plan.Kind, plan.Format, 0, cb, ct, formatLocked: true);
                break;
            case PrefStep.Run:
                await RunVariantAsync(ctx, entry, plan.Kind, plan.Format, plan.Index, cb.Message!.MessageId, cb, ct);
                break;
            case PrefStep.TooLarge:
                await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.AllTooLarge(limits.Value.MaxUploadMb), showAlert: true, cancellationToken: ct);
                break;
            default:
                await ShowTypesAsync(ctx, entry, token, cb, ct);
                break;
        }
    }

    async Task ChooseFormatAsync(UpdateContext ctx, CachedProbe entry, string token, string kind, string format, CallbackQuery cb, CancellationToken ct)
    {
        if (kind is not ("a" or "v"))
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
            return;
        }
        var plan = PrefResolver.Resolve(entry.Probe, entry.Pref, limits.Value.MaxUploadMb, kind, format);
        switch (plan.Step)
        {
            case PrefStep.Run:
                await RunVariantAsync(ctx, entry, kind, format, plan.Index, cb.Message!.MessageId, cb, ct);
                break;
            case PrefStep.TooLarge:
                await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.AllTooLarge(limits.Value.MaxUploadMb), showAlert: true, cancellationToken: ct);
                break;
            default:
                await ShowQualityAsync(ctx, entry, token, kind, format, 0, cb, ct);
                break;
        }
    }

    async Task ShowQualityAsync(UpdateContext ctx, CachedProbe entry, string token, string kind, string format, int page, CallbackQuery cb, CancellationToken ct, bool formatLocked = false)
    {
        if (kind is not ("a" or "v"))
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
            return;
        }
        var (markup, autoRun) = VariantKeyboard.BuildQuality(entry.Probe, token, kind, format, page, limits.Value.MaxUploadMb, ctx.L, formatLocked);
        if (autoRun is int index)
        {
            await RunVariantAsync(ctx, entry, kind, format, index, cb.Message!.MessageId, cb, ct);
            return;
        }
        await EditScreenAsync(ctx, entry, cb, ctx.L.ChooseQuality, markup, ct);
    }

    Task StartAsync(UpdateContext ctx, CachedProbe entry, string kind, string format, string indexText, CallbackQuery cb, CancellationToken ct)
    {
        if (kind is not ("a" or "v") || !int.TryParse(indexText, out var index))
            return ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
        return RunVariantAsync(ctx, entry, kind, format, index, cb.Message!.MessageId, cb, ct);
    }

    async Task RunVariantAsync(UpdateContext ctx, CachedProbe entry, string kind, string format, int index, int messageId, CallbackQuery? cb, CancellationToken ct)
    {
        var formatId = kind == "v"
            ? entry.Probe.VideoVariants.ElementAtOrDefault(index)?.FormatId
            : entry.Probe.AudioVariants.ElementAtOrDefault(index)?.FormatId;
        if (formatId is null)
        {
            await FailAsync(ctx, cb, messageId, ctx.L.Expired, ct);
            return;
        }
        if (ctx.State.DownloadsToday >= limits.Value.DailyDownloadsPerChat)
        {
            await FailAsync(ctx, cb, messageId, ctx.L.DailyLimit(limits.Value.DailyDownloadsPerChat), ct);
            return;
        }
        await runner.RunAsync(ctx, entry, kind == "v" ? "video" : "audio", format, formatId, messageId, ct);
    }

    async Task FailAsync(UpdateContext ctx, CallbackQuery? cb, int messageId, string text, CancellationToken ct)
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

    async Task StartGalleryAsync(UpdateContext ctx, CachedProbe entry, CallbackQuery cb, CancellationToken ct)
    {
        if (ctx.State.DownloadsToday >= limits.Value.DailyDownloadsPerChat)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.DailyLimit(limits.Value.DailyDownloadsPerChat), showAlert: true, cancellationToken: ct);
            return;
        }
        await runner.RunAsync(ctx, entry, "gallery", "", null, cb.Message!.MessageId, ct);
    }

    async Task EditScreenAsync(UpdateContext ctx, CachedProbe entry, CallbackQuery cb, string prompt, InlineKeyboardMarkup? markup, CancellationToken ct)
    {
        await ctx.Bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
        var header = string.IsNullOrWhiteSpace(entry.Probe.Title) ? entry.Url : entry.Probe.Title!;
        try
        {
            await ctx.Bot.EditMessageText(ctx.ChatId, cb.Message!.MessageId, $"{header}\n\n{prompt}", replyMarkup: markup, cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not modified"))
        {
        }
    }

    static string? ExtractUrl(Message? message)
    {
        if (message?.Entities is null || message.Text is null) return null;
        foreach (var entity in message.Entities)
        {
            if (entity.Type == MessageEntityType.Url)
                return message.Text.Substring(entity.Offset, entity.Length);
            if (entity.Type == MessageEntityType.TextLink)
                return entity.Url;
        }
        return null;
    }
}
