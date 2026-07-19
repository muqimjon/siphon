using Siphon.Bot.Data;
using Siphon.Bot.I18n;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Siphon.Bot.Core;

public sealed class UpdateContext
{
    public required Update Update { get; init; }
    public required ITelegramBotClient Bot { get; init; }
    public required IServiceProvider Services { get; init; }
    public required long ChatId { get; init; }
    public required long UserId { get; init; }
    public ChatState State { get; set; } = null!;
    public Msg L { get; set; } = Msg.For(null);
    public Message? Message => Update.Message;
    public CallbackQuery? Callback => Update.CallbackQuery;
}
