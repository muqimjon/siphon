using System.Security.Claims;
using System.Threading.RateLimiting;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Siphon.Accounts.Admin;
using Siphon.Accounts.Auth;
using Siphon.Accounts.Data;
using Siphon.Accounts.Email;
using Siphon.Accounts.Integration;
using Siphon.Accounts.Tokens;
using Siphon.Accounts.Usage;
using Siphon.Media.Http;

namespace Siphon.Accounts;

public static class AccountsModule
{
    public static IServiceCollection AddAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AccountsOptions>()
            .Bind(configuration.GetSection(AccountsOptions.Section))
            .Validate(o => o.Jwt.Secret.Length >= 32, "Accounts:Jwt:Secret must be at least 32 characters")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "Accounts:ConnectionString is required")
            .ValidateOnStart();

        var options = configuration.GetSection(AccountsOptions.Section).Get<AccountsOptions>() ?? new AccountsOptions();

        services.AddDbContext<AccountsDb>(o => o.UseNpgsql(options.ConnectionString));

        services.AddIdentityCore<AppUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 8;
        }).AddEntityFrameworkStores<AccountsDb>();

        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddSingleton<JwtService>();
        services.AddSingleton<EmailCodeService>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<UserService>();
        services.AddScoped<StaticKeyAuthenticator>();
        services.AddScoped<AuthHandlers>();
        services.AddScoped<ExternalAuthHandlers>();
        services.AddScoped<TokenHandlers>();
        services.AddScoped<AdminHandlers>();
        services.AddScoped<UsageHandlers>();
        services.AddScoped<IApiAuthenticator, DbApiAuthenticator>();
        services.AddScoped<IUsageSink, DbUsageSink>();

        var auth = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = JwtService.Validation(options.Jwt);
            });

        if (options.Google.Enabled || options.GitHub.Enabled)
            auth.AddCookie(ExternalAuthHandlers.SignInScheme);
        if (options.Google.Enabled)
            auth.AddGoogle(o =>
            {
                o.ClientId = options.Google.ClientId!;
                o.ClientSecret = options.Google.ClientSecret!;
                o.SignInScheme = ExternalAuthHandlers.SignInScheme;
                o.CallbackPath = "/signin-google";
            });
        if (options.GitHub.Enabled)
            auth.AddGitHub(o =>
            {
                o.ClientId = options.GitHub.ClientId!;
                o.ClientSecret = options.GitHub.ClientSecret!;
                o.SignInScheme = ExternalAuthHandlers.SignInScheme;
                o.CallbackPath = "/signin-github";
                o.Scope.Add("user:email");
            });

        services.AddAuthorization();

        services.AddRateLimiter(o => o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 10, QueueLimit = 0 })));

        return services;
    }

    public static IEndpointRouteBuilder MapAccounts(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<AccountsOptions>>().Value;

        var auth = app.MapGroup("/api/v1/auth").WithTags("Auth").RequireRateLimiting("auth");
        auth.MapPost("/register", (RegisterRequest r, AuthHandlers h) => h.Register(r))
            .WithSummary("Create an account with email and password");
        auth.MapPost("/login", (LoginRequest r, AuthHandlers h) => h.Login(r))
            .WithSummary("Sign in with email and password");
        auth.MapPost("/email/start", (EmailStartRequest r, AuthHandlers h) => h.EmailStart(r))
            .WithSummary("Send a one-time sign-in code by email");
        auth.MapPost("/email/verify", (EmailVerifyRequest r, AuthHandlers h) => h.EmailVerify(r))
            .WithSummary("Verify an email sign-in code and receive a token");
        auth.MapPost("/telegram", (TelegramLoginRequest r, AuthHandlers h) => h.Telegram(r))
            .WithSummary("Sign in with the Telegram Login Widget");

        if (options.Google.Enabled)
        {
            auth.MapGet("/google", (ExternalAuthHandlers h) => h.Challenge(GoogleDefaults.AuthenticationScheme, "google")).ExcludeFromDescription();
            auth.MapGet("/google/callback", (HttpContext c, ExternalAuthHandlers h) => h.Callback(c, "google")).ExcludeFromDescription();
        }
        if (options.GitHub.Enabled)
        {
            auth.MapGet("/github", (ExternalAuthHandlers h) => h.Challenge(GitHubAuthenticationDefaults.AuthenticationScheme, "github")).ExcludeFromDescription();
            auth.MapGet("/github/callback", (HttpContext c, ExternalAuthHandlers h) => h.Callback(c, "github")).ExcludeFromDescription();
        }

        app.MapGet("/api/v1/me", (ClaimsPrincipal u, AuthHandlers h) => h.Me(u))
            .RequireAuthorization().WithTags("Account").WithSummary("Current account, plan and linked providers");

        app.MapGet("/api/v1/usage", (ClaimsPrincipal u, int? days, UsageHandlers h) => h.Mine(u, days))
            .RequireAuthorization().WithTags("Account").WithSummary("Your daily request counts and effective limits");

        var tokens = app.MapGroup("/api/v1/tokens").WithTags("Tokens").RequireAuthorization();
        tokens.MapGet("", (ClaimsPrincipal u, TokenHandlers h) => h.List(u)).WithSummary("List your API tokens");
        tokens.MapPost("", (ClaimsPrincipal u, CreateTokenRequest r, TokenHandlers h) => h.Create(u, r))
            .WithSummary("Create an API token (secret shown once)");
        tokens.MapDelete("/{id:guid}", (ClaimsPrincipal u, Guid id, TokenHandlers h) => h.Delete(u, id)).WithSummary("Revoke an API token");

        var admin = app.MapGroup("/api/v1/admin").WithTags("Admin").RequireAuthorization(p => p.RequireRole("admin"));
        admin.MapGet("/usage", (DateOnly? from, DateOnly? to, AdminHandlers h) => h.Usage(from, to))
            .WithSummary("Usage aggregated by user and day");
        admin.MapGet("/users", (AdminHandlers h) => h.Users()).WithSummary("All users with plan and total usage");
        admin.MapPut("/users/{id}/limits", (string id, SetUserLimitsRequest r, AdminHandlers h) => h.SetLimits(id, r))
            .WithSummary("Set per-user file-size and daily-request overrides");

        return app;
    }

    public static void MigrateAccounts(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountsDb>();
        db.Database.Migrate();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<AccountsOptions>>().Value;
        if (db.Plans.Find(Plan.FreeId) is { } free)
        {
            free.DailyRequests = options.FreePlan.DailyRequests;
            free.MaxConcurrent = options.FreePlan.MaxConcurrent;
            free.MaxFileSizeMb = options.FreePlan.MaxFileSizeMb;
            db.SaveChanges();
        }
    }
}
