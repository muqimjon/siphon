using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Siphon.Bot.Core;

public sealed class PollingService(ITelegramBotClient bot, IServiceProvider services, ILogger<PollingService> log) : BackgroundService
{
    static readonly BotCommandScope[] StaleCommandScopes =
    [
        new BotCommandScopeAllPrivateChats(),
        new BotCommandScopeAllGroupChats(),
        new BotCommandScopeAllChatAdministrators(),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RegisterCommandsAsync(stoppingToken);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery, UpdateType.MyChatMember],
            DropPendingUpdates = true
        };
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await bot.ReceiveAsync(OnUpdateAsync, OnErrorAsync, receiverOptions, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Polling crashed, restarting");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
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

    Task OnUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        _ = Task.Run(() => DispatchAsync(update, ct), ct);
        return Task.CompletedTask;
    }

    async Task DispatchAsync(Update update, CancellationToken ct)
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

    Task OnErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken ct)
    {
        log.LogError(exception, "Polling error");
        return Task.CompletedTask;
    }
}
