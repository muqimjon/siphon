using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Siphon.Bot.Data;

namespace Siphon.Bot.MiniApp;

public sealed record PrefDto(string Platform, string Kind, string AudioFormat, string VideoFormat, string Quality);

public sealed record PrefUpdate(string Scope, long? GroupChatId, string Platform, string Kind, string AudioFormat, string VideoFormat, string Quality);

public static class MiniAppApi
{
    static readonly string[] Kinds = ["ask", "audio", "video"];
    static readonly string[] AudioFormats = ["ask", "mp3", "m4a", "opus"];
    static readonly string[] VideoFormats = ["ask", "mp4", "webm"];
    static readonly string[] Qualities = ["ask", "high", "medium", "low"];

    public static void MapMiniApp(this WebApplication app)
    {
        var group = app.MapGroup("/miniapp");
        group.MapGet("/prefs", GetPrefs);
        group.MapPut("/prefs", PutPrefs);
    }

    static async Task<IResult> GetPrefs(HttpContext http, BotDb db, IOptions<BotOptions> bot, IOptions<MiniAppOptions> mini, CancellationToken ct)
    {
        var user = Authenticate(http, bot.Value, mini.Value);
        if (user is null) return Results.Unauthorized();

        var groups = await db.Groups.Where(g => g.OwnerUserId == user.Id).OrderBy(g => g.Title).ToListAsync(ct);
        var groupDtos = new List<object>(groups.Count);
        foreach (var g in groups)
            groupDtos.Add(new { chatId = g.ChatId, title = g.Title, platforms = await PlatformsFor(db, g.ChatId, ct) });

        return Results.Ok(new
        {
            user = new { platforms = await PlatformsFor(db, user.Id, ct) },
            groups = groupDtos
        });
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
