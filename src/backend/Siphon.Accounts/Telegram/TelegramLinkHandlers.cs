using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Siphon.Accounts.Data;

namespace Siphon.Accounts.Telegram;

public sealed record RedeemLinkRequest(string Token, long TelegramUserId, string? Username, string? FirstName);

public sealed record LinkTokenResponse(string Token, string Url, DateTime ExpiresUtc);

public sealed record TelegramLinkView(long TelegramUserId, string? Username, string? FirstName, DateTime LinkedAtUtc);

public sealed class TelegramLinkHandlers(AccountsDb db, IOptions<AccountsOptions> options)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    public async Task<IResult> CreateToken(string userId)
    {
        var now = DateTime.UtcNow;
        db.TelegramLinkTokens.RemoveRange(await db.TelegramLinkTokens
            .Where(t => t.UserId == userId || t.ExpiresUtc < now)
            .ToListAsync());

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var expires = now.Add(TokenLifetime);
        db.TelegramLinkTokens.Add(new TelegramLinkToken { Token = token, UserId = userId, ExpiresUtc = expires });
        await db.SaveChangesAsync();

        var bot = options.Value.BotUsername;
        var url = string.IsNullOrEmpty(bot) ? "" : $"https://t.me/{bot}?start=link_{token}";
        return Results.Ok(new LinkTokenResponse(token, url, expires));
    }

    public async Task<IResult> Redeem(RedeemLinkRequest request)
    {
        var entry = await db.TelegramLinkTokens.FirstOrDefaultAsync(t => t.Token == request.Token);
        if (entry is null || entry.ExpiresUtc < DateTime.UtcNow)
            return Results.BadRequest(new { code = "link-expired" });

        var existing = await db.TelegramLinks.FindAsync(request.TelegramUserId);
        if (existing is not null && existing.UserId != entry.UserId)
            return Results.Conflict(new { code = "link-taken" });

        if (existing is null)
        {
            db.TelegramLinks.Add(new TelegramLink
            {
                TelegramUserId = request.TelegramUserId,
                UserId = entry.UserId,
                Username = request.Username,
                FirstName = request.FirstName,
                LinkedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Username = request.Username;
            existing.FirstName = request.FirstName;
        }

        db.TelegramLinkTokens.Remove(entry);
        await db.SaveChangesAsync();

        var user = await db.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == entry.UserId);
        return Results.Ok(new { ok = true, email = user?.Email });
    }

    public async Task<IResult> List(string userId)
    {
        var links = await db.TelegramLinks
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.LinkedAtUtc)
            .Select(l => new TelegramLinkView(l.TelegramUserId, l.Username, l.FirstName, l.LinkedAtUtc))
            .ToListAsync();
        return Results.Ok(links);
    }

    public async Task<IResult> Unlink(string userId, long telegramUserId)
    {
        var link = await db.TelegramLinks.FindAsync(telegramUserId);
        if (link is null || link.UserId != userId) return Results.NotFound();
        db.TelegramLinks.Remove(link);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
