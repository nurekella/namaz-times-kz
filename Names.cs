using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.Json;

namespace NamazTimes;

/// One of the 99 names of Allah. Texts live in names99.json so they can be reviewed without touching code.
public record Name99(int N, string Ar, string Kk, string Ru, string En, string Mkk, string Mru, string Men, string Dkk, string Dru, string Den)
{
    public string Translit => L.Lang switch { "kk" => Kk, "en" => En, _ => Ru };
    public string Meaning => L.Lang switch { "kk" => Mkk, "en" => Men, _ => Mru };
    public string Description => L.Lang switch { "kk" => Dkk, "en" => Den, _ => Dru };

    public static readonly Lazy<List<Name99>> All = new(() =>
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var stream = typeof(Name99).Assembly.GetManifestResourceStream("names99.json")!;
        return JsonSerializer.Deserialize<List<JsonElement>>(stream, opts)!.Select((e, i) => new Name99(i + 1,
            e.GetProperty("ar").GetString()!, e.GetProperty("kk").GetString()!, e.GetProperty("ru").GetString()!,
            e.GetProperty("en").GetString()!, e.GetProperty("mkk").GetString()!, e.GetProperty("mru").GetString()!,
            e.GetProperty("men").GetString()!, e.GetProperty("dkk").GetString()!, e.GetProperty("dru").GetString()!,
            e.GetProperty("den").GetString()!)).ToList();
    });

    /// A different name every day, cycling through all 99.
    public static Name99 OfDay(DateOnly d) => All.Value[d.DayNumber % 99];

    /// Best installed font for vocalised Arabic (the nicer ones are optional Windows features).
    public static readonly string ArabicFont = new[] { "Sakkal Majalla", "Traditional Arabic", "Arabic Typesetting", "Segoe UI" }
        .First(f => new InstalledFontCollection().Families.Any(x => x.Name == f));
}

public class NamesForm : Form
{
    readonly List<Tile> tiles = [];
    Name99 selected;
    Action<int> step = _ => { };
    TextBox? searchBox;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (searchBox is { Focused: false } && keyData is Keys.Left or Keys.Right)
        {
            step(keyData == Keys.Right ? 1 : -1);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    public NamesForm()
    {
        Theme.Apply(this, '\uE82D');
        Text = L.T("Names99");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(720), Theme.Dp(660));
        Padding = new Padding(Theme.Dp(12));
        var today = Name99.OfDay(DateOnly.FromDateTime(DateTime.Today));
        selected = today;

        var searchPanel = new SearchBox(L.T("Search")) { Dock = DockStyle.Top };
        var search = searchPanel.Box;
        var detail = new DetailPanel(this) { Dock = DockStyle.Bottom, Height = Theme.Dp(170) };
        var grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, Theme.Dp(8), 0, Theme.Dp(8)) };
        grid.HandleCreated += (_, _) => Theme.DarkScrollbars(grid);
        void Select(Name99 n)
        {
            selected = n;
            tiles.ForEach(x => { x.Selected = x.Item == n; x.Invalidate(); });
            detail.Invalidate();
            if (tiles[n.N - 1].Visible) grid.ScrollControlIntoView(tiles[n.N - 1]);
        }
        foreach (var n in Name99.All.Value)
        {
            var t = new Tile(n, n == today) { Selected = n == selected };
            t.Click += (_, _) => Select(n);
            tiles.Add(t);
        }
        grid.Controls.AddRange([.. tiles]);
        Controls.AddRange([grid, detail, searchPanel]);

        // ‹ › under the details: previous / next name (wraps around 1 ↔ 99); arrow keys do the same.
        step = d => Select(Name99.All.Value[(selected.N - 1 + d + 99) % 99]);
        var prev = Theme.Button("‹ " + L.T("Prev"));
        var next = Theme.Button(L.T("Next") + " ›", primary: true);
        prev.Click += (_, _) => step(-1);
        next.Click += (_, _) => step(1);
        var nav = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Padding = Padding.Empty };
        nav.Controls.AddRange([prev, next]);
        detail.Controls.Add(nav);
        void PlaceNav() => nav.Location = new Point(detail.Width - nav.Width - Theme.Dp(14), detail.Height - nav.Height - Theme.Dp(12));
        detail.Resize += (_, _) => PlaceNav();
        nav.SizeChanged += (_, _) => PlaceNav();
        detail.Layout += (_, _) => PlaceNav();
        Shown += (_, _) => PlaceNav();
        searchBox = search;

        search.TextChanged += (_, _) =>
        {
            var q = Data.Fold(search.Text.Trim());
            grid.SuspendLayout();
            foreach (var t in tiles)
                t.Visible = q == "" || t.Item.N.ToString() == q || t.Item.Ar.Contains(search.Text.Trim())
                    || new[] { t.Item.Kk, t.Item.Ru, t.Item.En, t.Item.Mkk, t.Item.Mru, t.Item.Men }.Any(x => Data.Fold(x).Contains(q));
            grid.ResumeLayout();
        };
        Shown += (_, _) => grid.ScrollControlIntoView(tiles[today.N - 1]);
    }

    class Tile : Control
    {
        public readonly Name99 Item;
        readonly bool isToday;
        public bool Selected;

        public Tile(Name99 n, bool today)
        {
            (Item, isToday) = (n, today);
            Size = new Size(Theme.Dp(160), Theme.Dp(104));
            Margin = new Padding(Theme.Dp(4));
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using var path = Theme.RoundRect(r, Theme.Dp(8));
            using (var b = new SolidBrush(Selected ? Theme.Line : Theme.Card)) g.FillPath(b, path);
            if (isToday || Selected)
                using (var p = new Pen(isToday ? Gold : Theme.Accent, Theme.Dp(1))) g.DrawPath(p, path);
            using var small = Theme.UI(8.5f);
            using var ar = new Font(Name99.ArabicFont, 17f * Theme.UiScale);
            using var tr = Theme.UI(9.5f, FontStyle.Bold);
            var label = isToday ? $"{Item.N} · {L.T("Today")}" : Item.N.ToString();
            TextRenderer.DrawText(g, label, small, new Point(Theme.Dp(8), Theme.Dp(5)), isToday ? Gold : Theme.Muted);
            TextRenderer.DrawText(g, Item.Ar, ar, new Rectangle(0, Theme.Dp(16), Width, Theme.Dp(40)), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft);
            TextRenderer.DrawText(g, Item.Translit, tr, new Rectangle(Theme.Dp(4), Theme.Dp(56), Width - Theme.Dp(8), Theme.Dp(20)), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Item.Meaning, small, new Rectangle(Theme.Dp(4), Theme.Dp(78), Width - Theme.Dp(8), Theme.Dp(20)), Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    class DetailPanel(NamesForm owner) : Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            var n = owner.selected;
            var r = new RectangleF(0.5f, Theme.Dp(6), Width - 1.5f, Height - Theme.Dp(6) - 1);
            using var path = Theme.RoundRect(r, Theme.Dp(10));
            using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
            using (var p = new Pen(Color.FromArgb(110, Gold), Theme.Dp(1))) g.DrawPath(p, path);
            using var ar = new Font(Name99.ArabicFont, 30f * Theme.UiScale);
            using var tr = Theme.UI(14f, FontStyle.Bold);
            using var mean = Theme.UI(11f);
            using var small = Theme.UI(9f);
            var arW = Theme.Dp(240);
            TextRenderer.DrawText(g, n.Ar, ar, new Rectangle(Width - arW - Theme.Dp(12), (int)r.Y, arW, (int)r.Height - Theme.Dp(48)), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft);
            int x = Theme.Dp(18), w = Width - arW - Theme.Dp(36);
            TextRenderer.DrawText(g, $"{n.N} / 99", small, new Point(x, (int)r.Y + Theme.Dp(12)), Gold);
            TextRenderer.DrawText(g, n.Translit, tr, new Rectangle(x, (int)r.Y + Theme.Dp(30), w, Theme.Dp(30)), Theme.Text, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, n.Meaning, mean, new Rectangle(x, (int)r.Y + Theme.Dp(60), w, Theme.Dp(24)), Gold, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, n.Description, mean, new Rectangle(x, (int)r.Y + Theme.Dp(86), w, (int)r.Height - Theme.Dp(92)), Theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }
    }

    public static Color Gold => Theme.Gold;
}
