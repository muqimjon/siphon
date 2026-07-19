using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Siphon.Accounts.Data;
using Siphon.Media;
using Siphon.Media.Http;

namespace Siphon.Accounts.Usage;

public sealed record UsageDayView(string DateUtc, int Count);

public sealed record UsageResponse(IReadOnlyList<UsageDayView> Daily, UserLimitsView Limits, int UsedToday);

public sealed class UsageHandlers(AccountsDb db)
{
    public async Task<IResult> Mine(ClaimsPrincipal principal, int? days)
    {
        var id = principal.FindFirstValue("sub");
        if (id is null) return Problems.Create(ErrorCodes.Unauthorized, "Missing subject.");
        var user = await db.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == id);
        if (user?.Plan is null) return Problems.Create(ErrorCodes.Unauthorized, "User not found.");

        var span = Math.Clamp(days ?? 14, 1, 90);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-(span - 1));
        var counts = await db.Usage
            .Where(u => u.UserId == id && u.DateUtc >= start && u.DateUtc <= today)
            .GroupBy(u => u.DateUtc)
            .Select(g => new { Date = g.Key, Count = g.Sum(u => u.Count) })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var daily = Enumerable.Range(0, span)
            .Select(i => start.AddDays(i))
            .Select(d => new UsageDayView(d.ToString("yyyy-MM-dd"), counts.GetValueOrDefault(d)))
            .ToList();

        return Results.Ok(new UsageResponse(daily, user.EffectiveLimits(), counts.GetValueOrDefault(today)));
    }

    public async Task<IResult> ByToken(ClaimsPrincipal principal, int? days)
    {
        var id = principal.FindFirstValue("sub");
        if (id is null) return Problems.Create(ErrorCodes.Unauthorized, "Missing subject.");

        var span = Math.Clamp(days ?? 30, 1, 90);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-(span - 1));

        var rows = await db.Usage
            .Where(u => u.UserId == id && u.DateUtc >= start && u.DateUtc <= today)
            .GroupBy(u => u.TokenId)
            .Select(g => new { g.Key, Total = g.Sum(u => u.Count), Today = g.Sum(u => u.DateUtc == today ? u.Count : 0) })
            .ToListAsync();

        var tokens = await db.ApiTokens
            .Where(t => t.UserId == id)
            .Select(t => new { t.Id, t.Name, t.Prefix })
            .ToListAsync();

        var perToken = rows.Select(r => new
        {
            tokenId = r.Key,
            name = r.Key is null
                ? null
                : tokens.FirstOrDefault(t => t.Id == r.Key)?.Name,
            prefix = r.Key is null ? null : tokens.FirstOrDefault(t => t.Id == r.Key)?.Prefix,
            total = r.Total,
            today = r.Today,
        }).OrderByDescending(x => x.total).ToList();

        return Results.Ok(perToken);
    }
}
