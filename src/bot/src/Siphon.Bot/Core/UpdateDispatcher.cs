using Telegram.Bot;
using Telegram.Bot.Types;

namespace Siphon.Bot.Core;

public sealed class UpdateDispatcher(IServiceProvider services, ITelegramBotClient bot, ILogger<UpdateDispatcher> log)
{
    public async Task DispatchAsync(Update update, CancellationToken ct)
    {
        var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat ?? update.MyChatMember?.Chat;
        var user = update.Message?.From ?? update.CallbackQuery?.From ?? update.MyChatMember?.From;
        if (chat is null || user is null) return;
        try
        {
            using var scope = services.CreateScope();
            var ctx = new UpdateContext
            {
                Update = update,
                Bot = bot,
                Services = scope.ServiceProvider,
                ChatId = chat.Id,
                UserId = user.Id,
                ChatType = chat.Type
            };
            await scope.ServiceProvider.GetRequiredService<UpdatePipeline>().RunAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Update dispatch failed");
        }
    }
}
