namespace Siphon.Bot.I18n;

public sealed class MsgRu : Msg
{
    public override string LangName => "Русский";
    public override string Welcome => "Привет! 👋 Я скачиваю видео, музыку и картинки из интернета.\n\nПришлите мне ссылку — например, с YouTube, Instagram или TikTok. Остальное сделаю сам 😉";
    public override string SendLink => "Пришлите ссылку — скачаю 😉";
    public override string Probing => "🔎 Проверяю ссылку…";
    public override string ChooseType => "Что скачать?";
    public override string ChooseFormat => "В каком формате?";
    public override string ChooseQuality => "В каком качестве?";
    public override string TypeAudio => "Аудио";
    public override string TypeVideo => "Видео";
    public override string Back => "⬅️ Назад";
    public override string Original => "оригинал";
    public override string Queued => "⏳ В очереди…";
    public override string Downloading => "⬇️ Скачиваю…";
    public override string Converting => "🎛 Обрабатываю…";
    public override string Uploading => "📤 Отправляю в Telegram…";
    public override string Busy => "Дождитесь окончания текущей загрузки 🙂";
    public override string ServerBusy => "Сейчас всё занято. Попробуйте через пару минут 🙏";
    public override string ServerDown => "Сервер немного отдыхает 😴 Попробуйте позже.";
    public override string Oops => "Что-то пошло не так 😅 Попробуйте ещё раз.";
    public override string TimedOut => "Слишком долго, пришлось остановить 😔 Попробуйте ещё раз.";
    public override string Expired => "Кнопка устарела. Пришлите ссылку заново 🙂";
    public override string SubscribeFirst => "Чтобы пользоваться ботом, сначала подпишитесь на наш канал 🙂";
    public override string JoinChannel => "➕ Перейти в канал";
    public override string CheckSub => "✅ Проверить";
    public override string SubOk => "Спасибо! Пользуйтесь на здоровье 🎉";
    public override string SubNotYet => "Вы ещё не подписались. Сначала вступите в канал 🙂";
    public override string TooLarge(int mb) => $"Этот файл больше {mb} МБ. Telegram не разрешает ботам отправлять файлы крупнее 😔 Выберите вариант поменьше.\n\n{AdminUpsell}";
    public override string AllTooLarge(int mb) => $"Увы, все варианты больше {mb} МБ — Telegram не разрешает ботам отправлять такие файлы 😔\n\n{AdminUpsell}";
    public override string FinalTooLarge(int mb) => $"Файл оказался больше, чем ожидалось, и не влез в лимит {mb} МБ 😔 Попробуйте вариант поменьше.\n\n{AdminUpsell}";
    public override string DailyLimit(int n) => $"Дневной лимит исчерпан ({n} загрузок). Возвращайтесь завтра! 🌙\n\n{AdminUpsell}";
    public override string Images(int n) => $"Картинки ({n})";
    public override string AdminUpsell => "Для файлов побольше свяжитесь с админом.";
    public override string SettingsIntro => "⚙️ Настройки\n\nДля какой платформы зададим настройки по умолчанию?";
    public override string SettingsHint => "Эти настройки применяются автоматически, когда приходит ссылка:";
    public override string PrefKind => "Тип";
    public override string PrefAudioFormat => "Аудио формат";
    public override string PrefVideoFormat => "Видео формат";
    public override string PrefQuality => "Качество";
    public override string KindAsk => "Спрашивать";
    public override string OptAsk => "Спросить";
    public override string QualHigh => "Высокое";
    public override string QualMedium => "Среднее";
    public override string QualLow => "Низкое";
    public override string OtherPlatform => "Другое";
    public override string ErrorFor(string code) => code switch
    {
        "invalid-url" => "Это не похоже на ссылку 🤔 Пришлите правильную ссылку.",
        "unsupported-site" => "Этот сайт я пока не знаю 😔 Попробуйте другую ссылку.",
        "unavailable" => "Контент не найден — похоже, он удалён или закрыт 😔",
        "geo-blocked" => "Этот контент недоступен в нашем регионе 😔",
        "age-restricted" => "Тут возрастное ограничение, скачать не получилось 😔",
        "login-required" => "Сайт требует вход в аккаунт, а я войти не могу 😔",
        "live-not-supported" => "Прямые эфиры скачать нельзя. Попробуйте после окончания эфира 🙂",
        "playlist-not-supported" => "Пришлите ссылку на одно видео, а не на весь плейлист 🙂",
        "too-large" => "Файл слишком большой, не смогу его отправить 😔",
        "extractor-broken" => "С этим сайтом сейчас проблема, скоро починим 🔧 Попробуйте позже.",
        "queue-full" => "Очередь переполнена, попробуйте чуть позже 🙏",
        "disk-full" => "На сервере закончилось место 😅 Попробуйте чуть позже.",
        _ => Oops
    };
}
