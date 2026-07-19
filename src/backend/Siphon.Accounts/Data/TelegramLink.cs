namespace Siphon.Accounts.Data;

public sealed class TelegramLink
{
    public long TelegramUserId { get; set; }
    public string UserId { get; set; } = "";
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public DateTime LinkedAtUtc { get; set; }
}

public sealed class TelegramConnectCode
{
    public string Code { get; set; } = "";
    public long TelegramUserId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public DateTime ExpiresUtc { get; set; }
}

public sealed class TelegramLinkToken
{
    public string Token { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
}
