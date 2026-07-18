using Microsoft.Extensions.Options;
using Siphon.Media;
using Siphon.Media.Delivery;

namespace Siphon.Tests;

public class FileTokenServiceTests
{
    private static FileTokenService Create(string secret = "unit-test-secret-0123456789abcdef0123") =>
        new(Options.Create(new SiphonOptions { FileTokenSecret = secret }));

    [Fact]
    public void Valid_token_round_trips()
    {
        var service = Create();
        var token = service.Issue("job1", DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Equal(TokenVerdict.Valid, service.Validate("job1", token));
    }

    [Fact]
    public void Expired_token_detected()
    {
        var service = Create();
        var token = service.Issue("job1", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(TokenVerdict.Expired, service.Validate("job1", token));
    }

    [Fact]
    public void Wrong_job_rejected()
    {
        var service = Create();
        var token = service.Issue("job1", DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Equal(TokenVerdict.Invalid, service.Validate("job2", token));
    }

    [Fact]
    public void Tampered_token_rejected()
    {
        var service = Create();
        var token = service.Issue("job1", DateTimeOffset.UtcNow.AddMinutes(5));
        var tampered = token[..^2] + (token[^1] == 'A' ? "BB" : "AA");
        Assert.Equal(TokenVerdict.Invalid, service.Validate("job1", tampered));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("a.b")]
    [InlineData(".")]
    public void Malformed_tokens_rejected(string token) =>
        Assert.Equal(TokenVerdict.Invalid, Create().Validate("job1", token));

    [Fact]
    public void Different_secret_rejects_token()
    {
        var token = Create().Issue("job1", DateTimeOffset.UtcNow.AddMinutes(5));
        var other = Create("another-secret-0123456789abcdef012345");
        Assert.Equal(TokenVerdict.Invalid, other.Validate("job1", token));
    }
}
