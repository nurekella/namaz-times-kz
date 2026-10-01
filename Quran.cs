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
    public static readonly (Color Color, string Key)[] Tajweed =
    [
        (Color.FromArgb(138, 145, 156), "TjSilent"), (Color.FromArgb(232, 176, 74), "TjMadd2"), (Color.FromArgb(240, 138, 60), "TjMaddJaiz"),
        (Color.FromArgb(232, 87, 63), "TjMaddWajib"), (Color.FromArgb(255, 77, 109), "TjMaddLazim"), (Color.FromArgb(79, 163, 255), "TjQalqala"),
        (Color.FromArgb(60, 207, 110), "TjGhunna"), (Color.FromArgb(42, 179, 160), "TjIdgham"), (Color.FromArgb(199, 125, 255), "TjIkhfa"),
        (Color.FromArgb(46, 196, 230), "TjIqlab"),
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
        Theme.Apply(this);
        Text = L.T("Quran");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(1180), Theme.Dp(780));
        MinimumSize = new Size(Theme.Dp(760), Theme.Dp(480));
        Padding = new Padding(Theme.Dp(12));

        // Toolbar: font, translation, show/hide transliteration and translation, Arabic size
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, Theme.Dp(8)) };
        Label Caption(string key) { var l = Theme.Label(L.T(key), Theme.Muted, 9.5f); l.Margin = new Padding(Theme.Dp(4), Theme.Dp(8), Theme.Dp(6), 0); return l; }
        var font = new Segmented(QuranText.Fonts.Select(f => f.Title), s.QuranFont) { Margin = new Padding(0, Theme.Dp(2), Theme.Dp(14), Theme.Dp(2)) };
        string[] langs = ["kk", "ru", "en"];
        // the last option hides the translation
        var trans = new Segmented(["Қазақша", "Русский", "English", L.T("QuranNone")], s.QuranMeaning ? Array.IndexOf(langs, s.QuranTrans) : langs.Length) { Margin = new Padding(0, Theme.Dp(2), Theme.Dp(14), Theme.Dp(2)) };
        var translit = Check(L.T("QuranTranslit"), s.QuranTranslit);
        var tajweed = Check(L.T("Tajweed"), s.QuranTajweed);
        var size = new Stepper(s.QuranSize, 14, 40, 2);
        bar.Controls.AddRange([font, trans, translit, tajweed, Caption("QuranSize"), size]); // the font and language names speak for themselves

        // Tajweed colour legend, shown while colouring is on
        var legend = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, Theme.Dp(8)), Visible = s.QuranTajweed };
        foreach (var (color, key) in QuranText.Tajweed)
        {
            var chip = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0, 0, Theme.Dp(5), Theme.Dp(5)), Padding = new Padding(Theme.Dp(4), Theme.Dp(1), Theme.Dp(6), Theme.Dp(1)) };
            var dot = Theme.Label("●", color, 10f);
            dot.Margin = new Padding(0, Theme.Dp(3), Theme.Dp(4), Theme.Dp(3));
            var name = Theme.Label(L.T(key), Theme.Text, 8.5f);
            name.Margin = new Padding(0, Theme.Dp(5), 0, Theme.Dp(3));
            chip.Controls.AddRange([dot, name]);
            legend.Controls.Add(chip);
        }

        // Left: search + sura list
        search = new TextBox
        {
            Dock = DockStyle.Top, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.UI(10.5f), PlaceholderText = L.T("QuranSearch"),
        };
        list = new ListBox
        {
            Dock = DockStyle.Fill, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.Dp(40), IntegralHeight = false, Font = Theme.UI(10f),
        };
        list.DrawItem += DrawSura;
        list.HandleCreated += (_, _) => Theme.DarkScrollbars(list);
        var left = new Panel { Dock = DockStyle.Left, Width = Theme.Dp(250), Padding = new Padding(0, 0, Theme.Dp(12), 0) };
        left.Controls.AddRange([list, new Panel { Dock = DockStyle.Top, Height = Theme.Dp(8) }, search]);

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

        view = new VerseView(s) { Dock = DockStyle.Fill };
        view.Bookmarked += UpdateBookmark;
        Controls.AddRange([view, left, legend, bar, bottom]);

        font.Changed += i => { s.QuranFont = i; Data.Save(s); view.Rebuild(); };
        trans.Changed += i => { s.QuranMeaning = i < langs.Length; if (s.QuranMeaning) s.QuranTrans = langs[i]; Data.Save(s); view.Rebuild(); };
        tajweed.CheckedChanged += (_, _) => { s.QuranTajweed = legend.Visible = tajweed.Checked; Data.Save(s); view.Rebuild(); };
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

    static CheckBox Check(string text, bool value)
    {
        var c = new CheckBox
        {
            Text = text, Checked = value, Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, Font = Theme.UI(),
            TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Margin = new Padding(0, Theme.Dp(2), Theme.Dp(6), Theme.Dp(2)),
            Padding = new Padding(Theme.Dp(8), Theme.Dp(2), Theme.Dp(8), Theme.Dp(2)),
        };
        c.Size = TextRenderer.MeasureText(text, c.Font) + new Size(Theme.Dp(24), Theme.Dp(12)); // AutoSize clips button-style check boxes
        c.FlatAppearance.BorderColor = Theme.Line;
        c.FlatAppearance.CheckedBackColor = Theme.Accent;
        c.FlatAppearance.MouseOverBackColor = Theme.Line;
        void Style() { c.BackColor = c.Checked ? Theme.Accent : Theme.Card; c.ForeColor = c.Checked ? Theme.Bg : Theme.Text; }
        c.CheckedChanged += (_, _) => Style();
        Style();
        return c;
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

    void DrawSura(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var n = shown[e.Index];
        var sura = QuranText.Suras.Value[n - 1];
        var selected = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(selected ? Theme.Line : Theme.Card)) e.Graphics.FillRectangle(bg, e.Bounds);
        if (selected) using (var bar = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y + Theme.Dp(8), Theme.Dp(3), e.Bounds.Height - Theme.Dp(16));
        var countW = Theme.Dp(44);
        TextRenderer.DrawText(e.Graphics, $"{n} · {sura.Name}", list.Font,
            new Rectangle(e.Bounds.X + Theme.Dp(14), e.Bounds.Y, e.Bounds.Width - Theme.Dp(20) - countW, e.Bounds.Height),
            selected ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, sura.N.ToString(), list.Font, new Rectangle(e.Bounds.Right - countW - Theme.Dp(10), e.Bounds.Y, countW, e.Bounds.Height),
            n == s.QuranSura ? NamesForm.Gold : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
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
        QuranText.Span[][] tj = [];
        Region?[][] tjRegions = []; // per verse, per colour group; measured lazily at the verse's own origin
        int[] tops = [], heights = [], arHeights = [];
        int headerH;
        Font? arFont;
        readonly Font tlFont = Theme.UI(10f, FontStyle.Italic), trFont = Theme.UI(11f), titleFont = Theme.UI(15f, FontStyle.Bold),
            subFont = Theme.UI(9.5f), numFont = Theme.UI(8.5f);
        static readonly Color Marked = Color.FromArgb(38, 48, 42);
        int Pad => Theme.Dp(22);
        int NumW => Theme.Dp(40);

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
            tr = s.QuranMeaning ? [.. QuranText.Verses(s.QuranTrans, Sura)] : [];
            tj = s.QuranTajweed ? QuranText.TajweedOf(Sura) : [];
            arFont?.Dispose();
            arFont = new Font(QuranText.Family(s.QuranFont), s.QuranSize * Theme.UiScale);
            Measure();
        }

        int TextW => Math.Max(Theme.Dp(200), ClientSize.Width - 2 * Pad - NumW);

        static TextFormatFlags Wrap => TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

        int H(string text, Font f, TextFormatFlags flags) => TextRenderer.MeasureText(text, f, new Size(TextW, int.MaxValue), flags).Height;

        // Arabic goes through GDI+ (DrawString): it can measure where each letter landed after shaping,
        // which is what tajweed colouring needs. Translations stay on GDI (TextRenderer).
        static readonly StringFormat ArFormat = new(StringFormatFlags.DirectionRightToLeft);
        Graphics? measure;
        Graphics M => measure ??= CreateGraphics();
        int ArH(string text) => (int)Math.Ceiling(M.MeasureString(text, arFont!, TextW, ArFormat).Height);

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
            var state = g.Save();
            g.TranslateTransform(x, y);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var text = new SolidBrush(Theme.Text);
            if (tj.Length == 0) { g.DrawString(ar[i], arFont!, text, rect, ArFormat); g.Restore(state); return; }
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

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var y = e.Y - AutoScrollPosition.Y;
            var i = Enumerable.Range(0, tops.Length).FirstOrDefault(k => y >= tops[k] && y < tops[k] + heights[k], -1);
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

                // verse number in a circle (gold when bookmarked)
                int d = Theme.Dp(28);
                var circle = new Rectangle(Pad, top + Theme.Dp(14), d, d);
                g.DrawEllipse(marked ? gold : accent, circle);
                TextRenderer.DrawText(g, (i + 1).ToString(), numFont, circle, marked ? NamesForm.Gold : Theme.Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                int y = top + Theme.Dp(14);
                int h = arHeights[i];
                DrawArabic(g, i, x, y, h);
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
                }
            }
        }
    }
}
