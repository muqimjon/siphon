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

public sealed class BotDb(DbContextOptions<BotDb> options) : DbContext(options)
{
    public DbSet<ChatState> Chats => Set<ChatState>();
    public DbSet<UsageEvent> Events => Set<UsageEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ChatState>(e =>
        {
            e.HasKey(x => x.ChatId);
            e.Property(x => x.ChatId).ValueGeneratedNever();
        });
        builder.Entity<UsageEvent>().HasIndex(x => x.Utc);
    }
}

public sealed class BotDbFactory : IDesignTimeDbContextFactory<BotDb>
{
    public BotDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BotDb>().UseSqlite("Data Source=bot.db").Options);
}
