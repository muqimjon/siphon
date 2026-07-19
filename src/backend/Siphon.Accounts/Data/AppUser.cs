using Microsoft.AspNetCore.Identity;

namespace Siphon.Accounts.Data;

public sealed class AppUser : IdentityUser
{
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public string Role { get; set; } = "user";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? FileSizeLimitMbOverride { get; set; }
    public int? DailyRequestLimitOverride { get; set; }
}
