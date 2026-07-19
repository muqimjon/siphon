namespace Siphon.Bot.I18n;

public sealed class MsgUz : Msg
{
    public override string LangName => "O'zbekcha";
    public override string Welcome => "Salom! 👋 Men internetdan video, musiqa va rasm yuklab beraman.\n\nMenga havola yuboring — masalan YouTube, Instagram yoki TikTokdan. Qolganini o'zim qilaman 😉";
    public override string SendLink => "Havola yuboring — yuklab beraman 😉";
    public override string Probing => "🔎 Havolani tekshiryapman…";
    public override string ChooseType => "Nimani yuklay?";
    public override string ChooseFormat => "Qaysi formatda?";
    public override string ChooseQuality => "Qaysi sifatda?";
    public override string TypeAudio => "Audio";
    public override string TypeVideo => "Video";
    public override string Back => "⬅️ Orqaga";
    public override string Original => "asl";
    public override string FormatBest => "⭐ Eng yaxshi (asl sifat)";
    public override string Queued => "⏳ Navbatda…";
    public override string Downloading => "⬇️ Yuklab olyapman…";
    public override string Converting => "🎛 Tayyorlayapman…";
    public override string Uploading => "📤 Telegramga yuboryapman…";
    public override string Busy => "Avvalgi yuklash tugashini kutib turing 🙂";
    public override string ServerBusy => "Hozir hamma joy band. Bir-ikki daqiqadan keyin urinib ko'ring 🙏";
    public override string ServerDown => "Server biroz dam olyapti 😴 Keyinroq urinib ko'ring.";
    public override string Oops => "Nimadir xato ketdi 😅 Yana bir bor urinib ko'ring.";
    public override string TimedOut => "Juda uzoq cho'zildi, to'xtatishga to'g'ri keldi 😔 Yana urinib ko'ring.";
    public override string Expired => "Bu tugmaning vaqti o'tib ketibdi. Havolani qaytadan yuboring 🙂";
    public override string SubscribeFirst => "Botdan foydalanish uchun avval kanalimizga obuna bo'ling 🙂";
    public override string JoinChannel => "➕ Kanalga o'tish";
    public override string CheckSub => "✅ Tekshirish";
    public override string SubOk => "Rahmat! Endi bemalol foydalanavering 🎉";
    public override string SubNotYet => "Hali obuna bo'lmagansiz. Avval kanalga qo'shiling 🙂";
    public override string TooLarge(int mb) => $"Bu fayl {mb} MB dan katta. Telegram botlarga bundan kattasini yuborishga ruxsat bermaydi 😔 Kichikroq variantni tanlang.\n\n{AdminUpsell}";
    public override string AllTooLarge(int mb) => $"Afsus, bu yerdagi hamma variant {mb} MB dan katta ekan — Telegram botlarga bundan kattasini yuborishga ruxsat bermaydi 😔\n\n{AdminUpsell}";
    public override string FinalTooLarge(int mb) => $"Fayl kutilganidan katta chiqib qoldi va {mb} MB limitga sig'madi 😔 Kichikroq variantni tanlab ko'ring.\n\n{AdminUpsell}";
    public override string DailyLimit(int n) => $"Bugungi limit tugadi ({n} ta yuklash). Ertaga yana keling! 🌙\n\n{AdminUpsell}";
    public override string Images(int n) => $"Rasmlar ({n})";
    public override string AdminUpsell => "Ko'proq hajm uchun admin bilan bog'laning.";
    public override string SettingsIntro => "⚙️ Sozlamalar\n\nQaysi platforma uchun standartni belgilaymiz?";
    public override string SettingsHint => "Havola kelganda shu sozlamalar avtomatik qo'llanadi:";
    public override string PrefKind => "Tur";
    public override string PrefAudioFormat => "Audio format";
    public override string PrefVideoFormat => "Video format";
    public override string PrefQuality => "Sifat";
    public override string KindAsk => "Har safar so'ra";
    public override string OptAsk => "So'ra";
    public override string QualHigh => "Yuqori";
    public override string QualMedium => "O'rta";
    public override string QualLow => "Past";
    public override string OtherPlatform => "Boshqa";
    public override string GroupAdded => "Salom! 👋 Endi bu guruhga video, musiqa yoki rasm havolasini tashlang — o'zim avtomatik yuklab beraman.\n\nStandart sozlamalarni o'zgartirish uchun /settings.";
    public override string GroupSettingsHint => "🔧 Bu sozlamalar shu guruh uchun standart hisoblanadi.";
    public override string GroupSettingsDenied => "Faqat guruh sozlamalarini qo'shgan odam yoki admin o'zgartira oladi.";
    public override string ErrorFor(string code) => code switch
    {
        "invalid-url" => "Bu havolaga o'xshamadi 🤔 To'g'ri havola yuborib ko'ring.",
        "unsupported-site" => "Bu saytni hali o'rganmaganman 😔 Boshqa havola yuborib ko'ring.",
        "unavailable" => "Bu kontent topilmadi — o'chirilgan yoki yopiq bo'lsa kerak 😔",
        "geo-blocked" => "Bu kontent bizning hudud uchun yopiq ekan 😔",
        "age-restricted" => "Bunda yosh cheklovi bor ekan, yuklab bo'lmadi 😔",
        "login-required" => "Buni ochish uchun sayt login so'rayapti, men esa kira olmayman 😔",
        "live-not-supported" => "Jonli efirni yuklab bo'lmaydi. Efir tugagach urinib ko'ring 🙂",
        "playlist-not-supported" => "Butun pleylistni emas, bitta videoning havolasini yuboring 🙂",
        "too-large" => "Bu fayl juda katta ekan, yuklab bera olmayman 😔",
        "extractor-broken" => "Bu sayt bilan hozircha muammo bor, tez orada tuzatiladi 🔧 Keyinroq urinib ko'ring.",
        "queue-full" => "Hozir navbat to'la, birozdan keyin urinib ko'ring 🙏",
        "disk-full" => "Serverda joy qolmabdi 😅 Birozdan keyin urinib ko'ring.",
        _ => Oops
    };
}
