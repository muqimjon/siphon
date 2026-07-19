namespace Siphon.Bot.Core;

public interface IUpdateMiddleware
{
    Task InvokeAsync(UpdateContext ctx, Func<Task> next, CancellationToken ct);
}
