using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Siphon.Bot.Core;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.Middleware;

public sealed class SubscriptionGateMiddleware(IOptions<GateOptions> options, IMemoryCache cache, ILogger<SubscriptionGateMiddleware> log) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next, CancellationToken ct)
    {
        var gate = options.Value;
        if (!gate.Enabled || gate.Channel.Length == 0 || IsBypassed(ctx))
        {
            await next();
            return;
        }
        if (cache.TryGetValue(CacheKey(ctx.UserId), out _) || await CheckNowAsync(ctx, ct))
        {
            await next();
            return;
        }
        if (ctx.Callback is { } cb)
        {
            await ctx.Bot.AnswerCallbackQuery(cb.Id, ctx.L.SubscribeFirst, showAlert: true, cancellationToken: ct);
            return;
        }
        var markup = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithUrl(ctx.L.JoinChannel, $"https://t.me/{gate.Channel.TrimStart('@')}") },
            new[] { InlineKeyboardButton.WithCallbackData(ctx.L.CheckSub, "c:sub:chk") }
        });
        await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.SubscribeFirst, replyMarkup: markup, cancellationToken: ct);
    }

    public async Task<bool> CheckNowAsync(UpdateContext ctx, CancellationToken ct)
    {
        var gate = options.Value;
        if (!gate.Enabled || gate.Channel.Length == 0) return true;
        try
        {
            var channel = gate.Channel.StartsWith('@') ? gate.Channel : "@" + gate.Channel;
            var member = await ctx.Bot.GetChatMember(channel, ctx.UserId, ct);
            var ok = member.Status is ChatMemberStatus.Creator or ChatMemberStatus.Administrator or ChatMemberStatus.Member
                     || member is ChatMemberRestricted { IsMember: true };
            if (ok) cache.Set(CacheKey(ctx.UserId), true, TimeSpan.FromMinutes(gate.CacheMinutes));
            return ok;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Subscription check failed, letting user through");
            return true;
        }
    }

    static bool IsBypassed(UpdateContext ctx)
    {
        if (ctx.Update.MyChatMember is not null) return true;
        var text = ctx.Message?.Text;
        if (text is not null && (text.StartsWith("/start") || text.StartsWith("/lang"))) return true;
        var data = ctx.Callback?.Data;
        return data is not null && (data.StartsWith("c:lang:") || data == "c:sub:chk");
    }

    static string CacheKey(long userId) => $"sub:{userId}";
}
