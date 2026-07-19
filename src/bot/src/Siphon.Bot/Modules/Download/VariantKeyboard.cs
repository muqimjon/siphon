using System.Globalization;
using Siphon.Bot.Backend;
using Siphon.Bot.I18n;
using Telegram.Bot.Types.ReplyMarkups;

namespace Siphon.Bot.Modules.Download;

public static class VariantKeyboard
{
    const int PageSize = 8;

    readonly record struct Row(string Label, bool Blocked, int Index);

    public static InlineKeyboardMarkup? BuildTypes(ProbeResult probe, string token, int maxUploadMb, Msg l)
    {
        var rows = new List<InlineKeyboardButton[]>();
        if (probe.AudioFormats.Count > 0 && AudioRows(probe, maxUploadMb).Any(r => !r.Blocked))
            rows.Add([InlineKeyboardButton.WithCallbackData($"🎵 {l.TypeAudio}", $"d:{token}:t:a")]);
        if (probe.VideoFormats.Count > 0 && VideoRows(probe, maxUploadMb).Any(r => !r.Blocked))
            rows.Add([InlineKeyboardButton.WithCallbackData($"🎬 {l.TypeVideo}", $"d:{token}:t:v")]);
        if (probe.Images.Count > 0)
            rows.Add([InlineKeyboardButton.WithCallbackData($"🖼 {l.Images(probe.Images.Count)}", $"d:{token}:g")]);
        return rows.Count == 0 ? null : new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup BuildFormats(ProbeResult probe, string token, string kind, Msg l)
    {
        var formats = kind == "v" ? probe.VideoFormats : probe.AudioFormats;
        var rows = formats
            .Select(f => new[] { InlineKeyboardButton.WithCallbackData(FormatLabel(f, l), $"d:{token}:f:{kind}:{f}") })
            .ToList();
        rows.Add([InlineKeyboardButton.WithCallbackData(l.Back, $"d:{token}:t")]);
        return new InlineKeyboardMarkup(rows);
    }

    public static int? ResolveQuality(ProbeResult probe, string kind, string quality, int maxUploadMb, string format = "mp3")
    {
        var rows = (kind == "v" ? VideoRows(probe, maxUploadMb) : AudioRows(probe, maxUploadMb, format))
            .Where(r => !r.Blocked)
            .ToList();
        if (rows.Count == 0) return null;
        var row = quality switch
        {
            "high" => rows[0],
            "low" => rows[^1],
            _ => rows[rows.Count / 2]
        };
        return row.Index;
    }

    public static (InlineKeyboardMarkup? Markup, int? AutoRunIndex) BuildQuality(ProbeResult probe, string token, string kind, string format, int page, int maxUploadMb, Msg l, bool formatLocked = false)
    {
        var rows = kind == "v" ? VideoRows(probe, maxUploadMb) : AudioRows(probe, maxUploadMb, format);
        if (rows.Count == 1 && !rows[0].Blocked)
            return (null, rows[0].Index);

        var pages = Math.Max(1, (rows.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        var keyboard = rows.Skip(page * PageSize).Take(PageSize)
            .Select(r => new[]
            {
                InlineKeyboardButton.WithCallbackData(r.Label, r.Blocked ? $"d:{token}:x" : $"d:{token}:d:{kind}:{format}:{r.Index}")
            })
            .ToList();
        if (pages > 1)
        {
            var nav = new List<InlineKeyboardButton>();
            if (page > 0) nav.Add(InlineKeyboardButton.WithCallbackData("«", $"d:{token}:pq:{kind}:{format}:{page - 1}"));
            nav.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}/{pages}", $"d:{token}:i"));
            if (page < pages - 1) nav.Add(InlineKeyboardButton.WithCallbackData("»", $"d:{token}:pq:{kind}:{format}:{page + 1}"));
            keyboard.Add(nav.ToArray());
        }
        var formats = kind == "v" ? probe.VideoFormats : probe.AudioFormats;
        var back = !formatLocked && formats.Count > 1 ? $"d:{token}:t:{kind}" : $"d:{token}:t";
        keyboard.Add([InlineKeyboardButton.WithCallbackData(l.Back, back)]);
        return (new InlineKeyboardMarkup(keyboard), null);
    }

    static List<Row> VideoRows(ProbeResult probe, int maxUploadMb)
    {
        var limit = maxUploadMb * 1024L * 1024;
        return probe.VideoVariants
            .Select((v, i) => (v, i))
            .OrderByDescending(x => x.v.Height ?? 0)
            .Select(x =>
            {
                var size = x.v.ApproxSizeBytes ?? Estimate(x.v.VbrKbps, probe.DurationSec);
                var blocked = size > limit;
                var res = x.v.Height is int h
                    ? $"{h}p{(x.v.Fps >= 50 ? ((int)x.v.Fps.Value).ToString() : "")}"
                    : x.v.FormatId;
                return new Row($"{Block(blocked)}🎬 {res}{SizePart(size)}", blocked, x.i);
            })
            .DistinctBy(r => r.Label)
            .ToList();
    }

    static List<Row> AudioRows(ProbeResult probe, int maxUploadMb, string format = "mp3")
    {
        var limit = maxUploadMb * 1024L * 1024;
        var transcoded = format == "mp3";
        return probe.AudioVariants
            .Select((a, i) => (a, i))
            .OrderByDescending(x => (transcoded ? x.a.PlannedMp3?.TypicalKbps : null) ?? x.a.AbrKbps ?? 0)
            .Select(x =>
            {
                var kbps = transcoded && x.a.PlannedMp3 is not null
                    ? x.a.PlannedMp3.TypicalKbps
                    : x.a.AbrKbps is double abr ? (int)Math.Round(abr) : 0;
                var size = transcoded && x.a.PlannedMp3 is not null
                    ? Estimate(x.a.PlannedMp3.TypicalKbps, probe.DurationSec) ?? x.a.ApproxSizeBytes
                    : x.a.ApproxSizeBytes ?? Estimate(x.a.AbrKbps, probe.DurationSec);
                var blocked = size > limit;
                return new Row($"{Block(blocked)}🎵{(kbps > 0 ? $" {kbps}k" : "")}{SizePart(size)}", blocked, x.i);
            })
            .DistinctBy(r => r.Label)
            .ToList();
    }

    static string FormatLabel(string format, Msg l) => format switch
    {
        "mp3" => "MP3",
        "m4a" => "M4A",
        "mp4" => "MP4",
        "opus" => $"OPUS ({l.Original})",
        "webm" => $"WEBM ({l.Original})",
        _ => format.ToUpperInvariant()
    };

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
