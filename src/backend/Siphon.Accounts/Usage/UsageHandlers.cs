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
}
