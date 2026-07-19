namespace Siphon.Accounts.Data;

public sealed record UserLimitsView(
    int MaxFileSizeMb,
    int DailyRequests,
    int MaxConcurrent,
    int MonthlyGb,
    int? FileSizeLimitMbOverride,
    int? DailyRequestLimitOverride,
    int? ConcurrentLimitOverride,
    int? MonthlyGbOverride,
    DateTime? OverridesExpireAt,
    bool OverridesActive);

public static class UserLimits
{
    public static bool OverridesActive(this AppUser user) =>
        user.OverridesExpireAt is null || user.OverridesExpireAt > DateTime.UtcNow;

    public static int EffectiveMaxFileSizeMb(this AppUser user) =>
        (user.OverridesActive() ? user.FileSizeLimitMbOverride : null) ?? user.Plan!.MaxFileSizeMb;

    public static int EffectiveDailyRequests(this AppUser user) =>
        (user.OverridesActive() ? user.DailyRequestLimitOverride : null) ?? user.Plan!.DailyRequests;

    public static int EffectiveMaxConcurrent(this AppUser user) =>
        (user.OverridesActive() ? user.ConcurrentLimitOverride : null) ?? user.Plan!.MaxConcurrent;

    public static int EffectiveMonthlyGb(this AppUser user) =>
        (user.OverridesActive() ? user.MonthlyGbOverride : null) ?? user.Plan!.MonthlyGb;

    public static UserLimitsView EffectiveLimits(this AppUser user) =>
        new(user.EffectiveMaxFileSizeMb(),
            user.EffectiveDailyRequests(),
            user.EffectiveMaxConcurrent(),
            user.EffectiveMonthlyGb(),
            user.FileSizeLimitMbOverride,
            user.DailyRequestLimitOverride,
            user.ConcurrentLimitOverride,
            user.MonthlyGbOverride,
            user.OverridesExpireAt,
            user.OverridesActive());
}
