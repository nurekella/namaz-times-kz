using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NamazTimes;

/// Fluent 2 (Windows 11) look for all windows: palette (dark or light, see SetMode), 4 px controls, 8 px cards,
/// and a few small owner-drawn controls. The desktop widget keeps its own night palette (Night*).
public static class Theme
{
    public static Color Bg, Card, Line, Text, Muted, Accent, OnAccent, Gold, Faded, Marked;
    // WinUI control fills and the elevation border of buttons
    public static Color CtlFill, CtlHover, CtlPressed, CtlStroke, CtlStrokeBottom;
    public static bool IsDark = true;
    public static readonly Color NightBg = Color.FromArgb(24, 28, 34), NightLine = Color.FromArgb(48, 54, 64), NightAccent = Color.FromArgb(94, 196, 140);

    static Theme() => SetMode(1);

    /// 0 = follow Windows, 1 = dark, 2 = light. Takes effect for windows opened afterwards.
    public static void SetMode(int mode)
    {
        IsDark = mode switch { 1 => true, 2 => false, _ => SystemDark() };
        if (IsDark)
        {
            // Mica base, card layer, subtle stroke / hover fill
            (Bg, Card, Line) = (Color.FromArgb(32, 32, 32), Color.FromArgb(43, 43, 43), Color.FromArgb(58, 58, 58));
            (Text, Muted, Accent, OnAccent) = (Color.White, Color.FromArgb(170, 170, 170), Color.FromArgb(108, 203, 143), Color.FromArgb(10, 30, 18));
            (Gold, Faded, Marked) = (Color.FromArgb(230, 190, 110), Color.FromArgb(110, 110, 110), Color.FromArgb(47, 56, 50));
            (CtlFill, CtlHover, CtlPressed) = (Color.FromArgb(45, 45, 45), Color.FromArgb(50, 50, 50), Color.FromArgb(39, 39, 39));
            (CtlStroke, CtlStrokeBottom) = (Color.FromArgb(55, 55, 55), Color.FromArgb(48, 48, 48));
        }
        else
        {
            (Bg, Card, Line) = (Color.FromArgb(243, 243, 243), Color.FromArgb(251, 251, 251), Color.FromArgb(229, 229, 229));
            (Text, Muted, Accent, OnAccent) = (Color.FromArgb(27, 27, 27), Color.FromArgb(97, 97, 97), Color.FromArgb(15, 123, 70), Color.White);
            (Gold, Faded, Marked) = (Color.FromArgb(154, 103, 0), Color.FromArgb(168, 168, 168), Color.FromArgb(232, 243, 236));
            (CtlFill, CtlHover, CtlPressed) = (Color.FromArgb(251, 251, 251), Color.FromArgb(246, 246, 246), Color.FromArgb(245, 245, 245));
            (CtlStroke, CtlStrokeBottom) = (Color.FromArgb(229, 229, 229), Color.FromArgb(204, 204, 204));
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
    // Windows 11 icon font (same code points as Segoe MDL2 Assets, which Windows 10 has)
    public static readonly string IconFamily = FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    public static readonly bool Variable = FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text");
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
            using var font = new Font(IconFamily, 34, GraphicsUnit.Pixel);
            using var fg = new SolidBrush(NightBg);
            g.DrawString(glyph.ToString(), font, fg, new RectangleF(0, 2, size, size),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// Rounds a panel's corners (clipping region, kept in step with its size).
    public const int CardRadius = 8, ControlRadius = 4; // Fluent 2

    public static T Round<T>(T c, int radius = CardRadius) where T : Control
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

    public static RoundButton Button(string text, bool primary = false, char glyph = '\0', char trailing = '\0') => new()
    {
        Text = text, Glyph = glyph, Trailing = trailing, AutoSize = true, Cursor = Cursors.Hand,
        BackColor = primary ? Accent : Card, ForeColor = primary ? OnAccent : Text, Font = UI(10f),
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
        outer.Controls.Add(Label(title, Text, 11f, FontStyle.Bold)); // Fluent: headings in text colour, accent is for actions
        grid = new TableLayoutPanel { AutoSize = true, ColumnCount = columns, Dock = DockStyle.Fill, Margin = Padding.Empty };
        outer.Controls.Add(grid);
        return outer;
    }

    /// Fluent list row: selection is a soft rounded fill with a short accent pill on the left.
    public static void ListItem(Graphics g, Rectangle b, bool selected)
    {
        using (var bg = new SolidBrush(Card)) g.FillRectangle(bg, b);
        if (!selected) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = RoundRect(new RectangleF(b.X + Dp(4), b.Y + Dp(2), b.Width - Dp(8), b.Height - Dp(4)), Dp(ControlRadius)))
        using (var fill = new SolidBrush(Line)) g.FillPath(fill, path);
        int h = Math.Min(Dp(16), b.Height - Dp(12));
        using (var pill = RoundRect(new RectangleF(b.X + Dp(4), b.Y + (b.Height - h) / 2f, Dp(3), h), Dp(1)))
        using (var accent = new SolidBrush(Accent)) g.FillPath(accent, pill);
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
        using var f = new Font(IconFamily, size * 0.62f, GraphicsUnit.Pixel);
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
                using var path = RoundRect(new RectangleF(Dp(2), 1, e.Item.Width - Dp(4), e.Item.Height - 2), Dp(ControlRadius));
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

/// Windows 11 (WinUI) button: 32 px tall, 4 px corners, elevation stroke (bottom edge stronger),
/// hover and pressed fills, optional icon before the text and a chevron after it.
/// BackColor == Theme.Accent makes it an accent button.
public class RoundButton : Button
{
    bool hot, down;
    /// Segoe Fluent Icons / MDL2 glyph shown before the text (or alone).
    public char Glyph;
    /// Small glyph after the text, e.g. a chevron for buttons that open a menu or a picker.
    public char Trailing;

    public RoundButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = down = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }

    Font IconFont => icon ??= new Font(Theme.IconFamily, Font.SizeInPoints * 0.92f);
    Font SmallIcon => small ??= new Font(Theme.IconFamily, Font.SizeInPoints * 0.62f);
    Font? icon, small;
    protected override void OnFontChanged(EventArgs e) { icon?.Dispose(); small?.Dispose(); icon = small = null; base.OnFontChanged(e); }

    int GlyphW => Glyph == '\0' ? 0 : Theme.Dp(16) + (Text.Length > 0 ? Theme.Dp(8) : 0);
    int TrailW => Trailing == '\0' ? 0 : Theme.Dp(8) + Theme.Dp(12);

    public override Size GetPreferredSize(Size proposed)
    {
        var text = Text.Length > 0 ? TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine) : Size.Empty;
        var pad = Text.Length > 0 ? Theme.Dp(12) : Theme.Dp(8);
        return new Size(Math.Max(Theme.Dp(32), pad * 2 + GlyphW + text.Width + TrailW), Math.Max(Theme.Dp(32), text.Height + Theme.Dp(12)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var accent = BackColor == Theme.Accent;
        Color fill, fg;
        if (!Enabled) (fill, fg) = (Theme.CtlFill, Theme.Faded);
        else if (accent)
        {
            // WinUI accent: 90 % on hover, 80 % while pressed
            fill = down ? Blend(Theme.Accent, Theme.Bg, 0.8f) : hot ? Blend(Theme.Accent, Theme.Bg, 0.9f) : Theme.Accent;
            fg = down ? Blend(Theme.OnAccent, Theme.Accent, 0.8f) : Theme.OnAccent;
        }
        else (fill, fg) = (down ? Theme.CtlPressed : hot ? Theme.CtlHover : Theme.CtlFill, down ? Theme.Muted : ForeColor);

        int rad = Theme.Dp(Theme.ControlRadius);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundRect(r, rad);
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        if (!down)
        {
            // Elevation border: hairline all round, a stronger line along the bottom
            if (!accent) using (var p = new Pen(Theme.CtlStroke)) g.DrawPath(p, path);
            using var bottom = new Pen(accent ? Blend(Color.Black, Theme.Accent, Theme.IsDark ? 0.15f : 0.25f) : Theme.CtlStrokeBottom);
            g.DrawLine(bottom, rad, Height - 1, Width - rad - 1, Height - 1);
        }

        // Icon + text + chevron, centred as one group
        var textW = Text.Length > 0 ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width : 0;
        var x = (Width - (GlyphW + textW + TrailW)) / 2;
        if (Glyph != '\0')
        {
            TextRenderer.DrawText(g, Glyph.ToString(), IconFont, new Rectangle(x, 0, Theme.Dp(16), Height), fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += GlyphW;
        }
        if (textW > 0)
        {
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, textW + 2, Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            x += textW;
        }
        if (Trailing != '\0')
            TextRenderer.DrawText(g, Trailing.ToString(), SmallIcon, new Rectangle(x + Theme.Dp(8), 0, Theme.Dp(12), Height), accent ? fg : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    static Color Blend(Color a, Color b, float k) =>
        Color.FromArgb((int)(a.R * k + b.R * (1 - k)), (int)(a.G * k + b.G * (1 - k)), (int)(a.B * k + b.B * (1 - k)));
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
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Dp(Theme.ControlRadius));
        using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
        using (var p = new Pen(Theme.Line)) g.DrawPath(p, path);
        // underline: grey at rest, 2 px accent while typing
        var focused = Box.Focused;
        using (var u = new Pen(focused ? Theme.Accent : Theme.Muted, focused ? Theme.Dp(2) : 1))
            g.DrawLine(u, Theme.Dp(Theme.ControlRadius), Height - (focused ? Theme.Dp(1) : 1), Width - Theme.Dp(Theme.ControlRadius), Height - (focused ? Theme.Dp(1) : 1));
        using var icon = new Font(Theme.IconFamily, 10f * Theme.UiScale);
        TextRenderer.DrawText(g, "", icon, new Rectangle(Theme.Dp(10), 0, Theme.Dp(20), Height), Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }
}

/// Fluent toggle switch: outlined track with a grey knob when off, accent track when on.
public class Toggle : Control
{
    bool on;
    public event EventHandler? CheckedChanged;

    public Toggle(bool value = false)
    {
        on = value;
        Size = new Size(Theme.Dp(40), Theme.Dp(20));
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
        if (on) using (var fill = new SolidBrush(Theme.Accent)) g.FillPath(fill, track);
        else using (var stroke = new Pen(Theme.Muted)) g.DrawPath(stroke, track);
        var d = r.Height - Theme.Dp(8);
        var x = on ? r.Right - d - Theme.Dp(4) : r.X + Theme.Dp(4);
        using var knob = new SolidBrush(on ? Theme.OnAccent : Theme.Muted);
        g.FillEllipse(knob, x, r.Y + Theme.Dp(4), d, d);
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
            AutoSize = false, Width = Theme.Dp(64), Height = Theme.Dp(32), TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Text, Font = Theme.UI(10f), Margin = Padding.Empty,
        };
        Controls.AddRange([Btn('\uE738', -1), text, Btn('\uE710', 1)]); // Remove, Add
        Value = value;
    }

    Button Btn(char glyph, int dir)
    {
        var b = Theme.Button("", glyph: glyph);
        b.AutoSize = false; b.Size = new Size(Theme.Dp(32), Theme.Dp(32)); b.Margin = Padding.Empty;
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
        Theme.Round(this, 6);
    }

    public int Selected
    {
        get => items.FindIndex(i => i.Checked);
        set => items[Math.Clamp(value, 0, items.Count - 1)].Checked = true;
    }

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
                using var pill = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Dp(Theme.ControlRadius));
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
