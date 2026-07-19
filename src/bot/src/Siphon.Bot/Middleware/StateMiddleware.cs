using Microsoft.EntityFrameworkCore;
using Siphon.Bot.Core;
using Siphon.Bot.Data;
using Siphon.Bot.I18n;

namespace Siphon.Bot.Middleware;

public sealed class StateMiddleware(BotDb db) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next, CancellationToken ct)
    {
        var state = await db.Chats.FindAsync([ctx.ChatId], ct);
        if (state is null)
        {
            var code = (ctx.Message?.From ?? ctx.Callback?.From)?.LanguageCode;
            state = new ChatState { ChatId = ctx.ChatId, Lang = Msg.Normalize(code) };
            db.Chats.Add(state);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                state = (await db.Chats.FindAsync([ctx.ChatId], ct))!;
            }
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        if (state.DayKey != today)
        {
            state.DayKey = today;
            state.DownloadsToday = 0;
        }
        state.LastActivityUtc = DateTime.UtcNow;
        ctx.State = state;
        ctx.L = Msg.For(state.Lang);
        await db.SaveChangesAsync(ct);
        await next();
        await db.SaveChangesAsync(ct);
    }
}
