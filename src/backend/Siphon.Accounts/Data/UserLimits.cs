namespace Siphon.Accounts.Data;

public sealed record UserLimitsView(int MaxFileSizeMb, int DailyRequests, int? FileSizeLimitMbOverride, int? DailyRequestLimitOverride);

public static class UserLimits
{
    public static int EffectiveMaxFileSizeMb(this AppUser user) => user.FileSizeLimitMbOverride ?? user.Plan!.MaxFileSizeMb;

    public static int EffectiveDailyRequests(this AppUser user) => user.DailyRequestLimitOverride ?? user.Plan!.DailyRequests;

    public static UserLimitsView EffectiveLimits(this AppUser user) =>
        new(user.EffectiveMaxFileSizeMb(), user.EffectiveDailyRequests(), user.FileSizeLimitMbOverride, user.DailyRequestLimitOverride);
}
