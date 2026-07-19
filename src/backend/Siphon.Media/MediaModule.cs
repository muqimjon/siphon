using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Siphon.Media.Delivery;
using Siphon.Media.Engine;
using Siphon.Media.Http;
using Siphon.Media.Jobs;

namespace Siphon.Media;

public static class MediaModule
{
    public static IServiceCollection AddMedia(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        services.AddOptions<SiphonOptions>()
            .Bind(configuration.GetSection(SiphonOptions.Section))
            .PostConfigure(o =>
            {
                o.TempRoot = Normalize(contentRoot, o.TempRoot);
                o.Tools.YtDlpPath = Normalize(contentRoot, o.Tools.YtDlpPath);
                o.Tools.FfmpegPath = Normalize(contentRoot, o.Tools.FfmpegPath);
                if (o.Tools.GalleryDlPath is { } galleryDl)
                {
                    var normalized = Normalize(contentRoot, galleryDl);
                    o.Tools.GalleryDlPath = File.Exists(normalized) ? normalized : null;
                }
            })
            .Validate(o => File.Exists(o.Tools.YtDlpPath), "yt-dlp binary not found (Siphon:Tools:YtDlpPath)")
            .Validate(o => File.Exists(o.Tools.FfmpegPath), "ffmpeg binary not found (Siphon:Tools:FfmpegPath)")
            .Validate(o => o.FileTokenSecret.Length >= 32, "Siphon:FileTokenSecret must be at least 32 characters")
            .Validate(o => o.ApiKeys.Count > 0, "Siphon:ApiKeys must contain at least one client key")
            .ValidateOnStart();

        services.AddSingleton<JobStore>();
        services.AddSingleton<JobEngine>();
        services.AddSingleton<FileTokenService>();
        services.AddSingleton<SelfUpdater>();
        services.AddSingleton<YtDlpEngine>();
        services.AddSingleton<GalleryDlEngine>();
        services.AddSingleton<ProbeHandler>();
        services.AddSingleton<JobHandlers>();
        services.AddSingleton<FileHandler>();
        services.AddSingleton<HealthHandler>();
        services.AddScoped<IApiAuthenticator, StaticKeyAuthenticator>();
        services.AddSingleton<IUsageSink, NullUsageSink>();
        services.AddHostedService<JobWorker>();
        services.AddHostedService<TtlCleaner>();
        services.AddHostedService(sp => sp.GetRequiredService<SelfUpdater>());

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("per-key", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Request.Headers["X-Api-Key"].ToString(),
                _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 300,
                    QueueLimit = 0,
                }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapMedia(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/health", (HealthHandler handler, CancellationToken ct) => handler.Handle(ct))
            .WithName("Health").WithSummary("Service health and tool versions").WithTags("System");
        api.MapGet("/files/{id}", (string id, string? token, FileHandler handler) => handler.Handle(id, token))
            .WithName("DownloadFile").WithSummary("Download a finished job's file with its short-lived token").WithTags("Files");

        var secured = api.MapGroup("").AddEndpointFilter<ApiKeyFilter>().RequireRateLimiting("per-key");
        secured.MapPost("/probe", (ProbeRequest request, ProbeHandler handler, CancellationToken ct) => handler.Handle(request, ct))
            .WithName("Probe").WithSummary("Inspect a URL and return the available audio/video/image variants").WithTags("Media");
        secured.MapPost("/jobs", (CreateJobRequest request, JobHandlers handler) => handler.Create(request))
            .WithName("CreateJob").WithSummary("Start a download job for a URL in the chosen output and format").WithTags("Media");
        secured.MapGet("/jobs/{id}", (string id, JobHandlers handler) => handler.Status(id))
            .WithName("JobStatus").WithSummary("Poll a job's state, progress and (when ready) its file link").WithTags("Media");
        secured.MapDelete("/jobs/{id}", (string id, JobHandlers handler) => handler.Delete(id))
            .WithName("CancelJob").WithSummary("Cancel a running job or expire a finished one").WithTags("Media");
        return app;
    }

    private static string Normalize(string contentRoot, string path) =>
        string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(contentRoot, path));
}
