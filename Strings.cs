using System.Globalization;

namespace NamazTimes;

public static class L
{
    public static string Lang = "ru";

    public static readonly (string Code, string Name)[] Languages = [("kk", "Қазақша"), ("ru", "Русский"), ("en", "English")];

    public static string Default() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch { "kk" => "kk", "en" => "en", _ => "ru" };

    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Lang switch { "kk" => "kk-KZ", "en" => "en-US", _ => "ru-RU" });

    public static string T(string key) =>
        S.TryGetValue(key, out var v) ? Lang switch { "kk" => v.Kk, "en" => v.En, _ => v.Ru } : key;

    public static string Name(P p) => T(p.ToString());

    public static bool Hour12;
    public static string Time(DateTime t) => t.ToString(Hour12 ? "h:mm tt" : "HH:mm", CultureInfo.InvariantCulture);

    static readonly Dictionary<string, (string Kk, string Ru, string En)> S = new()
    {
        [nameof(P.Tahajjud)] = ("Таһажуд", "Тахаджуд", "Tahajjud"),
        [nameof(P.Fajr)] = ("Таң", "Фаджр", "Fajr"),
        [nameof(P.Sunrise)] = ("Күн", "Восход", "Sunrise"),
        [nameof(P.Duha)] = ("Духа", "Духа", "Duha"),
        [nameof(P.Dhuhr)] = ("Бесін", "Зухр", "Dhuhr"),
        [nameof(P.Asr)] = ("Екінті", "Аср", "Asr"),
        [nameof(P.Maghrib)] = ("Ақшам", "Магриб", "Maghrib"),
        [nameof(P.Isha)] = ("Құптан", "Иша", "Isha"),

        // menu / widget / notifications
        ["ShowWidget"] = ("Виджетті көрсету", "Показать виджет", "Show widget"),
        ["Month"] = ("Айлық кесте", "Расписание на месяц", "Monthly timetable"),
        ["Holidays"] = ("Мерекелер", "Исламские праздники", "Islamic holidays"),
        ["Mute"] = ("Хабарламаларды өшіру", "Выключить уведомления", "Mute notifications"),
        ["Settings"] = ("Баптаулар", "Настройки", "Settings"),
        ["Menu"] = ("Мәзір", "Меню", "Menu"),
        ["Tasbih"] = ("Зікір", "Зикр", "Dhikr"),
        ["Names99"] = ("Алланың 99 есімі", "99 имён Аллаха", "99 Names of Allah"),
        ["NameOfDay"] = ("Есім күні", "Имя дня", "Name of the day"),
        ["ShowNameOfDay"] = ("Виджетте есім күнін көрсету", "Показывать имя дня в виджете", "Show name of the day"),
        ["TapOrSpace"] = ("Шерту немесе Бос орын пернесі", "Щелчок или пробел", "Click or press Space"),
        ["TodayCap"] = ("Бүгін", "Сегодня", "Today"),
        ["Reset"] = ("Басынан", "Сбросить", "Reset"),
        ["Search"] = ("Іздеу", "Поиск", "Search"),
        ["CheckUpdates"] = ("Жаңартуды тексеру", "Проверить обновления", "Check for updates"),
        ["UpToDate"] = ("Сізде соңғы нұсқа: {0}", "У вас последняя версия: {0}", "You have the latest version: {0}"),
        ["UpdateAsk"] = ("Жаңа нұсқа шықты: {0}. Қазір жаңарту керек пе?", "Вышла новая версия {0}. Обновить сейчас?", "Version {0} is available. Update now?"),
        ["UpdateCheckFailed"] = ("Жаңартуды тексеру мүмкін болмады. Интернетті тексеріңіз.", "Не удалось проверить обновления. Проверьте интернет.", "Couldn't check for updates. Check your connection."),
        ["DevBuild"] = ("Бұл әзірлеу нұсқасы: жаңартулар тексерілмейді.", "Это сборка для разработки: обновления не проверяются.", "This is a development build: updates aren't checked."),
        ["Prev"] = ("Алдыңғы", "Предыдущее", "Previous"),
        ["Next"] = ("Келесі", "Следующее", "Next"),
        ["PrayerTypes"] = ("Намаз түрлері", "Виды намазов", "Types of prayers"),
        ["PrayerTypesNote"] = ("Мәтіндер жалпы түсінік үшін берілген; толық үкімдерді имамнан немесе muftyat.kz сайтынан нақтылаңыз.", "Тексты даны для общего понимания; подробные правила уточняйте у имама или на muftyat.kz.", "These texts are an overview; check the detailed rulings with an imam or at muftyat.kz."),
        ["TimeLbl"] = ("Уақыты", "Время", "Time"),
        ["Rakats"] = ("Ракағат саны", "Количество ракаатов", "Rak'ahs"),
        ["HowToPray"] = ("Қалай оқылады", "Как совершать", "How to pray"),
        ["Ayah"] = ("Аят", "Аят", "Quran"),
        ["Hadith"] = ("Хадис", "Хадис", "Hadith"),
        ["Qada"] = ("Қаза намаздар", "Пропущенные намазы (каза)", "Missed prayers (qada)"),
        ["QadaMadeUp"] = ("Өтедім", "Восполнил", "Made up"),
        ["QadaTotal"] = ("Барлығы қалды: {0}", "Всего осталось: {0}", "Left in total: {0}"),
        ["QadaAddPeriod"] = ("Кезең бойынша қосу", "Добавить за период", "Add for a period"),
        ["QadaAddAll"] = ("Әр намазға қосу", "Добавить к каждому", "Add to each"),
        ["QadaConfirm"] = ("Әр намазға {0} қаза қосылсын ба?", "Добавить по {0} к каждому намазу?", "Add {0} to each prayer?"),
        ["QadaHint"] = ("«+» — өткізіп алған намазды қосу, «Өтедім» — қазасын оқығанда азайту. Кезеңмен қосқанда әр намазға (үтірмен бірге) күн санынша қосылады; 1 ай = 30 күн, 1 жыл = 365 күн. Үтір Ханафи мазхабы бойынша уәжіп, оның да қазасы оқылады. Ораза бөлек саналады: өткізілген әр күнге бір күн ораза ұсталады.", "«+» — добавить пропущенный намаз, «Восполнил» — уменьшить после восполнения. При добавлении за период к каждому намазу (и витру) добавляется число дней; 1 месяц = 30 дней, 1 год = 365 дней. Витр по ханафитскому мазхабу — ваджиб, его тоже восполняют. Пост считается отдельно: за каждый пропущенный день держат один день поста.", "\"+\" adds a missed prayer, \"Made up\" lowers the count after you make it up. Adding a period adds its number of days to each prayer (and Witr); 1 month = 30 days, 1 year = 365 days. In the Hanafi madhab Witr is wajib and is also made up. Fasting is counted separately: one day is fasted for each day missed."),
        ["QadaFast"] = ("Ораза қазасы", "Пропущенные дни поста", "Missed fasting days"),
        ["QadaFastDays"] = ("Ораза күндері", "Дни поста", "Fasting days"),
        ["Witr"] = ("Үтір", "Витр", "Witr"),
        ["UnitDays"] = ("күн", "дней", "days"),
        ["UnitMonths"] = ("ай", "месяцев", "months"),
        ["UnitYears"] = ("жыл", "лет", "years"),
        ["Exit"] = ("Шығу", "Выход", "Exit"),
        ["Update"] = ("Жаңарту: {0}", "Обновить до {0}", "Update to {0}"),
        ["UpdateAvail"] = ("Жаңа нұсқа шықты: {0}. Жаңарту үшін басыңыз.", "Вышла новая версия {0}. Нажмите, чтобы обновить.", "New version {0} is out. Click to update."),
        ["UpdateBanner"] = ("⬇ Жаңа нұсқа {0} — жаңарту", "⬇ Новая версия {0} — обновить", "⬇ New version {0} — update"),
        ["Downloading"] = ("Жүктелуде… {0}%", "Загрузка… {0}%", "Downloading… {0}%"),
        ["UpdateFailed"] = ("Жаңарту сәтсіз аяқталды. Жүктеу беті ашылады.", "Не удалось обновить. Открываю страницу загрузки.", "Update failed. Opening the download page."),
        ["PrayerTime"] = ("Намаз уақыты", "Время намаза", "Prayer time"),
        ["InMin"] = ("{0} — {1} минуттан кейін", "{0} через {1} мин", "{0} in {1} min"),
        ["NextIn"] = ("{0}: {1} қалды", "{0} через {1}", "{0} in {1}"),
        ["Jumuah"] = ("Жұма намазы", "Джума-намаз", "Jumu'ah prayer"),
        ["NoData"] = ("Деректер жоқ.\nИнтернетті тексеріңіз.", "Нет данных.\nПроверьте интернет.", "No data.\nCheck your connection."),

        // settings
        ["General"] = ("Жалпы", "Общие", "General"),
        ["FastTimes"] = ("Ораза уақыттары", "Время поста", "Fasting times"),
        ["FastAuto"] = ("Рамазанда", "В Рамадан", "In Ramadan"),
        ["FastAlways"] = ("Әрқашан", "Всегда", "Always"),
        ["FastOff"] = ("Жоқ", "Нет", "Off"),
        ["WindowScale"] = ("Терезелер масштабы", "Масштаб окон", "Window scale"),
        ["TimeFormat"] = ("Уақыт форматы", "Формат времени", "Time format"),
        ["H24"] = ("24 сағат", "24 часа", "24-hour"),
        ["H12"] = ("12 сағат", "12 часов", "12-hour"),
        ["Language"] = ("Тіл", "Язык", "Language"),
        ["City"] = ("Елді мекен", "Населённый пункт", "Location"),
        ["AsrMethod"] = ("Екінті уақыты", "Время Асра", "Asr time"),
        ["Hanafi"] = ("Ханафи", "Ханафи", "Hanafi"),
        ["OtherMadhabs"] = ("Басқа мазхабтар", "Другие мазхабы", "Other madhabs"),
        ["HijriAdjust"] = ("Хижра күнін түзету", "Поправка даты хиджры", "Hijri date adjustment"),
        ["Prayers"] = ("Намаз уақыттары", "Времена намазов", "Prayer times"),
        ["Show"] = ("Көрсету", "Показ", "Show"),
        ["Notify"] = ("Хабар", "Уведомл.", "Notify"),
        ["Offset"] = ("Түзету, мин", "Поправка, мин", "Adjust, min"),
        ["Notifications"] = ("Хабарламалар", "Уведомления", "Notifications"),
        ["TestAlert"] = ("Хабарламаны тексеру", "Проверить уведомление", "Test notification"),
        ["Suhoor"] = ("Сәресі", "Сухур", "Suhoor"),
        ["Iftar"] = ("Ауызашар", "Ифтар", "Iftar"),
        ["UntilSuhoor"] = ("Сәресі бітуіне", "До конца сухура", "Suhoor ends in"),
        ["UntilIftar"] = ("Ауызашарға", "До ифтара", "Iftar in"),
        ["NotifyOn"] = ("Хабарламалар қосулы", "Уведомления включены", "Notifications on"),
        ["RemindBefore"] = ("Алдын ала еске салу", "Напомнить заранее", "Remind before"),
        ["JumuahRemind"] = ("Жұма күні бесін алдында еске салу", "Джума: напомнить в пятницу до Зухра", "Friday: remind before Dhuhr"),
        ["Widget"] = ("Виджет", "Виджет", "Widget"),
        ["Size"] = ("Өлшемі", "Размер", "Size"),
        ["Opacity"] = ("Көрінуі", "Непрозрачность", "Opacity"),
        ["TopMost"] = ("Барлық терезелердің үстінде", "Поверх всех окон", "Always on top"),
        ["AutoStart"] = ("Windows-пен бірге іске қосу", "Запускать с Windows", "Start with Windows"),
        ["Save"] = ("Сақтау", "Сохранить", "Save"),
        ["Cancel"] = ("Болдырмау", "Отмена", "Cancel"),
        ["Off"] = ("жоқ", "нет", "off"),
        ["Min"] = ("{0} мин", "{0} мин", "{0} min"),
        ["Days"] = ("{0} күн", "{0} дн.", "{0} d"),
        ["ResizeHint"] = ("Өлшемді өзгерту: бұрышын тартыңыз немесе Ctrl + дөңгелек", "Размер: тяните за угол виджета или Ctrl + колесо", "Resize: drag the widget corner or Ctrl + wheel"),

        // city picker
        ["ChooseCity"] = ("Елді мекенді таңдау", "Выбор населённого пункта", "Choose location"),
        ["SearchHint"] = ("Іздеу: қала немесе ауыл атауы", "Поиск: город или село", "Search: city or village"),
        ["Choose"] = ("Таңдау", "Выбрать", "Choose"),

        // month / holidays
        ["Date"] = ("Күні", "Дата", "Date"),
        ["HijriCol"] = ("Хижра", "Хиджра", "Hijri"),
        ["Holiday"] = ("Мереке", "Праздник", "Holiday"),
        ["Left"] = ("Қалды", "Осталось", "In"),
        ["Today"] = ("бүгін", "сегодня", "today"),
        ["Evening"] = ("кешке басталады", "начинается вечером", "starts in the evening"),
        ["HolidayNote"] = ("Күндер Умм әл-Құра күнтізбесі бойынша есептелген; ДҰМ жариялаған күн 1 күнге өзгеше болуы мүмкін.",
            "Даты рассчитаны по календарю Умм аль-Кура; официальные даты ДУМК могут отличаться на день.",
            "Dates follow the Umm al-Qura calendar; official ДУМК dates may differ by a day."),

        ["NewYear"] = ("Хижра жаңа жылы", "Исламский Новый год", "Islamic New Year"),
        ["Ashura"] = ("Ашура күні", "День Ашура", "Day of Ashura"),
        ["Mawlid"] = ("Мәуліт", "Мавлид", "Mawlid"),
        ["Raghaib"] = ("Рағайып түні", "Ночь Рагаиб", "Night of Raghaib"),
        ["Miraj"] = ("Миғраж түні", "Ночь Мирадж", "Night of Miraj"),
        ["Barat"] = ("Бараат түні", "Ночь Бараат", "Night of Barat"),
        ["Ramadan"] = ("Рамазан айының басталуы", "Начало Рамадана", "Start of Ramadan"),
        ["Qadr"] = ("Қадір түні", "Ночь Кадр", "Laylat al-Qadr"),
        ["FitrEid"] = ("Ораза айт", "Ураза-байрам", "Eid al-Fitr"),
        ["Arafa"] = ("Арафа күні", "День Арафа", "Day of Arafah"),
        ["AdhaEid"] = ("Құрбан айт", "Курбан-байрам", "Eid al-Adha"),

        ["HM1"] = ("Мұхаррам", "Мухаррам", "Muharram"),
        ["HM2"] = ("Сафар", "Сафар", "Safar"),
        ["HM3"] = ("Рабиғул-әууәл", "Раби аль-авваль", "Rabi al-Awwal"),
        ["HM4"] = ("Рабиғус-сәни", "Раби ас-сани", "Rabi al-Thani"),
        ["HM5"] = ("Жумадәл-әууәл", "Джумада аль-уля", "Jumada al-Ula"),
        ["HM6"] = ("Жумадәс-сәни", "Джумада ас-сани", "Jumada al-Thani"),
        ["HM7"] = ("Ражаб", "Раджаб", "Rajab"),
        ["HM8"] = ("Шағбан", "Шаабан", "Shaban"),
        ["HM9"] = ("Рамазан", "Рамадан", "Ramadan"),
        ["HM10"] = ("Шәууәл", "Шавваль", "Shawwal"),
        ["HM11"] = ("Зұлқағда", "Зуль-када", "Dhu al-Qadah"),
        ["HM12"] = ("Зұлхижжа", "Зуль-хиджа", "Dhu al-Hijjah"),
    };
}
