namespace Siphon.Bot.I18n;

public sealed class MsgEn : Msg
{
    public override string LangName => "English";
    public override string Welcome => "Hi! 👋 I download videos, music and pictures from the internet.\n\nSend me a link — from YouTube, Instagram, TikTok and more. I'll handle the rest 😉";
    public override string SendLink => "Send me a link and I'll download it 😉";
    public override string Probing => "🔎 Opening the link…";
    public override string ChooseType => "What should I download?";
    public override string ChooseFormat => "Which format?";
    public override string ChooseQuality => "Which quality?";
    public override string TypeAudio => "Audio";
    public override string TypeVideo => "Video";
    public override string Back => "⬅️ Back";
    public override string Original => "original";
    public override string FormatBest => "⭐ Best (source quality)";
    public override string Queued => "⏳ In the queue…";
    public override string Downloading => "⬇️ Downloading…";
    public override string Converting => "🎛 Processing…";
    public override string Uploading => "📤 Sending…";
    public override string Busy => "Please wait for your current download to finish 🙂";
    public override string ServerBusy => "Everything is busy right now. Try again in a couple of minutes 🙏";
    public override string ServerDown => "The server is taking a nap 😴 Please try again later.";
    public override string Oops => "Something went wrong 😅 Please try again.";
    public override string TimedOut => "That took way too long, I had to stop 😔 Please try again.";
    public override string Expired => "This button has expired. Send the link again 🙂";
    public override string SubscribeFirst => "To use the bot, please join our channel first 🙂";
    public override string JoinChannel => "➕ Open channel";
    public override string CheckSub => "✅ Check";
    public override string SubOk => "Thanks! Enjoy the bot 🎉";
    public override string SubNotYet => "You haven't joined yet. Please join the channel first 🙂";
    public override string TooLarge(int mb) => $"This file is over {mb} MB. Telegram doesn't let bots send files that big 😔 Pick a smaller option.\n\n{AdminUpsell}";
    public override string AllTooLarge(int mb) => $"Sorry, every option here is over {mb} MB — Telegram doesn't let bots send files that big 😔\n\n{AdminUpsell}";
    public override string FinalTooLarge(int mb) => $"The file turned out bigger than expected and didn't fit the {mb} MB limit 😔 Try a smaller option.\n\n{AdminUpsell}";
    public override string DailyLimit(int n) => $"You've used today's limit ({n} downloads). Come back tomorrow! 🌙\n\n{AdminUpsell}";
    public override string Images(int n) => $"Pictures ({n})";
    public override string AdminUpsell => "For bigger files, contact the admin.";
    public override string SettingsIntro => "⚙️ Settings\n\nWhich platform should we set defaults for?";
    public override string SettingsHint => "These are applied automatically when a link arrives:";
    public override string PrefKind => "Type";
    public override string PrefAudioFormat => "Audio format";
    public override string PrefVideoFormat => "Video format";
    public override string PrefQuality => "Quality";
    public override string KindAsk => "Ask each time";
    public override string OptAsk => "Ask";
    public override string QualHigh => "High";
    public override string QualMedium => "Medium";
    public override string QualLow => "Low";
    public override string OtherPlatform => "Other";
    public override string GroupAdded => "Hi! 👋 Now just drop a video, music or picture link in this group — I'll download it automatically.\n\nTo change the defaults, use /settings.";
    public override string GroupSettingsHint => "🔧 These settings are the defaults for this group.";
    public override string GroupSettingsDenied => "Only the person who added the bot or a group admin can change these settings.";
    public override string ErrorFor(string code) => code switch
    {
        "invalid-url" => "That doesn't look like a link 🤔 Please send a valid one.",
        "unsupported-site" => "I don't know this site yet 😔 Try another link.",
        "unavailable" => "Couldn't find this content — it may be deleted or private 😔",
        "geo-blocked" => "This content is blocked in our region 😔",
        "age-restricted" => "This one is age-restricted, I couldn't download it 😔",
        "login-required" => "The site asks for a login, and I can't sign in 😔",
        "live-not-supported" => "Live streams can't be downloaded. Try again once it's over 🙂",
        "playlist-not-supported" => "Send a link to a single video, not a whole playlist 🙂",
        "too-large" => "This file is too big for me to send 😔",
        "extractor-broken" => "There's an issue with this site right now, we'll fix it soon 🔧 Try later.",
        "queue-full" => "The queue is full, try again in a bit 🙏",
        "disk-full" => "The server ran out of space 😅 Try again in a bit.",
        _ => Oops
    };
}
