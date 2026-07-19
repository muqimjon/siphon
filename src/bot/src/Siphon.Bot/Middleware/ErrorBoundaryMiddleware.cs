using Siphon.Bot.Core;
using Telegram.Bot;

namespace Siphon.Bot.Middleware;

public sealed class ErrorBoundaryMiddleware(ILogger<ErrorBoundaryMiddleware> log) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next, CancellationToken ct)
    {
        try
        {
            await next();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled error for chat {ChatId}", ctx.ChatId);
            try
            {
                await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.Oops, cancellationToken: CancellationToken.None);
            }
            catch
            {
            }
        }
    }
}
