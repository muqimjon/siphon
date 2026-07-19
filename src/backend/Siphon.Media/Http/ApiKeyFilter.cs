using Microsoft.AspNetCore.Http;

namespace Siphon.Media.Http;

public sealed class ApiKeyFilter(IApiAuthenticator authenticator, IUsageSink usage) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var outcome = await authenticator.AuthenticateAsync(context.HttpContext);
        if (outcome.Caller is null)
            return Problems.Create(outcome.ErrorCode ?? "unauthorized", "Missing or invalid API credentials.");

        context.HttpContext.Items["caller"] = outcome.Caller;
        var result = await next(context);
        await usage.RecordAsync(outcome.Caller, context.HttpContext.Request.Path);
        return result;
    }
}
