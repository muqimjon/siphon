namespace Siphon.Accounts.Data;

public sealed class UsageDaily
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public Guid? TokenId { get; set; }
    public DateOnly DateUtc { get; set; }
    public string Endpoint { get; set; } = "";
    public int Count { get; set; }
    public long Bytes { get; set; }
}
