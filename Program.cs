using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NamazTimes;

public enum P { Tahajjud, Fajr, Sunrise, Duha, Dhuhr, Asr, Maghrib, Isha }

public record City(string Title, string Lat, string Lng);

public class Settings
{
    public City City { get; set; } = new("Алматы қаласы", "43.238293", "76.945465");
    public string Lang { get; set; } = L.Default();
    public bool Hanafi { get; set; } = true;
    public Dictionary<P, int> Offsets { get; set; } = [];
    public int HijriAdjust { get; set; }
    public HashSet<P> Hidden { get; set; } = [];
    public HashSet<P> Alerts { get; set; } = [P.Fajr, P.Dhuhr, P.Asr, P.Maghrib, P.Isha];
    public bool Muted { get; set; }
    public int RemindBefore { get; set; }
    public bool Jumuah { get; set; } = true;
    public int JumuahBefore { get; set; } = 60;
    public int Zoom { get; set; } = 100;
    public int Opacity { get; set; } = 100;
    public int X { get; set; } = -1;
    public int Y { get; set; } = -1;
    public bool TopMost { get; set; } = true;
    public bool WidgetVisible { get; set; } = true;
}

public class Day
{
    public string Date { get; set; } = "";
    public string Fajr { get; set; } = "";
    public string Sunrise { get; set; } = "";
    public string Dhuhr { get; set; } = "";
    public string Asr { get; set; } = "";
    public string Maghrib { get; set; } = "";
    public string Isha { get; set; } = "";
}

public static class Data
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NamazTimes");
    static readonly string SettingsFile = Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() },
    };
    static readonly HttpClient Http = new() { BaseAddress = new("https://api.muftyat.kz/"), Timeout = TimeSpan.FromSeconds(10) };
    // ponytail: sync fetch on the UI thread, once per city+year (cached to disk); go async if the startup freeze bothers anyone.

    // Duha starts once the sun is a spear's length up; 20 min after sunrise is the common KZ convention.
    static readonly TimeSpan DuhaAfterSunrise = TimeSpan.FromMinutes(20);

    public static Settings LoadSettings()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsFile), Json) ?? new(); }
        catch { return new(); }
    }

    public static void Save(Settings s)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(s, Json));
    }

    static readonly Dictionary<string, Dictionary<string, Day>> Years = [];
    static DateTime lastFail;

    // Times from ДУМК are local wall-clock for the city (UTC+5 for all KZ since 2024).
    // ponytail: assumes the PC clock is in the city's timezone; convert via City timezone if users abroad need it.
    public static Day? GetDay(City c, DateOnly date)
    {
        var key = $"{date.Year}_{c.Lat}_{c.Lng}";
        if (!Years.TryGetValue(key, out var year))
        {
            var file = Path.Combine(Dir, key + ".json");
            try
            {
                if (!File.Exists(file))
                {
                    if (DateTime.Now - lastFail < TimeSpan.FromMinutes(1)) return null;
                    var json = Http.GetStringAsync($"prayer-times/{date.Year}/{c.Lat}/{c.Lng}").GetAwaiter().GetResult();
                    using var _ = JsonDocument.Parse(json); // validate before caching
                    Directory.CreateDirectory(Dir);
                    File.WriteAllText(file, json);
                }
                var days = JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("result")
                    .Deserialize<List<Day>>(Json)!;
                Years[key] = year = days.ToDictionary(d => d.Date);
            }
            catch { lastFail = DateTime.Now; return null; }
        }
        return year.GetValueOrDefault(date.ToString("yyyy-MM-dd"));
    }

    static DateTime At(DateOnly date, string hhmm) => date.ToDateTime(TimeOnly.Parse(hhmm));

    static DateTime CeilMinute(DateTime t) => new((t.Ticks + TimeSpan.TicksPerMinute - 1) / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute);

    /// ДУМК publishes Hanafi Asr (shadow = 2× object + noon shadow). Other madhabs use 1×.
    /// We subtract the astronomical gap between the two so ДУМК's own precautionary minutes are kept.
    public static TimeSpan AsrGap(DateOnly date, double latDeg)
    {
        static double R(double deg) => deg * Math.PI / 180;
        var n = (date.ToDateTime(new TimeOnly(7, 0)) - new DateTime(2000, 1, 1, 12, 0, 0)).TotalDays; // ≈ local noon, UTC+5
        var g = R(357.529 + 0.98560028 * n);
        var lon = R(280.459 + 0.98564736 * n + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g));
        var decl = Math.Asin(Math.Sin(R(23.439 - 0.00000036 * n)) * Math.Sin(lon));
        var phi = R(latDeg);
        double HourAngle(double shadow)
        {
            var alt = Math.Atan(1 / (shadow + Math.Tan(Math.Abs(phi - decl))));
            return Math.Acos((Math.Sin(alt) - Math.Sin(phi) * Math.Sin(decl)) / (Math.Cos(phi) * Math.Cos(decl)));
        }
        return TimeSpan.FromHours((HourAngle(2) - HourAngle(1)) * 180 / Math.PI / 15);
    }

    /// All times of a calendar day in order, with madhab and manual offsets applied.
    /// Tahajjud = start of the last third of the night that ends at this day's Fajr.
    public static List<(P P, DateTime At)>? Times(DateOnly date, Func<DateOnly, Day?> getDay, Settings? s = null)
    {
        if (getDay(date) is not { } d) return null;
        TimeSpan Off(P p) => TimeSpan.FromMinutes(s?.Offsets.GetValueOrDefault(p) ?? 0);
        var fajr = At(date, d.Fajr) + Off(P.Fajr);
        var sunrise = At(date, d.Sunrise) + Off(P.Sunrise);
        var asr = At(date, d.Asr) + Off(P.Asr);
        if (s is { Hanafi: false } && double.TryParse(s.City.Lat, System.Globalization.CultureInfo.InvariantCulture, out var lat))
            asr = CeilMinute(asr - AsrGap(date, lat));
        var list = new List<(P, DateTime)>();
        if (getDay(date.AddDays(-1)) is { } y && At(date.AddDays(-1), y.Maghrib) + Off(P.Maghrib) is var m)
            list.Add((P.Tahajjud, CeilMinute(m + (fajr - m) * 2 / 3) + Off(P.Tahajjud)));
        list.AddRange([
            (P.Fajr, fajr), (P.Sunrise, sunrise), (P.Duha, sunrise + DuhaAfterSunrise + Off(P.Duha)),
            (P.Dhuhr, At(date, d.Dhuhr) + Off(P.Dhuhr)), (P.Asr, asr),
            (P.Maghrib, At(date, d.Maghrib) + Off(P.Maghrib)), (P.Isha, At(date, d.Isha) + Off(P.Isha)),
        ]);
        return list;
    }

    /// First time strictly after `now` among shown prayers, looking into tomorrow if needed.
    public static (P P, DateTime At)? Next(DateTime now, Func<DateOnly, Day?> getDay, Func<P, bool> shown, Settings? s = null)
    {
        var today = DateOnly.FromDateTime(now);
        foreach (var date in new[] { today, today.AddDays(1) })
            foreach (var x in Times(date, getDay, s) ?? [])
                if (x.At > now && shown(x.P)) return x;
        return null;
    }

    /// Snapshot of api.muftyat.kz/cities (5694 places), embedded so the list works offline and instantly.
    /// Rows: [title, region, district, lat, lng]. Regenerate with tools/cities.mjs.
    public static readonly Lazy<List<string[]>> Cities = new(() =>
        JsonSerializer.Deserialize<List<string[]>>(typeof(Data).Assembly.GetManifestResourceStream("cities.json")!)!);

    /// Lowercase + fold Kazakh letters to Russian ones so "караганда" finds "Қарағанды".
    public static string Fold(string s)
    {
        var chars = s.ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            chars[i] = chars[i] switch
            {
                'қ' => 'к', 'ғ' => 'г', 'ә' => 'а', 'ө' => 'о', 'ү' or 'ұ' => 'у', 'һ' => 'х', 'і' => 'и', 'ң' => 'н', 'ё' => 'е',
                var c => c,
            };
        return new string(chars);
    }
}

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--selftest")) return SelfTest();

        using var mutex = new Mutex(true, "NamazTimes.SingleInstance", out var first);
        if (!first) return 0;

        ApplicationConfiguration.Initialize();
        Application.Run(new Widget(Data.LoadSettings()));
        return 0;
    }

    static int SelfTest()
    {
        var d = new Day { Fajr = "05:00", Sunrise = "06:30", Dhuhr = "12:00", Asr = "15:00", Maghrib = "18:00", Isha = "19:30" };
        Day? Get(DateOnly _) => d;
        bool All(P _) => true;
        var day = new DateOnly(2026, 12, 31);
        DateTime T(int h, int m, int plusDays = 0) => day.AddDays(plusDays).ToDateTime(new(h, m));

        var times = Data.Times(day, Get)!;
        Trace.Assert(times[0] == (P.Tahajjud, T(1, 20)), "18:00→05:00 is 11h, last third starts at 01:20");
        Trace.Assert(times[3] == (P.Duha, T(6, 50)));
        Trace.Assert(Data.Next(T(0, 0), Get, All) == (P.Tahajjud, T(1, 20)));
        Trace.Assert(Data.Next(T(12, 0), Get, All) == (P.Asr, T(15, 0)), "exactly at time -> next one");
        Trace.Assert(Data.Next(T(20, 0), Get, All) == (P.Tahajjud, T(1, 20, 1)), "rolls to tomorrow");
        Trace.Assert(Data.Next(T(20, 0), Get, p => p != P.Tahajjud) == (P.Fajr, T(5, 0, 1)), "hidden skipped");
        Trace.Assert(Data.Next(T(20, 0), _ => null, All) == null);

        var s = new Settings { Offsets = { [P.Fajr] = 5 } };
        Trace.Assert(Data.Times(day, Get, s)![1] == (P.Fajr, T(5, 5)), "manual offset");

        // Almaty, 30 Sep: Hanafi Asr ≈ 15:48, standard ≈ 15:00 → gap ≈ 48 min.
        var gap = Data.AsrGap(new DateOnly(2026, 9, 30), 43.238).TotalMinutes;
        Trace.Assert(gap is > 44 and < 52, $"asr gap {gap}");
        Trace.Assert(Data.Fold("Қарағанды") == "караганды");

        var h = Hijri.Of(new DateOnly(2026, 3, 20), 0); // Eid al-Fitr 1447 per Umm al-Qura
        Trace.Assert(h == (1447, 10, 1), $"hijri {h}");
        Trace.Assert(Hijri.ToGregorian(1447, 10, 1, 0) == new DateOnly(2026, 3, 20));
        Console.WriteLine("selftest ok");
        return 0;
    }
}
