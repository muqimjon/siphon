using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Siphon.Bot.Core;

public sealed class WebhookInitializer(
    ITelegramBotClient bot,
    IServiceProvider services,
    IOptions<BotOptions> botOptions,
    IOptions<MiniAppOptions> miniApp,
    ILogger<WebhookInitializer> log) : IHostedService
{
    static readonly BotCommandScope[] StaleCommandScopes =
    [
        new BotCommandScopeAllPrivateChats(),
        new BotCommandScopeAllGroupChats(),
        new BotCommandScopeAllChatAdministrators(),
    ];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = InitAsync(cancellationToken);
        return Task.CompletedTask;
    }

    async Task InitAsync(CancellationToken ct)
    {
        await RegisterCommandsAsync(ct);

        var baseUrl = miniApp.Value.BaseUrl.TrimEnd('/');
        if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            log.LogWarning("MiniApp:BaseUrl is not https, skipping webhook registration ({Url})", baseUrl);
            return;
        }
        try
        {
            await bot.SetWebhook(
                url: baseUrl + "/tg/webhook",
                allowedUpdates: [UpdateType.Message, UpdateType.CallbackQuery, UpdateType.MyChatMember],
                dropPendingUpdates: true,
                secretToken: botOptions.Value.WebhookSecret,
                cancellationToken: ct);
            log.LogInformation("Telegram webhook registered at {Url}/tg/webhook", baseUrl);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to register Telegram webhook");
        }
    }

    async Task RegisterCommandsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var commands = scope.ServiceProvider.GetServices<IFeatureModule>().SelectMany(m => m.Commands).ToList();
            foreach (var scopeToClear in StaleCommandScopes)
                await bot.DeleteMyCommands(scopeToClear, cancellationToken: ct);
            await bot.SetMyCommands(commands, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to register bot commands");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
