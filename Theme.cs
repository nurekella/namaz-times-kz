using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NamazTimes;

/// Dark palette shared by the widget and all windows, plus a few small owner-drawn controls.
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(24, 28, 34);
    public static readonly Color Card = Color.FromArgb(33, 38, 46);
    public static readonly Color Line = Color.FromArgb(48, 54, 64);
    public static readonly Color Text = Color.FromArgb(235, 238, 242);
    public static readonly Color Muted = Color.FromArgb(150, 160, 175);
    public static readonly Color Accent = Color.FromArgb(94, 196, 140);

    static readonly int Dpi = (int)Graphics.FromHwnd(IntPtr.Zero).DpiX;
    public static int Dp(int v) => v * Dpi / 96;

    public static Font UI(float size = 10f, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);

    public static Icon AppIcon(Size? size = null) =>
        new(typeof(Theme).Assembly.GetManifestResourceStream("app.ico")!, size ?? SystemInformation.IconSize);

    /// Dark window chrome + colours for a top-level form.
    public static void Apply(Form f)
    {
        f.BackColor = Bg;
        f.ForeColor = Text;
        f.Font = UI();
        f.Icon = AppIcon();
        f.HandleCreated += (_, _) =>
        {
            int on = 1;
            DwmSetWindowAttribute(f.Handle, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, ref on, sizeof(int));
        };
    }

    public static Label Label(string text, Color? color = null, float size = 10f, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, AutoSize = true, ForeColor = color ?? Text, Font = UI(size, style),
        Anchor = AnchorStyles.Left, Margin = new Padding(0, Dp(7), Dp(12), Dp(7)),
    };

    public static Button Button(string text, bool primary = false)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = primary ? Accent : Card, ForeColor = primary ? Bg : Text,
            Font = UI(10f, primary ? FontStyle.Bold : FontStyle.Regular),
            Padding = new Padding(Dp(10), Dp(3), Dp(10), Dp(3)),
        };
        b.FlatAppearance.BorderColor = primary ? Accent : Line;
        b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(Accent) : Line;
        return b;
    }

    /// Rounded "card" panel holding a two-column grid of rows.
    public static TableLayoutPanel Section(string title, out TableLayoutPanel grid, int columns = 2)
    {
        var outer = new TableLayoutPanel
        {
            AutoSize = true, ColumnCount = 1, BackColor = Card, Dock = DockStyle.Top,
            Padding = new Padding(Dp(14), Dp(10), Dp(14), Dp(10)), Margin = new Padding(0, 0, 0, Dp(12)),
        };
        outer.Controls.Add(Label(title, Accent, 10.5f, FontStyle.Bold));
        grid = new TableLayoutPanel { AutoSize = true, ColumnCount = columns, Dock = DockStyle.Fill, Margin = Padding.Empty };
        outer.Controls.Add(grid);
        return outer;
    }

    public static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// Windows 10/11 dark scrollbars for standard controls.
    public static void DarkScrollbars(Control c) => SetWindowTheme(c.Handle, "DarkMode_Explorer", null);

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string? app, string? idList);
}

/// iOS-style on/off switch.
public class Toggle : Control
{
    bool on;
    public event EventHandler? CheckedChanged;

    public Toggle(bool value = false)
    {
        on = value;
        Size = new Size(Theme.Dp(40), Theme.Dp(22));
        Margin = new Padding(0, Theme.Dp(4), Theme.Dp(4), Theme.Dp(4));
        Anchor = AnchorStyles.None;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    public bool Checked
    {
        get => on;
        set { if (on == value) return; on = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var track = Theme.RoundRect(r, r.Height / 2);
        using var fill = new SolidBrush(on ? Theme.Accent : Theme.Line);
        g.FillPath(fill, track);
        var d = r.Height - Theme.Dp(6);
        var x = on ? r.Right - d - Theme.Dp(3) : r.X + Theme.Dp(3);
        using var knob = new SolidBrush(on ? Theme.Bg : Theme.Muted);
        g.FillEllipse(knob, x, r.Y + Theme.Dp(3), d, d);
    }
}

/// "− value +" numeric picker.
public class Stepper : FlowLayoutPanel
{
    readonly Label text;
    int value;
    public int Min, Max, Step;
    public Func<int, string> Format = v => v.ToString();

    public Stepper(int value, int min, int max, int step = 1, Func<int, string>? format = null)
    {
        (Min, Max, Step) = (min, max, step);
        if (format != null) Format = format;
        AutoSize = true; WrapContents = false; Margin = new Padding(0, Theme.Dp(2), 0, Theme.Dp(2));
        Anchor = AnchorStyles.Left;
        text = new Label
        {
            AutoSize = false, Width = Theme.Dp(64), Height = Theme.Dp(28), TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Text, Font = Theme.UI(10f), Margin = Padding.Empty,
        };
        Controls.AddRange([Btn("−", -1), text, Btn("+", 1)]);
        Value = value;
    }

    Button Btn(string s, int dir)
    {
        var b = Theme.Button(s);
        b.AutoSize = false; b.Size = new Size(Theme.Dp(28), Theme.Dp(28)); b.Padding = Padding.Empty; b.Margin = Padding.Empty;
        b.Click += (_, _) => Value += dir * Step;
        return b;
    }

    public int Value
    {
        get => value;
        set { this.value = Math.Clamp(value, Min, Max); text.Text = Format(this.value); }
    }
}

/// Row of mutually exclusive flat buttons.
public class Segmented : FlowLayoutPanel
{
    readonly List<RadioButton> items = [];

    public Segmented(IEnumerable<string> options, int selected)
    {
        AutoSize = true; WrapContents = false; Margin = new Padding(0, Theme.Dp(2), 0, Theme.Dp(2)); Anchor = AnchorStyles.Left;
        foreach (var o in options)
        {
            var rb = new RadioButton
            {
                Text = o, Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter, Margin = Padding.Empty, Cursor = Cursors.Hand,
                Padding = new Padding(Theme.Dp(8), Theme.Dp(2), Theme.Dp(8), Theme.Dp(2)),
            };
            rb.FlatAppearance.BorderColor = Theme.Line;
            rb.FlatAppearance.CheckedBackColor = Theme.Accent;
            rb.FlatAppearance.MouseOverBackColor = Theme.Line;
            rb.CheckedChanged += (_, _) => Style(rb);
            items.Add(rb);
            Controls.Add(rb);
        }
        items[Math.Clamp(selected, 0, items.Count - 1)].Checked = true;
        items.ForEach(Style);
    }

    static void Style(RadioButton rb)
    {
        rb.BackColor = rb.Checked ? Theme.Accent : Theme.Card;
        rb.ForeColor = rb.Checked ? Theme.Bg : Theme.Text;
    }

    public int Selected => items.FindIndex(i => i.Checked);
}
