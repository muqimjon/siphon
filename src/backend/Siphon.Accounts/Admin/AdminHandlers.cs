using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Siphon.Accounts.Data;
using Siphon.Media;
using Siphon.Media.Http;

namespace Siphon.Accounts.Admin;

public sealed record SetUserLimitsRequest(int? FileSizeLimitMb, int? DailyRequestLimit, int? ConcurrentLimit, DateTime? ExpiresAt);

public sealed class AdminHandlers(AccountsDb db)
{
    public async Task<IResult> Usage(DateOnly? from, DateOnly? to)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await db.Usage
            .Where(u => u.DateUtc >= start && u.DateUtc <= end)
            .GroupBy(u => new { u.UserId, u.DateUtc })
            .Select(g => new { g.Key.UserId, g.Key.DateUtc, Count = g.Sum(u => u.Count) })
            .OrderBy(r => r.DateUtc).ThenBy(r => r.UserId)
            .ToListAsync();
        return Results.Ok(rows);
    }

    public async Task<IResult> Users()
    {
        var totals = await db.Usage.GroupBy(u => u.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(u => u.Count) })
            .ToDictionaryAsync(x => x.UserId, x => x.Total);

        var users = await db.Users.Include(u => u.Plan).ToListAsync();

        var result = users.Select(u => new
        {
            u.Id,
            u.Email,
            u.FirstName,
            u.LastName,
            u.Role,
            Plan = u.Plan!.Name,
            u.CreatedAt,
            TotalUsage = totals.GetValueOrDefault(u.Id),
            Limits = u.EffectiveLimits(),
        });
        return Results.Ok(result);
    }

    public async Task<IResult> SetLimits(string id, SetUserLimitsRequest request)
    {
        var user = await db.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == id);
        if (user?.Plan is null)
            return Problems.Create(ErrorCodes.JobNotFound, "User not found.");

        user.FileSizeLimitMbOverride = request.FileSizeLimitMb;
        user.DailyRequestLimitOverride = request.DailyRequestLimit;
        user.ConcurrentLimitOverride = request.ConcurrentLimit;
        user.OverridesExpireAt = request.ExpiresAt;
        await db.SaveChangesAsync();
        return Results.Ok(user.EffectiveLimits());
    }
}
