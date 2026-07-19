using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Siphon.Accounts.Data;
using Siphon.Media.Http;

namespace Siphon.Accounts.Integration;

public sealed class DbUsageSink(AccountsDb db, IHttpContextAccessor http) : IUsageSink
{
    public async Task<bool> TryConsumeAsync(ApiCaller caller, string endpoint)
    {
        if (caller.Kind != "user") return true;

        var tokenId = http.HttpContext?.Items[DbApiAuthenticator.TokenIdItem] as Guid?;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (caller.Limits.DailyRequests is { } cap)
        {
            var used = await db.Usage.Where(u => u.UserId == caller.Id && u.DateUtc == today).SumAsync(u => (int?)u.Count) ?? 0;
            if (used >= cap) return false;
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
                return true;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                foreach (var entry in db.ChangeTracker.Entries<UsageDaily>().ToList())
                    entry.State = EntityState.Detached;
            }
        }
    }
}
