using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace NamazTimes;

public record City(string Title, string Lat, string Lng);

public class Settings
{
    public City City { get; set; } = new("Алматы", "43.238293", "76.945465");
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
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    static readonly HttpClient Http = new() { BaseAddress = new("https://api.muftyat.kz/"), Timeout = TimeSpan.FromSeconds(10) };
    // ponytail: sync fetch on the UI thread, once per city+year (cached to disk); go async if the startup freeze bothers anyone.

    // Name, getter, alert?
    public static readonly (string Name, Func<Day, string> Get, bool Alert)[] Prayers =
    [
        ("Таң (Фаджр)", d => d.Fajr, true),
        ("Күн (Восход)", d => d.Sunrise, false),
        ("Бесін (Зухр)", d => d.Dhuhr, true),
        ("Екінті (Аср)", d => d.Asr, true),
        ("Ақшам (Магриб)", d => d.Maghrib, true),
        ("Құптан (Иша)", d => d.Isha, true),
    ];

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

    public static DateTime At(DateOnly date, string hhmm) => date.ToDateTime(TimeOnly.Parse(hhmm));

    /// Next prayer (index into Prayers) strictly after `now`; rolls over to tomorrow's Fajr.
    public static (int Index, DateTime At)? Next(DateTime now, Func<DateOnly, Day?> getDay)
    {
        var today = DateOnly.FromDateTime(now);
        if (getDay(today) is { } d)
            for (int i = 0; i < Prayers.Length; i++)
                if (At(today, Prayers[i].Get(d)) is var t && t > now) return (i, t);
        return getDay(today.AddDays(1)) is { } n ? (0, At(today.AddDays(1), n.Fajr)) : null;
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

public class Widget : Form
{
    readonly Settings s;
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    readonly ToolStripMenuItem topMostItem, autoStartItem, showItem;
    DateTime lastTick = DateTime.Now;
    Day? today;
    (int Index, DateTime At)? next;

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public Widget(Settings settings)
    {
        s = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 28, 34);
        TopMost = s.TopMost;
        Text = "Намаз";
        Icon = LoadIcon();

        var menu = new ContextMenuStrip();
        showItem = new ToolStripMenuItem("Показать виджет", null, (_, _) => SetWidgetVisible(!Visible)) { CheckOnClick = false };
        topMostItem = new ToolStripMenuItem("Поверх всех окон", null, (_, _) => { TopMost = s.TopMost = !s.TopMost; Data.Save(s); }) { Checked = s.TopMost };
        autoStartItem = new ToolStripMenuItem("Запускать с Windows", null, (_, _) => ToggleAutoStart()) { Checked = AutoStart };
        menu.Items.AddRange([
            showItem,
            new ToolStripMenuItem("Выбрать город…", null, (_, _) => PickCity()),
            topMostItem,
            autoStartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Выход", null, (_, _) => { tray!.Visible = false; Application.Exit(); }),
        ]);
        menu.Opening += (_, _) => { showItem.Checked = Visible; topMostItem.Checked = s.TopMost; autoStartItem.Checked = AutoStart; };
        ContextMenuStrip = menu;

        tray = new NotifyIcon { Icon = Icon, Text = "Время намаза", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) SetWidgetVisible(!Visible); };

        timer.Tick += (_, _) => Tick();
        timer.Start();
        Tick();
    }

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(value && s.WidgetVisible);

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ClientSize = new Size(Scale(230), Scale(200));
        var wa = Screen.PrimaryScreen!.WorkingArea;
        var pos = new Point(s.X, s.Y);
        Location = Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(pos)) ? pos
            : new Point(wa.Right - Width - Scale(16), wa.Top + Scale(16));
        Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Scale(14), Scale(14)));
    }

    int Scale(int v) => v * DeviceDpi / 96;

    void SetWidgetVisible(bool v)
    {
        s.WidgetVisible = v; Data.Save(s);
        if (v) { Show(); Activate(); } else Hide();
    }

    void Tick()
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        today = Data.GetDay(s.City, date);
        next = Data.Next(now, d => Data.GetDay(s.City, d));

        // Fire alerts for times crossed since last tick; skip stale ones (e.g. after sleep).
        if (today != null)
            foreach (var p in Data.Prayers.Where(p => p.Alert))
                if (Data.At(date, p.Get(today)) is var t && lastTick < t && t <= now && now - t < TimeSpan.FromMinutes(5))
                    tray.ShowBalloonTip(10000, $"{p.Name} — {p.Get(today)}", $"Время намаза ({s.City.Title})", ToolTipIcon.Info);
        lastTick = now;

        if (next is { } n)
        {
            var tip = $"{Data.Prayers[n.Index].Name} через {Fmt(n.At - now)}";
            tray.Text = tip.Length > 63 ? tip[..63] : tip;
        }
        if (Visible) Invalidate();
    }

    static string Fmt(TimeSpan t) => $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var small = new Font("Segoe UI", 8.5f);
        using var normal = new Font("Segoe UI", 10f);
        using var bold = new Font("Segoe UI Semibold", 10f);
        var muted = Color.FromArgb(150, 160, 175);
        var accent = Color.FromArgb(94, 196, 140);
        int pad = Scale(12), y = Scale(10), w = ClientSize.Width;

        TextRenderer.DrawText(g, s.City.Title, small, new Point(pad, y), muted);
        if (next is { } n)
            TextRenderer.DrawText(g, "−" + Fmt(n.At - DateTime.Now), small, new Rectangle(0, y, w - pad, Scale(18)), accent,
                TextFormatFlags.Right);
        y += Scale(24);

        if (today == null)
        {
            TextRenderer.DrawText(g, "Нет данных.\nПроверьте интернет.", normal, new Point(pad, y), Color.White);
            return;
        }
        var rowH = Scale(26);
        var nextIdx = next is { } nn && DateOnly.FromDateTime(nn.At) == DateOnly.FromDateTime(DateTime.Now) ? nn.Index : -1;
        for (int i = 0; i < Data.Prayers.Length; i++, y += rowH)
        {
            var (name, get, alert) = Data.Prayers[i];
            var isNext = i == nextIdx;
            if (isNext)
            {
                using var b = new SolidBrush(Color.FromArgb(40, accent));
                using var path = RoundRect(new Rectangle(Scale(6), y - Scale(3), w - Scale(12), rowH - Scale(2)), Scale(6));
                g.FillPath(b, path);
            }
            var color = isNext ? accent : alert ? Color.White : muted;
            TextRenderer.DrawText(g, name, isNext ? bold : normal, new Point(pad, y), color);
            TextRenderer.DrawText(g, get(today), isNext ? bold : normal, new Rectangle(0, y, w - pad, rowH), color, TextFormatFlags.Right);
        }
    }

    static GraphicsPath RoundRect(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
        p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
        p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
        p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Drag the borderless window by any point.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, 2 /*HTCAPTION*/, 0);
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        if (!IsHandleCreated || !Visible) return;
        s.X = Left; s.Y = Top; Data.Save(s);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; SetWidgetVisible(false); }
        else tray.Visible = false;
        base.OnFormClosing(e);
    }

    static bool AutoStart => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue("NamazTimes") != null;

    static void ToggleAutoStart()
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (AutoStart) k.DeleteValue("NamazTimes");
        else k.SetValue("NamazTimes", $"\"{Environment.ProcessPath}\"");
    }

    void PickCity()
    {
        using var f = new CityPicker();
        if (f.ShowDialog() != DialogResult.OK || f.Selected == null) return;
        s.City = f.Selected; Data.Save(s);
        Tick();
    }

    static Icon LoadIcon() =>
        new(typeof(Widget).Assembly.GetManifestResourceStream("app.ico")!, SystemInformation.SmallIconSize);

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
}

public class CityPicker : Form
{
    public City? Selected { get; private set; }
    readonly TextBox query = new() { Dock = DockStyle.Fill, PlaceholderText = "Название города или села" };
    readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    List<(string Title, City City)> found = [];

    public CityPicker()
    {
        Text = "Выбор населённого пункта";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(420, 360);
        Font = new Font("Segoe UI", 10f);
        var search = new Button { Text = "Найти", Dock = DockStyle.Right, Width = 90 };
        var top = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 4, 8, 0) };
        top.Controls.Add(query); top.Controls.Add(search);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        body.Controls.Add(list);
        Controls.Add(body); Controls.Add(top);
        AcceptButton = search;

        search.Click += async (_, _) =>
        {
            if (query.Text.Trim().Length < 2) return;
            search.Enabled = false;
            try
            {
                found = await Data.SearchCities(query.Text.Trim());
                list.DataSource = found.Select(f => f.Title).ToList();
                if (found.Count == 0) list.DataSource = new List<string> { "Ничего не найдено" };
            }
            catch { MessageBox.Show(this, "Не удалось связаться с api.muftyat.kz", Text); }
            finally { search.Enabled = true; }
        };
        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= found.Count) return;
            Selected = found[list.SelectedIndex].City;
            DialogResult = DialogResult.OK;
        };
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
        var day = new DateOnly(2026, 12, 31);
        Trace.Assert(Data.Next(day.ToDateTime(new(4, 0)), Get) == (0, day.ToDateTime(new(5, 0))));
        Trace.Assert(Data.Next(day.ToDateTime(new(12, 0)), Get) == (3, day.ToDateTime(new(15, 0))), "exactly at time -> next one");
        Trace.Assert(Data.Next(day.ToDateTime(new(20, 0)), Get) == (0, new DateTime(2027, 1, 1, 5, 0, 0)), "rolls to tomorrow");
        Trace.Assert(Data.Next(day.ToDateTime(new(20, 0)), _ => null) == null);
        Console.WriteLine("selftest ok");
        return 0;
    }
}
