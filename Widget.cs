using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NamazTimes;

public class Widget : Form
{
    readonly Settings s;
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    readonly ToolStripMenuItem showItem, monthItem, holidaysItem, tasbihItem, adhkarItem, namesItem, typesItem, qadaItem, zakatItem, muteItem, settingsItem, checkUpdateItem, updateItem, exitItem;
    readonly List<(RectangleF R, P P)> bells = [];
    DateTime lastTick = Clock(), lastUpdateCheck;
    DateOnly shownDate;
    List<(P P, DateTime At)>? today;
    (P P, DateTime At)? next;
    Hijri.Holiday? holiday;
    Updates.Release? update;
    int? downloading;          // update download progress, %
    RectangleF updateBanner;
    RectangleF cityRect;   // location in the header, click = choose city
    bool hotCity;
    Action? balloonAction;     // what clicking the last notification does (install update, open adhkar...)
    SettingsForm? settingsForm;
    MonthForm? monthForm;
    HolidaysForm? holidaysForm;
    TasbihForm? tasbihForm;
    NamesForm? namesForm;
    PrayerTypesForm? typesForm;
    QadaForm? qadaForm;
    AdhkarForm? adhkarForm;
    ZakatForm? zakatForm;
    RectangleF nameCard; // "name of the day" card, clickable
    bool full, hover;          // fullscreen mode; mouse over widget (shows window buttons)
    int hot = -1, fullZoom = 100;
    Rectangle normalBounds;
    readonly RectangleF[] captions = new RectangleF[3]; // minimize, fullscreen, close
    readonly RectangleF[] tools = new RectangleF[2];    // settings, menu — always visible, top-left
    static readonly string[] ToolTips = ["Settings", "Menu"];
    int hotTool = -1;
    readonly ToolTip tip = new();

    const int BaseWidth = 250;

    public Widget(Settings settings)
    {
        s = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true; // so "minimize" has somewhere to go
        KeyPreview = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        Text = "Namaz Times KZ";
        Icon = Theme.AppIcon();

        var menu = Theme.Menu();
        // Glyphs (Segoe MDL2): View, Calendar, FavoriteStar, RingerSilent, Settings, Download, PowerButton
        showItem = Theme.MenuItem('', (_, _) => SetWidgetVisible(!Visible));
        monthItem = Theme.MenuItem('', (_, _) => Open(ref monthForm, () => new MonthForm(s)));
        holidaysItem = Theme.MenuItem('', (_, _) => Open(ref holidaysForm, () => new HolidaysForm(s)));
        tasbihItem = Theme.MenuItem('', (_, _) => OpenTasbih()); // RadioBullet, a bead
        adhkarItem = Theme.MenuItem('', (_, _) => OpenAdhkar()); // Brightness (sun)
        zakatItem = Theme.MenuItem('', (_, _) => OpenZakat());   // PaymentCard
        qadaItem = Theme.MenuItem('', (_, _) => OpenQada()); // History
        typesItem = Theme.MenuItem('', (_, _) => OpenTypes()); // ReadingMode (open book)
        namesItem = Theme.MenuItem('', (_, _) => OpenNames());   // Dictionary (book)
        muteItem = Theme.MenuItem('', (_, _) => { s.Muted = !s.Muted; Data.Save(s); Invalidate(); });
        settingsItem = Theme.MenuItem('', (_, _) => OpenSettings());
        checkUpdateItem = Theme.MenuItem('', (_, _) => _ = CheckUpdatesNow()); // Sync
        updateItem = Theme.MenuItem('', (_, _) => _ = InstallUpdate());
        updateItem.Visible = false;
        exitItem = Theme.MenuItem('', (_, _) => { tray!.Visible = false; Application.Exit(); });
        menu.Items.AddRange([showItem, monthItem, holidaysItem, tasbihItem, adhkarItem, namesItem, typesItem, qadaItem, zakatItem, new ToolStripSeparator(), muteItem, settingsItem, checkUpdateItem, updateItem,
            new ToolStripSeparator(), exitItem]);
        menu.Opening += (_, _) => { showItem.Checked = Visible; muteItem.Checked = s.Muted; };
        ContextMenuStrip = menu;

        tray = new NotifyIcon { Icon = Theme.AppIcon(SystemInformation.SmallIconSize), Text = Text, ContextMenuStrip = menu, Visible = true };
        tray.BalloonTipClicked += (_, _) => balloonAction?.Invoke();
        tray.MouseClick +=(_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (WindowState == FormWindowState.Minimized) { WindowState = FormWindowState.Normal; Activate(); }
            else SetWidgetVisible(!Visible);
        };

        ApplySettings();
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    Day? Get(DateOnly d) => Data.GetDay(s.City, d);
    bool IsShown(P p) => !s.Hidden.Contains(p);
    int EZ => full ? fullZoom : s.Zoom;
    float Z(float v) => v * DeviceDpi / 96f * EZ / 100f;
    int Zi(float v) => (int)Math.Round(Z(v));
    Font F(float pt, FontStyle st = FontStyle.Regular) => new(st == FontStyle.Bold ? "Segoe UI Semibold" : "Segoe UI", pt * EZ / 100f);

    public void ApplySettings()
    {
        L.Lang = s.Lang;
        L.Hour12 = s.Hour12;
        Theme.UiScale = Math.Clamp(s.UiScale, 80, 160) / 100f;
        showItem.Text = L.T("ShowWidget");
        monthItem.Text = L.T("Month");
        holidaysItem.Text = L.T("Holidays");
        tasbihItem.Text = L.T("Tasbih");
        namesItem.Text = L.T("Names99");
        typesItem.Text = L.T("PrayerTypes");
        qadaItem.Text = L.T("Qada");
        adhkarItem.Text = L.T("Adhkar");
        zakatItem.Text = L.T("Zakat");
        muteItem.Text = L.T("Mute");
        settingsItem.Text = L.T("Settings");
        checkUpdateItem.Text = L.T("CheckUpdates");
        exitItem.Text = L.T("Exit");
        TopMost = s.TopMost;
        Opacity = Math.Clamp(s.Opacity, 30, 100) / 100.0;
        Tick();
        if (IsHandleCreated) Relayout();
    }

    /// One line of the prayer card with an optional grey sub-line: "Духа 08:43" under Sunrise,
    /// and in Ramadan "Сәресі" under Fajr and "Ауызашар" under Maghrib.
    record Row(P P, DateTime At, string? Sub)
    {
        public int H => Sub == null ? 34 : 48;
    }
    List<Row> rows = [];
    P? current;
    (string Label, DateTime At)? fast; // Ramadan countdown: to end of suhoor or to iftar

    /// Test seam: lets offscreen renders show a Ramadan day.
    internal static Func<DateTime> Clock = () => DateTime.Now;

    // Days that show suhoor/iftar: Ramadan by default; "Always" also covers voluntary fasts.
    bool IsRamadan(DateTime now) => s.Fasting switch
    {
        FastingMode.Always => true,
        FastingMode.Off => false,
        _ => Hijri.Of(DateOnly.FromDateTime(now), s.HijriAdjust).M == 9,
    };

    static DateTime? Find(List<(P P, DateTime At)>? l, P p) => l?.Where(x => x.P == p).Select(x => (DateTime?)x.At).FirstOrDefault();

    void BuildRows(DateTime now)
    {
        rows = [];
        current = null;
        fast = null;
        if (today == null) return;
        var ramadan = IsRamadan(now);
        void Add(P p, DateTime? t, string? sub = null) { if (t is { } v && IsShown(p)) rows.Add(new(p, v, sub)); }
        var duhaSub = IsShown(P.Sunrise) && IsShown(P.Duha) && Find(today, P.Duha) is { } duha ? $"{L.Name(P.Duha)} {L.Time(duha)}" : null;
        Add(P.Fajr, Find(today, P.Fajr), ramadan ? L.T("Suhoor") : null);
        Add(P.Sunrise, Find(today, P.Sunrise), duhaSub);
        if (!IsShown(P.Sunrise)) Add(P.Duha, Find(today, P.Duha));
        Add(P.Dhuhr, Find(today, P.Dhuhr));
        Add(P.Asr, Find(today, P.Asr));
        Add(P.Maghrib, Find(today, P.Maghrib), ramadan ? L.T("Iftar") : null);
        Add(P.Isha, Find(today, P.Isha));
        // Tahajjud goes last: tonight's, unless we are still in the night before today's Fajr.
        var tomorrow = Data.Times(DateOnly.FromDateTime(now).AddDays(1), Get, s);
        // Ramadan: suhoor ends at Fajr (Таң), iftar at Maghrib (Ақшам). After iftar, count down to
        // tomorrow's suhoor if tomorrow is a fasting day (also covers the eve of the first fast).
        if (Find(today, P.Fajr) is { } f && Find(today, P.Maghrib) is { } m)
            fast = ramadan && now < f ? (L.T("UntilSuhoor"), f)
                : ramadan && now < m ? (L.T("UntilIftar"), m)
                : now >= m && IsRamadan(now.AddDays(1)) && Find(tomorrow, P.Fajr) is { } f2 ? (L.T("UntilSuhoor"), f2)
                : null;
        Add(P.Tahajjud, now < Find(today, P.Fajr) ? Find(today, P.Tahajjud) : Find(tomorrow, P.Tahajjud));
        // Current period = last passed time today (Duha is a sub-line, not a period); before the first one it's still Isha.
        current = today.LastOrDefault(x => x.P != P.Duha && x.At <= now) is { At.Year: > 1 } c ? c.P : P.Isha;
    }

    int HeaderLines => 3 + (holiday != null ? 1 : 0);
    int CardHeight => 6 + Math.Max(rows.Sum(r => r.H), 60) + 4 + (fast != null ? 38 : 0) + 38;
    const int TopStrip = 24; // free space above the header for the minimize/fullscreen/close buttons
    const int NameCardHeight = 100;
    int ContentHeight => TopStrip + 6 + HeaderLines * 19 + 8 + (update != null ? 36 : 0) + CardHeight + (s.ShowNameOfDay ? 10 + NameCardHeight : 0) + 12; // at 96 dpi, zoom 100
    Size SizeFor() => new(Zi(BaseWidth), Zi(ContentHeight));

    void Relayout()
    {
        if (full)
        {
            var screen = Screen.FromRectangle(normalBounds).Bounds;
            fullZoom = Math.Clamp((int)(screen.Height * 0.9 / (ContentHeight * DeviceDpi / 96f) * 100), 100, 800);
            Bounds = screen;
        }
        else ClientSize = SizeFor();
        Invalidate();
    }

    void ToggleFull()
    {
        if (!full) { normalBounds = Bounds; full = true; }
        else { full = false; Bounds = normalBounds; }
        Relayout();
        Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape && full) ToggleFull();
        if (e.KeyCode == Keys.F11) ToggleFull();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Region = full ? null : Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Zi(14), Zi(14)));
    }

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(value && s.WidgetVisible);

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Relayout();
        var wa = Screen.PrimaryScreen!.WorkingArea;
        var pos = new Point(s.X, s.Y);
        Location = Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(pos)) ? pos
            : new Point(wa.Right - Width - Zi(16), wa.Top + Zi(16));
    }

    /// Shown again when the app is launched while already running (e.g. from the Start menu).
    public void BringBack()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        SetWidgetVisible(true);
    }

    void SetWidgetVisible(bool v)
    {
        s.WidgetVisible = v; Data.Save(s);
        if (v) { Show(); Activate(); } else Hide();
    }

    /// Shows a notification; onClick runs if the user clicks it (e.g. open the adhkar window).
    void Notify(string title, string text, Action? onClick = null)
    {
        if (s.Muted) return;
        balloonAction = onClick;
        tray.ShowBalloonTip(10000, title, text, ToolTipIcon.None);
    }

    /// Sample alert for the next prayer, exactly as a real one looks (ignores mute).
    public void TestAlert()
    {
        var (p, t) = next ?? (P.Fajr, DateTime.Today.AddHours(5));
        balloonAction = null;
        tray.ShowBalloonTip(10000, $"{L.Name(p)} — {L.Time(t)}", $"{L.T("PrayerTime")} · {s.City.Title}", ToolTipIcon.None);
    }

    /// Daily reminders besides prayer times: sunnah fasts, holidays, al-Kahf on Friday, morning/evening adhkar,
    /// fitr sadaqa. Each fires once when its moment is crossed; staggered so balloons don't replace each other.
    void Reminders(DateOnly date, Func<DateTime, bool> crossed)
    {
        if (today == null) return;
        var place = s.City.Title;
        var (_, hm, hd) = Hijri.Of(date, s.HijriAdjust);

        // 20 min after Isha: what tomorrow brings (a sunnah fast and/or a holiday).
        if (Find(today, P.Isha) is { } isha && crossed(isha.AddMinutes(20)))
        {
            var tomorrow = date.AddDays(1);
            var lines = new List<string>();
            if (s.RemindSunnahFasts && Hijri.SunnahFast(tomorrow, s.HijriAdjust) is { } why) lines.Add(L.T("FastTomorrow") + ": " + L.T(why));
            if (s.RemindHolidays && Hijri.On(tomorrow, s.HijriAdjust) is { Night: false } h) lines.Add(string.Format(L.T("HolidayTomorrow"), L.T(h.Key)));
            if (s.RemindHolidays && hm == 9 && hd == 27) lines.Add(FitrReminder());
            if (lines.Count > 0) Notify(L.T("Reminder"), string.Join("\n", lines));
        }
        if (Find(today, P.Maghrib) is { } maghrib)
        {
            if (s.RemindHolidays && holiday is { Night: true } nh && crossed(maghrib.AddMinutes(5)))
                Notify(string.Format(L.T("HolidayTonight"), L.T(nh.Key)), place);
            if (s.RemindKahf && date.DayOfWeek == DayOfWeek.Thursday && crossed(maghrib.AddMinutes(10)))
                Notify(L.T("KahfTitle"), L.T("KahfText"));
        }
        if (s.RemindKahf && date.DayOfWeek == DayOfWeek.Friday && Find(today, P.Sunrise) is { } sunrise && crossed(sunrise.AddMinutes(60)))
            Notify(L.T("KahfTitle"), L.T("KahfText"));
        if (s.RemindAdhkar && Find(today, P.Fajr) is { } fajr && crossed(fajr.AddMinutes(15)))
            Notify(L.T("AdhkarMorningTitle"), L.T("AdhkarTap"), () => OpenAdhkar(true));
        if (s.RemindAdhkar && Find(today, P.Asr) is { } asr && crossed(asr.AddMinutes(15)))
            Notify(L.T("AdhkarEveningTitle"), L.T("AdhkarTap"), () => OpenAdhkar(false));
    }

    string FitrReminder()
    {
        var z = s.Zakat;
        return z.FitrAmount > 0
            ? string.Format(L.T("FitrReminderAmount"), (z.FitrAmount * z.FitrPeople).ToString("N0", L.Culture) + " ₸")
            : L.T("FitrReminder");
    }

    void Tick()
    {
        var now = Clock();
        var date = DateOnly.FromDateTime(now);
        today = Data.Times(date, Get, s);
        // "Next" = the next main time; Tahajjud and Duha are voluntary extras, not the next prayer.
        next = Data.Next(now, Get, p => IsShown(p) && p is not (P.Tahajjud or P.Duha), s);
        var height = ContentHeight;
        if (date != shownDate)
        {
            shownDate = date;
            holiday = Hijri.On(date, s.HijriAdjust);
        }
        BuildRows(now);
        if (ContentHeight != height && IsHandleCreated) Relayout();

        // Fire alerts for moments crossed since last tick; skip stale ones (e.g. after sleep).
        bool Crossed(DateTime t) => lastTick < t && t <= now && now - t < TimeSpan.FromMinutes(5);
        var place = s.City.Title;
        foreach (var (p, t) in (today ?? []).Concat(Data.Times(date.AddDays(1), Get, s) ?? []))
        {
            if (p == P.Dhuhr && s.Jumuah && t.DayOfWeek == DayOfWeek.Friday && Crossed(t.AddMinutes(-s.JumuahBefore)))
                Notify(L.T("Jumuah"), $"{L.Name(P.Dhuhr)} {L.Time(t)} · {place}");
            if (!s.Alerts.Contains(p)) continue;
            if (Crossed(t))
            {
                Notify($"{L.Name(p)} — {L.Time(t)}", $"{L.T("PrayerTime")} · {place}");
                if (s.FullscreenAlert && !s.Muted && p is P.Fajr or P.Dhuhr or P.Asr or P.Maghrib or P.Isha)
                    new PrayerOverlay(string.Format(L.T("OverlayTitle"), L.Name(p)), $"{L.Time(t)} · {place}").Show();
            }
            if (s.RemindBefore > 0 && Crossed(t.AddMinutes(-s.RemindBefore)))
                Notify(string.Format(L.T("InMin"), L.Name(p), s.RemindBefore), $"{L.Time(t)} · {place}");
        }
        Reminders(date, Crossed);
        lastTick = now;

        if (now - lastUpdateCheck > TimeSpan.FromHours(24) && Updates.Repo != null)
        {
            lastUpdateCheck = now;
            _ = CheckUpdates();
        }

        if (next is { } n)
        {
            var tip = string.Format(L.T("NextIn"), L.Name(n.P), Fmt(n.At - now));
            tray.Text = tip.Length > 63 ? tip[..63] : tip;
        }
        if (Visible) Invalidate();
    }

    async Task CheckUpdates()
    {
        try
        {
            if (await Updates.Check() is not { } u || update?.Version == u.Version) return;
            SetUpdate(u);
            balloonAction = () => _ = InstallUpdate();
            tray.ShowBalloonTip(10000, "Namaz Times KZ", string.Format(L.T("UpdateAvail"), "v" + u.Version.ToString(3)), ToolTipIcon.None);
        }
        catch { /* offline or rate-limited: try again tomorrow */ }
    }

    void SetUpdate(Updates.Release u)
    {
        update = u;
        updateItem.Text = string.Format(L.T("Update"), "v" + u.Version.ToString(3));
        updateItem.Visible = true;
        if (IsHandleCreated) Relayout(); // room for the update banner
    }

    /// "Check for updates" from the menu or settings: always answers, and offers to install right away.
    public async Task CheckUpdatesNow(IWin32Window? owner = null)
    {
        owner ??= this;
        const string title = "Namaz Times KZ";
        if (Updates.Repo == null) { MessageBox.Show(owner, L.T("DevBuild"), title); return; }
        Updates.Release? u;
        try { u = await Updates.Check(); }
        catch { MessageBox.Show(owner, L.T("UpdateCheckFailed"), title); return; }
        if (u == null)
        {
            MessageBox.Show(owner, string.Format(L.T("UpToDate"), "v" + Updates.Current.ToString(3)), title);
            return;
        }
        SetUpdate(u);
        if (MessageBox.Show(owner, string.Format(L.T("UpdateAsk"), "v" + u.Version.ToString(3)), title, MessageBoxButtons.YesNo) == DialogResult.Yes)
            await InstallUpdate();
    }

    /// Download and install the found update, then exit so the installer can replace us.
    async Task InstallUpdate()
    {
        if (update == null || downloading != null) return;
        downloading = 0;
        Invalidate();
        try
        {
            await Updates.Download(update, new Progress<int>(p => { downloading = p; Invalidate(); }));
            tray.Visible = false;
            Application.Exit();
        }
        catch
        {
            downloading = null;
            Invalidate();
            MessageBox.Show(this, L.T("UpdateFailed"), "Namaz Times KZ");
            Process.Start(new ProcessStartInfo(update.Page) { UseShellExecute = true });
        }
    }

    /// For the --update flag: check right now and install if there is something newer.
    public async Task UpdateNow()
    {
        await CheckUpdates();
        await InstallUpdate();
    }

    static string Fmt(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    // Palette of the KZ mobile prayer apps: night-sky background, grey card, green "next" bar.
    static readonly Color Sky1 = Color.FromArgb(10, 16, 38), Sky2 = Color.FromArgb(27, 36, 66);
    static readonly Color CardFill = Color.FromArgb(242, 44, 46, 54), CardBorder = Color.FromArgb(70, 255, 255, 255);
    static readonly Color Pill = Color.FromArgb(78, 80, 90), Green = Color.FromArgb(22, 163, 116);
    static readonly Color Grey = Color.FromArgb(142, 142, 147), Speaker = Color.FromArgb(128, 131, 138);
    static readonly Color Gold = Color.FromArgb(230, 190, 110), GoldBar = Color.FromArgb(176, 128, 44); // Ramadan

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var head = F(10f);
        using var small = F(9f);
        using var name = F(11.5f, FontStyle.Bold);
        using var time = F(12f, FontStyle.Bold);
        using var clock = F(14f, FontStyle.Bold);
        using var icons = new Font("Segoe MDL2 Assets", 10f * EZ / 100f);
        var now = Clock();
        const TextFormatFlags Right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter;
        const TextFormatFlags Left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

        // Background: gradient night sky with a few fixed stars.
        using (var sky = new LinearGradientBrush(ClientRectangle, Sky1, Sky2, 90f)) g.FillRectangle(sky, ClientRectangle);
        var rnd = new Random(7);
        var starCount = ClientSize.Width * ClientSize.Height / Math.Max(1, Zi(38) * Zi(38));
        for (int i = 0; i < starCount; i++)
        {
            var d = Z(0.8f + (float)rnd.NextDouble() * 1.6f);
            using var star = new SolidBrush(Color.FromArgb(rnd.Next(50, 170), Color.White));
            g.FillEllipse(star, (float)rnd.NextDouble() * ClientSize.Width, (float)rnd.NextDouble() * ClientSize.Height, d, d);
        }

        // Content is BaseWidth wide; in fullscreen it is scaled up and centred on the screen.
        int cw = Zi(BaseWidth), ox = (ClientSize.Width - cw) / 2, oy = full ? (ClientSize.Height - Zi(ContentHeight)) / 2 : 0;
        float left = ox + Z(14), right = ox + cw - Z(14);

        // Header: clock on the left; location, dates and holiday right-aligned.
        TextRenderer.DrawText(g, L.Time(now), clock, new Point((int)left - Zi(2), oy + Zi(TopStrip + 9)), Color.White);
        float y = oy + Z(TopStrip + 6);
        var city = s.City.Title + (s.Muted ? " 🔕" : "");
        var cityW = TextRenderer.MeasureText(g, city, head, Size.Empty, TextFormatFlags.NoPadding).Width;
        var cityColor = hotCity ? Theme.Accent : Color.White; // green on hover: it is clickable
        TextRenderer.DrawText(g, city, head, Rectangle.Round(new RectangleF(left, y, right - left, Z(19))), cityColor, Right | TextFormatFlags.EndEllipsis);
        // Location arrow (like iOS "location.fill"); Segoe MDL2 has no such glyph.
        float ax = right - cityW - Z(19), ay = y + Z(4.5f), a = Z(10);
        cityRect = new RectangleF(ax - Z(4), y, right - ax + Z(4), Z(19));
        using (var arrow = new SolidBrush(cityColor))
            g.FillPolygon(arrow, new PointF[] { new(ax + a, ay), new(ax, ay + a * 0.42f), new(ax + a * 0.45f, ay + a * 0.55f), new(ax + a * 0.58f, ay + a) });
        y += Z(19);
        var date = now.ToString(L.Lang == "ru" ? "d MMMM yyyy" : "d MMMM, yyyy", L.Culture);
        if (L.Lang != "ru") { var sp = date.IndexOf(' ') + 1; date = date[..sp] + char.ToUpper(date[sp], L.Culture) + date[(sp + 1)..]; }
        var lines = new List<(string Text, Color Color)> { (date, Grey), (Hijri.Format(DateOnly.FromDateTime(now), s.HijriAdjust), Grey) };
        if (holiday != null) lines.Add(("✦ " + L.T(holiday.Key), Theme.Accent));
        foreach (var (text, color) in lines)
        {
            TextRenderer.DrawText(g, text, head, Rectangle.Round(new RectangleF(left, y, right - left, Z(19))), color, Right | TextFormatFlags.EndEllipsis);
            y += Z(19);
        }
        y += Z(8);

        // Update banner: "New version — update" (click installs), then download progress.
        updateBanner = RectangleF.Empty;
        if (update != null)
        {
            updateBanner = new RectangleF(ox + Z(12), y, cw - Z(24), Z(28));
            using var bp = Theme.RoundRect(updateBanner, Z(8));
            using (var bb = new SolidBrush(Color.FromArgb(downloading == null ? 70 : 40, Green))) g.FillPath(bb, bp);
            using (var bpen = new Pen(Green, Math.Max(1, Z(1)))) g.DrawPath(bpen, bp);
            if (downloading is { } pct) // progress fill
            {
                g.SetClip(bp);
                using var pf = new SolidBrush(Color.FromArgb(120, Green));
                g.FillRectangle(pf, updateBanner.X, updateBanner.Y, updateBanner.Width * pct / 100f, updateBanner.Height);
                g.ResetClip();
            }
            var text = downloading is { } d ? string.Format(L.T("Downloading"), d)
                : string.Format(L.T("UpdateBanner"), "v" + update.Version.ToString(3));
            TextRenderer.DrawText(g, text, small, Rectangle.Round(updateBanner), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            y += Z(36);
        }

        // Card with prayer rows.
        var card = new RectangleF(ox + Z(12), y, cw - Z(24), Z(CardHeight));
        using var cardPath = Theme.RoundRect(card, Z(14));
        using (var fill = new SolidBrush(CardFill)) g.FillPath(fill, cardPath);
        float rx = card.X + Z(14), rr = card.Right - Z(14), ry = card.Y + Z(6);
        bells.Clear();
        if (today == null)
            TextRenderer.DrawText(g, L.T("NoData"), small, Rectangle.Round(new RectangleF(rx, ry, rr - rx, Z(60))), Color.White, Left);
        foreach (var r in rows)
        {
            var h = Z(r.H);
            if (r.Sub is { } sub)
            {
                TextRenderer.DrawText(g, L.Name(r.P), name, Rectangle.Round(new RectangleF(rx, ry + Z(4), rr - rx, Z(24))), Color.White, Left);
                TextRenderer.DrawText(g, sub, small, Rectangle.Round(new RectangleF(rx, ry + Z(26), rr - rx, Z(18))),
                    r.P == P.Sunrise ? Grey : Gold, Left);
            }
            else TextRenderer.DrawText(g, L.Name(r.P), name, Rectangle.Round(new RectangleF(rx, ry, rr - rx, h)), Color.White, Left);

            // Times sit centred in a fixed-width box at the right; the current one gets a pill of exactly that box.
            var boxW = TextRenderer.MeasureText(g, L.Hour12 ? "00:00 PM" : "00:00", time, Size.Empty, TextFormatFlags.NoPadding).Width + Z(14);
            var box = new RectangleF(rr - boxW + Z(6), ry + (h - Z(28)) / 2, boxW, Z(28));
            if (r.P == current)
            {
                using var pill = Theme.RoundRect(box, Z(7));
                using var pb = new SolidBrush(Pill);
                g.FillPath(pb, pill);
            }
            TextRenderer.DrawText(g, L.Time(r.At), time, Rectangle.Round(box), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // Speaker: click toggles this prayer's notification.
            var on = s.Alerts.Contains(r.P);
            // Speaker column sits in the middle of the card, between the names and the times.
            var icon = new RectangleF(Math.Min(card.X + card.Width / 2 + Z(4), box.X - Z(36)), ry, Z(24), h);
            // Volume3 vs Mute: both start at the same x and are nearly the same width, so the column lines up.
            TextRenderer.DrawText(g, on ? "" : "", icons, Rectangle.Round(icon),
                on && !s.Muted ? Color.FromArgb(205, 208, 214) : Speaker, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            bells.Add((icon, r.P));
            ry += h;
        }

        // Bottom bars, clipped to the card's rounded bottom: Ramadan countdown (gold), then next prayer (green).
        void Bar(float top, Color fillColor, string label, TimeSpan left)
        {
            var bar = new RectangleF(card.X, top, card.Width, Z(38));
            g.SetClip(cardPath);
            using (var gb = new SolidBrush(fillColor)) g.FillRectangle(gb, bar);
            g.ResetClip();
            var timeW = TextRenderer.MeasureText(g, "00:00:00", time, Size.Empty, TextFormatFlags.NoPadding).Width + Z(8);
            TextRenderer.DrawText(g, label, name, Rectangle.Round(new RectangleF(rx, bar.Y, rr - rx - timeW, bar.Height)), Color.White, Left);
            TextRenderer.DrawText(g, Fmt(left), time, Rectangle.Round(new RectangleF(rx, bar.Y, rr - rx, bar.Height)), Color.White, Right);
        }
        if (fast is { } f) Bar(card.Bottom - Z(76), GoldBar, f.Label, f.At - now);
        if (next is { } n) Bar(card.Bottom - Z(38), Green, L.Name(n.P), n.At - now);
        using (var border = new Pen(CardBorder, Math.Max(1, Z(1)))) g.DrawPath(border, cardPath);

        // Name of the day: one of the 99 names, changes daily; click opens the full list.
        nameCard = RectangleF.Empty;
        if (s.ShowNameOfDay)
        {
            var nm = Name99.OfDay(DateOnly.FromDateTime(now));
            nameCard = new RectangleF(card.X, card.Bottom + Z(10), card.Width, Z(NameCardHeight));
            using var np = Theme.RoundRect(nameCard, Z(14));
            using (var nf = new SolidBrush(CardFill)) g.FillPath(nf, np);
            using (var nb = new Pen(Color.FromArgb(110, Gold), Math.Max(1, Z(1)))) g.DrawPath(nb, np);
            using var arFont = new Font(Name99.ArabicFont, 17f * EZ / 100f);
            TextRenderer.DrawText(g, $"{L.T("NameOfDay")} · {nm.N} / 99", small,
                Rectangle.Round(new RectangleF(rx, nameCard.Y + Z(6), rr - rx, Z(18))), Gold, Left);
            TextRenderer.DrawText(g, nm.Ar, arFont, Rectangle.Round(new RectangleF(rx, nameCard.Y + Z(22), rr - rx, Z(36))), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft);
            TextRenderer.DrawText(g, nm.Translit, name, Rectangle.Round(new RectangleF(rx, nameCard.Y + Z(56), rr - rx, Z(22))), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, nm.Meaning, small, Rectangle.Round(new RectangleF(rx, nameCard.Y + Z(76), rr - rx, Z(18))), Grey,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // Settings, tasbih, 99 names and menu buttons: always visible in the top strip, so nobody has to guess the right-click.
        using (var toolFont = new Font("Segoe MDL2 Assets", 9.5f * EZ / 100f))
        {
            string[] toolGlyphs = ["", ""]; // Settings, More — everything else lives in the ⋯ menu so the strip never overflows
            for (int i = 0; i < tools.Length; i++)
            {
                tools[i] = new RectangleF(ox + Z(6) + i * Z(26), oy + Z(2), Z(25), Z(24));
                if (i == hotTool)
                {
                    using var chip = Theme.RoundRect(tools[i], Z(6));
                    using var cb = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
                    g.FillPath(cb, chip);
                }
                TextRenderer.DrawText(g, toolGlyphs[i], toolFont, Rectangle.Round(tools[i]), i == hotTool ? Color.White : Grey,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // Window buttons (minimize / fullscreen / close) appear while the mouse is over the widget.
        var bw = full ? Theme.Dp(46) : Z(26);
        var bh = full ? Theme.Dp(32) : Z(24);
        for (int i = 0; i < 3; i++) captions[i] = new RectangleF(ClientSize.Width - (3 - i) * bw, 0, bw, bh);
        if (!hover) return;
        using var capFont = new Font("Segoe MDL2 Assets", full ? 8f : 7f * EZ / 100f);
        using (var bg = new SolidBrush(Sky1)) g.FillRectangle(bg, captions[0].X, 0, 3 * bw, bh);
        string[] glyphs = ["", full ? "" : "", ""]; // ChromeMinimize, ChromeRestore/Maximize, ChromeClose
        for (int i = 0; i < 3; i++)
        {
            if (i == hot)
                using (var hb = new SolidBrush(i == 2 ? Color.FromArgb(196, 43, 28) : Theme.Line)) g.FillRectangle(hb, captions[i]);
            TextRenderer.DrawText(g, glyphs[i], capFont, Rectangle.Round(captions[i]), i == hot ? Color.White : Grey,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        switch (Array.FindIndex(tools, r => r.Contains(e.Location)))
        {
            case 0: OpenSettings(); return;
            case 1: ContextMenuStrip!.Show(this, Point.Round(new PointF(tools[1].Left, tools[1].Bottom))); return;
        }
        switch (hover ? Array.FindIndex(captions, r => r.Contains(e.Location)) : -1)
        {
            case 0: WindowState = FormWindowState.Minimized; return;
            case 1: ToggleFull(); return;
            case 2: if (full) ToggleFull(); SetWidgetVisible(false); return;
        }
        if (nameCard.Contains(e.Location)) { OpenNames(); return; }
        if (updateBanner.Contains(e.Location)) { _ = InstallUpdate(); return; }
        if (cityRect.Contains(e.Location)) { PickCity(); return; }
        foreach (var (r, p) in bells)
            if (r.Contains(e.Location))
            {
                if (!s.Alerts.Remove(p)) s.Alerts.Add(p);
                Data.Save(s);
                Invalidate();
                return;
            }
        if (full) return;
        // Drag the borderless window by any other point.
        ReleaseCapture();
        SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, 2 /*HTCAPTION*/, 0);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = Array.FindIndex(captions, r => r.Contains(e.Location));
        if (!hover || h != hot) { hover = true; hot = h; Invalidate(); }
        var t = Array.FindIndex(tools, r => r.Contains(e.Location));
        if (t != hotTool)
        {
            hotTool = t;
            Invalidate();
            if (t >= 0) tip.Show(L.T(ToolTips[t]), this, Point.Round(new PointF(tools[t].Left, tools[t].Bottom + Z(4))), 2500);
            else tip.Hide(this);
        }
        var overCity = cityRect.Contains(e.Location);
        if (overCity != hotCity)
        {
            hotCity = overCity;
            Invalidate();
            if (overCity) tip.Show(L.T("ChooseCity"), this, Point.Round(new PointF(cityRect.Left, cityRect.Bottom + Z(4))), 2500);
            else if (t < 0) tip.Hide(this);
        }
        Cursor = h >= 0 || t >= 0 || overCity || nameCard.Contains(e.Location) || updateBanner.Contains(e.Location) || bells.Any(b => b.R.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
    }

    /// Click on the location in the header: pick another city right away.
    void PickCity()
    {
        using var f = new CityPicker { StartPosition = FormStartPosition.CenterScreen, TopMost = TopMost };
        if (f.ShowDialog(this) != DialogResult.OK || f.Selected == null) return;
        s.City = f.Selected;
        Data.Save(s);
        shownDate = default;
        ApplySettings();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = false; hot = -1; hotTool = -1;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if ((ModifierKeys & Keys.Control) == 0 || full) return;
        s.Zoom = Math.Clamp(s.Zoom + Math.Sign(e.Delta) * 10, 70, 250);
        Data.Save(s);
        Relayout();
    }

    // Bottom-right corner resizes; width drives zoom, height follows the content.
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84, WM_SIZING = 0x214, WM_EXITSIZEMOVE = 0x232, HTBOTTOMRIGHT = 17;
        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST && !full)
        {
            var p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
            if (p.X > Width - Zi(18) && p.Y > Height - Zi(18)) m.Result = HTBOTTOMRIGHT;
        }
        else if (m.Msg == WM_SIZING)
        {
            var r = Marshal.PtrToStructure<RECT>(m.LParam);
            s.Zoom = Math.Clamp((int)Math.Round((r.R - r.L) * 96.0 / DeviceDpi / BaseWidth * 100), 70, 250);
            var size = SizeFor();
            r.R = r.L + size.Width; r.B = r.T + size.Height;
            Marshal.StructureToPtr(r, m.LParam, false);
            m.Result = 1;
            Invalidate();
        }
        else if (m.Msg == WM_EXITSIZEMOVE) Data.Save(s);
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        if (!IsHandleCreated || !Visible || full || WindowState != FormWindowState.Normal) return;
        s.X = Left; s.Y = Top; Data.Save(s);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; SetWidgetVisible(false); }
        else tray.Visible = false;
        base.OnFormClosing(e);
    }

    // Windows opened from the widget must not end up behind it when the widget is "always on top".
    void Open<T>(ref T? form, Func<T> make) where T : Form
    {
        if (form is { IsDisposed: false }) { form.Activate(); return; }
        form = make();
        form.TopMost = TopMost;
        form.Show();
    }

    void OpenTasbih() => Open(ref tasbihForm, () => new TasbihForm(s));
    void OpenNames() => Open(ref namesForm, () => new NamesForm());
    void OpenTypes() => Open(ref typesForm, () => new PrayerTypesForm());
    void OpenQada() => Open(ref qadaForm, () => new QadaForm(s));
    void OpenZakat() => Open(ref zakatForm, () => new ZakatForm(s));
    void OpenAdhkar(bool? morning = null)
    {
        adhkarForm?.Close(); // reopen on the requested tab (morning / evening)
        adhkarForm = new AdhkarForm(morning) { TopMost = TopMost };
        adhkarForm.Show();
    }

    void OpenSettings()
    {
        if (settingsForm is { IsDisposed: false }) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(s, TestAlert, CheckUpdatesNow) { TopMost = TopMost };
        settingsForm.FormClosed += (_, _) =>
        {
            if (settingsForm.DialogResult != DialogResult.OK) return;
            Data.Save(s);
            shownDate = default; // re-evaluate holiday with new hijri adjustment
            ApplySettings();
        };
        settingsForm.Show();
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool AutoStart
    {
        get => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue("NamazTimes") != null;
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue("NamazTimes", $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue("NamazTimes", false);
        }
    }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
}
