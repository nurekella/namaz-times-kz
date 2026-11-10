using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NamazTimes;

/// The one window of the app, laid out like Wintor: our own title bar (menu button, name, search in the middle,
/// window buttons) over a navigation pane on the left and a content layer with a rounded corner on the right.
/// Each page is one of the section forms, embedded as a child control; pages are kept alive while switching.
public class MainWindow : Form
{
    public record Page(string Key, char Glyph, string TitleKey, Func<Form> Make, int Group); // 0 top, 1 "worship", 2 footer

    readonly Settings s;
    readonly Func<Form> makeSettings;
    readonly Page[] pages;
    readonly Dictionary<string, Form> made = [];
    readonly TitleBar bar;
    readonly NavPane nav;
    readonly Panel host;
    readonly SearchBox search;
    readonly SearchResults results;
    string current = "";
    public string Current => current;
    const string SettingsKey = "settings";
    static bool compact; // navigation folded to icons (menu button), kept for the session

    public MainWindow(Settings settings, Func<Form> makeSettings)
    {
        s = settings;
        this.makeSettings = makeSettings;
        pages =
        [
            new("today", '', "TodayCap", () => new TodayForm(s), 0),
            new("month", '', "Month", () => new MonthForm(s), 0),
            new("holidays", '', "Holidays", () => new HolidaysForm(s), 0),
            new("quran", '', "Quran", () => new QuranForm(s), 1),
            new("tasbih", '', "Tasbih", () => new TasbihForm(s), 1),
            new("adhkar", '', "Adhkar", () => new AdhkarForm(s), 1),
            new("names", '', "Names99", () => new NamesForm(), 1),
            new("types", '', "PrayerTypes", () => new PrayerTypesForm(), 1),
            new("qada", '', "Qada", () => new QadaForm(s), 1),
            new("zakat", '', "Zakat", () => new ZakatForm(s), 1),
            new("news", '', "News", () => new NewsForm(), 2),
            new("about", '', "About", () => new AboutForm(() => ShowPage("news")), 2),
            new(SettingsKey, '', "Settings", () => this.makeSettings(), 2),
        ];

        Theme.Apply(this);
        BackColor = Theme.Chrome;
        Text = "Namaz Times KZ";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(1360), Theme.Dp(840));
        MinimumSize = new Size(Theme.Dp(900), Theme.Dp(660));

        // Content layer: the pages lie on it; its top-left corner is rounded and edged with a hairline.
        var layer = new ContentLayer { Dock = DockStyle.Fill };
        host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        layer.Controls.Add(host);
        layer.Padding = new Padding(Theme.Dp(16), Theme.Dp(12), Theme.Dp(12), Theme.Dp(8));

        nav = new NavPane(this) { Dock = DockStyle.Left, Width = compact ? Theme.Dp(48) : Theme.Dp(260) };
        nav.Compact = compact;

        search = new SearchBox(L.T("SearchApp"), Theme.IsDark ? Color.FromArgb(23, 23, 25) : Color.FromArgb(252, 252, 252));
        bar = new TitleBar(this, search) { Dock = DockStyle.Top, Height = Theme.Dp(48) };
        results = new SearchResults { Visible = false };
        Controls.AddRange([layer, nav, bar, results]);
        results.BringToFront();
        WireSearch();
    }

    public IReadOnlyList<Page> Pages => pages;

    public void ToggleNav()
    {
        compact = nav.Compact = !nav.Compact;
        nav.Width = nav.Compact ? Theme.Dp(48) : Theme.Dp(260);
        nav.Invalidate();
    }

    /// Text shown at the right of a navigation item (the next prayer time on "Today").
    public string? Badge(Page p)
    {
        if (p.Key != "today") return null;
        var n = Data.Next(DateTime.Now, d => Data.GetDay(s.City, d), x => !s.Hidden.Contains(x) && x is not (P.Tahajjud or P.Duha), s);
        return n is { } v ? L.Time(v.At) : null;
    }

    /// Show a section. With make, the page is rebuilt (e.g. the reader opened on a given surah).
    public void ShowPage(string key, Func<Form>? make = null)
    {
        var form = Ensure(key, make);
        foreach (var f in made.Values) if (f != form) f.Hide();
        current = key;
        form.Show();
        form.BringToFront();
        nav.Invalidate();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        if (!Visible) Show();
        Activate();
    }

    /// The page's form, created and embedded if it isn't yet (it stays hidden until shown).
    Form Ensure(string key, Func<Form>? make = null)
    {
        var page = pages.First(p => p.Key == key);
        if (make != null && made.Remove(key, out var old)) old.Dispose();
        if (made.TryGetValue(key, out var form)) return form;
        form = (make ?? page.Make)();
        made[key] = form;
        // Embed: a form can live inside another as a plain child control.
        var compactPage = form.AutoSize && form.Controls.Count == 1; // a fixed-size layout (tasbih, zakat, qada, about)
        form.Opacity = 1;
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.AutoSize = false;
        form.MinimumSize = Size.Empty;
        form.Dock = DockStyle.Fill;
        form.Visible = false;
        if (compactPage)
        {
            // Keep it in the middle of the page instead of the top-left corner; scroll if the window is smaller.
            var root = form.Controls[0];
            form.AutoScroll = true;
            if (form.IsHandleCreated) Theme.DarkScrollbars(form); else form.HandleCreated += (_, _) => Theme.DarkScrollbars(form);
            void Center() => root.Location = new Point(Math.Max(0, (form.ClientSize.Width - root.Width) / 2),
                Math.Max(0, (form.ClientSize.Height - root.Height) / 2) + form.AutoScrollPosition.Y);
            form.Resize += (_, _) => Center();
            root.SizeChanged += (_, _) => Center();
            Center();
        }
        form.FormClosed += (_, _) => made.Remove(key);
        host.Controls.Add(form);
        return form;
    }

    // ---------- search in the title bar: sections, surahs and settings ----------

    void WireSearch()
    {
        var box = search.Box;
        box.TextChanged += (_, _) => UpdateResults();
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Down or Keys.Up && results.Visible) { results.Move(e.KeyCode == Keys.Down ? 1 : -1); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { results.Activate(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { box.Text = ""; host.Focus(); e.SuppressKeyPress = true; }
        };
        box.LostFocus += (_, _) => BeginInvoke(() => { if (!box.Focused) results.Visible = false; });
        box.GotFocus += (_, _) => UpdateResults();
        results.Chosen += hit => { box.Text = ""; results.Visible = false; host.Focus(); hit.Go(); };
    }

    void UpdateResults()
    {
        var q = Data.Fold(search.Box.Text.Trim());
        if (q.Length == 0) { results.Visible = false; return; }
        var hits = new List<SearchResults.Hit>();
        bool Match(string text) => Data.Fold(text).Contains(q);

        foreach (var p in pages)
            if (Match(L.T(p.TitleKey))) hits.Add(new(p.Glyph, L.T(p.TitleKey), L.T("Section"), () => ShowPage(p.Key)));
        foreach (var (su, i) in QuranText.Suras.Value.Select((x, i) => (x, i)))
        {
            var n = i + 1; // Sura.N is the number of ayahs
            var name = L.Lang switch { "kk" => su.Kk, "en" => su.En, _ => su.Ru };
            if (Match(name) || n.ToString() == q || Match(su.Ru) || Match(su.En))
            {
                hits.Add(new('', $"{n}. {name}", L.T("SuraHit"), () => ShowPage("quran", () => new QuranForm(s, n))));
            }
        }
        if (Ensure(SettingsKey) is SettingsForm sf)
            foreach (var (title, glyph, card) in sf.Cards)
                if (Match(title)) hits.Add(new(glyph, title, L.T("Settings"), () => { ShowPage(SettingsKey); sf.Reveal(card); }));

        results.Show(hits.Take(8).ToList(), L.T("NothingFound"));
        var at = PointToClient(search.PointToScreen(Point.Empty));
        results.Bounds = new Rectangle(at.X, at.Y + search.Height + Theme.Dp(4), search.Width, results.PreferredHeight);
        results.BringToFront();
    }

    // ---------- own title bar: client area up to the top edge, but Windows still treats it as the caption ----------

    const int WM_NCCALCSIZE = 0x83, WM_NCHITTEST = 0x84, WM_NCMOUSEMOVE = 0xA0, WM_NCLBUTTONDOWN = 0xA1, WM_NCLBUTTONUP = 0xA2,
        WM_NCLBUTTONDBLCLK = 0xA3, WM_NCMOUSELEAVE = 0x2A2, HTCLIENT = 1, HTCAPTION = 2, HTMAXBUTTON = 9, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;

    int Frame => GetSystemMetricsForDpi(33 /*SM_CYSIZEFRAME*/, (uint)DeviceDpi) + GetSystemMetricsForDpi(92 /*SM_CXPADDEDBORDER*/, (uint)DeviceDpi);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0020); // NOSIZE|NOMOVE|NOZORDER|FRAMECHANGED
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_NCCALCSIZE when m.WParam != IntPtr.Zero:
            {
                // Keep the side and bottom frame (resizing, shadow, snap) but drop the caption: the client area starts at the top.
                var top = Marshal.PtrToStructure<RECT>(m.LParam).T;
                base.WndProc(ref m);
                var r = Marshal.PtrToStructure<RECT>(m.LParam);
                r.T = top + (IsZoomed(Handle) ? Frame : 0); // a maximized window hangs its frame off-screen
                Marshal.StructureToPtr(r, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }
            case WM_NCHITTEST:
            {
                base.WndProc(ref m);
                if (m.Result != HTCLIENT) return;
                var p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
                if (!IsZoomed(Handle) && p.Y < Frame)
                    m.Result = p.X < Frame * 2 ? HTTOPLEFT : p.X > Width - Frame * 2 ? HTTOPRIGHT : HTTOP;
                else if (p.Y < bar.Height)
                    m.Result = bar.MaxRect.Contains(p) ? HTMAXBUTTON : HTCAPTION; // HTMAXBUTTON shows Windows 11 snap layouts
                return;
            }
            // The maximize button belongs to Windows (for snap layouts), so its mouse arrives as non-client messages.
            case WM_NCMOUSEMOVE:
                bar.SetMaxHot(m.WParam == HTMAXBUTTON);
                if (m.WParam == HTMAXBUTTON) TrackNcLeave();
                break;
            case WM_NCMOUSELEAVE:
                bar.SetMaxHot(false);
                break;
            case WM_NCLBUTTONDOWN or WM_NCLBUTTONDBLCLK when m.WParam == HTMAXBUTTON:
                bar.SetMaxDown(true);
                m.Result = IntPtr.Zero;
                return;
            case WM_NCLBUTTONUP when m.WParam == HTMAXBUTTON:
                bar.SetMaxDown(false);
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                m.Result = IntPtr.Zero;
                return;
        }
        base.WndProc(ref m);
    }

    void TrackNcLeave()
    {
        var t = new TRACKMOUSEEVENT { cbSize = Marshal.SizeOf<TRACKMOUSEEVENT>(), dwFlags = 0x2 | 0x10 /*LEAVE|NONCLIENT*/, hwndTrack = Handle };
        TrackMouseEvent(ref t);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        bar?.Invalidate();
    }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct TRACKMOUSEEVENT { public int cbSize; public uint dwFlags; public IntPtr hwndTrack; public uint dwHoverTime; }
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT e);

    /// Pages lie on this layer: lighter than the chrome, top-left corner rounded, hairline along the top and left.
    class ContentLayer : Panel
    {
        public ContentLayer() { DoubleBuffered = true; BackColor = Theme.Bg; ResizeRedraw = true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float r = Theme.Dp(8);
            using (var corner = new GraphicsPath())
            {
                corner.AddLine(0, 0, r, 0);
                corner.AddArc(0, 0, r * 2, r * 2, 270, -90);
                corner.CloseFigure();
                using var chrome = new SolidBrush(Theme.Chrome);
                g.FillPath(chrome, corner);
            }
            using var edge = new GraphicsPath();
            edge.AddLine(0.5f, Height, 0.5f, r);
            edge.AddArc(0.5f, 0.5f, r * 2, r * 2, 180, 90);
            edge.AddLine(r, 0.5f, Width, 0.5f);
            using var pen = new Pen(Theme.IsDark ? Color.FromArgb(39, 39, 42) : Theme.CardStroke);
            g.DrawPath(pen, edge);
        }
    }
}

/// Wintor's title bar: menu button and the app name on the left, search in the middle, window buttons on the right.
/// Empty parts are transparent to the mouse, so the window treats them as its caption (drag, double-click, snap).
class TitleBar : Control
{
    readonly MainWindow w;
    readonly SearchBox search;
    int hot = -1, pressed = -1; // 0 menu, 1 minimize, 2 maximize, 3 close
    bool maxHot, maxDown, down;
    readonly ToolTip tip = new();

    public TitleBar(MainWindow w, SearchBox search)
    {
        this.w = w;
        this.search = search;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Theme.Chrome;
        Font = Theme.UI(9f);
        Controls.Add(search);
        Layout += (_, _) => PlaceSearch();
    }

    int Bw => Theme.Dp(46);
    Rectangle MenuRect => new(Theme.Dp(4), Theme.Dp(6), Theme.Dp(40), Height - Theme.Dp(12));
    Rectangle Button(int i) => new(Width - (4 - i) * Bw, 0, Bw, Height); // i = 1..3
    public Rectangle MaxRect => Button(2); // the bar sits at the window's top-left, so its coordinates are the window's

    void PlaceSearch()
    {
        var left = Theme.Dp(200);
        var right = Width - 3 * Bw - Theme.Dp(16);
        var width = Math.Max(Theme.Dp(160), Math.Min(Theme.Dp(440), right - left));
        var x = Math.Max(left, Math.Min((Width - width) / 2, right - width));
        search.Bounds = new Rectangle(x, (Height - search.Height) / 2, width, search.Height);
    }

    int HitTest(Point p) => MenuRect.Contains(p) ? 0 : Button(1).Contains(p) ? 1 : Button(2).Contains(p) ? 2 : Button(3).Contains(p) ? 3 : -1;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x84 /*WM_NCHITTEST*/)
        {
            var p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
            if (HitTest(p) is -1 or 2) m.Result = -1; // HTTRANSPARENT: the window decides (caption / maximize)
        }
    }

    public void SetMaxHot(bool v) { if (maxHot != v) { maxHot = v; if (!v) maxDown = false; Invalidate(); } }
    public void SetMaxDown(bool v) { maxDown = v; Invalidate(); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = HitTest(e.Location);
        if (h == hot) return;
        hot = h;
        SetMaxHot(false);
        Invalidate();
        if (h == 0) tip.Show(L.T("Navigation"), this, MenuRect.Left, MenuRect.Bottom + Theme.Dp(4), 2000); else tip.Hide(this);
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; down = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        down = e.Button == MouseButtons.Left;
        pressed = down ? HitTest(e.Location) : -1;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        down = false;
        Invalidate();
        // a button acts only when it was pressed and released on itself
        var was = pressed;
        pressed = -1;
        if (e.Button != MouseButtons.Left || HitTest(e.Location) != was) return;
        switch (was)
        {
            case 0: w.ToggleNav(); break;
            case 1: w.WindowState = FormWindowState.Minimized; break;
            case 3: w.Close(); break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Chrome);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var active = Form.ActiveForm == w;

        // menu button
        if (hot == 0)
            using (var p = Theme.RoundRect(MenuRect, Theme.Dp(Theme.ControlRadius)))
            using (var b = new SolidBrush(down ? Theme.CtlPressed : Theme.Line)) g.FillPath(b, p);
        Icons.Draw(g, '', new RectangleF(MenuRect.X + (MenuRect.Width - Theme.Dp(16)) / 2f, MenuRect.Y + (MenuRect.Height - Theme.Dp(16)) / 2f, Theme.Dp(16), Theme.Dp(16)), Theme.Text);
        TextRenderer.DrawText(g, "Namaz Times KZ", Font, new Rectangle(Theme.Dp(52), 0, Theme.Dp(150), Height), active ? Theme.Text : Theme.Faded,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        // window buttons, Windows 11 glyphs
        using var font = new Font(Theme.IconFamily, 7.5f * Theme.UiScale);
        var maximized = w.WindowState == FormWindowState.Maximized;
        string[] glyphs = ["", "", maximized ? "" : "", ""];
        for (int i = 1; i <= 3; i++)
        {
            var r = Button(i);
            var isHot = i == 2 ? maxHot : hot == i;
            var pressed = i == 2 ? maxDown : isHot && down;
            Color fg = active ? Theme.Text : Theme.Faded;
            if (isHot)
            {
                var fill = i == 3 ? Color.FromArgb(pressed ? 200 : 255, 196, 43, 28) : pressed ? Theme.CtlPressed : Theme.Line;
                using var b = new SolidBrush(fill);
                g.FillRectangle(b, r);
                if (i == 3) fg = Color.White;
            }
            TextRenderer.DrawText(g, glyphs[i], font, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

/// The navigation pane: an item per page (icon, name, an optional value at the right), a "worship" group header,
/// and the footer items (news, about, settings) at the bottom. Folds to icons with the menu button.
class NavPane : Control
{
    readonly MainWindow w;
    int hot = -1;
    public bool Compact;
    readonly List<(Rectangle R, MainWindow.Page? P, string? Header)> items = [];
    readonly ToolTip tip = new();
    readonly System.Windows.Forms.Timer clock = new() { Interval = 30_000 };

    public NavPane(MainWindow w)
    {
        this.w = w;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Theme.Chrome;
        Font = Theme.UI(10.5f);
        clock.Tick += (_, _) => Invalidate(); // the next-prayer badge
        clock.Start();
        Disposed += (_, _) => clock.Dispose();
    }

    int ItemH => Theme.Dp(40);

    void Build()
    {
        items.Clear();
        int y = Theme.Dp(4);
        foreach (var group in new[] { 0, 1 })
        {
            if (group == 1)
            {
                if (Compact) y += Theme.Dp(8);
                else { items.Add((new Rectangle(0, y, Width, ItemH), null, L.T("Worship"))); y += ItemH; }
            }
            foreach (var p in w.Pages.Where(p => p.Group == group))
            {
                items.Add((new Rectangle(0, y, Width, ItemH), p, null));
                y += ItemH;
            }
        }
        var footer = w.Pages.Where(p => p.Group == 2).ToList();
        y = Math.Max(y + Theme.Dp(8), Height - Theme.Dp(4) - footer.Count * ItemH);
        foreach (var p in footer) { items.Add((new Rectangle(0, y, Width, ItemH), p, null)); y += ItemH; }
    }

    int HitTest(Point pt) => items.FindIndex(i => i.P != null && i.R.Contains(pt));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = HitTest(e.Location);
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        if (h == hot) return;
        hot = h;
        Invalidate();
        if (Compact && h >= 0) tip.Show(L.T(items[h].P!.TitleKey), this, Width, items[h].R.Y + Theme.Dp(8), 2000); else tip.Hide(this);
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; Invalidate(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var h = HitTest(e.Location);
        if (h >= 0 && e.Button == MouseButtons.Left) w.ShowPage(items[h].P!.Key);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Build();
        var g = e.Graphics;
        g.Clear(Theme.Chrome);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var semibold = Theme.UI(10.5f, FontStyle.Bold);
        using var caption = Theme.UI(9f);
        for (int i = 0; i < items.Count; i++)
        {
            var (r, p, header) = items[i];
            if (header != null)
            {
                TextRenderer.DrawText(g, header, semibold, new Rectangle(Theme.Dp(15), r.Y, r.Width - Theme.Dp(20), r.Height), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                continue;
            }
            var selected = p!.Key == w.Current;
            var cell = new RectangleF(Theme.Dp(4), r.Y + Theme.Dp(2), r.Width - Theme.Dp(8), r.Height - Theme.Dp(4));
            if (selected || i == hot)
            {
                using var path = Theme.RoundRect(cell, Theme.Dp(Theme.ControlRadius));
                using var b = new SolidBrush(selected ? Theme.NavSelected : Theme.Line);
                g.FillPath(b, path);
            }
            if (selected)
            {
                using var pill = Theme.RoundRect(new RectangleF(cell.X, cell.Y + (cell.Height - Theme.Dp(16)) / 2f, Theme.Dp(3), Theme.Dp(16)), Theme.Dp(1));
                using var a = new SolidBrush(Theme.Accent);
                g.FillPath(a, pill);
            }
            Icons.Draw(g, p.Glyph, new RectangleF(Theme.Dp(16), r.Y + (r.Height - Theme.Dp(16)) / 2f, Theme.Dp(16), Theme.Dp(16)), Theme.Text);
            if (Compact) continue;
            var badge = w.Badge(p);
            var badgeW = badge == null ? 0 : TextRenderer.MeasureText(g, badge, caption).Width + Theme.Dp(8);
            TextRenderer.DrawText(g, L.T(p.TitleKey), Font, new Rectangle(Theme.Dp(48), r.Y, r.Width - Theme.Dp(60) - badgeW, r.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            if (badge != null)
                TextRenderer.DrawText(g, badge, caption, new Rectangle(r.Width - Theme.Dp(16) - badgeW, r.Y, badgeW, r.Height), Theme.Faded,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
        }
    }
}

/// The drop-down under the title-bar search: matching sections, surahs and settings.
class SearchResults : Control
{
    public record Hit(char Glyph, string Title, string Kind, Action Go);
    List<Hit> hits = [];
    string empty = "";
    int selected, hot = -1;
    public event Action<Hit>? Chosen;

    public SearchResults()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false); // clicking it must not take the focus from the search box
        Font = Theme.UI(10f);
        SizeChanged += (_, _) =>
        {
            if (Width <= 0 || Height <= 0) return;
            using var p = Theme.RoundRect(new RectangleF(0, 0, Width, Height), Theme.Dp(8));
            Region = new Region(p);
        };
    }

    int RowH => Theme.Dp(40);
    public int PreferredHeight => Theme.Dp(8) + Math.Max(1, hits.Count) * RowH;

    public void Show(List<Hit> list, string nothing)
    {
        hits = list;
        empty = nothing;
        selected = 0;
        hot = -1;
        Visible = true;
        Invalidate();
    }

    public void Move(int d) { if (hits.Count > 0) { selected = (selected + d + hits.Count) % hits.Count; Invalidate(); } }
    public void Activate() { if (Visible && selected < hits.Count) Chosen?.Invoke(hits[selected]); }

    Rectangle Row(int i) => new(Theme.Dp(4), Theme.Dp(4) + i * RowH, Width - Theme.Dp(8), RowH);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = Enumerable.Range(0, hits.Count).FirstOrDefault(i => Row(i).Contains(e.Location), -1);
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        if (h != hot) { hot = h; if (h >= 0) selected = h; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (hot >= 0 && hot < hits.Count) Chosen?.Invoke(hits[hot]);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Flyout);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var border = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Dp(8)))
        using (var pen = new Pen(Theme.CtlStroke)) g.DrawPath(pen, border);
        if (hits.Count == 0)
        {
            TextRenderer.DrawText(g, empty, Font, Row(0) with { X = Theme.Dp(16) }, Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return;
        }
        using var small = Theme.UI(9f);
        for (int i = 0; i < hits.Count; i++)
        {
            var r = Row(i);
            if (i == selected)
            {
                using var p = Theme.RoundRect(r, Theme.Dp(Theme.ControlRadius));
                using var b = new SolidBrush(Theme.Line);
                g.FillPath(b, p);
            }
            Icons.Draw(g, hits[i].Glyph, new RectangleF(r.X + Theme.Dp(12), r.Y + (r.Height - Theme.Dp(16)) / 2f, Theme.Dp(16), Theme.Dp(16)), Theme.Text);
            var kindW = TextRenderer.MeasureText(g, hits[i].Kind, small).Width + Theme.Dp(8);
            TextRenderer.DrawText(g, hits[i].Title, Font, new Rectangle(r.X + Theme.Dp(40), r.Y, r.Width - Theme.Dp(52) - kindW, r.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, hits[i].Kind, small, new Rectangle(r.Right - Theme.Dp(8) - kindW, r.Y, kindW, r.Height), Theme.Faded,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
        }
    }
}
