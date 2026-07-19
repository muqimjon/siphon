using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.MiniApp;

public sealed class MenuButtonSetup(ITelegramBotClient bot, IOptions<MiniAppOptions> options, ILogger<MenuButtonSetup> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = ApplyAsync(options.Value.BaseUrl);
        return Task.CompletedTask;
    }

    async Task ApplyAsync(string url)
    {
        try
        {
            MenuButton button = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? new MenuButtonWebApp { Text = "Sozlamalar", WebApp = new WebAppInfo { Url = url } }
                : new MenuButtonDefault();
            await bot.SetChatMenuButton(menuButton: button);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not set the Mini App menu button");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
