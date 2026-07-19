using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Siphon.Accounts.Data;
using Siphon.Accounts.Tokens;
using Siphon.Media.Http;

namespace Siphon.Accounts.Integration;

public sealed class DbApiAuthenticator(StaticKeyAuthenticator staticKey, AccountsDb db) : IApiAuthenticator
{
    internal const string TokenIdItem = "usage.tokenId";

    public async Task<AuthOutcome> AuthenticateAsync(HttpContext context)
    {
        var firstParty = await staticKey.AuthenticateAsync(context);
        if (firstParty.Caller is not null)
            return firstParty;

        var presented = Extract(context);
        if (!ApiTokenService.IsApiToken(presented))
            return AuthOutcome.Unauthorized;

        var hash = ApiTokenService.Hash(presented!);
        var token = await db.ApiTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);
        if (token is null)
            return AuthOutcome.Unauthorized;

        var user = await db.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == token.UserId);
        if (user?.Plan is null)
            return AuthOutcome.Unauthorized;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var used = await db.Usage.Where(u => u.UserId == token.UserId && u.DateUtc == today).SumAsync(u => (int?)u.Count) ?? 0;
        if (used >= user.Plan.DailyRequests)
            return AuthOutcome.Fail(Siphon.Media.ErrorCodes.QuotaExceeded);

        token.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        context.Items[TokenIdItem] = token.Id;
        return AuthOutcome.Ok(new ApiCaller("user", token.UserId));
    }

    private static string? Extract(HttpContext context)
    {
        var bearer = context.Request.Headers.Authorization.ToString();
        if (bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return bearer["Bearer ".Length..].Trim();
        var apiKey = context.Request.Headers["X-Api-Key"].ToString();
        return apiKey.Length == 0 ? null : apiKey;
    }
}
