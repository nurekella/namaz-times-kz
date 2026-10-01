using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NamazTimes;

/// The Quran text, translations and transliteration (quran/*.txt, one verse per line, built by tools/quran.mjs)
/// and the bundled Arabic fonts (fonts/*.ttf, SIL OFL).
public static class QuranText
{
    public record Sura(int N, bool Mecca, string Ar, string Kk, string Ru, string En)
    {
        public string Name => L.Lang switch { "kk" => Kk, "en" => En, _ => Ru };
    }

    public static readonly Lazy<Sura[]> Suras = new(() =>
        JsonSerializer.Deserialize<List<JsonElement>>(Res("quran/suras.json"))!.Select(e => new Sura(
            e.GetProperty("n").GetInt32(), e.GetProperty("mecca").GetBoolean(), e.GetProperty("ar").GetString()!,
            e.GetProperty("kk").GetString()!, e.GetProperty("ru").GetString()!, e.GetProperty("en").GetString()!)).ToArray());

    static readonly Dictionary<string, string[]> texts = [];

    /// All 6236 verses of "ar", "kk", "ru", "en" or "tl" (transliteration), loaded on first use.
    public static string[] Lines(string key)
    {
        if (texts.TryGetValue(key, out var t)) return t;
        using var r = new StreamReader(Res($"quran/{key}.txt"));
        return texts[key] = r.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToArray(); // git may check out CRLF
    }

    /// Verses of sura s (1-based) from Lines(key).
    public static ArraySegment<string> Verses(string key, int s)
    {
        var start = Suras.Value.Take(s - 1).Sum(x => x.N);
        return new ArraySegment<string>(Lines(key), start, Suras.Value[s - 1].N);
    }

    /// Tajweed colour groups (tajweed.txt stores "group start end;..." per verse, offsets into the "ar" line).
    /// Colours are lighter on the dark theme and deeper on the light one.
    public static (Color Color, string Key)[] Tajweed => Theme.IsDark ? DarkTajweed : LightTajweed;
    static readonly (Color, string)[] DarkTajweed =
    [
        (Color.FromArgb(138, 145, 156), "TjSilent"), (Color.FromArgb(232, 176, 74), "TjMadd2"), (Color.FromArgb(240, 138, 60), "TjMaddJaiz"),
        (Color.FromArgb(232, 87, 63), "TjMaddWajib"), (Color.FromArgb(255, 77, 109), "TjMaddLazim"), (Color.FromArgb(79, 163, 255), "TjQalqala"),
        (Color.FromArgb(60, 207, 110), "TjGhunna"), (Color.FromArgb(42, 179, 160), "TjIdgham"), (Color.FromArgb(199, 125, 255), "TjIkhfa"),
        (Color.FromArgb(46, 196, 230), "TjIqlab"),
    ];
    static readonly (Color, string)[] LightTajweed =
    [
        (Color.FromArgb(150, 155, 165), "TjSilent"), (Color.FromArgb(196, 132, 10), "TjMadd2"), (Color.FromArgb(222, 104, 20), "TjMaddJaiz"),
        (Color.FromArgb(205, 50, 35), "TjMaddWajib"), (Color.FromArgb(170, 20, 60), "TjMaddLazim"), (Color.FromArgb(25, 100, 215), "TjQalqala"),
        (Color.FromArgb(25, 150, 70), "TjGhunna"), (Color.FromArgb(10, 128, 115), "TjIdgham"), (Color.FromArgb(135, 55, 200), "TjIkhfa"),
        (Color.FromArgb(0, 145, 190), "TjIqlab"),
    ];

    public record struct Span(int Group, int Start, int End);

    public static Span[][] TajweedOf(int s) => [.. Verses("tajweed", s).Select(line => line.Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => t.Split(' ')).Select(p => new Span(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]))).ToArray())];

    static Stream Res(string name) => typeof(QuranText).Assembly.GetManifestResourceStream(name)!;

    // Fonts: GDI+ (Font objects) reads them from a PrivateFontCollection; GDI text drawing (TextRenderer)
    // resolves the same face name only if the font is also registered for this process.
    public static readonly (string File, string Title)[] Fonts =
        [("AmiriQuran-Regular.ttf", "Amiri Quran"), ("ScheherazadeNew-Regular.ttf", "Scheherazade"), ("NotoNaskhArabic.ttf", "Noto Naskh")];
    static readonly PrivateFontCollection collection = new();
    static readonly Dictionary<int, FontFamily> families = [];

    public static FontFamily Family(int i)
    {
        i = Math.Clamp(i, 0, Fonts.Length - 1);
        if (families.TryGetValue(i, out var f)) return f;
        try
        {
            using var ms = new MemoryStream();
            Res("fonts/" + Fonts[i].File).CopyTo(ms);
            var data = ms.ToArray();
            var p = Marshal.AllocCoTaskMem(data.Length); // must outlive the collection: never freed
            Marshal.Copy(data, 0, p, data.Length);
            var before = collection.Families.Length;
            collection.AddMemoryFont(p, data.Length);
            uint n = 0;
            AddFontMemResourceEx(p, (uint)data.Length, IntPtr.Zero, ref n);
            f = collection.Families.Except(collection.Families.Take(before)).FirstOrDefault()
                ?? collection.Families.Last();
        }
        catch { f = new FontFamily(Name99.ArabicFont); } // fall back to a system Arabic font
        return families[i] = f;
    }

    [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);
}

/// Quran reader: suras on the left, verses with transliteration and translation on the right.
/// Clicking a verse bookmarks it; the window reopens at the bookmark.
public class QuranForm : Form
{
    readonly Settings s;
    readonly ListBox list;
    readonly TextBox search;
    readonly VerseView view;
    readonly Label bookmark;
    List<int> shown = []; // sura numbers in the list (after search)

    public QuranForm(Settings settings, int? sura = null)
    {
        s = settings;
        if (s.QuranTrans is not ("kk" or "ru" or "en")) s.QuranTrans = L.Lang is "kk" or "en" ? L.Lang : "ru";
        Theme.Apply(this, '\uE8F1');
        Text = L.T("Quran");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(1180), Theme.Dp(780));
        MinimumSize = new Size(Theme.Dp(760), Theme.Dp(480));
        Padding = new Padding(Theme.Dp(12));

        // Toolbar: font, translation, show/hide transliteration and translation, Arabic size
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, Theme.Dp(8)) };
        Label Caption(string key) { var l = Theme.Label(L.T(key), Theme.Muted, 9.5f); l.Margin = new Padding(Theme.Dp(4), Theme.Dp(8), Theme.Dp(6), 0); return l; }
        // Font and Russian translation: buttons with a drop-down menu, to keep the toolbar on one line
        var font = Drop(QuranText.Fonts.Select(f => f.Title).ToArray(), () => s.QuranFont, i => { s.QuranFont = i; Data.Save(s); view!.Rebuild(); });
        string[] langs = ["kk", "ru", "en"];
        // the last option hides the translation
        var trans = new Segmented(["Қазақша", "Русский", "English", L.T("QuranNone")], s.QuranMeaning ? Array.IndexOf(langs, s.QuranTrans) : langs.Length) { Margin = new Padding(0, Theme.Dp(2), Theme.Dp(8), Theme.Dp(2)) };
        string[] ru = ["muntahab", "abuadel", "kuliev"];
        var ruButton = Drop(["Аль-Мунтахаб", "Абу Адель", "Кулиев"], () => Math.Max(0, Array.IndexOf(ru, s.QuranRu)), i => { s.QuranRu = ru[i]; Data.Save(s); view!.Rebuild(); });
        void ShowRu() => ruButton.Visible = s.QuranMeaning && s.QuranTrans == "ru"; // shown while "Русский" is picked
        var translitSwitch = Theme.Switch(L.T("QuranTranslit"), s.QuranTranslit, out var translit);
        var tajweedSwitch = Theme.Switch(L.T("Tajweed"), s.QuranTajweed, out var tajweed);
        var colours = Theme.Button(L.T("TjColours"));
        colours.Margin = new Padding(0, Theme.Dp(2), Theme.Dp(8), Theme.Dp(2));
        var size = new Stepper(s.QuranSize, 14, 40, 2);
        bar.Controls.AddRange([font, trans, ruButton, translitSwitch, tajweedSwitch, colours, Caption("QuranSize"), size]); // the font and language names speak for themselves

        // Tajweed colour legend, shown while colouring is on
        var legend = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, Theme.Dp(8)) };
        foreach (var (color, key) in QuranText.Tajweed)
        {
            var chip = Theme.Round(new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0, 0, Theme.Dp(5), Theme.Dp(5)), Padding = new Padding(Theme.Dp(4), Theme.Dp(1), Theme.Dp(6), Theme.Dp(1)) }, 8);
            var dot = Theme.Label("●", color, 10f);
            dot.Margin = new Padding(0, Theme.Dp(3), Theme.Dp(4), Theme.Dp(3));
            var name = Theme.Label(L.T(key), Theme.Text, 8.5f);
            name.Margin = new Padding(0, Theme.Dp(5), 0, Theme.Dp(3));
            chip.Controls.AddRange([dot, name]);
            legend.Controls.Add(chip);
        }

        // Left: search + sura list
        var searchBox = new SearchBox(L.T("QuranSearch")) { Dock = DockStyle.Top };
        search = searchBox.Box;
        list = new ListBox
        {
            Dock = DockStyle.Fill, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.Dp(54), IntegralHeight = false, Font = Theme.UI(10f),
        };
        list.DrawItem += DrawSura;
        list.HandleCreated += (_, _) => Theme.DarkScrollbars(list);
        Theme.Round(list, 10);
        var left = new Panel { Dock = DockStyle.Left, Width = Theme.Dp(290), Padding = new Padding(0, 0, Theme.Dp(12), 0) };
        left.Controls.AddRange([list, new Panel { Dock = DockStyle.Top, Height = Theme.Dp(8) }, searchBox]);

        // Bottom: bookmark and sources (Tanzil asks for a visible link)
        bookmark = Theme.Label("", NamesForm.Gold, 9.5f);
        var sources = new LinkLabel
        {
            Text = L.T("QuranSources"), AutoSize = true, Font = Theme.UI(8.5f), LinkColor = Theme.Muted, ActiveLinkColor = Theme.Accent,
            ForeColor = Theme.Muted, LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, Theme.Dp(8), 0, 0),
        };
        var t = sources.Text.IndexOf("Tanzil.net");
        sources.Links.Clear();
        if (t >= 0) sources.Links.Add(t, "Tanzil.net".Length, "https://tanzil.net");
        var q = sources.Text.IndexOf("QuranEnc.com");
        if (q >= 0) sources.Links.Add(q, "QuranEnc.com".Length, "https://quranenc.com");
        sources.LinkClicked += (_, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo((string)e.Link!.LinkData!) { UseShellExecute = true });
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, Padding = new Padding(0, Theme.Dp(6), 0, 0) };
        bottom.Controls.AddRange([bookmark, sources]);

        view = Theme.Round(new VerseView(s) { Dock = DockStyle.Fill }, 10);
        view.Bookmarked += UpdateBookmark;
        Controls.AddRange([view, left, legend, bar, bottom]);

        trans.Changed += i => { s.QuranMeaning = i < langs.Length; if (s.QuranMeaning) s.QuranTrans = langs[i]; Data.Save(s); ShowRu(); view.Rebuild(); };
        ShowRu();
        // The legend folds away behind "Colours ▾"; hovering a coloured letter also names its rule.
        void ShowLegend()
        {
            colours.Visible = s.QuranTajweed;
            legend.Visible = s.QuranTajweed && s.QuranLegend;
            colours.Text = L.T("TjColours") + (legend.Visible ? "  ▴" : "  ▾");
        }
        colours.Click += (_, _) => { s.QuranLegend = !s.QuranLegend; Data.Save(s); ShowLegend(); };
        ShowLegend();
        tajweed.CheckedChanged += (_, _) => { s.QuranTajweed = tajweed.Checked; Data.Save(s); ShowLegend(); view.Rebuild(); };
        translit.CheckedChanged += (_, _) => { s.QuranTranslit = translit.Checked; Data.Save(s); view.Rebuild(); };
        size.ValueChanged += v => { s.QuranSize = v; Data.Save(s); view.Rebuild(); };
        search.TextChanged += (_, _) => Filter();
        list.SelectedIndexChanged += (_, _) => { if (list.SelectedIndex >= 0) view.Show(shown[list.SelectedIndex]); };

        Filter();
        var start = sura ?? Math.Clamp(s.QuranSura, 1, 114);
        Shown += (_, _) =>
        {
            SelectSura(start);
            if (sura == null) view.ScrollTo(s.QuranAya); // reopen at the bookmark
            UpdateBookmark();
        };
    }

    /// A button showing the current choice; clicking it opens a menu of the others.
    static Button Drop(string[] titles, Func<int> current, Action<int> pick)
    {
        var b = Theme.Button(titles[current()] + "  ▾");
        b.Margin = new Padding(0, Theme.Dp(2), Theme.Dp(8), Theme.Dp(2));
        var menu = Theme.Menu();
        for (int i = 0; i < titles.Length; i++)
        {
            int k = i;
            menu.Items.Add(new ToolStripMenuItem(titles[i], null, (_, _) => { b.Text = titles[k] + "  ▾"; pick(k); }) { Padding = new Padding(0, Theme.Dp(5), Theme.Dp(8), Theme.Dp(5)) });
        }
        menu.Opening += (_, _) => { for (int i = 0; i < titles.Length; i++) ((ToolStripMenuItem)menu.Items[i]).Font = Theme.UI(10f, i == current() ? FontStyle.Bold : FontStyle.Regular); };
        b.Click += (_, _) => menu.Show(b, new Point(0, b.Height));
        return b;
    }

    void Filter()
    {
        var f = search.Text.Trim();
        var suras = QuranText.Suras.Value;
        shown = Enumerable.Range(1, 114).Where(n => f == "" || n.ToString() == f
            || new[] { suras[n - 1].Kk, suras[n - 1].Ru, suras[n - 1].En, suras[n - 1].Ar }.Any(x => x.Contains(f, StringComparison.CurrentCultureIgnoreCase))).ToList();
        list.BeginUpdate();
        list.Items.Clear();
        list.Items.AddRange(shown.Select(n => (object)n).ToArray());
        list.EndUpdate();
        var i = shown.IndexOf(view.Sura);
        if (i >= 0) list.SelectedIndex = i;
    }

    void SelectSura(int sura)
    {
        if (!shown.Contains(sura)) { search.Text = ""; }
        list.SelectedIndex = shown.IndexOf(sura);
    }

    Font? suraArFont;
    readonly Font suraSub = Theme.UI(8.5f);

    /// Sura row: number in a soft square, name with "Meccan · 7 verses" under it, Arabic name on the right.
    void DrawSura(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics;
        var n = shown[e.Index];
        var sura = QuranText.Suras.Value[n - 1];
        var selected = (e.State & DrawItemState.Selected) != 0;
        var b = e.Bounds;
        using (var bg = new SolidBrush(Theme.Card)) g.FillRectangle(bg, b);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (selected)
            using (var path = Theme.RoundRect(new RectangleF(b.X + Theme.Dp(4), b.Y + Theme.Dp(3), b.Width - Theme.Dp(8), b.Height - Theme.Dp(6)), Theme.Dp(8)))
            using (var sel = new SolidBrush(Theme.Line)) g.FillPath(sel, path);
        int sq = Theme.Dp(30), x = b.X + Theme.Dp(12);
        var box = new Rectangle(x, b.Y + (b.Height - sq) / 2, sq, sq);
        using (var path = Theme.RoundRect(box, Theme.Dp(7)))
        using (var pen = new Pen(n == s.QuranSura ? Theme.Gold : selected ? Theme.Accent : Theme.Line, Theme.Dp(1))) g.DrawPath(pen, path);
        TextRenderer.DrawText(g, n.ToString(), suraSub, box, n == s.QuranSura ? Theme.Gold : selected ? Theme.Accent : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        suraArFont ??= new Font(QuranText.Family(0), 15f * Theme.UiScale);
        int arW = Theme.Dp(96), tx = x + sq + Theme.Dp(10), tw = b.Right - tx - arW - Theme.Dp(10);
        TextRenderer.DrawText(g, sura.Name, list.Font, new Rectangle(tx, b.Y + Theme.Dp(8), tw, Theme.Dp(22)),
            selected ? Theme.Text : Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, $"{L.T(sura.Mecca ? "Meccan" : "Medinan")} · {string.Format(L.T("Ayahs"), sura.N)}", suraSub,
            new Rectangle(tx, b.Y + Theme.Dp(29), tw, Theme.Dp(18)), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, sura.Ar, suraArFont, new Rectangle(b.Right - arW - Theme.Dp(12), b.Y, arW, b.Height),
            selected ? Theme.Accent : Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
    }

    void UpdateBookmark()
    {
        bookmark.Text = $"{string.Format(L.T("QuranBookmark"), s.QuranSura, s.QuranAya)} · {QuranText.Suras.Value[s.QuranSura - 1].Name}   ";
        list.Invalidate();
    }

    /// Owner-drawn, virtualised verse list: only verses in view are painted, so long suras stay fast.
    class VerseView : ScrollableControl
    {
        readonly Settings s;
        public int Sura { get; private set; } = 1;
        public event Action? Bookmarked;
        string[] ar = [], tl = [], tr = [];
        string?[] notes = [];
        QuranText.Span[][] tj = [];
        Region?[][] tjRegions = []; // per verse, per colour group; measured lazily at the verse's own origin
        int[] tops = [], heights = [], arHeights = [];
        int headerH;
        Font? arFont;
        readonly Font tlFont = Theme.UI(10f, FontStyle.Italic), trFont = Theme.UI(11f), titleFont = Theme.UI(15f, FontStyle.Bold),
            subFont = Theme.UI(9.5f), numFont = Theme.UI(8.5f), noteFont = Theme.UI(9.5f, FontStyle.Italic);
        static Color Marked => Theme.Marked;
        int Pad => Theme.Dp(22);
        int NumW => Theme.Dp(46);
        readonly ToolTip tip = new() { InitialDelay = 150, ReshowDelay = 50 };
        string? tipText;
        int trimTop, trimBottom; // unused line space the Arabic font reserves above and below

        public VerseView(Settings settings)
        {
            s = settings;
            AutoScroll = true;
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.Card;
            HandleCreated += (_, _) => Theme.DarkScrollbars(this);
        }

        public void Show(int sura)
        {
            Sura = sura;
            Rebuild();
            AutoScrollPosition = Point.Empty;
        }

        public void Rebuild()
        {
            ar = [.. QuranText.Verses("ar", Sura)];
            tl = s.QuranTranslit ? [.. QuranText.Verses("tl", Sura)] : [];
            tr = s.QuranMeaning ? [.. QuranText.Verses(s.QuranTrans == "ru" ? "ru-" + s.QuranRu : s.QuranTrans, Sura)] : [];
            // Al-Muntakhab puts a sura introduction in [[...]] inside a verse: shown under it as a grey note
            notes = new string[tr.Length];
            for (int k = 0; k < tr.Length; k++)
            {
                int a = tr[k].IndexOf("[["), b = tr[k].IndexOf("]]");
                if (a < 0 || b < a) continue;
                notes[k] = tr[k][(a + 2)..b].Trim();
                tr[k] = (tr[k][..a] + tr[k][(b + 2)..]).Trim();
            }
            tj = s.QuranTajweed ? QuranText.TajweedOf(Sura) : [];
            arFont?.Dispose();
            arFont = new Font(QuranText.Family(s.QuranFont), s.QuranSize * Theme.UiScale);
            // Quran fonts declare very tall lines (room for stacked marks); trim what goes beyond a generous margin.
            var fam = arFont.FontFamily;
            float em = fam.GetEmHeight(arFont.Style), px = arFont.SizeInPoints * DeviceDpi / 72f;
            float ascent = fam.GetCellAscent(arFont.Style) / em, descent = fam.GetCellDescent(arFont.Style) / em;
            trimTop = (int)(Math.Max(0, ascent - 1.25f) * px);
            trimBottom = (int)(Math.Max(0, descent - 0.65f) * px);
            Measure();
        }

        int TextW => Math.Max(Theme.Dp(200), ClientSize.Width - 2 * Pad - NumW);

        static TextFormatFlags Wrap => TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

        int H(string text, Font f, TextFormatFlags flags) => TextRenderer.MeasureText(text, f, new Size(TextW, int.MaxValue), flags).Height;

        // Arabic goes through GDI+ (DrawString): it can measure where each letter landed after shaping,
        // which is what tajweed colouring needs. Translations stay on GDI (TextRenderer).
        static readonly StringFormat ArFormat = new(StringFormatFlags.DirectionRightToLeft);
        Graphics? measure;
        // Measuring and every drawing pass must use the same text hint: grid fitting changes glyph advances,
        // and colours would then land beside their letters.
        const System.Drawing.Text.TextRenderingHint Hint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        Graphics M => measure ??= Measuring();
        Graphics Measuring() { var g = CreateGraphics(); g.TextRenderingHint = Hint; return g; }
        int FullArH(string text) => (int)Math.Ceiling(M.MeasureString(text, arFont!, TextW, ArFormat).Height);
        int ArH(string text) => FullArH(text) - trimTop - trimBottom;

        /// Union of the areas of each tajweed group's letters, for a verse laid out at (0, 0).
        Region?[] Regions(int i, int h)
        {
            if (tjRegions[i] is { } done) return done;
            var result = new Region?[QuranText.Tajweed.Length];
            var rect = new RectangleF(0, 0, TextW, h);
            foreach (var group in tj[i].GroupBy(t => t.Group))
                foreach (var chunk in group.Chunk(32)) // GDI+ measures at most 32 ranges per call
                {
                    using var f = (StringFormat)ArFormat.Clone();
                    f.SetMeasurableCharacterRanges([.. chunk.Select(t => new CharacterRange(t.Start, Math.Max(1, Math.Min(t.End, ar[i].Length) - t.Start)))]);
                    foreach (var r in M.MeasureCharacterRanges(ar[i], arFont!, rect, f))
                    {
                        if (result[group.Key] is { } u) { u.Union(r); r.Dispose(); } else result[group.Key] = r;
                    }
                }
            return tjRegions[i] = result;
        }

        void DrawArabic(Graphics g, int i, int x, int y, int h)
        {
            var rect = new RectangleF(0, 0, TextW, h);
            var hint = g.TextRenderingHint;
            g.TextRenderingHint = Hint;
            var state = g.Save();
            g.TranslateTransform(x, y);
            using var text = new SolidBrush(Theme.Text);
            if (tj.Length == 0) { g.DrawString(ar[i], arFont!, text, rect, ArFormat); g.Restore(state); g.TextRenderingHint = hint; return; }
            // Each pixel is drawn once: plain text outside the coloured letters, each colour inside its own area
            // (drawing colour over white would leave white anti-aliasing fringes).
            var regions = Regions(i, h);
            using (var rest = new Region(rect))
            {
                foreach (var r in regions) if (r != null) rest.Exclude(r);
                g.SetClip(rest, System.Drawing.Drawing2D.CombineMode.Intersect);
                g.DrawString(ar[i], arFont!, text, rect, ArFormat);
            }
            for (int k = 0; k < regions.Length; k++)
            {
                if (regions[k] == null) continue;
                g.Restore(state);
                state = g.Save();
                g.TranslateTransform(x, y);
                g.SetClip(regions[k]!, System.Drawing.Drawing2D.CombineMode.Intersect);
                using var b = new SolidBrush(QuranText.Tajweed[k].Color);
                g.DrawString(ar[i], arFont!, b, rect, ArFormat);
            }
            g.Restore(state);
            g.TextRenderingHint = hint;
        }

        void Measure()
        {
            if (arFont == null) return;
            int gap = Theme.Dp(6), y = headerH = Theme.Dp(78);
            tops = new int[ar.Length];
            heights = new int[ar.Length];
            arHeights = new int[ar.Length];
            foreach (var row in tjRegions) foreach (var r in row ?? []) r?.Dispose();
            tjRegions = new Region?[ar.Length][];
            for (int i = 0; i < ar.Length; i++)
            {
                arHeights[i] = ArH(ar[i]);
                int h = Theme.Dp(14) + arHeights[i];
                if (tl.Length > 0) h += gap + H(tl[i], tlFont, Wrap);
                if (tr.Length > 0) h += gap + H(tr[i], trFont, Wrap);
                if (tr.Length > 0 && notes[i] != null) h += gap + H(notes[i]!, noteFont, Wrap);
                h += Theme.Dp(16);
                tops[i] = y;
                heights[i] = h;
                y += h;
            }
            AutoScrollMinSize = new Size(0, y + Theme.Dp(20));
            Invalidate();
        }

        int lastWidth;
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (ClientSize.Width != lastWidth) { lastWidth = ClientSize.Width; Measure(); }
        }

        public void ScrollTo(int aya)
        {
            if (aya < 1 || aya > tops.Length) return;
            AutoScrollPosition = new Point(0, Math.Max(0, tops[aya - 1] - Theme.Dp(40)));
            Invalidate();
        }

        static System.Drawing.Drawing2D.GraphicsPath Star(Rectangle r)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, ro = r.Width / 2f, ri = ro * 0.82f;
            var pts = new PointF[16];
            for (int k = 0; k < 16; k++)
            {
                var a = Math.PI / 8 * k - Math.PI / 2;
                var rad = k % 2 == 0 ? ro : ri;
                pts[k] = new PointF(cx + (float)(rad * Math.Cos(a)), cy + (float)(rad * Math.Sin(a)));
            }
            p.AddPolygon(pts);
            return p;
        }

        int VerseAt(int y) => Enumerable.Range(0, tops.Length).FirstOrDefault(k => y >= tops[k] && y < tops[k] + heights[k], -1);

        // Name the tajweed rule under the mouse.
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string? text = null;
            int y = e.Y - AutoScrollPosition.Y, i = VerseAt(y);
            if (i >= 0 && tj.Length > 0)
            {
                var pt = new PointF(e.X - (Pad + NumW), y - (tops[i] + Theme.Dp(14)) + trimTop);
                var regions = Regions(i, arHeights[i] + trimTop + trimBottom);
                for (int k = 0; k < regions.Length; k++)
                    if (regions[k]?.IsVisible(pt, M) == true) { text = L.T(QuranText.Tajweed[k].Key); break; }
            }
            if (text == tipText) return;
            tipText = text;
            if (text == null) tip.Hide(this); else tip.Show(text, this, e.X + Theme.Dp(12), e.Y + Theme.Dp(18));
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); tipText = null; tip.Hide(this); }

        // Smooth wheel scrolling: glide to the target instead of jumping.
        readonly System.Windows.Forms.Timer glide = new() { Interval = 15 };
        int glideTarget = -1;
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, AutoScrollMinSize.Height - ClientSize.Height);
            int from = glideTarget >= 0 ? glideTarget : -AutoScrollPosition.Y;
            glideTarget = Math.Clamp(from - e.Delta * SystemInformation.MouseWheelScrollLines * Theme.Dp(40) / 120 / 3 * 2, 0, max);
            if (!glide.Enabled)
            {
                glide.Tick -= Glide;
                glide.Tick += Glide;
                glide.Start();
            }
        }

        void Glide(object? sender, EventArgs e)
        {
            int cur = -AutoScrollPosition.Y;
            int step = (glideTarget - cur) / 4;
            if (Math.Abs(glideTarget - cur) <= 2 || step == 0) { AutoScrollPosition = new Point(0, glideTarget); glide.Stop(); glideTarget = -1; return; }
            AutoScrollPosition = new Point(0, cur + step);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var y = e.Y - AutoScrollPosition.Y;
            var i = VerseAt(y);
            if (i < 0) return;
            s.QuranSura = Sura;
            s.QuranAya = i + 1;
            Data.Save(s);
            Invalidate();
            Bookmarked?.Invoke();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Card);
            if (arFont == null) return;
            int dy = AutoScrollPosition.Y, w = ClientSize.Width;
            var sura = QuranText.Suras.Value[Sura - 1];

            // Header: name, Arabic name, Meccan/Medinan · verses
            TextRenderer.DrawText(g, $"{Sura} · {sura.Name}", titleFont, new Rectangle(0, dy + Theme.Dp(12), w, Theme.Dp(30)), Theme.Accent, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, $"{L.T(sura.Mecca ? "Meccan" : "Medinan")} · {string.Format(L.T("Ayahs"), sura.N)}   ·   {L.T("QuranBookmarkHint")}", subFont,
                new Rectangle(0, dy + Theme.Dp(44), w, Theme.Dp(22)), Theme.Muted, TextFormatFlags.HorizontalCenter);

            int gap = Theme.Dp(6), x = Pad + NumW;
            using var line = new Pen(Theme.Line);
            using var gold = new Pen(NamesForm.Gold, Theme.Dp(1));
            using var accent = new Pen(Theme.Accent, Theme.Dp(1));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            for (int i = 0; i < ar.Length; i++)
            {
                int top = tops[i] + dy;
                if (top + heights[i] < e.ClipRectangle.Top) continue;
                if (top > e.ClipRectangle.Bottom) break;
                var marked = s.QuranSura == Sura && s.QuranAya == i + 1;
                if (marked) using (var b = new SolidBrush(Marked)) g.FillRectangle(b, 0, top, w, heights[i]);
                g.DrawLine(line, Pad, top, w - Pad, top);

                // verse number in an eight-point star, like the ayah marks of a mushaf (gold when bookmarked)
                int d = Theme.Dp(34);
                var star = new Rectangle(Pad - Theme.Dp(3), top + Theme.Dp(12), d, d);
                using (var path = Star(star)) g.DrawPath(marked ? gold : accent, path);
                TextRenderer.DrawText(g, (i + 1).ToString(), numFont, star, marked ? NamesForm.Gold : Theme.Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                int y = top + Theme.Dp(14);
                int h = arHeights[i];
                var clip = g.Clip;
                g.SetClip(new Rectangle(x - Theme.Dp(4), y - Theme.Dp(4), TextW + Theme.Dp(8), h + Theme.Dp(8)), System.Drawing.Drawing2D.CombineMode.Intersect);
                DrawArabic(g, i, x, y - trimTop, h + trimTop + trimBottom);
                g.Clip = clip;
                y += h;
                if (tl.Length > 0)
                {
                    y += gap;
                    h = H(tl[i], tlFont, Wrap);
                    TextRenderer.DrawText(g, tl[i], tlFont, new Rectangle(x, y, TextW, h), Theme.Muted, Wrap);
                    y += h;
                }
                if (tr.Length > 0)
                {
                    y += gap;
                    h = H(tr[i], trFont, Wrap);
                    TextRenderer.DrawText(g, tr[i], trFont, new Rectangle(x, y, TextW, h), Theme.Text, Wrap);
                    y += h;
                    if (notes[i] != null)
                    {
                        y += gap;
                        h = H(notes[i]!, noteFont, Wrap);
                        TextRenderer.DrawText(g, notes[i]!, noteFont, new Rectangle(x, y, TextW, h), Theme.Muted, Wrap);
                    }
                }
            }
        }
    }
}
