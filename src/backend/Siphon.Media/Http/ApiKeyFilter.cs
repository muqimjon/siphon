using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Siphon.Media.Http;

public sealed class ApiKeyFilter(IOptions<SiphonOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var key = context.HttpContext.Request.Headers["X-Api-Key"].ToString();
        var client = key.Length == 0 ? null : options.Value.ApiKeys.FirstOrDefault(kv => kv.Value == key).Key;
        if (client is null)
            return Results.Problem(detail: "Missing or invalid API key.", statusCode: 401, title: "unauthorized",
                extensions: new Dictionary<string, object?> { ["code"] = "unauthorized" });
        context.HttpContext.Items["client"] = client;
        return await next(context);
    }
}
