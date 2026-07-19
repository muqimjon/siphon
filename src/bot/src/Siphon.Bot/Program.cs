using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Siphon.Bot;
using Siphon.Bot.Backend;
using Siphon.Bot.Core;
using Siphon.Bot.Data;
using Siphon.Bot.Middleware;
using Siphon.Bot.MiniApp;
using Siphon.Bot.Modules.Core;
using Siphon.Bot.Modules.Download;
using Siphon.Bot.Modules.Group;
using Siphon.Bot.Modules.Settings;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "MiniApp", "wwwroot")
});
var services = builder.Services;
var config = builder.Configuration;

services.Configure<BotOptions>(config.GetSection("Bot"));
services.Configure<BackendOptions>(config.GetSection("Backend"));
services.Configure<GateOptions>(config.GetSection("Gate"));
services.Configure<LimitsOptions>(config.GetSection("Limits"));
services.Configure<MiniAppOptions>(config.GetSection("MiniApp"));
services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

services.AddMemoryCache();
services.AddDbContext<BotDb>(o => o.UseSqlite($"Data Source={config["Db:Path"] ?? "bot.db"}"));

var backend = config.GetSection("Backend").Get<BackendOptions>() ?? new();
var backendBase = new Uri(backend.BaseUrl.TrimEnd('/') + "/");
services.AddHttpClient<SiphonApi>(c =>
{
    c.BaseAddress = backendBase;
    c.DefaultRequestHeaders.Add("X-Api-Key", backend.ApiKey);
    c.Timeout = Timeout.InfiniteTimeSpan;
}).AddStandardResilienceHandler(o =>
{
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(backend.TimeoutSeconds);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(backend.TimeoutSeconds * 4);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(backend.TimeoutSeconds * 2);
    o.Retry.DisableForUnsafeHttpMethods();
});
services.AddHttpClient("siphon-files", c =>
{
    c.BaseAddress = backendBase;
    c.Timeout = TimeSpan.FromMinutes(10);
});
services.AddHttpClient("telegram", c => c.Timeout = TimeSpan.FromMinutes(10));

var botToken = config.GetSection("Bot").Get<BotOptions>()?.Token ?? "";
Siphon.Bot.Backend.SiphonApi.TelegramFileBase = $"https://api.telegram.org/file/bot{botToken}";

services.AddSingleton<ITelegramBotClient>(sp =>
{
    var bot = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    var options = new TelegramBotClientOptions(bot.Token, bot.ApiBaseUrl);
    return new TelegramBotClient(options, sp.GetRequiredService<IHttpClientFactory>().CreateClient("telegram"));
});

services.AddScoped<IUpdateMiddleware, ErrorBoundaryMiddleware>();
services.AddScoped<IUpdateMiddleware, StateMiddleware>();
services.AddScoped<SubscriptionGateMiddleware>();
services.AddScoped<IUpdateMiddleware>(sp => sp.GetRequiredService<SubscriptionGateMiddleware>());
services.AddScoped<IUpdateMiddleware, Siphon.Bot.Core.RouterMiddleware>();
services.AddScoped<UpdatePipeline>();
services.AddScoped<IFeatureModule, DownloadModule>();
services.AddScoped<IFeatureModule, SettingsModule>();
services.AddScoped<IFeatureModule, GroupModule>();
services.AddScoped<IFeatureModule, Siphon.Bot.Modules.Convert.ConvertModule>();
services.AddScoped<IFeatureModule, CoreModule>();
services.AddSingleton<ProbeCache>();
services.AddSingleton<JobRunner>();
services.AddSingleton<Siphon.Bot.Modules.Convert.ConvertCache>();
services.AddHostedService<PollingService>();
services.AddHostedService<MenuButtonSetup>();
services.AddHostedService<Siphon.Bot.Modules.Download.FileCacheJanitor>();

var miniApp = config.GetSection("MiniApp").Get<MiniAppOptions>() ?? new();
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls($"http://+:{miniApp.Port}");

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<BotDb>().Database.Migrate();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapMiniApp();
app.MapFallbackToFile("index.html");

app.Run();

namespace Siphon.Bot
{
    public sealed class BotOptions
    {
        public string Token { get; set; } = "";
        public string? ApiBaseUrl { get; set; }
    }

    public sealed class BackendOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:5046";
        public string ApiKey { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 30;
    }

    public sealed class GateOptions
    {
        public bool Enabled { get; set; }
        public string Channel { get; set; } = "";
        public int CacheMinutes { get; set; } = 10;
    }

    public sealed class LimitsOptions
    {
        public int MaxUploadMb { get; set; } = 49;
        public int MaxActiveJobsPerChat { get; set; } = 1;
        public int GlobalMaxActiveJobs { get; set; } = 5;
        public double EditThrottleSeconds { get; set; } = 2.5;
        public int PollSeconds { get; set; } = 2;
        public int ProbeCacheMinutes { get; set; } = 15;
        public int JobTimeoutMinutes { get; set; } = 10;
        public int DailyDownloadsPerChat { get; set; } = 30;
        public int FileCacheCheckDays { get; set; } = 7;
        public int MaxTelegramFileMb { get; set; } = 20;
    }

    public sealed class MiniAppOptions
    {
        public string BaseUrl { get; set; } = "";
        public int Port { get; set; } = 8090;
        public int InitDataTtlHours { get; set; } = 24;
    }
}
