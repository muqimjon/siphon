using Microsoft.Extensions.Caching.Memory;
using Siphon.Accounts.Auth;

namespace Siphon.Tests;

public class EmailCodeServiceTests
{
    private static EmailCodeService New() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Correct_code_verifies_once()
    {
        var codes = New();
        var code = codes.Issue("a@b.com");
        Assert.True(codes.Verify("a@b.com", code));
        Assert.False(codes.Verify("a@b.com", code));
    }

    [Fact]
    public void Wrong_code_is_rejected()
    {
        var codes = New();
        codes.Issue("a@b.com");
        Assert.False(codes.Verify("a@b.com", "000000"));
    }

    [Fact]
    public void Code_is_invalidated_after_five_wrong_attempts()
    {
        var codes = New();
        var code = codes.Issue("a@b.com");
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
            Assert.False(codes.Verify("a@b.com", wrong));
        Assert.False(codes.Verify("a@b.com", code));
    }
}
