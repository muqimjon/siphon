using Microsoft.Extensions.Options;
using Siphon.Bot.Backend;
using Siphon.Bot.Core;
using Siphon.Bot.Data;
using Siphon.Bot.Modules.Download;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.Modules.Convert;

public sealed record IncomingFile(string FileId, long Size, bool HasVideo, bool HasAudio);

public sealed class ConvertModule(SiphonApi api, ConvertCache cache, JobRunner runner, IOptions<LimitsOptions> limits) : IFeatureModule
{
    public IReadOnlyList<BotCommand> Commands { get; } = [];

    public bool CanHandle(UpdateContext ctx) =>
        ctx.Callback?.Data?.StartsWith("cv:") == true || (ctx.State.ConvertFiles && Extract(ctx.Message) is not null);

    public Task HandleAsync(UpdateContext ctx, CancellationToken ct) =>
        ctx.Callback is null ? OfferAsync(ctx, ct) : RunAsync(ctx, ct);

    async Task OfferAsync(UpdateContext ctx, CancellationToken ct)
    {
        var file = Extract(ctx.Message)!;
        var cap = limits.Value.MaxTelegramFileMb * 1024L * 1024;
        if (file.Size > cap)
        {
            await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.ConvertTooBig(limits.Value.MaxTelegramFileMb),
                replyParameters: Reply(ctx), cancellationToken: ct);
            return;
        }

        var token = cache.Put(new CachedFileJob(file, ctx.Message!.MessageId));
        var rows = new List<InlineKeyboardButton[]>();
        if (file.HasAudio)
            rows.Add([InlineKeyboardButton.WithCallbackData(ctx.L.ToMp3, $"cv:{token}:mp3")]);
        if (file.HasVideo)
        {
            rows.Add([
                InlineKeyboardButton.WithCallbackData(ctx.L.ToVideoNote, $"cv:{token}:videonote"),
                InlineKeyboardButton.WithCallbackData(ctx.L.ToGif, $"cv:{token}:gif"),
            ]);
            rows.Add([InlineKeyboardButton.WithCallbackData(ctx.L.ToMp4, $"cv:{token}:mp4")]);
        }

        await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.ConvertPrompt,
            replyParameters: Reply(ctx), replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    async Task RunAsync(UpdateContext ctx, CancellationToken ct)
    {
        var cb = ctx.Callback!;
        var parts = cb.Data!.Split(':');
        if (parts.Length < 3 || cache.Get(parts[1]) is not { } entry)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Expired, showAlert: true, cancellationToken: ct);
            return;
        }

        var link = await api.TelegramFileUrlAsync(ctx.Bot, entry.File.FileId, ct);
        if (link is null)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.Oops, showAlert: true, cancellationToken: ct);
            return;
        }

        await runner.RunConvertAsync(ctx, link, parts[2], entry.SourceMessageId, cb.Message!.MessageId, ct);
    }

    static ReplyParameters Reply(UpdateContext ctx) =>
        new() { MessageId = ctx.Message!.MessageId, AllowSendingWithoutReply = true };

    public static IncomingFile? Extract(Message? message) => message switch
    {
        { Video: { } v } => new IncomingFile(v.FileId, v.FileSize ?? 0, true, true),
        { Animation: { } a } => new IncomingFile(a.FileId, a.FileSize ?? 0, true, false),
        { Audio: { } a } => new IncomingFile(a.FileId, a.FileSize ?? 0, false, true),
        { Voice: { } v } => new IncomingFile(v.FileId, v.FileSize ?? 0, false, true),
        { VideoNote: { } n } => new IncomingFile(n.FileId, n.FileSize ?? 0, true, true),
        { Document: { } d } when d.MimeType?.StartsWith("video") == true =>
            new IncomingFile(d.FileId, d.FileSize ?? 0, true, true),
        { Document: { } d } when d.MimeType?.StartsWith("audio") == true =>
            new IncomingFile(d.FileId, d.FileSize ?? 0, false, true),
        _ => null,
    };
}
