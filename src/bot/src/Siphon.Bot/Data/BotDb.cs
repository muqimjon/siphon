using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Siphon.Bot.Data;

public sealed class ChatState
{
    public long ChatId { get; set; }
    public string Lang { get; set; } = "uz";
    public DateTime LastActivityUtc { get; set; }
    public string DayKey { get; set; } = "";
    public int DownloadsToday { get; set; }
}

public sealed class UsageEvent
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public string Kind { get; set; } = "";
    public string? Site { get; set; }
    public DateTime Utc { get; set; }
}

public sealed class UserPref
{
    public long ChatId { get; set; }
    public string Platform { get; set; } = "";
    public string Kind { get; set; } = "ask";
    public string AudioFormat { get; set; } = "ask";
    public string VideoFormat { get; set; } = "ask";
    public string Quality { get; set; } = "ask";
}

public sealed class GroupInfo
{
    public long ChatId { get; set; }
    public string Title { get; set; } = "";
    public long OwnerUserId { get; set; }
    public DateTime AddedUtc { get; set; }
}

public sealed class BotDb(DbContextOptions<BotDb> options) : DbContext(options)
{
    public DbSet<ChatState> Chats => Set<ChatState>();
    public DbSet<UsageEvent> Events => Set<UsageEvent>();
    public DbSet<UserPref> Prefs => Set<UserPref>();
    public DbSet<GroupInfo> Groups => Set<GroupInfo>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ChatState>(e =>
        {
            e.HasKey(x => x.ChatId);
            e.Property(x => x.ChatId).ValueGeneratedNever();
        });
        builder.Entity<UsageEvent>().HasIndex(x => x.Utc);
        builder.Entity<UserPref>().HasKey(x => new { x.ChatId, x.Platform });
        builder.Entity<GroupInfo>(e =>
        {
            e.HasKey(x => x.ChatId);
            e.Property(x => x.ChatId).ValueGeneratedNever();
        });
    }
}

public sealed class BotDbFactory : IDesignTimeDbContextFactory<BotDb>
{
    public BotDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BotDb>().UseSqlite("Data Source=bot.db").Options);
}
