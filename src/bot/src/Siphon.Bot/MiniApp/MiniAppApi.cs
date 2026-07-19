using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Siphon.Bot.Data;
using Siphon.Bot.I18n;

namespace Siphon.Bot.MiniApp;

public sealed record PrefDto(string Platform, string Kind, string AudioFormat, string VideoFormat, string Quality);

public sealed record PrefUpdate(string Scope, long? GroupChatId, string Platform, string Kind, string AudioFormat, string VideoFormat, string Quality);

public sealed record LangUpdate(string Lang);

public static class MiniAppApi
{
    static readonly string[] Kinds = ["ask", "audio", "video"];
    static readonly string[] AudioFormats = ["ask", "best", "mp3", "m4a", "opus"];
    static readonly string[] VideoFormats = ["ask", "best", "mp4", "webm"];
    static readonly string[] Qualities = ["ask", "high", "medium", "low"];
    static readonly string[] Langs = ["uz", "ru", "en"];

    public static void MapMiniApp(this WebApplication app)
    {
        var group = app.MapGroup("/miniapp");
        group.MapGet("/profile", GetProfile);
        group.MapPut("/prefs", PutPrefs);
        group.MapPut("/lang", PutLang);
    }

    static async Task<IResult> GetProfile(HttpContext http, BotDb db, IOptions<BotOptions> bot, IOptions<MiniAppOptions> mini, IOptions<LimitsOptions> limits, CancellationToken ct)
    {
        var user = Authenticate(http, bot.Value, mini.Value);
        if (user is null) return Results.Unauthorized();

        var state = await db.Chats.FindAsync([user.Id], ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var downloadsToday = state?.DayKey == today ? state.DownloadsToday : 0;
        var totalDownloads = await db.Events.CountAsync(e => e.ChatId == user.Id && e.Kind == "download", ct);
        var topSites = await db.Events
            .Where(e => e.ChatId == user.Id && e.Site != null)
            .GroupBy(e => e.Site!)
            .Select(g => new { site = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(3)
            .ToListAsync(ct);

        var groups = await db.Groups.Where(g => g.OwnerUserId == user.Id).OrderBy(g => g.Title).ToListAsync(ct);
        var groupDtos = new List<object>(groups.Count);
        foreach (var g in groups)
            groupDtos.Add(new { chatId = g.ChatId, title = g.Title, addedUtc = g.AddedUtc, platforms = await PlatformsFor(db, g.ChatId, ct) });

        return Results.Ok(new
        {
            user = new
            {
                id = user.Id,
                firstName = user.FirstName,
                lastName = user.LastName,
                username = user.Username,
                photoUrl = user.PhotoUrl,
                lang = state?.Lang ?? Msg.Normalize(user.LanguageCode)
            },
            stats = new
            {
                downloadsToday,
                dailyLimit = limits.Value.DailyDownloadsPerChat,
                totalDownloads,
                maxUploadMb = limits.Value.MaxUploadMb,
                since = state?.LastActivityUtc,
                topSites
            },
            platforms = await PlatformsFor(db, user.Id, ct),
            groups = groupDtos
        });
    }

    static async Task<IResult> PutLang(HttpContext http, BotDb db, IOptions<BotOptions> bot, IOptions<MiniAppOptions> mini, LangUpdate body, CancellationToken ct)
    {
        var user = Authenticate(http, bot.Value, mini.Value);
        if (user is null) return Results.Unauthorized();
        if (!Langs.Contains(body.Lang)) return Results.BadRequest();

        var state = await db.Chats.FindAsync([user.Id], ct);
        if (state is null)
        {
            state = new ChatState { ChatId = user.Id, Lang = body.Lang };
            db.Chats.Add(state);
        }
        else
        {
            state.Lang = body.Lang;
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { ok = true });
    }

    static async Task<IResult> PutPrefs(HttpContext http, BotDb db, IOptions<BotOptions> bot, IOptions<MiniAppOptions> mini, PrefUpdate body, CancellationToken ct)
    {
        var user = Authenticate(http, bot.Value, mini.Value);
        if (user is null) return Results.Unauthorized();

        if (!Platforms.All.Contains(body.Platform) || !Kinds.Contains(body.Kind) || !AudioFormats.Contains(body.AudioFormat)
            || !VideoFormats.Contains(body.VideoFormat) || !Qualities.Contains(body.Quality))
            return Results.BadRequest();

        long chatId;
        if (body.Scope == "group")
        {
            if (body.GroupChatId is not { } gid) return Results.BadRequest();
            var info = await db.Groups.FindAsync([gid], ct);
            if (info is null || info.OwnerUserId != user.Id) return Results.StatusCode(StatusCodes.Status403Forbidden);
            chatId = gid;
        }
        else if (body.Scope == "user")
        {
            chatId = user.Id;
        }
        else
        {
            return Results.BadRequest();
        }

        var pref = await db.Prefs.FindAsync([chatId, body.Platform], ct);
        if (pref is null)
        {
            pref = new UserPref { ChatId = chatId, Platform = body.Platform };
            db.Prefs.Add(pref);
        }
        pref.Kind = body.Kind;
        pref.AudioFormat = body.AudioFormat;
        pref.VideoFormat = body.VideoFormat;
        pref.Quality = body.Quality;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { ok = true });
    }

    static async Task<List<PrefDto>> PlatformsFor(BotDb db, long chatId, CancellationToken ct)
    {
        var rows = await db.Prefs.Where(p => p.ChatId == chatId).ToDictionaryAsync(p => p.Platform, ct);
        return Platforms.All.Select(p =>
        {
            rows.TryGetValue(p, out var r);
            return new PrefDto(p, r?.Kind ?? "ask", r?.AudioFormat ?? "ask", r?.VideoFormat ?? "ask", r?.Quality ?? "ask");
        }).ToList();
    }

    static TelegramUser? Authenticate(HttpContext http, BotOptions bot, MiniAppOptions mini)
    {
        var initData = ExtractInitData(http.Request);
        return TelegramInitData.Validate(initData, bot.Token, TimeSpan.FromHours(mini.InitDataTtlHours));
    }

    static string? ExtractInitData(HttpRequest req)
    {
        var auth = req.Headers.Authorization.ToString();
        if (auth.StartsWith("tma ", StringComparison.OrdinalIgnoreCase))
            return auth["tma ".Length..].Trim();
        var header = req.Headers["X-Init-Data"].ToString();
        return string.IsNullOrEmpty(header) ? null : header;
    }
}
