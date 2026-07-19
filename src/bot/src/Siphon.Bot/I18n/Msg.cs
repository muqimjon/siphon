namespace Siphon.Bot.I18n;

public abstract class Msg
{
    public static readonly Msg Uz = new MsgUz();
    public static readonly Msg Ru = new MsgRu();
    public static readonly Msg En = new MsgEn();

    public const string ChooseLang = "Tilni tanlang · Выберите язык · Choose a language";

    public static Msg For(string? lang) => lang switch
    {
        "ru" => Ru,
        "en" => En,
        _ => Uz
    };

    public static string Normalize(string? code) => code?.Split('-')[0].ToLowerInvariant() switch
    {
        "uz" or null or "" => "uz",
        "ru" => "ru",
        _ => "en"
    };

    public string PhaseFor(string? phase) => phase?.ToLowerInvariant() switch
    {
        "convert" or "converting" or "postprocess" or "postprocessing" or "merge" or "merging" or "pack" or "packing" => Converting,
        "upload" or "uploading" => Uploading,
        _ => Downloading
    };

    public string PlatformName(string platform) => platform switch
    {
        "youtube" => "YouTube",
        "instagram" => "Instagram",
        "tiktok" => "TikTok",
        "x" => "X",
        "facebook" => "Facebook",
        _ => OtherPlatform
    };

    public string KindLabel(string value) => value switch
    {
        "audio" => TypeAudio,
        "video" => TypeVideo,
        _ => KindAsk
    };

    public string AudioFormatLabel(string value) => value switch
    {
        "mp3" => "MP3",
        "m4a" => "M4A",
        "opus" => "OPUS",
        _ => OptAsk
    };

    public string VideoFormatLabel(string value) => value switch
    {
        "mp4" => "MP4",
        "webm" => "WEBM",
        _ => OptAsk
    };

    public string QualityLabel(string value) => value switch
    {
        "high" => QualHigh,
        "medium" => QualMedium,
        "low" => QualLow,
        _ => OptAsk
    };

    public string SettingsPlatform(string name) => $"⚙️ {name}\n\n{SettingsHint}";

    public abstract string LangName { get; }
    public abstract string Welcome { get; }
    public abstract string SendLink { get; }
    public abstract string Probing { get; }
    public abstract string ChooseType { get; }
    public abstract string ChooseFormat { get; }
    public abstract string ChooseQuality { get; }
    public abstract string TypeAudio { get; }
    public abstract string TypeVideo { get; }
    public abstract string Back { get; }
    public abstract string Original { get; }
    public abstract string Queued { get; }
    public abstract string Downloading { get; }
    public abstract string Converting { get; }
    public abstract string Uploading { get; }
    public abstract string Busy { get; }
    public abstract string ServerBusy { get; }
    public abstract string ServerDown { get; }
    public abstract string Oops { get; }
    public abstract string TimedOut { get; }
    public abstract string Expired { get; }
    public abstract string SubscribeFirst { get; }
    public abstract string JoinChannel { get; }
    public abstract string CheckSub { get; }
    public abstract string SubOk { get; }
    public abstract string SubNotYet { get; }
    public abstract string TooLarge(int mb);
    public abstract string AllTooLarge(int mb);
    public abstract string FinalTooLarge(int mb);
    public abstract string DailyLimit(int n);
    public abstract string Images(int n);
    public abstract string ErrorFor(string code);
    public abstract string AdminUpsell { get; }
    public abstract string SettingsIntro { get; }
    public abstract string SettingsHint { get; }
    public abstract string PrefKind { get; }
    public abstract string PrefAudioFormat { get; }
    public abstract string PrefVideoFormat { get; }
    public abstract string PrefQuality { get; }
    public abstract string KindAsk { get; }
    public abstract string OptAsk { get; }
    public abstract string QualHigh { get; }
    public abstract string QualMedium { get; }
    public abstract string QualLow { get; }
    public abstract string OtherPlatform { get; }
    public abstract string GroupAdded { get; }
    public abstract string GroupSettingsHint { get; }
    public abstract string GroupSettingsDenied { get; }
}
