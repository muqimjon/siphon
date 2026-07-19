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

    public bool CanHandle(UpdateContext ctx) => ctx.Update.MyChatMember is not null;

    public async Task HandleAsync(UpdateContext ctx, CancellationToken ct)
    {
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
