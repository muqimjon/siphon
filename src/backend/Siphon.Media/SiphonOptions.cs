namespace Siphon.Media;

public sealed class SiphonOptions
{
    public const string Section = "Siphon";

    public ToolPaths Tools { get; set; } = new();
    public string TempRoot { get; set; } = "";
    public int MaxConcurrentJobs { get; set; } = 2;
    public int MaxQueuedJobs { get; set; } = 20;
    public int JobTtlMinutes { get; set; } = 30;
    public int MaxFileSizeMb { get; set; } = 2048;
    public int MaxDurationMinutes { get; set; } = 240;
    public int MaxGalleryImages { get; set; } = 50;
    public int ProbeTimeoutSeconds { get; set; } = 30;
    public int DownloadTimeoutMinutes { get; set; } = 20;
    public int MinFreeDiskMb { get; set; } = 5120;
    public string? ProxyUrl { get; set; }
    public SelfUpdateOptions SelfUpdate { get; set; } = new();
    public string FileTokenSecret { get; set; } = "";
    public Dictionary<string, string> ApiKeys { get; set; } = [];
    public Dictionary<string, int> ClientLimits { get; set; } = [];

    public sealed class ToolPaths
    {
        public string YtDlpPath { get; set; } = "";
        public string FfmpegPath { get; set; } = "";
        public string? GalleryDlPath { get; set; }
    }

    public sealed class SelfUpdateOptions
    {
        public bool Enabled { get; set; } = true;
        public int IntervalHours { get; set; } = 168;
    }
}
