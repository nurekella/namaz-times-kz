using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NamazTimes;

public class Widget : Form
{
    readonly Settings s;
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    readonly ToolStripMenuItem showItem, settingsItem, exitItem;
    DateTime lastTick = DateTime.Now;
    List<(P P, DateTime At)>? today;
    (P P, DateTime At)? next;

    public Widget(Settings settings)
    {
        s = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 28, 34);
        Text = "Namaz Times KZ";
        Icon = new(typeof(Widget).Assembly.GetManifestResourceStream("app.ico")!, SystemInformation.SmallIconSize);

        var menu = new ContextMenuStrip();
        showItem = new ToolStripMenuItem("", null, (_, _) => SetWidgetVisible(!Visible));
        settingsItem = new ToolStripMenuItem("", null, (_, _) => OpenSettings());
        exitItem = new ToolStripMenuItem("", null, (_, _) => { tray!.Visible = false; Application.Exit(); });
        menu.Items.AddRange([showItem, settingsItem, new ToolStripSeparator(), exitItem]);
        menu.Opening += (_, _) => showItem.Checked = Visible;
        ContextMenuStrip = menu;

        tray = new NotifyIcon { Icon = Icon, Text = Text, ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) SetWidgetVisible(!Visible); };

        ApplySettings();
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    Day? Get(DateOnly d) => Data.GetDay(s.City, d);
    bool IsShown(P p) => !s.Hidden.Contains(p);
    int Scale(int v) => v * DeviceDpi / 96;

    public void ApplySettings()
    {
        L.Lang = s.Lang;
        showItem.Text = L.T("ShowWidget");
        settingsItem.Text = L.T("Settings");
        exitItem.Text = L.T("Exit");
        TopMost = s.TopMost;
        Opacity = Math.Clamp(s.Opacity, 30, 100) / 100.0;
        if (IsHandleCreated) Relayout();
        Tick();
    }

    void Relayout()
    {
        var rows = Enum.GetValues<P>().Count(IsShown);
        ClientSize = new Size(Scale(230), Scale(38) + Scale(26) * Math.Max(rows, 2));
        Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Scale(14), Scale(14)));
    }

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(value && s.WidgetVisible);

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Relayout();
        var wa = Screen.PrimaryScreen!.WorkingArea;
        var pos = new Point(s.X, s.Y);
        Location = Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(pos)) ? pos
            : new Point(wa.Right - Width - Scale(16), wa.Top + Scale(16));
    }

    void SetWidgetVisible(bool v)
    {
        s.WidgetVisible = v; Data.Save(s);
        if (v) { Show(); Activate(); } else Hide();
    }

    void Tick()
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        today = Data.Times(date, Get);
        next = Data.Next(now, Get, IsShown);

        // Fire alerts for moments crossed since last tick; skip stale ones (e.g. after sleep).
        bool Crossed(DateTime t) => lastTick < t && t <= now && now - t < TimeSpan.FromMinutes(5);
        foreach (var (p, t) in (today ?? []).Concat(Data.Times(date.AddDays(1), Get) ?? []))
        {
            if (!s.Alerts.Contains(p)) continue;
            if (Crossed(t))
                tray.ShowBalloonTip(10000, $"{L.Name(p)} — {t:HH:mm}", $"{L.T("PrayerTime")} · {s.City.Title}", ToolTipIcon.Info);
            if (s.RemindBefore > 0 && Crossed(t.AddMinutes(-s.RemindBefore)))
                tray.ShowBalloonTip(10000, string.Format(L.T("InMin"), L.Name(p), s.RemindBefore), $"{t:HH:mm} · {s.City.Title}", ToolTipIcon.Info);
        }
        lastTick = now;

        if (next is { } n)
        {
            var tip = string.Format(L.T("NextIn"), L.Name(n.P), Fmt(n.At - now));
            tray.Text = tip.Length > 63 ? tip[..63] : tip;
        }
        if (Visible) Invalidate();
    }

    static string Fmt(TimeSpan t) => $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var small = new Font("Segoe UI", 8.5f);
        using var normal = new Font("Segoe UI", 10f);
        using var bold = new Font("Segoe UI Semibold", 10f);
        var muted = Color.FromArgb(150, 160, 175);
        var accent = Color.FromArgb(94, 196, 140);
        int pad = Scale(12), y = Scale(10), w = ClientSize.Width;

        TextRenderer.DrawText(g, s.City.Title, small, new Rectangle(pad, y, w - Scale(90), Scale(18)), muted, TextFormatFlags.EndEllipsis);
        if (next is { } n)
            TextRenderer.DrawText(g, "−" + Fmt(n.At - DateTime.Now), small, new Rectangle(0, y, w - pad, Scale(18)), accent, TextFormatFlags.Right);
        y += Scale(24);

        if (today == null)
        {
            TextRenderer.DrawText(g, L.T("NoData"), normal, new Point(pad, y), Color.White);
            return;
        }
        var rowH = Scale(26);
        foreach (var (p, t) in today.Where(x => IsShown(x.P)))
        {
            var isNext = next is { } nn && nn.P == p && nn.At == t;
            if (isNext)
            {
                using var b = new SolidBrush(Color.FromArgb(40, accent));
                using var path = RoundRect(new Rectangle(Scale(6), y - Scale(3), w - Scale(12), rowH - Scale(2)), Scale(6));
                g.FillPath(b, path);
            }
            var main = p is P.Fajr or P.Dhuhr or P.Asr or P.Maghrib or P.Isha;
            var color = isNext ? accent : main ? Color.White : muted;
            var font = isNext ? bold : normal;
            TextRenderer.DrawText(g, L.Name(p), font, new Point(pad, y), color);
            TextRenderer.DrawText(g, t.ToString("HH:mm"), font, new Rectangle(0, y, w - pad, rowH), color, TextFormatFlags.Right);
            y += rowH;
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

    SettingsForm? settingsForm;

    void OpenSettings()
    {
        if (settingsForm is { IsDisposed: false }) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(s);
        settingsForm.FormClosed += (_, _) =>
        {
            if (settingsForm.DialogResult == DialogResult.OK) { Data.Save(s); ApplySettings(); }
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

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
}
