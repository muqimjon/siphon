using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Siphon.Accounts.Data;

namespace Siphon.Accounts.Admin;

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

        var users = await db.Users.Include(u => u.Plan)
            .Select(u => new { u.Id, u.Email, u.Role, Plan = u.Plan!.Name, u.CreatedAt })
            .ToListAsync();

        var result = users.Select(u => new
        {
            u.Id,
            u.Email,
            u.Role,
            u.Plan,
            u.CreatedAt,
            TotalUsage = totals.GetValueOrDefault(u.Id),
        });
        return Results.Ok(result);
    }
}
