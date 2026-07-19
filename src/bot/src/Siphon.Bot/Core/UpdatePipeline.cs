namespace Siphon.Bot.Core;

public sealed class UpdatePipeline(IEnumerable<IUpdateMiddleware> middlewares)
{
    readonly IReadOnlyList<IUpdateMiddleware> _middlewares = middlewares.ToList();

    public Task RunAsync(UpdateContext ctx, CancellationToken ct)
    {
        Func<int, Task> step = null!;
        step = i => i < _middlewares.Count
            ? _middlewares[i].InvokeAsync(ctx, () => step(i + 1), ct)
            : Task.CompletedTask;
        return step(0);
    }
}
