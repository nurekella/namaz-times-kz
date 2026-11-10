using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NamazTimes;

/// The app's look, after Wintor: Windows 11 structure (Fluent controls, 4 px controls, 8 px cards) on Tailwind "zinc"
/// neutrals instead of Fluent's translucent greys, with the chosen accent colour. Dark or light (SetMode).
public static class Theme
{
    /// Chrome = title bar and navigation pane; Bg = the content layer the pages lie on; Card = cards and tables.
    public static Color Chrome, Bg, Card, Line, Text, Muted, Accent, OnAccent, Gold, Faded, Marked;
    // control fills and strokes
    public static Color CtlFill, CtlHover, CtlPressed, CtlStroke, CtlStrokeBottom;
    public static Color CardStroke, Flyout, NavSelected, Track; // card border, menu background, chosen nav item, empty progress
    public static Color Blue, Green, Neutral; // progress: in progress, done, segmented track
    public static bool IsDark = true;
    /// 0 = the Windows accent, 1 = green, 2 = purple. Read by SetMode.
    public static int AccentChoice;
    public static readonly Color NightBg = Color.FromArgb(24, 24, 27), NightAccent = Color.FromArgb(34, 197, 94);

    static Theme() => SetMode(1);

    // Tailwind zinc
    static readonly Color Z50 = Hex(0xFAFAFA), Z100 = Hex(0xF4F4F5), Z200 = Hex(0xE4E4E7), Z300 = Hex(0xD4D4D8), Z400 = Hex(0xA1A1AA),
        Z500 = Hex(0x71717A), Z600 = Hex(0x52525B), Z700 = Hex(0x3F3F46), Z800 = Hex(0x27272A), Z900 = Hex(0x18181B), Z950 = Hex(0x09090B);
    static Color Hex(int rgb) => Color.FromArgb(255, Color.FromArgb(rgb));
    static Color Mix(Color a, Color b, float k) => Color.FromArgb((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));

    /// 0 = follow Windows, 1 = dark, 2 = light. Takes effect for windows opened afterwards.
    public static void SetMode(int mode)
    {
        IsDark = mode switch { 1 => true, 2 => false, _ => SystemDark() };
        var (light, dark) = ChosenAccent();
        if (IsDark)
        {
            (Chrome, Bg, Card, Line) = (Z950, Z900, Z800, Hex(0x343437));
            (Text, Muted, Faded) = (Z100, Z300, Z400);
            (Accent, OnAccent) = (light, Z950);
            (Gold, Marked) = (Hex(0xFBBF24), Mix(Z800, light, 0.14f));
            (CtlFill, CtlHover, CtlPressed) = (Z800, Hex(0x2F2F33), Hex(0x232326));
            (CtlStroke, CtlStrokeBottom) = (Z700, Z700);
            (CardStroke, Flyout, NavSelected, Track) = (Z700, Z800, Hex(0x18181B), Z700);
            (Blue, Green, Neutral) = (Hex(0x3B82F6), Hex(0x22C55E), Z950);
        }
        else
        {
            (Chrome, Bg, Card, Line) = (Z100, Z50, Color.White, Hex(0xEDEDEF));
            (Text, Muted, Faded) = (Z900, Z600, Z500);
            (Accent, OnAccent) = (dark, Color.White);
            (Gold, Marked) = (Hex(0xB45309), Mix(Color.White, dark, 0.09f));
            (CtlFill, CtlHover, CtlPressed) = (Color.White, Z50, Z100);
            (CtlStroke, CtlStrokeBottom) = (Z200, Z300);
            (CardStroke, Flyout, NavSelected, Track) = (Z200, Color.White, Hex(0xE8E8EA), Z200);
            (Blue, Green, Neutral) = (Hex(0x2563EB), Hex(0x16A34A), Z100);
        }
    }

    /// Light shade for dark mode, dark shade for light mode — the same split WinUI uses for the Windows accent.
    static (Color Light, Color Dark) ChosenAccent() => AccentChoice switch
    {
        1 => (Hex(0x4ADE80), Hex(0x15803D)), // green-400 / green-700
        2 => (Hex(0xA78BFA), Hex(0x6D28D9)), // violet-400 / violet-700
        _ => SystemAccent(),
    };

    /// The Windows accent colour in its light (for dark mode) and dark (for light mode) shades, as WinUI uses them.
    static (Color Light, Color Dark) SystemAccent()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (k?.GetValue("AccentPalette") is byte[] { Length: >= 32 } p)
            {
                Color At(int i) => Color.FromArgb(p[i * 4], p[i * 4 + 1], p[i * 4 + 2]);
                return (At(1), At(4)); // AccentLight2, AccentDark1
            }
        }
        catch { }
        return (Hex(0x99EBFF), Hex(0x005FB8)); // Windows default blue
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
            int caption = ColorTranslator.ToWin32(Chrome), text = ColorTranslator.ToWin32(Text), border = ColorTranslator.ToWin32(CardStroke);
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
            using var tile = RoundRect(new RectangleF(2, 2, size - 4, size - 4), 14);
            using var b = new SolidBrush(Accent);
            g.FillPath(b, tile);
            Icons.Draw(g, glyph, new RectangleF(14, 14, size - 28, size - 28), OnAccent);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public const int CardRadius = 8, ControlRadius = 4; // Fluent 2

    /// Fluent card corners. Panels get smooth (anti-aliased) corners and a hairline border painted over
    /// their background; controls that paint themselves (lists, grids) are clipped to a rounded region.
    public static T Round<T>(T c, int radius = CardRadius) where T : Control
    {
        if (c is Panel)
        {
            c.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new RectangleF(0.5f, 0.5f, c.Width - 1.5f, c.Height - 1.5f);
                using var path = RoundRect(r, Dp(radius));
                using (var outside = new Region(new Rectangle(0, 0, c.Width, c.Height)))
                {
                    outside.Exclude(path);
                    using var bg = new SolidBrush(c.Parent?.BackColor ?? Bg);
                    g.FillRegion(bg, outside);
                }
                using var stroke = new Pen(c.BackColor == Card ? CardStroke : CtlStroke);
                g.DrawPath(stroke, path);
            };
            c.SizeChanged += (_, _) => c.Invalidate();
            return c;
        }
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
        Subtle = !primary && text.Length == 0, // icon-only: a command-bar button, no frame until hovered
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
            Padding = new Padding(Dp(4)), ImageScalingSize = new Size(Dp(16), Dp(16)), DropShadowEnabled = true,
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
        Tag = glyph, Padding = new Padding(0, Dp(7), Dp(16), Dp(7)),
    };

    public static Bitmap Glyph(char glyph, Color color) => Icons.Bitmap(glyph, Dp(16), color);

    class MenuRenderer : ToolStripProfessionalRenderer
    {

        // Windows 11 flyout: its own surface colour and a hairline border (DWM rounds the corners)
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Flyout);

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(CtlStroke);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (e.Item.Selected && e.Item.Enabled)
            {
                using var path = RoundRect(new RectangleF(Dp(2), 1, e.Item.Width - Dp(4), e.Item.Height - 2), Dp(ControlRadius));
                using var b = new SolidBrush(IsDark ? Color.FromArgb(56, 56, 56) : Color.FromArgb(237, 237, 237));
                e.Graphics.FillPath(b, path);
            }
            // checked item ("show widget", "mute"): a short accent pill on the left, like a selected nav item
            if (e.Item is ToolStripMenuItem { Checked: true })
            {
                int h = Dp(16);
                using var pill = RoundRect(new RectangleF(Dp(3), (e.Item.Height - h) / 2f, Dp(3), h), Dp(1));
                using var a = new SolidBrush(Accent);
                e.Graphics.FillPath(a, pill);
            }
        }

        // Icons are drawn from the glyph each time, so they follow theme changes; checked ones are accent-coloured.
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item.Tag is not char glyph) { if (e.Image != null) e.Graphics.DrawImage(e.Image, e.ImageRectangle); return; }
            var color = e.Item is ToolStripMenuItem { Checked: true } ? Accent : e.Item.Enabled ? Text : Faded;
            NamazTimes.Icons.Draw(e.Graphics, glyph, e.ImageRectangle, color);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : Faded;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // Suppress the default check box; OnRenderItemImage marks checked items.
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(CtlStroke);
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 0, y, e.Item.Width, y); // Fluent separators run edge to edge
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
    /// Command-bar style: no fill or border until hovered (icon-only buttons in toolbars).
    public bool Subtle;

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

    int GlyphW => Glyph == '\0' ? 0 : Theme.Dp(16) + (Text.Length > 0 ? Theme.Dp(8) : 0);
    int TrailW => Trailing == '\0' ? 0 : Theme.Dp(8) + Theme.Dp(12);

    public override Size GetPreferredSize(Size proposed)
    {
        var text = Text.Length > 0 ? TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine) : Size.Empty;
        var pad = Text.Length > 0 || Subtle ? Theme.Dp(12) : Theme.Dp(8);
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
        else if (Subtle) (fill, fg) = (down ? Theme.CtlPressed : hot ? Theme.Line : Parent?.BackColor ?? Theme.Bg, down ? Theme.Muted : ForeColor);
        else (fill, fg) = (down ? Theme.CtlPressed : hot ? Theme.CtlHover : Theme.CtlFill, down ? Theme.Muted : ForeColor);

        int rad = Theme.Dp(Theme.ControlRadius);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundRect(r, rad);
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        if (!down && !Subtle)
        {
            // Elevation border: hairline all round, a stronger line along the bottom
            if (!accent) using (var p = new Pen(Theme.CtlStroke)) g.DrawPath(p, path);
            using var bottom = new Pen(accent ? Blend(Color.Black, Theme.Accent, Theme.IsDark ? 0.15f : 0.25f) : Theme.CtlStrokeBottom);
            g.DrawLine(bottom, rad, Height - 1, Width - rad - 1, Height - 1);
        }

        if (!DrawContent) return;
        // Icon + text + chevron, centred as one group
        var textW = Text.Length > 0 ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width : 0;
        var x = (Width - (GlyphW + textW + TrailW)) / 2;
        if (Glyph != '\0')
        {
            Icons.Draw(g, Glyph, new RectangleF(x, (Height - Theme.Dp(16)) / 2f, Theme.Dp(16), Theme.Dp(16)), fg);
            x += GlyphW;
        }
        if (textW > 0)
        {
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, textW + 2, Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            x += textW;
        }
        if (Trailing != '\0')
            Icons.Draw(g, Trailing, new RectangleF(x + Theme.Dp(8), (Height - Theme.Dp(12)) / 2f, Theme.Dp(12), Theme.Dp(12)), accent ? fg : Theme.Muted);
    }

    /// False for controls that draw their own content over the button field (Dropdown).
    protected virtual bool DrawContent => true;

    public static Color Blend(Color a, Color b, float k) =>
        Color.FromArgb((int)(a.R * k + b.R * (1 - k)), (int)(a.G * k + b.G * (1 - k)), (int)(a.B * k + b.B * (1 - k)));
}

/// Text box with a magnifier, rounded corners and no white Windows border.
public class SearchBox : Panel
{
    public readonly TextBox Box;
    readonly Color fill;

    public SearchBox(string placeholder, Color? fill = null)
    {
        this.fill = fill ?? Theme.CtlFill;
        Height = Theme.Dp(32);
        Padding = new Padding(Theme.Dp(36), Theme.Dp(7), Theme.Dp(10), 0);
        BackColor = this.fill;
        DoubleBuffered = true;
        Box = new TextBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = this.fill, ForeColor = Theme.Text,
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
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        using (var p = new Pen(Theme.CtlStroke)) g.DrawPath(p, path);
        // 2 px accent underline while typing
        if (Box.Focused)
            using (var u = new Pen(Theme.Accent, Theme.Dp(2)))
                g.DrawLine(u, Theme.Dp(Theme.ControlRadius), Height - Theme.Dp(1), Width - Theme.Dp(Theme.ControlRadius), Height - Theme.Dp(1));
        Icons.Draw(g, "Search01", new RectangleF(Theme.Dp(12), (Height - Theme.Dp(16)) / 2f, Theme.Dp(16), Theme.Dp(16)), Theme.Muted);
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
        else
        {
            using (var bg = new SolidBrush(Theme.CtlFill)) g.FillPath(bg, track);
            using var stroke = new Pen(Theme.Muted);
            g.DrawPath(stroke, track);
        }
        var d = r.Height - Theme.Dp(on ? 8 : 10);
        var x = on ? r.Right - d - Theme.Dp(4) : r.X + Theme.Dp(5);
        using var knob = new SolidBrush(on ? Theme.OnAccent : Theme.Muted);
        g.FillEllipse(knob, x, r.Y + (r.Height - d) / 2, d, d);
    }
}

/// Counter in the Windows 11 NumberBox look: the value in a Fluent field with inline ▲▼ spin buttons.
/// Arrow keys and the mouse wheel step it as well. Format shows units ("60 мин", "100%", "+2").
public class Stepper : Control
{
    int value, hotSpin = -1;
    public int Min, Max, Step;
    public Func<int, string> Format = v => v.ToString();
    public event Action<int>? ValueChanged;

    public Stepper(int value, int min, int max, int step = 1, Func<int, string>? format = null)
    {
        (Min, Max, Step) = (min, max, step);
        if (format != null) Format = format;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        TabStop = true;
        Font = Theme.UI(10f);
        Size = new Size(Theme.Dp(150), Theme.Dp(32));
        Margin = new Padding(0, Theme.Dp(2), 0, Theme.Dp(2));
        Anchor = AnchorStyles.Left;
        this.value = Math.Clamp(value, min, max);
    }

    public int Value
    {
        get => value;
        set
        {
            var v = Math.Clamp(value, Min, Max);
            if (v == this.value) return;
            this.value = v;
            Invalidate();
            ValueChanged?.Invoke(v);
        }
    }

    Rectangle Spin(int i) => NumberBox.SpinRect(Size, i);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = Spin(0).Contains(e.Location) ? 0 : Spin(1).Contains(e.Location) ? 1 : -1;
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        if (h != hotSpin) { hotSpin = h; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hotSpin = -1; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (Spin(0).Contains(e.Location)) Value += Step;
        else if (Spin(1).Contains(e.Location)) Value -= Step;
    }

    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Value += Math.Sign(e.Delta) * Step; }

    protected override bool IsInputKey(Keys k) => k is Keys.Up or Keys.Down || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Up) Value += Step;
        else if (e.KeyCode == Keys.Down) Value -= Step;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        NumberBox.PaintField(e.Graphics, this, Focused, hotSpin);
        var text = new Rectangle(Theme.Dp(10), 0, Spin(0).X - Theme.Dp(12), Height);
        TextRenderer.DrawText(e.Graphics, Format(value), Font, text, Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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
        BackColor = Theme.Neutral;
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
                using var b = new SolidBrush(Checked ? Theme.Card : Theme.Line);
                g.FillPath(b, pill);
                if (Checked) using (var p = new Pen(Theme.CardStroke)) g.DrawPath(p, pill);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Checked ? Theme.Text : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        // Radio buttons size for their check circle; a pill only needs the text plus padding.
        public override Size GetPreferredSize(Size proposed) =>
            TextRenderer.MeasureText(Text, Font) + new Size(Padding.Horizontal + Theme.Dp(6), Padding.Vertical + Theme.Dp(10));
    }
}

/// Windows 11 ComboBox, as in Wintor's settings: a 200 px field with the chosen option and a chevron;
/// clicking opens a menu of the options with the current one marked.
public class Dropdown : RoundButton
{
    readonly string[] options;
    int selected;
    public event Action<int>? Changed;

    public Dropdown(IEnumerable<string> options, int selected)
    {
        this.options = options.ToArray();
        this.selected = Math.Clamp(selected, 0, this.options.Length - 1);
        AutoSize = false;
        Size = new Size(Theme.Dp(200), Theme.Dp(32));
        Font = Theme.UI(10f);
        BackColor = Theme.CtlFill; ForeColor = Theme.Text;
        Cursor = Cursors.Hand;
        Text = this.options[this.selected];
        Click += (_, _) =>
        {
            var m = Theme.Menu();
            m.ShowImageMargin = false;
            for (int i = 0; i < this.options.Length; i++)
            {
                var k = i;
                var item = new ToolStripMenuItem(this.options[i]) { Checked = i == this.selected, Padding = new Padding(Theme.Dp(8), Theme.Dp(7), Theme.Dp(16), Theme.Dp(7)) };
                item.Click += (_, _) => Selected = k;
                m.Items.Add(item);
            }
            m.MinimumSize = new Size(Width, 0);
            m.Show(this, new Point(0, Height + Theme.Dp(2)));
        };
    }

    public int Selected
    {
        get => selected;
        set
        {
            value = Math.Clamp(value, 0, options.Length - 1);
            if (value == selected) return;
            selected = value;
            Text = options[value];
            Invalidate();
            Changed?.Invoke(value);
        }
    }

    public override Size GetPreferredSize(Size proposed) => Size;
    protected override bool DrawContent => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); // the field: fill, elevation border, hover (DrawContent is off, so no centred text)
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Theme.Dp(11), 0, Width - Theme.Dp(44), Height), Enabled ? Theme.Text : Theme.Faded,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        Icons.Draw(e.Graphics, "ArrowDown01", new RectangleF(Width - Theme.Dp(28), (Height - Theme.Dp(14)) / 2f, Theme.Dp(14), Theme.Dp(14)), Theme.Muted);
    }
}
