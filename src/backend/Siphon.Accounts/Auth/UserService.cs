using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Siphon.Accounts.Data;

namespace Siphon.Accounts.Auth;

public sealed class UserService(UserManager<AppUser> users, IOptions<AccountsOptions> options)
{
    public UserManager<AppUser> Users => users;

    public async Task<AppUser> FindOrCreateByEmailAsync(string email)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = New(email);
            await users.CreateAsync(user);
        }
        else
        {
            await PromoteIfAdminAsync(user);
        }
        return user;
    }

    public async Task<AppUser> FindOrCreateExternalAsync(string provider, string key, string? email)
    {
        var user = await users.FindByLoginAsync(provider, key);
        if (user is not null)
        {
            await PromoteIfAdminAsync(user);
            return user;
        }

        if (!string.IsNullOrEmpty(email))
            user = await users.FindByEmailAsync(email);

        if (user is null)
        {
            user = email is null ? New($"{provider}:{key}", null) : New(email);
            await users.CreateAsync(user);
        }

        await users.AddLoginAsync(user, new UserLoginInfo(provider, key, provider));
        await PromoteIfAdminAsync(user);
        return user;
    }

    public async Task PromoteIfAdminAsync(AppUser user)
    {
        if (user.Email is not null && RoleFor(user.Email) == "admin" && user.Role != "admin")
        {
            user.Role = "admin";
            await users.UpdateAsync(user);
        }
    }

    public string RoleFor(string email) =>
        string.Equals(email, options.Value.AdminEmail, StringComparison.OrdinalIgnoreCase) ? "admin" : "user";

    private AppUser New(string email) => New(email, email);

    private AppUser New(string userName, string? email) => new()
    {
        UserName = userName,
        Email = email,
        PlanId = Plan.FreeId,
        Role = email is not null ? RoleFor(email) : "user",
    };
}
