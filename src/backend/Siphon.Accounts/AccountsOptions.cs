namespace Siphon.Accounts;

public sealed class AccountsOptions
{
    public const string Section = "Accounts";

    public string ConnectionString { get; set; } = "";
    public JwtOptions Jwt { get; set; } = new();
    public string? AdminEmail { get; set; }
    public string WebBaseUrl { get; set; } = "";
    public OAuthProvider Google { get; set; } = new();
    public OAuthProvider GitHub { get; set; } = new();
    public string? TelegramBotToken { get; set; }
    public string? BotUsername { get; set; }
    public SmtpOptions Smtp { get; set; } = new();
    public FreePlanOptions FreePlan { get; set; } = new();

    public sealed class JwtOptions
    {
        public string Secret { get; set; } = "";
        public string Issuer { get; set; } = "siphon";
        public string Audience { get; set; } = "siphon";
        public int AccessTokenDays { get; set; } = 7;
    }

    public sealed class OAuthProvider
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public bool Enabled => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    public sealed class SmtpOptions
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? User { get; set; }
        public string? Password { get; set; }
        public string From { get; set; } = "no-reply@siphon.local";
        public bool Enabled => !string.IsNullOrWhiteSpace(Host);
    }

    public sealed class FreePlanOptions
    {
        public int DailyRequests { get; set; } = 100;
        public int MaxConcurrent { get; set; } = 2;
        public int MaxFileSizeMb { get; set; } = 500;
    }
}
