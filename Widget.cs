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
    readonly ToolStripMenuItem showItem, monthItem, holidaysItem, muteItem, settingsItem, updateItem, exitItem;
    readonly List<(RectangleF R, P P)> bells = [];
    DateTime lastTick = DateTime.Now, lastUpdateCheck;
    DateOnly shownDate;
    List<(P P, DateTime At)>? today;
    (P P, DateTime At)? next;
    Hijri.Holiday? holiday;
    string? updateUrl;
    SettingsForm? settingsForm;
    MonthForm? monthForm;
    HolidaysForm? holidaysForm;
    bool full, hover;          // fullscreen mode; mouse over widget (shows window buttons)
    int hot = -1, fullZoom = 100;
    Rectangle normalBounds;
    readonly RectangleF[] captions = new RectangleF[3]; // minimize, fullscreen, close

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
        muteItem = Theme.MenuItem('', (_, _) => { s.Muted = !s.Muted; Data.Save(s); Invalidate(); });
        settingsItem = Theme.MenuItem('', (_, _) => OpenSettings());
        updateItem = Theme.MenuItem('', (_, _) => Process.Start(new ProcessStartInfo(updateUrl!) { UseShellExecute = true }));
        updateItem.Visible = false;
        exitItem = Theme.MenuItem('', (_, _) => { tray!.Visible = false; Application.Exit(); });
        menu.Items.AddRange([showItem, monthItem, holidaysItem, new ToolStripSeparator(), muteItem, settingsItem, updateItem,
            new ToolStripSeparator(), exitItem]);
        menu.Opening += (_, _) => { showItem.Checked = Visible; muteItem.Checked = s.Muted; };
        ContextMenuStrip = menu;

        tray = new NotifyIcon { Icon = Theme.AppIcon(SystemInformation.SmallIconSize), Text = Text, ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) =>
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
        showItem.Text = L.T("ShowWidget");
        monthItem.Text = L.T("Month");
        holidaysItem.Text = L.T("Holidays");
        muteItem.Text = L.T("Mute");
        settingsItem.Text = L.T("Settings") + "…";
        exitItem.Text = L.T("Exit");
        TopMost = s.TopMost;
        Opacity = Math.Clamp(s.Opacity, 30, 100) / 100.0;
        Tick();
        if (IsHandleCreated) Relayout();
    }

    /// One line of the prayer card; Sunrise carries Duha as a sub-line like the KZ mobile apps.
    record Row(P P, DateTime At, DateTime? Duha)
    {
        public int H => Duha == null ? 34 : 48;
    }
    List<Row> rows = [];
    P? current;

    static DateTime? Find(List<(P P, DateTime At)>? l, P p) => l?.Where(x => x.P == p).Select(x => (DateTime?)x.At).FirstOrDefault();

    void BuildRows(DateTime now)
    {
        rows = [];
        current = null;
        if (today == null) return;
        void Add(P p, DateTime? t, DateTime? duha = null) { if (t is { } v && IsShown(p)) rows.Add(new(p, v, duha)); }
        var duhaSub = IsShown(P.Sunrise) && IsShown(P.Duha);
        Add(P.Fajr, Find(today, P.Fajr));
        Add(P.Sunrise, Find(today, P.Sunrise), duhaSub ? Find(today, P.Duha) : null);
        if (!IsShown(P.Sunrise)) Add(P.Duha, Find(today, P.Duha));
        foreach (var p in new[] { P.Dhuhr, P.Asr, P.Maghrib, P.Isha }) Add(p, Find(today, p));
        // Tahajjud goes last: tonight's, unless we are still in the night before today's Fajr.
        var tomorrow = Data.Times(DateOnly.FromDateTime(now).AddDays(1), Get, s);
        Add(P.Tahajjud, now < Find(today, P.Fajr) ? Find(today, P.Tahajjud) : Find(tomorrow, P.Tahajjud));
        // Current period = last passed time today (Duha is a sub-line, not a period); before the first one it's still Isha.
        current = today.LastOrDefault(x => x.P != P.Duha && x.At <= now) is { At.Year: > 1 } c ? c.P : P.Isha;
    }

    int HeaderLines => 3 + (holiday != null ? 1 : 0);
    int CardHeight => 6 + Math.Max(rows.Sum(r => r.H), 60) + 4 + 38;
    const int TopStrip = 24; // free space above the header for the minimize/fullscreen/close buttons
    int ContentHeight => TopStrip + 6 + HeaderLines * 19 + 8 + CardHeight + 12; // at 96 dpi, zoom 100
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

    void SetWidgetVisible(bool v)
    {
        s.WidgetVisible = v; Data.Save(s);
        if (v) { Show(); Activate(); } else Hide();
    }

    void Notify(string title, string text)
    {
        if (!s.Muted) tray.ShowBalloonTip(10000, title, text, ToolTipIcon.None);
    }

    /// Sample alert for the next prayer, exactly as a real one looks (ignores mute).
    public void TestAlert()
    {
        var (p, t) = next ?? (P.Fajr, DateTime.Today.AddHours(5));
        tray.ShowBalloonTip(10000, $"{L.Name(p)} — {t:HH:mm}", $"{L.T("PrayerTime")} · {s.City.Title}", ToolTipIcon.None);
    }

    void Tick()
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        today = Data.Times(date, Get, s);
        next = Data.Next(now, Get, IsShown, s);
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
                Notify(L.T("Jumuah"), $"{L.Name(P.Dhuhr)} {t:HH:mm} · {place}");
            if (!s.Alerts.Contains(p)) continue;
            if (Crossed(t))
                Notify($"{L.Name(p)} — {t:HH:mm}", $"{L.T("PrayerTime")} · {place}");
            if (s.RemindBefore > 0 && Crossed(t.AddMinutes(-s.RemindBefore)))
                Notify(string.Format(L.T("InMin"), L.Name(p), s.RemindBefore), $"{t:HH:mm} · {place}");
        }
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
            if (await Updates.Check() is not { } u || updateUrl == u.Url) return;
            updateUrl = u.Url;
            updateItem.Text = string.Format(L.T("Update"), "v" + u.Version.ToString(3));
            updateItem.Visible = true;
            tray.ShowBalloonTip(10000, "Namaz Times KZ", string.Format(L.T("UpdateAvail"), "v" + u.Version.ToString(3)), ToolTipIcon.None);
        }
        catch { /* offline or rate-limited: try again tomorrow */ }
    }

    static string Fmt(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    // Palette of the KZ mobile prayer apps: night-sky background, grey card, green "next" bar.
    static readonly Color Sky1 = Color.FromArgb(10, 16, 38), Sky2 = Color.FromArgb(27, 36, 66);
    static readonly Color CardFill = Color.FromArgb(242, 44, 46, 54), CardBorder = Color.FromArgb(70, 255, 255, 255);
    static readonly Color Pill = Color.FromArgb(78, 80, 90), Green = Color.FromArgb(22, 163, 116);
    static readonly Color Grey = Color.FromArgb(142, 142, 147), Speaker = Color.FromArgb(128, 131, 138);

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
        var now = DateTime.Now;
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
        TextRenderer.DrawText(g, now.ToString("HH:mm"), clock, new Point((int)left - Zi(2), oy + Zi(TopStrip + 2)), Color.White);
        float y = oy + Z(TopStrip + 6);
        var city = s.City.Title + (s.Muted ? " 🔕" : "");
        var cityW = TextRenderer.MeasureText(g, city, head, Size.Empty, TextFormatFlags.NoPadding).Width;
        TextRenderer.DrawText(g, city, head, Rectangle.Round(new RectangleF(left, y, right - left, Z(19))), Color.White, Right | TextFormatFlags.EndEllipsis);
        // Location arrow (like iOS "location.fill"); Segoe MDL2 has no such glyph.
        float ax = right - cityW - Z(19), ay = y + Z(4.5f), a = Z(10);
        using (var arrow = new SolidBrush(Color.White))
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
            if (r.Duha is { } duha)
            {
                TextRenderer.DrawText(g, L.Name(r.P), name, Rectangle.Round(new RectangleF(rx, ry + Z(4), rr - rx, Z(24))), Color.White, Left);
                TextRenderer.DrawText(g, $"{L.Name(P.Duha)} {duha:HH:mm}", small, Rectangle.Round(new RectangleF(rx, ry + Z(26), rr - rx, Z(18))), Grey, Left);
            }
            else TextRenderer.DrawText(g, L.Name(r.P), name, Rectangle.Round(new RectangleF(rx, ry, rr - rx, h)), Color.White, Left);

            // Times sit centred in a fixed-width box at the right; the current one gets a pill of exactly that box.
            var boxW = TextRenderer.MeasureText(g, "00:00", time, Size.Empty, TextFormatFlags.NoPadding).Width + Z(14);
            var box = new RectangleF(rr - boxW + Z(6), ry + (h - Z(28)) / 2, boxW, Z(28));
            if (r.P == current)
            {
                using var pill = Theme.RoundRect(box, Z(7));
                using var pb = new SolidBrush(Pill);
                g.FillPath(pb, pill);
            }
            TextRenderer.DrawText(g, r.At.ToString("HH:mm"), time, Rectangle.Round(box), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // Speaker: click toggles this prayer's notification.
            var on = s.Alerts.Contains(r.P);
            var icon = new RectangleF(rr - Z(92), ry, Z(24), h);
            TextRenderer.DrawText(g, on ? "" : "", icons, Rectangle.Round(icon), // Volume3 / Volume0
                on && !s.Muted ? Color.FromArgb(205, 208, 214) : Speaker, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            bells.Add((icon, r.P));
            ry += h;
        }

        // Green bar: next prayer and countdown, clipped to the card's rounded bottom.
        if (next is { } n)
        {
            var bar = new RectangleF(card.X, card.Bottom - Z(38), card.Width, Z(38));
            g.SetClip(cardPath);
            using (var gb = new SolidBrush(Green)) g.FillRectangle(gb, bar);
            g.ResetClip();
            TextRenderer.DrawText(g, L.Name(n.P), name, Rectangle.Round(new RectangleF(rx, bar.Y, rr - rx, bar.Height)), Color.White, Left);
            TextRenderer.DrawText(g, Fmt(n.At - now), time, Rectangle.Round(new RectangleF(rx, bar.Y, rr - rx, bar.Height)), Color.White, Right);
        }
        using (var border = new Pen(CardBorder, Math.Max(1, Z(1)))) g.DrawPath(border, cardPath);

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
        switch (hover ? Array.FindIndex(captions, r => r.Contains(e.Location)) : -1)
        {
            case 0: WindowState = FormWindowState.Minimized; return;
            case 1: ToggleFull(); return;
            case 2: if (full) ToggleFull(); SetWidgetVisible(false); return;
        }
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
        Cursor = h >= 0 || bells.Any(b => b.R.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = false; hot = -1;
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

    void OpenSettings()
    {
        if (settingsForm is { IsDisposed: false }) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(s, TestAlert) { TopMost = TopMost };
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
