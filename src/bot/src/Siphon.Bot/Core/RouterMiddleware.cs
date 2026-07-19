using Telegram.Bot;

namespace Siphon.Bot.Core;

public sealed class RouterMiddleware(IEnumerable<IFeatureModule> modules) : IUpdateMiddleware
{
    readonly IReadOnlyList<IFeatureModule> _modules = modules.ToList();

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next, CancellationToken ct)
    {
        var module = _modules.FirstOrDefault(m => m.CanHandle(ctx));
        if (module is not null)
        {
            await module.HandleAsync(ctx, ct);
            return;
        }
        if (ctx.Callback is { } cb)
            await ctx.Bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
        await next();
    }
}
