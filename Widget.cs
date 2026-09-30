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

    const int BaseWidth = 250;

    public Widget(Settings settings)
    {
        s = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        Text = "Namaz Times KZ";
        Icon = Theme.AppIcon(SystemInformation.SmallIconSize);

        var menu = new ContextMenuStrip();
        showItem = new ToolStripMenuItem("", null, (_, _) => SetWidgetVisible(!Visible));
        monthItem = new ToolStripMenuItem("", null, (_, _) => Open(ref monthForm, () => new MonthForm(s)));
        holidaysItem = new ToolStripMenuItem("", null, (_, _) => Open(ref holidaysForm, () => new HolidaysForm(s)));
        muteItem = new ToolStripMenuItem("", null, (_, _) => { s.Muted = !s.Muted; Data.Save(s); Invalidate(); });
        settingsItem = new ToolStripMenuItem("", null, (_, _) => OpenSettings());
        updateItem = new ToolStripMenuItem("", null, (_, _) => Process.Start(new ProcessStartInfo(updateUrl!) { UseShellExecute = true })) { Visible = false };
        exitItem = new ToolStripMenuItem("", null, (_, _) => { tray!.Visible = false; Application.Exit(); });
        menu.Items.AddRange([showItem, monthItem, holidaysItem, new ToolStripSeparator(), muteItem, settingsItem, updateItem,
            new ToolStripSeparator(), exitItem]);
        menu.Opening += (_, _) => { showItem.Checked = Visible; muteItem.Checked = s.Muted; };
        ContextMenuStrip = menu;

        tray = new NotifyIcon { Icon = Icon, Text = Text, ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) SetWidgetVisible(!Visible); };

        ApplySettings();
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    Day? Get(DateOnly d) => Data.GetDay(s.City, d);
    bool IsShown(P p) => !s.Hidden.Contains(p);
    float Z(float v) => v * DeviceDpi / 96f * s.Zoom / 100f;
    int Zi(float v) => (int)Math.Round(Z(v));
    Font F(float pt, FontStyle st = FontStyle.Regular) => new(st == FontStyle.Bold ? "Segoe UI Semibold" : "Segoe UI", pt * s.Zoom / 100f);

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

    int HeaderLines => 3 + (holiday != null ? 1 : 0);
    int Rows => Enum.GetValues<P>().Count(IsShown);
    Size SizeFor() => new(Zi(BaseWidth), Zi(52 + HeaderLines * 18 + 10 + Math.Max(Rows, 2) * 26 + 8));

    void Relayout()
    {
        ClientSize = SizeFor();
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Zi(14), Zi(14)));
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
        if (!s.Muted) tray.ShowBalloonTip(10000, title, text, ToolTipIcon.Info);
    }

    void Tick()
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        today = Data.Times(date, Get, s);
        next = Data.Next(now, Get, IsShown, s);
        if (date != shownDate)
        {
            shownDate = date;
            holiday = Hijri.On(date, s.HijriAdjust);
            if (IsHandleCreated) Relayout();
        }

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
            tray.ShowBalloonTip(10000, "Namaz Times KZ", string.Format(L.T("UpdateAvail"), "v" + u.Version.ToString(3)), ToolTipIcon.Info);
        }
        catch { /* offline or rate-limited: try again tomorrow */ }
    }

    static string Fmt(TimeSpan t) => $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var small = F(8.5f);
        using var normal = F(10f);
        using var bold = F(10f, FontStyle.Bold);
        using var clock = F(20f, FontStyle.Bold);
        using var countdown = F(11f, FontStyle.Bold);
        using var icons = new Font("Segoe MDL2 Assets", 9f * s.Zoom / 100f);
        int pad = Zi(14), w = ClientSize.Width;
        var now = DateTime.Now;

        TextRenderer.DrawText(g, now.ToString("HH:mm"), clock, new Point(pad - Zi(2), Zi(6)), Theme.Text);
        if (next is { } n)
        {
            TextRenderer.DrawText(g, L.Name(n.P), small, new Rectangle(0, Zi(10), w - pad, Zi(18)), Theme.Muted, TextFormatFlags.Right);
            TextRenderer.DrawText(g, "−" + Fmt(n.At - now), countdown, new Rectangle(0, Zi(26), w - pad, Zi(22)), Theme.Accent, TextFormatFlags.Right);
        }

        float y = Z(52);
        void Line(string text, Color c)
        {
            TextRenderer.DrawText(g, text, small, new Rectangle(pad, (int)y, w - 2 * pad, Zi(18)), c, TextFormatFlags.EndEllipsis);
            y += Z(18);
        }
        var greg = now.ToString("dddd, d MMMM yyyy", L.Culture);
        Line(char.ToUpper(greg[0], L.Culture) + greg[1..], Theme.Muted);
        Line(Hijri.Format(DateOnly.FromDateTime(now), s.HijriAdjust), Theme.Muted);
        Line(s.City.Title + (s.Muted ? "  ·  🔕" : ""), Theme.Muted);
        if (holiday != null) Line("✦ " + L.T(holiday.Key), Theme.Accent);

        y += Z(4);
        using (var pen = new Pen(Theme.Line, Math.Max(1, Z(1)))) g.DrawLine(pen, pad, y, w - pad, y);
        y += Z(6);

        bells.Clear();
        if (today == null)
        {
            TextRenderer.DrawText(g, L.T("NoData"), normal, new Point(pad, (int)y), Theme.Text);
            return;
        }
        var rowH = Z(26);
        var bellW = Z(22);
        foreach (var (p, t) in today.Where(x => IsShown(x.P)))
        {
            var isNext = next is { } nn && nn.P == p && nn.At == t;
            if (isNext)
            {
                using var b = new SolidBrush(Color.FromArgb(40, Theme.Accent));
                using var path = Theme.RoundRect(new RectangleF(Z(6), y - Z(2), w - Z(12), rowH - Z(2)), Z(6));
                g.FillPath(b, path);
            }
            var main = p is P.Fajr or P.Dhuhr or P.Asr or P.Maghrib or P.Isha;
            var color = isNext ? Theme.Accent : main ? Theme.Text : Theme.Muted;
            var font = isNext ? bold : normal;
            var row = new Rectangle(pad, (int)y, w - 2 * pad, (int)rowH);
            TextRenderer.DrawText(g, L.Name(p), font, row, color, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, t.ToString("HH:mm"), font, new Rectangle(row.X, row.Y, (int)(row.Width - bellW), row.Height), color,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            // Bell: click toggles this prayer's notification.
            var on = s.Alerts.Contains(p);
            var bell = new RectangleF(w - pad - bellW + Z(4), y, bellW, rowH);
            TextRenderer.DrawText(g, on ? "" : "", icons, Rectangle.Round(bell), // Ringer / RingerSilent
                on && !s.Muted ? Theme.Accent : Color.FromArgb(110, Theme.Muted), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            bells.Add((bell, p));
            y += rowH;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        foreach (var (r, p) in bells)
            if (r.Contains(e.Location))
            {
                if (!s.Alerts.Remove(p)) s.Alerts.Add(p);
                Data.Save(s);
                Invalidate();
                return;
            }
        // Drag the borderless window by any other point.
        ReleaseCapture();
        SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, 2 /*HTCAPTION*/, 0);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = bells.Any(b => b.R.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if ((ModifierKeys & Keys.Control) == 0) return;
        s.Zoom = Math.Clamp(s.Zoom + Math.Sign(e.Delta) * 10, 70, 250);
        Data.Save(s);
        Relayout();
    }

    // Bottom-right corner resizes; width drives zoom, height follows the content.
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84, WM_SIZING = 0x214, WM_EXITSIZEMOVE = 0x232, HTBOTTOMRIGHT = 17;
        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST)
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
        if (!IsHandleCreated || !Visible) return;
        s.X = Left; s.Y = Top; Data.Save(s);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; SetWidgetVisible(false); }
        else tray.Visible = false;
        base.OnFormClosing(e);
    }

    static void Open<T>(ref T? form, Func<T> make) where T : Form
    {
        if (form is { IsDisposed: false }) { form.Activate(); return; }
        form = make();
        form.Show();
    }

    void OpenSettings()
    {
        if (settingsForm is { IsDisposed: false }) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(s);
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
