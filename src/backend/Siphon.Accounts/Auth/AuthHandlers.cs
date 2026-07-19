using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Siphon.Accounts.Data;
using Siphon.Accounts.Email;
using Siphon.Media;
using Siphon.Media.Http;

namespace Siphon.Accounts.Auth;

public sealed class AuthHandlers(
    UserService users,
    JwtService jwt,
    EmailCodeService codes,
    IEmailSender email,
    AccountsDb db,
    IOptions<AccountsOptions> options)
{
    public async Task<IResult> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Problems.Create(ErrorCodes.InvalidUrl, "Email and password are required.");
        if (users.RoleFor(request.Email) == "admin")
            return Problems.Create(ErrorCodes.InvalidUrl, "This email must sign in with a provider or an email sign-in code.");
        if (await users.Users.FindByEmailAsync(request.Email) is not null)
            return Problems.Create(ErrorCodes.InvalidUrl, "An account with this email already exists.");

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            PlanId = Plan.FreeId,
            Role = "user",
        };
        var result = await users.Users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return Problems.Create(ErrorCodes.InvalidUrl, string.Join(" ", result.Errors.Select(e => e.Description)));

        return Results.Ok(new TokenResponse(jwt.Issue(user)));
    }

    public async Task<IResult> Login(LoginRequest request)
    {
        var user = await users.Users.FindByEmailAsync(request.Email);
        if (user is null || !await users.Users.CheckPasswordAsync(user, request.Password))
            return Problems.Create(ErrorCodes.Unauthorized, "Invalid email or password.");
        return Results.Ok(new TokenResponse(jwt.Issue(user)));
    }

    public async Task<IResult> EmailStart(EmailStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Problems.Create(ErrorCodes.InvalidUrl, "Email is required.");
        await users.FindOrCreateByEmailAsync(request.Email);
        var code = codes.Issue(request.Email);
        await email.SendAsync(request.Email, "Your Siphon sign-in code", $"Your Siphon sign-in code is {code}. It expires in 10 minutes.");
        return Results.Ok();
    }

    public async Task<IResult> EmailVerify(EmailVerifyRequest request)
    {
        if (!codes.Verify(request.Email, request.Code))
            return Problems.Create(ErrorCodes.Unauthorized, "Invalid or expired code.");
        var user = await users.FindOrCreateByEmailAsync(request.Email);
        return Results.Ok(new TokenResponse(jwt.Issue(user)));
    }

    public async Task<IResult> Telegram(TelegramLoginRequest request)
    {
        var botToken = options.Value.TelegramBotToken;
        if (string.IsNullOrEmpty(botToken))
            return Problems.Create(ErrorCodes.Unauthorized, "Telegram login is not enabled.");

        var fields = new Dictionary<string, string> { ["id"] = request.Id.ToString(), ["auth_date"] = request.AuthDate.ToString() };
        if (request.FirstName is not null) fields["first_name"] = request.FirstName;
        if (request.LastName is not null) fields["last_name"] = request.LastName;
        if (request.Username is not null) fields["username"] = request.Username;
        if (request.PhotoUrl is not null) fields["photo_url"] = request.PhotoUrl;

        if (!TelegramLoginVerifier.Verify(fields, request.Hash, botToken, TimeSpan.FromDays(1)))
            return Problems.Create(ErrorCodes.Unauthorized, "Telegram signature is invalid or expired.");

        var user = await users.FindOrCreateExternalAsync("telegram", request.Id.ToString(), null);
        return Results.Ok(new TokenResponse(jwt.Issue(user)));
    }

    public async Task<IResult> Me(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue("sub");
        if (id is null) return Problems.Create(ErrorCodes.Unauthorized, "Missing subject.");
        var user = await db.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return Problems.Create(ErrorCodes.Unauthorized, "User not found.");

        var providers = (await users.Users.GetLoginsAsync(user)).Select(l => l.LoginProvider).ToList();
        if (user.PasswordHash is not null) providers.Add("password");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var used = await db.Usage.Where(u => u.UserId == user.Id && u.DateUtc == today).SumAsync(u => (int?)u.Count) ?? 0;
        var limits = user.EffectiveLimits();
        return Results.Ok(new MeResponse(user.Email ?? "", user.Role,
            new PlanView(user.Plan!.Name, limits.DailyRequests, user.Plan!.MaxConcurrent, limits.MaxFileSizeMb, used), providers));
    }
}
