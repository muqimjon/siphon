using System.Security.Claims;
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
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return await ForUserAsync(userId, null, context);
        }

        var presented = Extract(context);
        if (ApiTokenService.IsApiToken(presented))
        {
            var hash = ApiTokenService.Hash(presented!);
            var token = await db.ApiTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);
            if (token is null)
                return AuthOutcome.Unauthorized;

            var outcome = await ForUserAsync(token.UserId, token.Id, context);
            if (outcome.Caller is not null)
            {
                token.LastUsedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return outcome;
        }

        var client = await staticKey.AuthenticateAsync(context);
        return client.Caller is { Kind: "client", Id: "bot" }
            ? await ForTelegramAsync(context, client)
            : client;
    }

    private async Task<AuthOutcome> ForTelegramAsync(HttpContext context, AuthOutcome fallback)
    {
        if (!long.TryParse(context.Request.Headers["X-Telegram-User"], out var telegramUserId))
            return fallback;

        var link = await db.TelegramLinks.FirstOrDefaultAsync(l => l.TelegramUserId == telegramUserId);
        if (link is null) return fallback;

        var linked = await ForUserAsync(link.UserId, null, context);
        if (linked.Caller is null) return fallback;

        var limits = linked.Caller.Limits;
        return AuthOutcome.Ok(linked.Caller with
        {
            Limits = limits with { MaxFileSizeMb = Math.Min(limits.MaxFileSizeMb, fallback.Caller!.Limits.MaxFileSizeMb) }
        });
    }

    private async Task<AuthOutcome> ForUserAsync(string? userId, Guid? tokenId, HttpContext context)
    {
        if (userId is null)
            return AuthOutcome.Unauthorized;

        var user = await db.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Plan is null)
            return AuthOutcome.Unauthorized;

        if (tokenId is not null)
            context.Items[TokenIdItem] = tokenId;
        return AuthOutcome.Ok(new ApiCaller("user", userId,
            new CallerLimits(user.EffectiveMaxFileSizeMb(), user.EffectiveDailyRequests())));
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
