using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Siphon.Bot.Data;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace Siphon.Bot.Modules.Download;

public sealed class FileCacheJanitor(
    ITelegramBotClient bot,
    IServiceProvider services,
    IOptions<LimitsOptions> limits,
    ILogger<FileCacheJanitor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromDays(Math.Max(1, limits.Value.FileCacheCheckDays));
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "File cache sweep failed");
            }
            await Task.Delay(period, stoppingToken);
        }
    }

    async Task SweepAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDb>();
        var rows = await db.Files.OrderBy(f => f.CreatedUtc).ToListAsync(ct);
        var removed = 0;
        foreach (var row in rows)
        {
            if (ct.IsCancellationRequested) return;
            if (await IsValidAsync(row.FileId, ct)) continue;
            db.Files.Remove(row);
            removed++;
        }
        if (removed > 0) await db.SaveChangesAsync(ct);
        log.LogInformation("File cache sweep: {Checked} checked, {Removed} removed", rows.Count, removed);
    }

    async Task<bool> IsValidAsync(string fileId, CancellationToken ct)
    {
        try
        {
            await bot.GetFile(fileId, ct);
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("too big", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        catch (ApiRequestException)
        {
            return false;
        }
        catch (Exception)
        {
            return true;
        }
        finally
        {
            await Task.Delay(TimeSpan.FromMilliseconds(120), ct);
        }
    }
}
