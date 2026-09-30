using System.Globalization;

namespace NamazTimes;

/// Hijri dates via Umm al-Qura. ДУМК sometimes announces months a day apart — Settings.HijriAdjust (±days) covers that.
public static class Hijri
{
    static readonly UmAlQuraCalendar Cal = new();

    public static (int Y, int M, int D) Of(DateOnly date, int adjust)
    {
        var dt = date.ToDateTime(default).AddDays(adjust);
        return (Cal.GetYear(dt), Cal.GetMonth(dt), Cal.GetDayOfMonth(dt));
    }

    public static DateOnly ToGregorian(int y, int m, int d, int adjust) =>
        DateOnly.FromDateTime(Cal.ToDateTime(y, m, d, 0, 0, 0, 0)).AddDays(-adjust);

    public static string Format(DateOnly date, int adjust)
    {
        var (y, m, d) = Of(date, adjust);
        return $"{d} {L.T("HM" + m)} {y}";
    }

    /// Night holidays are dated by the evening that begins the night.
    public record Holiday(string Key, DateOnly Date, bool Night);

    public static List<Holiday> Year(int hy, int adj)
    {
        DateOnly G(int m, int d) => ToGregorian(hy, m, d, adj);
        var rajab1 = G(7, 1);
        var firstFriday = rajab1.AddDays(((int)DayOfWeek.Friday - (int)rajab1.DayOfWeek + 7) % 7);
        return
        [
            new("NewYear", G(1, 1), false),
            new("Ashura", G(1, 10), false),
            new("Mawlid", G(3, 12), false),
            new("Raghaib", firstFriday.AddDays(-1), true),
            new("Miraj", G(7, 27).AddDays(-1), true),
            new("Barat", G(8, 15).AddDays(-1), true),
            new("Ramadan", G(9, 1), false),
            new("Qadr", G(9, 27).AddDays(-1), true),
            new("FitrEid", G(10, 1), false),
            new("Arafa", G(12, 9), false),
            new("AdhaEid", G(12, 10), false),
        ];
    }

    /// Holidays from `from` onward, spanning about a year.
    public static IEnumerable<Holiday> Upcoming(DateOnly from, int adj)
    {
        var hy = Of(from, adj).Y;
        return Year(hy, adj).Concat(Year(hy + 1, adj)).Where(h => h.Date >= from && h.Date < from.AddDays(370));
    }

    public static Holiday? On(DateOnly date, int adj) => Upcoming(date, adj).FirstOrDefault(h => h.Date == date);

    /// Why this day is a recommended (sunnah) fast, as a string key, or null. Never in Ramadan (already obligatory)
    /// or on the Eid days, when fasting is forbidden. Most specific reason first.
    public static string? SunnahFast(DateOnly date, int adj)
    {
        var (_, m, d) = Of(date, adj);
        if (m == 9 || (m == 10 && d == 1) || (m == 12 && d is >= 10 and <= 13)) return null;
        if (m == 12 && d == 9) return "FastArafa";
        if (m == 1 && d is 9 or 10) return "FastAshura";
        if (d is >= 13 and <= 15) return "FastWhiteDays";
        if (m == 10 && d is >= 2 and <= 7) return "FastShawwal";
        return date.DayOfWeek switch { DayOfWeek.Monday => "FastMonday", DayOfWeek.Thursday => "FastThursday", _ => null };
    }
}
