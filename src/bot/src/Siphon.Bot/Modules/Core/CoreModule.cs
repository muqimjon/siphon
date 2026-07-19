using Siphon.Bot.Core;
using Siphon.Bot.I18n;
using Siphon.Bot.Middleware;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.Modules.Core;

public sealed class CoreModule(SubscriptionGateMiddleware gate) : IFeatureModule
{
    static readonly InlineKeyboardMarkup LangKeyboard = new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🇺🇿 O'zbekcha", "c:lang:uz"),
            InlineKeyboardButton.WithCallbackData("🇷🇺 Русский", "c:lang:ru"),
            InlineKeyboardButton.WithCallbackData("🇬🇧 English", "c:lang:en")
        }
    });

    public IReadOnlyList<BotCommand> Commands { get; } =
    [
        new("start", "Boshlash · Start"),
        new("lang", "Til · Language")
    ];

    public bool CanHandle(UpdateContext ctx) =>
        ctx.Callback?.Data?.StartsWith("c:") == true
        || (ctx.Message is not null && (!ctx.IsGroup || IsCommand(ctx.Message.Text)));

    static bool IsCommand(string? text) =>
        text is not null && (text.StartsWith("/start") || text.StartsWith("/lang"));

    public async Task HandleAsync(UpdateContext ctx, CancellationToken ct)
    {
        if (ctx.Callback is { } cb)
        {
            await HandleCallbackAsync(ctx, cb, ct);
            return;
        }
        var text = ctx.Message!.Text ?? "";
        if (text.StartsWith("/start") || text.StartsWith("/lang"))
            await ctx.Bot.SendMessage(ctx.ChatId, Msg.ChooseLang, replyMarkup: LangKeyboard, cancellationToken: ct);
        else
            await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.SendLink, replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
    }

    async Task HandleCallbackAsync(UpdateContext ctx, CallbackQuery cb, CancellationToken ct)
    {
        var data = cb.Data ?? "";
        if (data.StartsWith("c:lang:"))
        {
            var lang = Msg.Normalize(data[7..]);
            ctx.State.Lang = lang;
            ctx.L = Msg.For(lang);
            await ctx.Bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
            if (cb.Message is { } message)
            {
                try
                {
                    await ctx.Bot.EditMessageText(ctx.ChatId, message.MessageId, ctx.L.Welcome, cancellationToken: ct);
                }
                catch (ApiRequestException ex) when (ex.Message.Contains("not modified"))
                {
                }
            }
            return;
        }
        if (data == "c:sub:chk")
        {
            if (await gate.CheckNowAsync(ctx, ct))
            {
                await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.SubOk, cancellationToken: ct);
                if (cb.Message is { } message)
                {
                    try
                    {
                        await ctx.Bot.DeleteMessage(ctx.ChatId, message.MessageId, ct);
                    }
                    catch (ApiRequestException)
                    {
                    }
                }
                await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.SendLink, replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
            }
            else
            {
                await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.SubNotYet, showAlert: true, cancellationToken: ct);
            }
            return;
        }
        await ctx.Bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
    }
}
