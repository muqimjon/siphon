using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Siphon.Accounts.Data;

public sealed class AccountsDb(DbContextOptions<AccountsDb> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<UsageDaily> Usage => Set<UsageDaily>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>()
            .HasOne(u => u.Plan).WithMany().HasForeignKey(u => u.PlanId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Plan>().HasData(new Plan
        {
            Id = Plan.FreeId,
            Name = "Free",
            DailyRequests = 500,
            MaxConcurrent = 2,
            MaxFileSizeMb = 2048,
        });

        builder.Entity<ApiToken>().HasIndex(t => t.TokenHash).IsUnique();
        builder.Entity<ApiToken>().HasIndex(t => t.UserId);

        builder.Entity<UsageDaily>()
            .HasIndex(u => new { u.UserId, u.TokenId, u.DateUtc, u.Endpoint }).IsUnique();
    }
}
