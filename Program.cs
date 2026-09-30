using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NamazTimes;

public enum P { Tahajjud, Fajr, Sunrise, Duha, Dhuhr, Asr, Maghrib, Isha }

public record City(string Title, string Lat, string Lng);

public class Settings
{
    public City City { get; set; } = new("Алматы", "43.238293", "76.945465");
    public string Lang { get; set; } = L.Default();
    public HashSet<P> Hidden { get; set; } = [];
    public HashSet<P> Alerts { get; set; } = [P.Fajr, P.Dhuhr, P.Asr, P.Maghrib, P.Isha];
    public int RemindBefore { get; set; }
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

    /// All times of a calendar day in order. Tahajjud = start of the last third of the night
    /// that ends at this day's Fajr (previous Maghrib → Fajr).
    public static List<(P P, DateTime At)>? Times(DateOnly date, Func<DateOnly, Day?> getDay)
    {
        if (getDay(date) is not { } d) return null;
        var fajr = At(date, d.Fajr);
        var sunrise = At(date, d.Sunrise);
        var list = new List<(P, DateTime)>();
        if (getDay(date.AddDays(-1)) is { } y && At(date.AddDays(-1), y.Maghrib) is var m)
            list.Add((P.Tahajjud, CeilMinute(m + (fajr - m) * 2 / 3)));
        list.AddRange([
            (P.Fajr, fajr), (P.Sunrise, sunrise), (P.Duha, sunrise + DuhaAfterSunrise),
            (P.Dhuhr, At(date, d.Dhuhr)), (P.Asr, At(date, d.Asr)), (P.Maghrib, At(date, d.Maghrib)), (P.Isha, At(date, d.Isha)),
        ]);
        return list;
    }

    /// First time strictly after `now` among shown prayers, looking into tomorrow if needed.
    public static (P P, DateTime At)? Next(DateTime now, Func<DateOnly, Day?> getDay, Func<P, bool> shown)
    {
        var today = DateOnly.FromDateTime(now);
        foreach (var date in new[] { today, today.AddDays(1) })
            foreach (var x in Times(date, getDay) ?? [])
                if (x.At > now && shown(x.P)) return x;
        return null;
    }

    public static async Task<List<(string Title, City City)>> SearchCities(string q)
    {
        var res = await Http.GetFromJsonAsync<JsonElement>($"cities/?search={Uri.EscapeDataString(q)}");
        return res.GetProperty("results").EnumerateArray().Select(e =>
        {
            var title = e.GetProperty("title").GetString()!;
            var region = e.GetProperty("region").GetString();
            var district = e.TryGetProperty("district", out var dd) ? dd.GetString() : null;
            var label = string.Join(", ", new[] { title, district, region }.Where(s => !string.IsNullOrEmpty(s)));
            return (label, new City(title, e.GetProperty("lat").GetString()!, e.GetProperty("lng").GetString()!));
        }).ToList();
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
        Console.WriteLine("selftest ok");
        return 0;
    }
}
