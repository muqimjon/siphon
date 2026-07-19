using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Siphon.Accounts.Data;
using Siphon.Media.Http;

namespace Siphon.Accounts.Integration;

public sealed class DbUsageSink(AccountsDb db, IHttpContextAccessor http, ILogger<DbUsageSink> logger) : IUsageSink
{
    public async Task RecordAsync(ApiCaller caller, string endpoint)
    {
        if (caller.Kind != "user") return;

        var tokenId = http.HttpContext?.Items[DbApiAuthenticator.TokenIdItem] as Guid?;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        try
        {
            var row = await db.Usage.FirstOrDefaultAsync(u =>
                u.UserId == caller.Id && u.TokenId == tokenId && u.DateUtc == today && u.Endpoint == endpoint);
            if (row is null)
                db.Usage.Add(new UsageDaily { UserId = caller.Id, TokenId = tokenId, DateUtc = today, Endpoint = endpoint, Count = 1 });
            else
                row.Count++;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record usage for {UserId} {Endpoint}", caller.Id, endpoint);
        }
    }
}
