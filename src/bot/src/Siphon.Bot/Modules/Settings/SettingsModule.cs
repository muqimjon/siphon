using Siphon.Bot.Core;
using Siphon.Bot.Data;
using Siphon.Bot.I18n;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.Modules.Settings;

public sealed class SettingsModule(BotDb db) : IFeatureModule
{
    static readonly string[] Kinds = ["ask", "audio", "video"];
    static readonly string[] AudioFormats = ["best", "mp3", "m4a", "opus"];
    static readonly string[] VideoFormats = ["best", "mp4", "webm"];
    static readonly string[] Qualities = ["ask", "high", "medium", "low"];

    public IReadOnlyList<BotCommand> Commands { get; } =
    [
        new("settings", "Sozlamalar · Settings")
    ];

    public bool CanHandle(UpdateContext ctx) =>
        ctx.Message?.Text?.StartsWith("/settings") == true || ctx.Callback?.Data?.StartsWith("s:") == true;

    public async Task HandleAsync(UpdateContext ctx, CancellationToken ct)
    {
        if (ctx.IsGroup && !await IsAllowedAsync(ctx, ct))
        {
            if (ctx.Callback is { } denied)
                await ctx.Bot.AnswerCallbackQuery(denied.Id, ctx.L.GroupSettingsDenied, showAlert: true, cancellationToken: ct);
            else
                await ctx.Bot.SendMessage(ctx.ChatId, ctx.L.GroupSettingsDenied, cancellationToken: ct);
            return;
        }
        if (ctx.Callback is { } cb)
        {
            await HandleCallbackAsync(ctx, cb, ct);
            return;
        }
        await ctx.Bot.SendMessage(ctx.ChatId, Intro(ctx), replyMarkup: PlatformKeyboard(ctx.L), cancellationToken: ct);
    }

    async Task<bool> IsAllowedAsync(UpdateContext ctx, CancellationToken ct)
    {
        var info = await db.Groups.FindAsync([ctx.ChatId], ct);
        if (info?.OwnerUserId == ctx.UserId) return true;
        try
        {
            var member = await ctx.Bot.GetChatMember(ctx.ChatId, ctx.UserId, ct);
            return member.Status is ChatMemberStatus.Creator or ChatMemberStatus.Administrator;
        }
        catch
        {
            return false;
        }
    }

    static string Intro(UpdateContext ctx) =>
        ctx.IsGroup ? $"{ctx.L.GroupSettingsHint}\n\n{ctx.L.SettingsIntro}" : ctx.L.SettingsIntro;

    async Task HandleCallbackAsync(UpdateContext ctx, CallbackQuery cb, CancellationToken ct)
    {
        var parts = cb.Data!.Split(':');
        var action = parts.Length > 1 ? parts[1] : "";
        await ctx.Bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
        if (action == "back")
        {
            await EditAsync(ctx, cb, Intro(ctx), PlatformKeyboard(ctx.L), ct);
            return;
        }
        if (parts.Length < 3 || !Platforms.All.Contains(parts[2]))
            return;
        var platform = parts[2];
        var pref = await db.Prefs.FindAsync([ctx.ChatId, platform], ct);
        if (action is "t" or "af" or "vf" or "q")
        {
            if (pref is null)
            {
                pref = new UserPref { ChatId = ctx.ChatId, Platform = platform };
                db.Prefs.Add(pref);
            }
            switch (action)
            {
                case "t": pref.Kind = Cycle(pref.Kind, Kinds); break;
                case "af": pref.AudioFormat = Cycle(pref.AudioFormat, AudioFormats); break;
                case "vf": pref.VideoFormat = Cycle(pref.VideoFormat, VideoFormats); break;
                case "q": pref.Quality = Cycle(pref.Quality, Qualities); break;
            }
        }
        pref ??= new UserPref { ChatId = ctx.ChatId, Platform = platform };
        await EditAsync(ctx, cb, ctx.L.SettingsPlatform(ctx.L.PlatformName(platform)), ConfigKeyboard(ctx.L, pref), ct);
    }

    static string Cycle(string current, string[] options)
    {
        var i = Array.IndexOf(options, current);
        return options[(i + 1) % options.Length];
    }

    static InlineKeyboardMarkup PlatformKeyboard(Msg l) => new(new[]
    {
        new[] { Platform(l, Platforms.YouTube, "▶️"), Platform(l, Platforms.Instagram, "📸") },
        new[] { Platform(l, Platforms.TikTok, "🎵"), Platform(l, Platforms.X, "✖️") },
        new[] { Platform(l, Platforms.Facebook, "📘"), Platform(l, Platforms.Other, "🌐") }
    });

    static InlineKeyboardButton Platform(Msg l, string platform, string icon) =>
        InlineKeyboardButton.WithCallbackData($"{icon} {l.PlatformName(platform)}", $"s:p:{platform}");

    static InlineKeyboardMarkup ConfigKeyboard(Msg l, UserPref p) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData($"{l.PrefKind}: {l.KindLabel(p.Kind)}", $"s:t:{p.Platform}") },
        new[] { InlineKeyboardButton.WithCallbackData($"{l.PrefAudioFormat}: {l.AudioFormatLabel(p.AudioFormat)}", $"s:af:{p.Platform}") },
        new[] { InlineKeyboardButton.WithCallbackData($"{l.PrefVideoFormat}: {l.VideoFormatLabel(p.VideoFormat)}", $"s:vf:{p.Platform}") },
        new[] { InlineKeyboardButton.WithCallbackData($"{l.PrefQuality}: {l.QualityLabel(p.Quality)}", $"s:q:{p.Platform}") },
        new[] { InlineKeyboardButton.WithCallbackData(l.Back, "s:back") }
    });

    static async Task EditAsync(UpdateContext ctx, CallbackQuery cb, string text, InlineKeyboardMarkup markup, CancellationToken ct)
    {
        if (cb.Message is null) return;
        try
        {
            await ctx.Bot.EditMessageText(ctx.ChatId, cb.Message.MessageId, text, replyMarkup: markup, cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not modified"))
        {
        }
    }
}
