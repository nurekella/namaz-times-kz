using System.Globalization;

namespace NamazTimes;

public static class L
{
    public static string Lang = "ru";

    public static readonly (string Code, string Name)[] Languages = [("kk", "Қазақша"), ("ru", "Русский"), ("en", "English")];

    public static string Default() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch { "kk" => "kk", "en" => "en", _ => "ru" };

    public static string T(string key) =>
        S.TryGetValue(key, out var v) ? Lang switch { "kk" => v.Kk, "en" => v.En, _ => v.Ru } : key;

    public static string Name(P p) => T(p.ToString());

    static readonly Dictionary<string, (string Kk, string Ru, string En)> S = new()
    {
        [nameof(P.Tahajjud)] = ("Тәһажжуд", "Тахаджуд", "Tahajjud"),
        [nameof(P.Fajr)] = ("Таң", "Фаджр", "Fajr"),
        [nameof(P.Sunrise)] = ("Күн шығуы", "Восход", "Sunrise"),
        [nameof(P.Duha)] = ("Дұха", "Духа", "Duha"),
        [nameof(P.Dhuhr)] = ("Бесін", "Зухр", "Dhuhr"),
        [nameof(P.Asr)] = ("Екінті", "Аср", "Asr"),
        [nameof(P.Maghrib)] = ("Ақшам", "Магриб", "Maghrib"),
        [nameof(P.Isha)] = ("Құптан", "Иша", "Isha"),

        ["ShowWidget"] = ("Виджетті көрсету", "Показать виджет", "Show widget"),
        ["Settings"] = ("Баптаулар…", "Настройки…", "Settings…"),
        ["Exit"] = ("Шығу", "Выход", "Exit"),
        ["PrayerTime"] = ("Намаз уақыты", "Время намаза", "Prayer time"),
        ["InMin"] = ("{0} — {1} минуттан кейін", "{0} через {1} мин", "{0} in {1} min"),
        ["NextIn"] = ("{0}: {1} қалды", "{0} через {1}", "{0} in {1}"),
        ["NoData"] = ("Деректер жоқ.\nИнтернетті тексеріңіз.", "Нет данных.\nПроверьте интернет.", "No data.\nCheck your connection."),

        ["Language"] = ("Тіл", "Язык", "Language"),
        ["City"] = ("Елді мекен", "Населённый пункт", "Location"),
        ["Change"] = ("Өзгерту…", "Изменить…", "Change…"),
        ["Prayer"] = ("Намаз", "Намаз", "Prayer"),
        ["Show"] = ("Көрсету", "Показывать", "Show"),
        ["Notify"] = ("Хабарлау", "Уведомлять", "Notify"),
        ["RemindBefore"] = ("Алдын ала еске салу, мин (0 — жоқ)", "Напомнить заранее, мин (0 — нет)", "Remind before, min (0 — off)"),
        ["Opacity"] = ("Виджеттің көрінуі, %", "Непрозрачность виджета, %", "Widget opacity, %"),
        ["TopMost"] = ("Барлық терезелердің үстінде", "Поверх всех окон", "Always on top"),
        ["AutoStart"] = ("Windows-пен бірге іске қосу", "Запускать с Windows", "Start with Windows"),
        ["Save"] = ("Сақтау", "Сохранить", "Save"),
        ["Cancel"] = ("Болдырмау", "Отмена", "Cancel"),

        ["ChooseCity"] = ("Елді мекенді таңдау", "Выбор населённого пункта", "Choose location"),
        ["Search"] = ("Іздеу", "Найти", "Search"),
        ["SearchHint"] = ("Қала немесе ауыл атауы", "Название города или села", "City or village name"),
        ["NotFound"] = ("Ештеңе табылмады", "Ничего не найдено", "Nothing found"),
        ["NetError"] = ("api.muftyat.kz қосылу мүмкін болмады", "Не удалось связаться с api.muftyat.kz", "Could not reach api.muftyat.kz"),
    };
}
