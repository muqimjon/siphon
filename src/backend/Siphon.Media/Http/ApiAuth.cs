using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Siphon.Media.Http;

public sealed record CallerLimits(int MaxFileSizeMb, int? DailyRequests, int? MaxConcurrent = null, int? MonthlyGb = null);

public sealed record ApiCaller(string Kind, string Id, CallerLimits Limits)
{
    public Guid? TokenId { get; init; }
}

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
    Task<string?> TryConsumeAsync(ApiCaller caller, string endpoint);
    Task RecordBytesAsync(ApiCaller caller, long bytes);
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
    public Task<string?> TryConsumeAsync(ApiCaller caller, string endpoint) => Task.FromResult<string?>(null);
    public Task RecordBytesAsync(ApiCaller caller, long bytes) => Task.CompletedTask;
}
