using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Siphon.Media.Http;

public sealed record CallerLimits(int MaxFileSizeMb, int? DailyRequests);

public sealed record ApiCaller(string Kind, string Id, CallerLimits Limits);

public sealed record AuthOutcome(ApiCaller? Caller, string? ErrorCode)
{
    public static readonly AuthOutcome Unauthorized = new(null, "unauthorized");
    public static AuthOutcome Ok(ApiCaller caller) => new(caller, null);
}

public interface IApiAuthenticator
{
    Task<AuthOutcome> AuthenticateAsync(HttpContext context);
}

public interface IUsageSink
{
    Task<bool> TryConsumeAsync(ApiCaller caller, string endpoint);
}

public sealed class StaticKeyAuthenticator(IOptions<SiphonOptions> options) : IApiAuthenticator
{
    public Task<AuthOutcome> AuthenticateAsync(HttpContext context)
    {
        var key = context.Request.Headers["X-Api-Key"].ToString();
        var client = key.Length == 0 ? null : options.Value.ApiKeys.FirstOrDefault(kv => kv.Value == key).Key;
        if (client is null) return Task.FromResult(AuthOutcome.Unauthorized);
        var maxMb = options.Value.ClientLimits.GetValueOrDefault(client, options.Value.MaxFileSizeMb);
        return Task.FromResult(AuthOutcome.Ok(new ApiCaller("client", client, new CallerLimits(maxMb, null))));
    }
}

public sealed class NullUsageSink : IUsageSink
{
    public Task<bool> TryConsumeAsync(ApiCaller caller, string endpoint) => Task.FromResult(true);
}
