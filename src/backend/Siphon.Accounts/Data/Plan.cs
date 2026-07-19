namespace Siphon.Accounts.Data;

public sealed class Plan
{
    public const int FreeId = 1;

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int DailyRequests { get; set; }
    public int MaxConcurrent { get; set; }
    public int MaxFileSizeMb { get; set; }
}
