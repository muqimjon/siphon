using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Siphon.Accounts.Auth;
using Siphon.Accounts.Data;

namespace Siphon.Accounts.Profile;

public sealed record UpdateProfileRequest(string? FirstName, string? LastName, string? UserName);

public sealed record SetPasswordRequest(string? CurrentPassword, string NewPassword);

public sealed record LoginView(string Provider, string DisplayName);

public sealed class ProfileHandlers(UserService users, AccountsDb db)
{
    public async Task<IResult> Get(ClaimsPrincipal principal)
    {
        var user = await Current(principal);
        if (user is null) return Results.Unauthorized();

        var logins = await users.Users.GetLoginsAsync(user);
        return Results.Ok(new
        {
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            userName = user.UserName,
            role = user.Role,
            createdAt = user.CreatedAt,
            hasPassword = await users.Users.HasPasswordAsync(user),
            logins = logins.Select(l => new LoginView(l.LoginProvider, l.ProviderDisplayName ?? l.LoginProvider)),
        });
    }

    public async Task<IResult> Update(ClaimsPrincipal principal, UpdateProfileRequest request)
    {
        var user = await Current(principal);
        if (user is null) return Results.Unauthorized();

        user.FirstName = Trim(request.FirstName);
        user.LastName = Trim(request.LastName);

        var name = Trim(request.UserName);
        if (name is not null && !string.Equals(name, user.UserName, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await users.Users.FindByNameAsync(name);
            if (taken is not null && taken.Id != user.Id)
                return Results.Conflict(new { code = "name-taken" });
            var renamed = await users.Users.SetUserNameAsync(user, name);
            if (!renamed.Succeeded)
                return Results.BadRequest(new { code = "name-invalid" });
        }

        await users.Users.UpdateAsync(user);
        return await Get(principal);
    }

    public async Task<IResult> SetPassword(ClaimsPrincipal principal, SetPasswordRequest request)
    {
        var user = await Current(principal);
        if (user is null) return Results.Unauthorized();
        if (request.NewPassword.Length < 8) return Results.BadRequest(new { code = "password-weak" });

        var result = await users.Users.HasPasswordAsync(user)
            ? await users.Users.ChangePasswordAsync(user, request.CurrentPassword ?? "", request.NewPassword)
            : await users.Users.AddPasswordAsync(user, request.NewPassword);

        return result.Succeeded
            ? Results.Ok(new { ok = true })
            : Results.BadRequest(new { code = "password-rejected" });
    }

    public async Task<IResult> Unlink(ClaimsPrincipal principal, string provider)
    {
        var user = await Current(principal);
        if (user is null) return Results.Unauthorized();

        var logins = await users.Users.GetLoginsAsync(user);
        var login = logins.FirstOrDefault(l => l.LoginProvider == provider);
        if (login is null) return Results.NotFound();

        var hasPassword = await users.Users.HasPasswordAsync(user);
        if (logins.Count == 1 && !hasPassword)
            return Results.BadRequest(new { code = "last-login" });

        await users.Users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
        return Results.NoContent();
    }

    public async Task<IResult> LinkToken(ClaimsPrincipal principal)
    {
        var user = await Current(principal);
        if (user is null) return Results.Unauthorized();

        var now = DateTime.UtcNow;
        db.TelegramLinkTokens.RemoveRange(await db.TelegramLinkTokens.Where(t => t.ExpiresUtc < now).ToListAsync());
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        db.TelegramLinkTokens.Add(new TelegramLinkToken { Token = token, UserId = user.Id, ExpiresUtc = now.AddMinutes(10) });
        await db.SaveChangesAsync();
        return Results.Ok(new { token });
    }

    private async Task<AppUser?> Current(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub") is { } id ? await users.Users.FindByIdAsync(id) : null;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
