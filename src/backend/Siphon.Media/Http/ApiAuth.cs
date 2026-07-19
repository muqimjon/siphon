using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Siphon.Media.Http;

public sealed record ApiCaller(string Kind, string Id);

public sealed record AuthOutcome(ApiCaller? Caller, string? ErrorCode)
{
    public static readonly AuthOutcome Unauthorized = new(null, "unauthorized");
    public static AuthOutcome Ok(ApiCaller caller) => new(caller, null);
    public static AuthOutcome Fail(string code) => new(null, code);
}

public interface IApiAuthenticator
{
    Task<AuthOutcome> AuthenticateAsync(HttpContext context);
}

public interface IUsageSink
{
    Task RecordAsync(ApiCaller caller, string endpoint);
}

public sealed class StaticKeyAuthenticator(IOptions<SiphonOptions> options) : IApiAuthenticator
{
    public Task<AuthOutcome> AuthenticateAsync(HttpContext context)
    {
        var key = context.Request.Headers["X-Api-Key"].ToString();
        var client = key.Length == 0 ? null : options.Value.ApiKeys.FirstOrDefault(kv => kv.Value == key).Key;
        return Task.FromResult(client is null ? AuthOutcome.Unauthorized : AuthOutcome.Ok(new ApiCaller("client", client)));
    }
}

public sealed class NullUsageSink : IUsageSink
{
    public Task RecordAsync(ApiCaller caller, string endpoint) => Task.CompletedTask;
}
