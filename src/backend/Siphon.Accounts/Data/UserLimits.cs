namespace Siphon.Accounts.Data;

public sealed record UserLimitsView(
    int MaxFileSizeMb,
    int DailyRequests,
    int MaxConcurrent,
    int? FileSizeLimitMbOverride,
    int? DailyRequestLimitOverride,
    int? ConcurrentLimitOverride,
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

    public static UserLimitsView EffectiveLimits(this AppUser user) =>
        new(user.EffectiveMaxFileSizeMb(),
            user.EffectiveDailyRequests(),
            user.EffectiveMaxConcurrent(),
            user.FileSizeLimitMbOverride,
            user.DailyRequestLimitOverride,
            user.ConcurrentLimitOverride,
            user.OverridesExpireAt,
            user.OverridesActive());
}
