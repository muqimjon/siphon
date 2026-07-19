namespace Siphon.Accounts.Auth;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record EmailStartRequest(string Email);

public sealed record EmailVerifyRequest(string Email, string Code);

public sealed record TelegramLoginRequest(
    long Id, string? FirstName, string? LastName, string? Username, string? PhotoUrl, long AuthDate, string Hash);

public sealed record TokenResponse(string Token);

public sealed record MeResponse(string Email, string Role, PlanView Plan, IReadOnlyList<string> Providers);

public sealed record PlanView(string Name, int DailyRequests, int MaxConcurrent, int MaxFileSizeMb);
