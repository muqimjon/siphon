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
                bool allBlocked;
                (markup, allBlocked) = VariantKeyboard.Build(probe, token, 0, limits.Value.MaxUploadMb, ctx.L);
                var title = string.IsNullOrWhiteSpace(probe.Title) ? url : probe.Title;
                text = $"{title}\n\n{(allBlocked ? ctx.L.AllTooLarge(limits.Value.MaxUploadMb) : ctx.L.ChooseVariant)}";
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
        var parts = cb.Data!.Split(':', 4);
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
        if (kind == "p")
        {
            var page = parts.Length > 3 && int.TryParse(parts[3], out var p) ? p : 0;
            var (markup, _) = VariantKeyboard.Build(entry.Probe, token, page, limits.Value.MaxUploadMb, ctx.L);
            await ctx.Bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
            try
            {
                await ctx.Bot.EditMessageReplyMarkup(ctx.ChatId, cb.Message.MessageId, markup, cancellationToken: ct);
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("not modified"))
            {
            }
            return;
        }
        string? formatId = null;
        if (kind is "v" or "a")
        {
            formatId = int.TryParse(parts.ElementAtOrDefault(3), out var index)
                ? kind == "v"
                    ? entry.Probe.VideoVariants.ElementAtOrDefault(index)?.FormatId
                    : entry.Probe.AudioVariants.ElementAtOrDefault(index)?.FormatId
                : null;
            if (formatId is null)
            {
                await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
                return;
            }
        }
        if (ctx.State.DownloadsToday >= limits.Value.DailyDownloadsPerChat)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.DailyLimit(limits.Value.DailyDownloadsPerChat), showAlert: true, cancellationToken: ct);
            return;
        }
        var output = kind switch { "v" => "video", "a" => "audio", _ => "gallery" };
        await runner.RunAsync(ctx, entry, output, formatId, cb.Message.MessageId, ct);
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
