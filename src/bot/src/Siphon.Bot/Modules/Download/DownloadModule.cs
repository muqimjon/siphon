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
        var placeholder = await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.Probing, replyParameters: new ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true }, cancellationToken: ct);
        await ctx.Bot.SendChatAction(ctx.ChatId, ChatAction.Typing, cancellationToken: ct);
        string text;
        InlineKeyboardMarkup? markup = null;
        try
        {
            var probe = await api.ProbeAsync(url, ct);
            if (probe.IsLive)
            {
                text = ctx.L.ErrorFor("live-not-supported");
            }
            else
            {
                var token = probes.Put(new CachedProbe(url, probe, message.MessageId));
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    db.Events.Add(new UsageEvent { ChatId = ctx.ChatId, Kind = "probe", Site = uri.Host, Utc = DateTime.UtcNow });
                markup = VariantKeyboard.BuildTypes(probe, token, limits.Value.MaxUploadMb, ctx.L);
                var title = string.IsNullOrWhiteSpace(probe.Title) ? url : probe.Title;
                var prompt = markup is null ? ctx.L.AllTooLarge(limits.Value.MaxUploadMb) : ctx.L.ChooseType;
                text = $"{title}\n\n{prompt}";
            }
        }
        catch (BackendException ex)
        {
            text = ctx.L.ErrorFor(ex.Code);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException && !ct.IsCancellationRequested)
        {
            text = ctx.L.ServerDown;
        }
        await ctx.Bot.EditMessageText(ctx.ChatId, placeholder.MessageId, text, replyMarkup: markup, cancellationToken: ct);
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
                await ShowQualityAsync(ctx, entry, token, parts[3], parts[4], 0, cb, ct);
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
        var formats = kind == "v" ? entry.Probe.VideoFormats : entry.Probe.AudioFormats;
        if (formats.Count == 1)
        {
            await ShowQualityAsync(ctx, entry, token, kind, formats[0], 0, cb, ct);
            return;
        }
        await EditScreenAsync(ctx, entry, cb, ctx.L.ChooseFormat, VariantKeyboard.BuildFormats(entry.Probe, token, kind, ctx.L), ct);
    }

    async Task ShowQualityAsync(UpdateContext ctx, CachedProbe entry, string token, string kind, string format, int page, CallbackQuery cb, CancellationToken ct)
    {
        if (kind is not ("a" or "v"))
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
            return;
        }
        var (markup, autoRun) = VariantKeyboard.BuildQuality(entry.Probe, token, kind, format, page, limits.Value.MaxUploadMb, ctx.L);
        if (autoRun is int index)
        {
            await RunAsync(ctx, entry, kind, format, index, cb, ct);
            return;
        }
        await EditScreenAsync(ctx, entry, cb, ctx.L.ChooseQuality, markup, ct);
    }

    Task StartAsync(UpdateContext ctx, CachedProbe entry, string kind, string format, string indexText, CallbackQuery cb, CancellationToken ct)
    {
        if (kind is not ("a" or "v") || !int.TryParse(indexText, out var index))
            return ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
        return RunAsync(ctx, entry, kind, format, index, cb, ct);
    }

    async Task RunAsync(UpdateContext ctx, CachedProbe entry, string kind, string format, int index, CallbackQuery cb, CancellationToken ct)
    {
        var formatId = kind == "v"
            ? entry.Probe.VideoVariants.ElementAtOrDefault(index)?.FormatId
            : entry.Probe.AudioVariants.ElementAtOrDefault(index)?.FormatId;
        if (formatId is null)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
            return;
        }
        if (ctx.State.DownloadsToday >= limits.Value.DailyDownloadsPerChat)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.DailyLimit(limits.Value.DailyDownloadsPerChat), showAlert: true, cancellationToken: ct);
            return;
        }
        await runner.RunAsync(ctx, entry, kind == "v" ? "video" : "audio", format, formatId, cb.Message!.MessageId, ct);
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
