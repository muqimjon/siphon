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

    public abstract string LangName { get; }
    public abstract string Welcome { get; }
    public abstract string SendLink { get; }
    public abstract string Probing { get; }
    public abstract string ChooseVariant { get; }
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
}
