using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Siphon.Accounts.Data;
using Siphon.Media.Http;

namespace Siphon.Accounts.Integration;

public sealed class DbUsageSink(AccountsDb db, IHttpContextAccessor http) : IUsageSink
{
    public async Task RecordBytesAsync(ApiCaller caller, long bytes)
    {
        if (caller.Kind != "user" || bytes <= 0) return;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await db.Usage.FirstOrDefaultAsync(u =>
            u.UserId == caller.Id && u.TokenId == caller.TokenId && u.DateUtc == today && u.Endpoint == "download");
        if (row is null) return;
        row.Bytes += bytes;
        await db.SaveChangesAsync();
    }

    public async Task<string?> TryConsumeAsync(ApiCaller caller, string endpoint)
    {
        if (caller.Kind != "user") return null;

        var tokenId = caller.TokenId ?? http.HttpContext?.Items[DbApiAuthenticator.TokenIdItem] as Guid?;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (caller.Limits.MonthlyGb is { } gb and > 0)
        {
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var usedBytes = await db.Usage
                .Where(u => u.UserId == caller.Id && u.DateUtc >= monthStart)
                .SumAsync(u => (long?)u.Bytes) ?? 0;
            if (usedBytes >= (long)gb * 1024 * 1024 * 1024)
                return $"Monthly download volume of {gb} GB is used up. It resets at the start of next month.";
        }

        if (caller.Limits.DailyRequests is { } cap)
        {
            var used = await db.Usage.Where(u => u.UserId == caller.Id && u.DateUtc == today).SumAsync(u => (int?)u.Count) ?? 0;
            if (used >= cap) return $"Daily limit of {cap} downloads is used up.";
        }

        for (var attempt = 0; ; attempt++)
        {
            var row = await db.Usage.FirstOrDefaultAsync(u =>
                u.UserId == caller.Id && u.TokenId == tokenId && u.DateUtc == today && u.Endpoint == endpoint);
            if (row is null)
                db.Usage.Add(new UsageDaily { UserId = caller.Id, TokenId = tokenId, DateUtc = today, Endpoint = endpoint, Count = 1 });
            else
                row.Count++;
            try
            {
                await db.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                foreach (var entry in db.ChangeTracker.Entries<UsageDaily>().ToList())
                    entry.State = EntityState.Detached;
            }
        }
    }
}
