using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Siphon.Accounts.Data;
using Siphon.Media;
using Siphon.Media.Http;

namespace Siphon.Accounts.Tokens;

public sealed class TokenHandlers(AccountsDb db)
{
    public async Task<IResult> List(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue("sub")!;
        var tokens = await db.ApiTokens.Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TokenView(t.Id, t.Name, t.Prefix, t.CreatedAt, t.LastUsedAt, t.RevokedAt != null))
            .ToListAsync();
        return Results.Ok(tokens);
    }

    public async Task<IResult> Create(ClaimsPrincipal principal, CreateTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Problems.Create(ErrorCodes.InvalidUrl, "Token name is required.");

        var userId = principal.FindFirstValue("sub")!;
        var (secret, hash, prefix) = ApiTokenService.Generate();
        var token = new ApiToken { UserId = userId, Name = request.Name, TokenHash = hash, Prefix = prefix };
        db.ApiTokens.Add(token);
        await db.SaveChangesAsync();
        return Results.Ok(new CreatedTokenView(token.Id, token.Name, token.Prefix, secret));
    }

    public async Task<IResult> Delete(ClaimsPrincipal principal, Guid id)
    {
        var userId = principal.FindFirstValue("sub")!;
        var token = await db.ApiTokens.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (token is null) return Problems.Create(ErrorCodes.JobNotFound, "Token not found.");
        if (token.RevokedAt is null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return Results.NoContent();
    }
}
