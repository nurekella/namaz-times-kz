using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NamazTimes;

/// Palette shared by all windows (dark or light, see SetMode), plus a few small owner-drawn controls.
/// The desktop widget keeps its own night palette (Night*).
public static class Theme
{
    public static Color Bg, Card, Line, Text, Muted, Accent, OnAccent, Gold, Faded, Marked;
    public static bool IsDark = true;
    public static readonly Color NightBg = Color.FromArgb(24, 28, 34), NightLine = Color.FromArgb(48, 54, 64), NightAccent = Color.FromArgb(94, 196, 140);

    static Theme() => SetMode(1);

    /// 0 = follow Windows, 1 = dark, 2 = light. Takes effect for windows opened afterwards.
    public static void SetMode(int mode)
    {
        IsDark = mode switch { 1 => true, 2 => false, _ => SystemDark() };
        if (IsDark)
        {
            (Bg, Card, Line) = (NightBg, Color.FromArgb(33, 38, 46), NightLine);
            (Text, Muted, Accent, OnAccent) = (Color.FromArgb(235, 238, 242), Color.FromArgb(150, 160, 175), NightAccent, NightBg);
            (Gold, Faded, Marked) = (Color.FromArgb(230, 190, 110), Color.FromArgb(96, 105, 120), Color.FromArgb(38, 48, 42));
        }
        else
        {
            (Bg, Card, Line) = (Color.FromArgb(243, 244, 246), Color.White, Color.FromArgb(222, 226, 231));
            (Text, Muted, Accent, OnAccent) = (Color.FromArgb(28, 32, 38), Color.FromArgb(100, 108, 120), Color.FromArgb(26, 140, 88), Color.White);
            (Gold, Faded, Marked) = (Color.FromArgb(166, 112, 14), Color.FromArgb(170, 176, 186), Color.FromArgb(232, 245, 236));
        }
    }

    static bool SystemDark()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is not int light || light == 0;
        }
        catch { return true; }
    }

    static readonly int Dpi = (int)Graphics.FromHwnd(IntPtr.Zero).DpiX;
    /// Scale of all windows (Settings → General → Window scale); applied when a window is built.
    public static float UiScale = 1f;
    public static int Dp(int v) => (int)Math.Round(v * Dpi / 96f * UiScale);

    // Windows 11 ships Segoe UI Variable, which reads better; Windows 10 falls back to Segoe UI.
    static readonly bool Variable = FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text");
    public static Font UI(float size = 10f, FontStyle style = FontStyle.Regular) => Variable
        ? new(style.HasFlag(FontStyle.Bold) ? "Segoe UI Variable Text Semibold" : "Segoe UI Variable Text", size * UiScale, style & ~FontStyle.Bold)
        : new("Segoe UI", size * UiScale, style);

    public static Icon AppIcon(Size? size = null) =>
        new(typeof(Theme).Assembly.GetManifestResourceStream("app.ico")!, size ?? SystemInformation.IconSize);

    /// Window chrome in the theme's colours (Windows 11 colours the title bar too), an icon for the window
    /// (the feature's glyph on a green tile, or the app icon), and a short fade-in.
    public static void Apply(Form f, char glyph = '\0')
    {
        f.BackColor = Bg;
        f.ForeColor = Text;
        f.Font = UI();
        f.Icon = glyph == '\0' ? AppIcon() : GlyphIcon(glyph);
        f.HandleCreated += (_, _) =>
        {
            int dark = IsDark ? 1 : 0;
            DwmSetWindowAttribute(f.Handle, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, ref dark, sizeof(int));
            int caption = ColorTranslator.ToWin32(Bg), text = ColorTranslator.ToWin32(Text), border = ColorTranslator.ToWin32(Line);
            DwmSetWindowAttribute(f.Handle, 35 /*DWMWA_CAPTION_COLOR*/, ref caption, sizeof(int));
            DwmSetWindowAttribute(f.Handle, 36 /*DWMWA_TEXT_COLOR*/, ref text, sizeof(int));
            DwmSetWindowAttribute(f.Handle, 34 /*DWMWA_BORDER_COLOR*/, ref border, sizeof(int));
        };
        f.Opacity = 0;
        f.Shown += (_, _) =>
        {
            var t = new System.Windows.Forms.Timer { Interval = 15 };
            var start = Environment.TickCount64;
            t.Tick += (_, _) =>
            {
                var k = Math.Min(1.0, (Environment.TickCount64 - start) / 160.0);
                f.Opacity = 1 - Math.Pow(1 - k, 3); // ease-out
                if (k >= 1) t.Dispose();
            };
            t.Start();
        };
    }

    static Icon GlyphIcon(char glyph)
    {
        const int size = 64;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var tile = RoundRect(new RectangleF(2, 2, size - 4, size - 4), 14);
            using var b = new SolidBrush(NightAccent);
            g.FillPath(b, tile);
            using var font = new Font("Segoe MDL2 Assets", 34, GraphicsUnit.Pixel);
            using var fg = new SolidBrush(NightBg);
            g.DrawString(glyph.ToString(), font, fg, new RectangleF(0, 2, size, size),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// Rounds a panel's corners (clipping region, kept in step with its size).
    public static T Round<T>(T c, int radius = 10) where T : Control
    {
        void Clip()
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            using var p = RoundRect(new RectangleF(0, 0, c.Width, c.Height), Dp(radius));
            var old = c.Region;
            c.Region = new Region(p);
            old?.Dispose();
        }
        c.SizeChanged += (_, _) => Clip();
        Clip();
        return c;
    }

    public static Label Label(string text, Color? color = null, float size = 10f, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, AutoSize = true, ForeColor = color ?? Text, Font = UI(size, style),
        Anchor = AnchorStyles.Left, Margin = new Padding(0, Dp(7), Dp(12), Dp(7)),
    };

    public static Button Button(string text, bool primary = false) => new RoundButton
    {
        Text = text, AutoSize = true, Cursor = Cursors.Hand,
        BackColor = primary ? Accent : Card, ForeColor = primary ? OnAccent : Text,
        Font = UI(10f, primary ? FontStyle.Bold : FontStyle.Regular),
        Padding = new Padding(Dp(10), Dp(3), Dp(10), Dp(3)),
    };

    /// On/off switch with a label; clicking the label toggles it too.
    public static FlowLayoutPanel Switch(string text, bool value, out Toggle toggle)
    {
        var t = toggle = new Toggle(value);
        var l = Label(text, Text, 10f);
        l.Margin = new Padding(Dp(4), Dp(5), 0, 0);
        l.Cursor = Cursors.Hand;
        l.Click += (_, _) => t.Checked = !t.Checked;
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(Dp(6), Dp(6), Dp(10), Dp(2)) };
        p.Controls.AddRange([t, l]);
        return p;
    }

    /// Rounded "card" panel holding a two-column grid of rows.
    public static TableLayoutPanel Section(string title, out TableLayoutPanel grid, int columns = 2)
    {
        var outer = new TableLayoutPanel
        {
            AutoSize = true, ColumnCount = 1, BackColor = Card, Dock = DockStyle.Top,
            Padding = new Padding(Dp(14), Dp(10), Dp(14), Dp(10)), Margin = new Padding(0, 0, 0, Dp(12)),
        };
        Round(outer);
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

    /// Windows 10/11 dark scrollbars for standard controls (light theme keeps the default ones).
    public static void DarkScrollbars(Control c) { if (IsDark) SetWindowTheme(c.Handle, "DarkMode_Explorer", null); }

    /// Context menu in the theme's colours with rounded corners (Windows 11) and icon glyphs.
    public static ContextMenuStrip Menu()
    {
        var m = new ContextMenuStrip
        {
            Renderer = new MenuRenderer(), Font = UI(10f), ShowCheckMargin = false, ShowImageMargin = true,
            Padding = new Padding(Dp(4)), ImageScalingSize = new Size(Dp(18), Dp(18)),
        };
        m.HandleCreated += (_, _) =>
        {
            int round = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(m.Handle, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref round, sizeof(int));
        };
        return m;
    }

    public static ToolStripMenuItem MenuItem(char glyph, EventHandler onClick) => new("", Glyph(glyph, Text), onClick)
    {
        Tag = glyph, Padding = new Padding(0, Dp(5), Dp(8), Dp(5)),
    };

    public static Bitmap Glyph(char glyph, Color color)
    {
        var size = Dp(18);
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; // ClearType would fringe on transparent
        using var f = new Font("Segoe MDL2 Assets", size * 0.62f, GraphicsUnit.Pixel);
        using var b = new SolidBrush(color);
        g.DrawString(glyph.ToString(), f, b, new RectangleF(0, 0, size, size),
            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        return bmp;
    }

    class MenuRenderer : ToolStripProfessionalRenderer
    {

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Card);

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Line);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (e.Item.Selected && e.Item.Enabled)
            {
                using var path = RoundRect(new RectangleF(Dp(2), 1, e.Item.Width - Dp(4), e.Item.Height - 2), Dp(5));
                using var b = new SolidBrush(Line);
                e.Graphics.FillPath(b, path);
            }
        }

        // Checked items ("show widget", "mute") get a green icon on a soft green chip instead of the default blue box.
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Image == null) return;
            if (e.Item is ToolStripMenuItem { Checked: true, Tag: char glyph })
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = RectangleF.Inflate(e.ImageRectangle, Dp(4), Dp(4));
                using var chip = RoundRect(r, Dp(5));
                using var b = new SolidBrush(Color.FromArgb(55, Accent));
                e.Graphics.FillPath(b, chip);
                e.Graphics.DrawImage(Checked.TryGetValue(glyph, out var img) ? img : Checked[glyph] = Glyph(glyph, Accent), e.ImageRectangle);
            }
            else e.Graphics.DrawImage(e.Image, e.ImageRectangle);
        }

        static readonly Dictionary<char, Bitmap> Checked = [];

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // Suppress the default check box; OnRenderItemImage marks checked items.
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Line);
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, Dp(10), y, e.Item.Width - Dp(10), y);
        }
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string? app, string? idList);
}

/// Flat button with rounded corners; hover lightens it. BackColor/ForeColor as with a normal button.
public class RoundButton : Button
{
    bool hot;

    public RoundButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var primary = BackColor == Theme.Accent;
        var fill = !Enabled ? Theme.Card : hot ? (primary ? ControlPaint.Light(BackColor, 0.25f) : Theme.Line) : BackColor;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundRect(r, Theme.Dp(7));
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        if (!primary) using (var p = new Pen(Theme.Line)) g.DrawPath(p, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

/// Text box with a magnifier, rounded corners and no white Windows border.
public class SearchBox : Panel
{
    public readonly TextBox Box;

    public SearchBox(string placeholder)
    {
        Height = Theme.Dp(36);
        Padding = new Padding(Theme.Dp(34), Theme.Dp(8), Theme.Dp(10), 0);
        BackColor = Theme.Card;
        DoubleBuffered = true;
        Box = new TextBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text,
            Font = Theme.UI(10.5f), PlaceholderText = placeholder,
        };
        Controls.Add(Box);
        Cursor = Cursors.IBeam;
        Click += (_, _) => Box.Focus();
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Dp(8));
        using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
        using (var p = new Pen(Box.Focused ? Theme.Accent : Theme.Line)) g.DrawPath(p, path);
        using var icon = new Font("Segoe MDL2 Assets", 10f * Theme.UiScale);
        TextRenderer.DrawText(g, "", icon, new Rectangle(Theme.Dp(10), 0, Theme.Dp(20), Height), Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }
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
        using var knob = new SolidBrush(on ? Theme.OnAccent : Theme.Muted);
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
    public event Action<int>? ValueChanged;

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
        set
        {
            var v = Math.Clamp(value, Min, Max);
            var changed = v != this.value;
            this.value = v;
            text.Text = Format(v);
            if (changed) ValueChanged?.Invoke(v);
        }
    }
}

/// Row of mutually exclusive options in a rounded track; the chosen one is a green pill.
public class Segmented : FlowLayoutPanel
{
    readonly List<Item> items = [];
    public event Action<int>? Changed;

    public Segmented(IEnumerable<string> options, int selected)
    {
        AutoSize = true; WrapContents = false; Margin = new Padding(0, Theme.Dp(2), 0, Theme.Dp(2)); Anchor = AnchorStyles.Left;
        Padding = new Padding(Theme.Dp(3));
        BackColor = Theme.Card;
        DoubleBuffered = true;
        foreach (var o in options)
        {
            var rb = new Item(this)
            {
                Text = o, AutoSize = true, Margin = Padding.Empty, Cursor = Cursors.Hand, Font = Theme.UI(),
                Padding = new Padding(Theme.Dp(8), Theme.Dp(2), Theme.Dp(8), Theme.Dp(2)),
            };
            rb.CheckedChanged += (_, _) => { rb.Invalidate(); if (rb.Checked) Changed?.Invoke(items.IndexOf(rb)); };
            items.Add(rb);
            Controls.Add(rb);
        }
        items[Math.Clamp(selected, 0, items.Count - 1)].Checked = true;
        Theme.Round(this, 9);
    }

    public int Selected => items.FindIndex(i => i.Checked);

    class Item(Segmented owner) : RadioButton
    {
        bool hot;
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(owner.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Checked || hot)
            {
                using var pill = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Dp(6));
                using var b = new SolidBrush(Checked ? Theme.Accent : Theme.Line);
                g.FillPath(b, pill);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Checked ? Theme.OnAccent : Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        // Radio buttons size for their check circle; a pill only needs the text plus padding.
        public override Size GetPreferredSize(Size proposed) =>
            TextRenderer.MeasureText(Text, Font) + new Size(Padding.Horizontal + Theme.Dp(6), Padding.Vertical + Theme.Dp(10));
    }
}
