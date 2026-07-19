using Siphon.Accounts.Data;

namespace Siphon.Tests;

public class UserLimitsTests
{
    private static AppUser User(int? fileOverride, int? dailyOverride) => new()
    {
        Plan = new Plan { MaxFileSizeMb = 1024, DailyRequests = 500 },
        FileSizeLimitMbOverride = fileOverride,
        DailyRequestLimitOverride = dailyOverride,
    };

    [Fact]
    public void Falls_back_to_plan_when_no_override()
    {
        var user = User(null, null);
        Assert.Equal(1024, user.EffectiveMaxFileSizeMb());
        Assert.Equal(500, user.EffectiveDailyRequests());
    }

    [Fact]
    public void Override_wins_over_plan()
    {
        var user = User(10, 50);
        Assert.Equal(10, user.EffectiveMaxFileSizeMb());
        Assert.Equal(50, user.EffectiveDailyRequests());
    }

    [Fact]
    public void Effective_limits_view_reports_overrides()
    {
        var limits = User(10, null).EffectiveLimits();
        Assert.Equal(10, limits.MaxFileSizeMb);
        Assert.Equal(500, limits.DailyRequests);
        Assert.Equal(10, limits.FileSizeLimitMbOverride);
        Assert.Null(limits.DailyRequestLimitOverride);
    }
}
