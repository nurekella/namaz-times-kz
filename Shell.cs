namespace NamazTimes;

/// One window for every section, laid out like Windows 11 Settings: navigation on the left, the page on the right.
/// Each page is one of the section forms, embedded as a child control; pages are kept alive while switching.
public class MainWindow : Form
{
    record Page(string Key, char Glyph, string TitleKey, Func<Form> Make);

    readonly Settings s;
    readonly Func<Form> makeSettings;
    readonly Page[] pages;
    readonly Dictionary<string, Form> made = [];
    readonly ListBox nav;
    readonly Panel host;
    readonly Label title;
    string current = "";
    public string Current => current;
    const string SettingsKey = "settings";

    public MainWindow(Settings settings, Func<Form> makeSettings)
    {
        s = settings;
        this.makeSettings = makeSettings;
        pages =
        [
            new("quran", '', "Quran", () => new QuranForm(s)),
            new("tasbih", '', "Tasbih", () => new TasbihForm(s)),
            new("adhkar", '', "Adhkar", () => new AdhkarForm(s)),
            new("names", '', "Names99", () => new NamesForm()),
            new("types", '', "PrayerTypes", () => new PrayerTypesForm()),
            new("qada", '', "Qada", () => new QadaForm(s)),
            new("zakat", '', "Zakat", () => new ZakatForm(s)),
            new("month", '', "Month", () => new MonthForm(s)),
            new("holidays", '', "Holidays", () => new HolidaysForm(s)),
            new("news", '', "News", () => new NewsForm()),
            new("about", '', "About", () => new AboutForm(() => ShowPage("news"))),
            new(SettingsKey, '', "Settings", () => this.makeSettings()),
        ];

        Theme.Apply(this);
        Text = "Namaz Times KZ";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(1360), Theme.Dp(840));
        MinimumSize = new Size(Theme.Dp(900), Theme.Dp(560));

        // Navigation pane (NavigationView): glyph + name, the chosen one with a soft fill and an accent pill.
        nav = new ListBox
        {
            Dock = DockStyle.Fill, BackColor = Theme.Bg, ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.Dp(40), IntegralHeight = false, Font = Theme.UI(10f),
        };
        nav.Items.AddRange([.. pages.Select(p => (object)p.Key)]);
        var glyphFont = new Font(Theme.IconFamily, 11f * Theme.UiScale);
        nav.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var p = pages[e.Index];
            var selected = p.Key == current;
            var b = e.Bounds;
            using (var bg = new SolidBrush(Theme.Bg)) e.Graphics.FillRectangle(bg, b);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (selected)
            {
                using var path = Theme.RoundRect(new RectangleF(b.X + Theme.Dp(4), b.Y + Theme.Dp(2), b.Width - Theme.Dp(8), b.Height - Theme.Dp(4)), Theme.Dp(Theme.ControlRadius));
                using var fill = new SolidBrush(Theme.Card);
                e.Graphics.FillPath(fill, path);
                using var pill = Theme.RoundRect(new RectangleF(b.X + Theme.Dp(4), b.Y + (b.Height - Theme.Dp(16)) / 2f, Theme.Dp(3), Theme.Dp(16)), Theme.Dp(1));
                using var accent = new SolidBrush(Theme.Accent);
                e.Graphics.FillPath(accent, pill);
            }
            TextRenderer.DrawText(e.Graphics, p.Glyph.ToString(), glyphFont, new Rectangle(b.X + Theme.Dp(16), b.Y, Theme.Dp(22), b.Height), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, L.T(p.TitleKey), nav.Font, new Rectangle(b.X + Theme.Dp(50), b.Y, b.Width - Theme.Dp(56), b.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        nav.MouseClick += (_, e) =>
        {
            var i = nav.IndexFromPoint(e.Location);
            if (i >= 0) ShowPage(pages[i].Key);
        };
        nav.HandleCreated += (_, _) => Theme.DarkScrollbars(nav);
        var appName = Theme.Label("Namaz Times KZ", Theme.Muted, 9f);
        appName.Dock = DockStyle.Top;
        appName.Padding = new Padding(Theme.Dp(16), Theme.Dp(4), 0, Theme.Dp(8));
        var left = new Panel { Dock = DockStyle.Left, Width = Theme.Dp(280), Padding = new Padding(Theme.Dp(6), Theme.Dp(4), Theme.Dp(6), Theme.Dp(8)) };
        left.Controls.AddRange([nav, appName]);

        // Content: page title, then the page itself
        title = Theme.Label("", Theme.Text, 20f, FontStyle.Bold);
        title.Dock = DockStyle.Top;
        title.AutoSize = false;
        title.Height = Theme.Dp(56);
        title.Padding = new Padding(Theme.Dp(12), Theme.Dp(4), 0, 0);
        title.TextAlign = ContentAlignment.MiddleLeft;
        host = new Panel { Dock = DockStyle.Fill };
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, Theme.Dp(6), 0) };
        content.Controls.AddRange([host, title]);
        Controls.AddRange([content, left]);
    }

    /// Show a section. With make, the page is rebuilt (e.g. the reader opened on a given surah).
    public void ShowPage(string key, Func<Form>? make = null)
    {
        var page = pages.First(p => p.Key == key);
        if (make != null && made.Remove(key, out var old)) old.Dispose();
        if (!made.TryGetValue(key, out var form))
        {
            form = (make ?? page.Make)();
            made[key] = form;
            // Embed: a form can live inside another as a plain child control.
            var compact = form.AutoSize && form.Controls.Count == 1; // a fixed-size layout (tasbih, zakat, qada, about)
            form.Opacity = 1;
            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.AutoSize = false;
            form.MinimumSize = Size.Empty;
            form.Dock = DockStyle.Fill;
            if (compact)
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
        }
        foreach (var f in made.Values) if (f != form) f.Hide();
        current = key;
        title.Text = L.T(page.TitleKey);
        form.Show();
        form.BringToFront();
        nav.Invalidate();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        if (!Visible) Show();
        Activate();
    }
}
