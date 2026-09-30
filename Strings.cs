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
        ["Exit"] = ("Шығу", "Выход", "Exit"),
        ["Update"] = ("Жаңарту: {0}", "Обновить до {0}", "Update to {0}"),
        ["UpdateAvail"] = ("Жаңа нұсқа бар: {0}", "Доступна новая версия {0}", "New version available: {0}"),
        ["PrayerTime"] = ("Намаз уақыты", "Время намаза", "Prayer time"),
        ["InMin"] = ("{0} — {1} минуттан кейін", "{0} через {1} мин", "{0} in {1} min"),
        ["NextIn"] = ("{0}: {1} қалды", "{0} через {1}", "{0} in {1}"),
        ["Jumuah"] = ("Жұма намазы", "Джума-намаз", "Jumu'ah prayer"),
        ["NoData"] = ("Деректер жоқ.\nИнтернетті тексеріңіз.", "Нет данных.\nПроверьте интернет.", "No data.\nCheck your connection."),

        // settings
        ["General"] = ("Жалпы", "Общие", "General"),
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
