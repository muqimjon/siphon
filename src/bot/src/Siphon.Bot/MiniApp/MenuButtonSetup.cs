using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.MiniApp;

public sealed class MenuButtonSetup(ITelegramBotClient bot, IOptions<MiniAppOptions> options, ILogger<MenuButtonSetup> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var url = options.Value.BaseUrl;
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            _ = ApplyAsync(url);
        return Task.CompletedTask;
    }

    async Task ApplyAsync(string url)
    {
        try
        {
            await bot.SetChatMenuButton(menuButton: new MenuButtonWebApp
            {
                Text = "Sozlamalar",
                WebApp = new WebAppInfo { Url = url }
            });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not set the Mini App menu button");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
