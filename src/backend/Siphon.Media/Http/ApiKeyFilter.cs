using Microsoft.AspNetCore.Http;

namespace Siphon.Media.Http;

public sealed class ApiKeyFilter(IApiAuthenticator authenticator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var outcome = await authenticator.AuthenticateAsync(context.HttpContext);
        if (outcome.Caller is null)
            return Problems.Create(outcome.ErrorCode ?? "unauthorized", "Missing or invalid API credentials.");

        context.HttpContext.Items["caller"] = outcome.Caller;
        return await next(context);
    }
}
