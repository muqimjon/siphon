using Microsoft.EntityFrameworkCore;
using Siphon.Bot.Core;
using Siphon.Bot.Data;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Siphon.Bot.Modules.Group;

public sealed class GroupModule(BotDb db) : IFeatureModule
{
    public IReadOnlyList<BotCommand> Commands { get; } = [];

    async Task MigrateAsync(long oldChatId, long newChatId, CancellationToken ct)
    {
        var old = await db.Groups.FindAsync([oldChatId], ct);
        if (old is null) return;

        if (await db.Groups.FindAsync([newChatId], ct) is null)
        {
            db.Groups.Add(new GroupInfo
            {
                ChatId = newChatId,
                Title = old.Title,
                OwnerUserId = old.OwnerUserId,
                AddedUtc = old.AddedUtc,
            });
        }

        foreach (var pref in await db.Prefs.Where(p => p.ChatId == oldChatId).ToListAsync(ct))
        {
            if (await db.Prefs.FindAsync([newChatId, pref.Platform], ct) is null)
                db.Prefs.Add(new UserPref
                {
                    ChatId = newChatId,
                    Platform = pref.Platform,
                    Kind = pref.Kind,
                    AudioFormat = pref.AudioFormat,
                    VideoFormat = pref.VideoFormat,
                    Quality = pref.Quality,
                });
            db.Prefs.Remove(pref);
        }

        db.Groups.Remove(old);
    }

    public bool CanHandle(UpdateContext ctx) =>
        ctx.Update.MyChatMember is not null || ctx.Update.Message?.MigrateToChatId is not null;

    public async Task HandleAsync(UpdateContext ctx, CancellationToken ct)
    {
        if (ctx.Update.Message?.MigrateToChatId is long newChatId)
        {
            await MigrateAsync(ctx.Update.Message.Chat.Id, newChatId, ct);
            return;
        }

        var upd = ctx.Update.MyChatMember!;
        if (upd.Chat.Type is not (ChatType.Group or ChatType.Supergroup)) return;
        var existing = await db.Groups.FindAsync([upd.Chat.Id], ct);
        if (upd.NewChatMember.Status is ChatMemberStatus.Member or ChatMemberStatus.Administrator)
        {
            if (existing is null)
            {
                db.Groups.Add(new GroupInfo
                {
                    ChatId = upd.Chat.Id,
                    Title = upd.Chat.Title ?? "",
                    OwnerUserId = upd.From.Id,
                    AddedUtc = DateTime.UtcNow
                });
                try
                {
                    await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.GroupAdded, cancellationToken: ct);
                }
                catch (ApiRequestException)
                {
                }
            }
            else
            {
                existing.Title = upd.Chat.Title ?? existing.Title;
            }
        }
        else if (upd.NewChatMember.Status is ChatMemberStatus.Left or ChatMemberStatus.Kicked && existing is not null)
        {
            db.Groups.Remove(existing);
            db.Prefs.RemoveRange(await db.Prefs.Where(p => p.ChatId == upd.Chat.Id).ToListAsync(ct));
        }
    }
}
