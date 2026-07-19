using Siphon.Bot.Backend;
using Siphon.Bot.Data;

namespace Siphon.Bot.Modules.Download;

public enum PrefStep { Type, Format, Quality, Run, TooLarge }

public readonly record struct PrefPlan(PrefStep Step, string Kind, string Format, int Index);

public static class PrefResolver
{
    public static PrefPlan Resolve(ProbeResult probe, UserPref pref, int maxUploadMb, string? kind = null, string? format = null)
    {
        kind ??= ResolveKind(probe, pref);
        if (kind is null) return new PrefPlan(PrefStep.Type, "", "", 0);
        format ??= ResolveFormat(probe, pref, kind);
        if (format is null) return new PrefPlan(PrefStep.Format, kind, "", 0);
        if (pref.Quality == "ask") return new PrefPlan(PrefStep.Quality, kind, format, 0);
        return VariantKeyboard.ResolveQuality(probe, kind, pref.Quality, maxUploadMb) is int index
            ? new PrefPlan(PrefStep.Run, kind, format, index)
            : new PrefPlan(PrefStep.TooLarge, kind, format, 0);
    }

    static string? ResolveKind(ProbeResult probe, UserPref pref) => pref.Kind switch
    {
        "audio" when probe.AudioVariants.Count > 0 => "a",
        "video" when probe.VideoVariants.Count > 0 => "v",
        _ => null
    };

    static string? ResolveFormat(ProbeResult probe, UserPref pref, string kind)
    {
        var formats = kind == "v" ? probe.VideoFormats : probe.AudioFormats;
        var want = kind == "v" ? pref.VideoFormat : pref.AudioFormat;
        if (want != "ask" && formats.Contains(want)) return want;
        if (want != "ask" && formats.Contains(kind == "v" ? "mp4" : "mp3")) return kind == "v" ? "mp4" : "mp3";
        return formats.Count == 1 ? formats[0] : null;
    }
}
