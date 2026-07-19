using Telegram.Bot.Types;

namespace Siphon.Bot.Core;

public interface IFeatureModule
{
    IReadOnlyList<BotCommand> Commands { get; }
    bool CanHandle(UpdateContext ctx);
    Task HandleAsync(UpdateContext ctx, CancellationToken ct);
}
