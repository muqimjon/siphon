using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Siphon.Accounts.Data;

public sealed class AccountsDbFactory : IDesignTimeDbContextFactory<AccountsDb>
{
    public AccountsDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AccountsDb>()
            .UseNpgsql("Host=localhost;Database=siphon;Username=postgres;Password=postgres")
            .Options;
        return new AccountsDb(options);
    }
}
