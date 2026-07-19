using System.Globalization;
using Siphon.Bot.Backend;
using Siphon.Bot.I18n;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.Modules.Download;

public static class VariantKeyboard
{
    const int PageSize = 8;

    public static (InlineKeyboardMarkup? Markup, bool AllBlocked) Build(ProbeResult probe, string token, int page, int maxUploadMb, Msg l)
    {
        var rows = BuildRows(probe, token, maxUploadMb, l);
        if (rows.Count == 0) return (null, false);
        if (rows.All(r => r.Blocked)) return (null, true);

        var pages = (rows.Count + PageSize - 1) / PageSize;
        page = Math.Clamp(page, 0, pages - 1);
        var keyboard = rows.Skip(page * PageSize).Take(PageSize)
            .Select(r => new[] { InlineKeyboardButton.WithCallbackData(r.Label, r.Data) })
            .ToList();
        if (pages > 1)
        {
            var nav = new List<InlineKeyboardButton>();
            if (page > 0) nav.Add(InlineKeyboardButton.WithCallbackData("«", $"d:{token}:p:{page - 1}"));
            nav.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}/{pages}", $"d:{token}:i"));
            if (page < pages - 1) nav.Add(InlineKeyboardButton.WithCallbackData("»", $"d:{token}:p:{page + 1}"));
            keyboard.Add(nav.ToArray());
        }
        return (new InlineKeyboardMarkup(keyboard), false);
    }

    static List<(string Label, string Data, bool Blocked)> BuildRows(ProbeResult probe, string token, int maxUploadMb, Msg l)
    {
        var limit = maxUploadMb * 1024L * 1024;
        var rows = new List<(string, string, bool)>();
        foreach (var (v, i) in probe.VideoVariants.Select((v, i) => (v, i)).OrderByDescending(x => x.v.Height ?? 0))
        {
            var size = v.ApproxSizeBytes ?? Estimate(v.VbrKbps, probe.DurationSec);
            var blocked = size > limit;
            var res = v.Height is int h
                ? $"{h}p{(v.Fps >= 50 ? ((int)v.Fps.Value).ToString() : "")}"
                : v.FormatId;
            rows.Add(($"{Block(blocked)}🎬 Video {res}{SizePart(size)}", blocked ? $"d:{token}:x" : $"d:{token}:v:{i}", blocked));
        }
        foreach (var (a, i) in probe.AudioVariants.Select((a, i) => (a, i)).OrderByDescending(x => x.a.PlannedMp3?.TypicalKbps ?? x.a.AbrKbps ?? 0))
        {
            var kbps = a.PlannedMp3?.TypicalKbps ?? (a.AbrKbps is double abr ? (int)Math.Round(abr) : 0);
            var size = a.PlannedMp3 is not null
                ? Estimate(a.PlannedMp3.TypicalKbps, probe.DurationSec) ?? a.ApproxSizeBytes
                : a.ApproxSizeBytes ?? Estimate(a.AbrKbps, probe.DurationSec);
            var blocked = size > limit;
            rows.Add(($"{Block(blocked)}🎵 Audio MP3{(kbps > 0 ? $" · ~{kbps}k" : "")}{SizePart(size)}", blocked ? $"d:{token}:x" : $"d:{token}:a:{i}", blocked));
        }
        if (probe.Images.Count > 0)
            rows.Add(($"🖼 {l.Images(probe.Images.Count)}", $"d:{token}:g", false));
        return rows;
    }

    static string Block(bool blocked) => blocked ? "⛔ " : "";

    static long? Estimate(double? kbps, double? durationSec) =>
        kbps > 0 && durationSec > 0 ? (long)(kbps!.Value * 1000 / 8 * durationSec!.Value) : null;

    static string SizePart(long? bytes) => bytes is long b ? $" · ~{FormatMb(b)}" : "";

    static string FormatMb(long bytes)
    {
        var mb = bytes / 1048576.0;
        return mb.ToString(mb >= 10 ? "0" : "0.#", CultureInfo.InvariantCulture) + " MB";
    }
}
