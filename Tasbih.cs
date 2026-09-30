using System.Drawing.Drawing2D;
using System.Media;
using System.Text.Json;

namespace NamazTimes;

/// A dhikr with its recommended count and virtue. Texts live in dhikr.json (kk/ru/en) for review.
/// Order matters: saved daily counts are indexed by position, so new entries go at the end.
public record Dhikr(string Ar, int Recommended, JsonElement Json)
{
    string Tr(string field) => Json.GetProperty(field).GetProperty(L.Lang is "kk" or "en" ? L.Lang : "ru").GetString()!;
    public string Translit => Tr("t");
    public string Meaning => Tr("m");
    public string Virtue => Tr("v");
    public string Source => Tr("ref");

    public static readonly Lazy<Dhikr[]> All = new(() =>
        JsonSerializer.Deserialize<List<JsonElement>>(typeof(Dhikr).Assembly.GetManifestResourceStream("dhikr.json")!)!
            .Select(e => new Dhikr(e.GetProperty("ar").GetString()!, e.GetProperty("n").GetInt32(), e)).ToArray());
}

/// Digital prayer beads. Counts per dhikr are kept for the current day in settings.
public class TasbihForm : Form
{
    const int Recommended = -1; // target = the selected dhikr's recommended count
    static readonly int[] Targets = [Recommended, 33, 99, 100, 0]; // 0 = no limit
    readonly Settings s;
    readonly ListBox list;
    readonly Label ar, translit, meaning, virtue, total, hint;
    readonly Ring ring;
    int idx, n;

    public TasbihForm(Settings settings)
    {
        s = settings;
        Theme.Apply(this);
        Text = L.T("Tasbih");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ResetIfNewDay();

        // Left: every dhikr with today's count
        list = new ListBox
        {
            Dock = DockStyle.Fill, Width = Theme.Dp(300), BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.Dp(40), IntegralHeight = false, Font = Theme.UI(10f),
            Margin = new Padding(0, 0, Theme.Dp(14), 0),
        };
        list.Items.AddRange(Dhikr.All.Value.Select(d => (object)d.Translit).ToArray());
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(selected ? Theme.Line : Theme.Card)) e.Graphics.FillRectangle(bg, e.Bounds);
            if (selected) using (var bar = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y + Theme.Dp(8), Theme.Dp(3), e.Bounds.Height - Theme.Dp(16));
            var count = s.TasbihCounts[e.Index];
            var countW = count > 0 ? Theme.Dp(48) : 0;
            TextRenderer.DrawText(e.Graphics, Dhikr.All.Value[e.Index].Translit, list.Font,
                new Rectangle(e.Bounds.X + Theme.Dp(14), e.Bounds.Y, e.Bounds.Width - Theme.Dp(20) - countW, e.Bounds.Height),
                selected ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (count > 0)
                TextRenderer.DrawText(e.Graphics, count.ToString(), list.Font, new Rectangle(e.Bounds.Right - countW - Theme.Dp(10), e.Bounds.Y, countW, e.Bounds.Height),
                    Theme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        };
        list.HandleCreated += (_, _) => Theme.DarkScrollbars(list);

        // Right: the selected dhikr and the counter (fixed heights so the window doesn't jump between dhikrs)
        int w = Theme.Dp(400);
        Label Block(Font f, Color c, int h) => new()
        {
            AutoSize = false, Width = w, Height = Theme.Dp(h), TextAlign = ContentAlignment.MiddleCenter, Font = f, ForeColor = c,
            Margin = Padding.Empty, UseMnemonic = false,
        };
        ar = Block(new Font(Name99.ArabicFont, 22f * Theme.UiScale), Theme.Text, 110);
        ar.RightToLeft = RightToLeft.Yes;
        translit = Block(Theme.UI(11f, FontStyle.Bold), Theme.Text, 70);
        meaning = Block(Theme.UI(9.5f), Theme.Muted, 44);
        virtue = Block(Theme.UI(9f), NamesForm.Gold, 76);
        ring = new Ring(this) { Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.Dp(6), 0, Theme.Dp(2)) };
        hint = Block(Theme.UI(8.5f), Theme.Muted, 26);

        var target = new Segmented(Targets.Select(t => t switch { Recommended => L.T("TargetRecommended"), 0 => "∞", _ => t.ToString() }),
            Math.Max(0, Array.IndexOf(Targets, s.TasbihTarget)));
        var reset = Theme.Button("↺");
        var targetRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.Dp(4), 0, Theme.Dp(6)) };
        target.Margin = new Padding(0, 0, Theme.Dp(8), 0);
        targetRow.Controls.AddRange([target, reset]);
        total = Block(Theme.UI(10f, FontStyle.Bold), Theme.Accent, 26);

        var right = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
        right.Controls.AddRange([ar, translit, meaning, virtue, ring, hint, targetRow, total]);
        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(Theme.Dp(14)) };
        root.Controls.Add(list);
        root.Controls.Add(right);
        Controls.Add(root);

        list.SelectedIndexChanged += (_, _) => { idx = Math.Max(0, list.SelectedIndex); n = 0; UpdateView(); };
        target.Changed += i => { s.TasbihTarget = Targets[i]; n = 0; Data.Save(s); UpdateView(); };
        reset.Click += (_, _) => { n = 0; UpdateView(); };
        list.SelectedIndex = 0;
    }

    int Target => s.TasbihTarget == Recommended ? Dhikr.All.Value[idx].Recommended : s.TasbihTarget;
    int Current => n;

    void ResetIfNewDay()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var count = Dhikr.All.Value.Length;
        if (s.TasbihDate != today) { s.TasbihDate = today; s.TasbihCounts = new int[count]; }
        else if (s.TasbihCounts.Length != count) { var c = s.TasbihCounts; Array.Resize(ref c, count); s.TasbihCounts = c; } // new dhikrs added
    }

    void UpdateView()
    {
        var d = Dhikr.All.Value[idx];
        ar.Text = d.Ar;
        // long dhikrs get a smaller Arabic font so they fit in the fixed box
        SetFont(ar, new Font(Name99.ArabicFont, (d.Ar.Length > 70 ? 15f : d.Ar.Length > 35 ? 18f : 24f) * Theme.UiScale));
        translit.Text = d.Translit;
        SetFont(translit, Theme.UI(d.Translit.Length > 80 ? 9.5f : d.Translit.Length > 40 ? 10.5f : 12f, FontStyle.Bold));
        meaning.Text = d.Meaning;
        virtue.Text = $"{d.Virtue}\n{d.Source} · {string.Format(L.T("RecommendedTimes"), d.Recommended)}";
        total.Text = $"{L.T("TodayCap")}: {s.TasbihCounts.Sum()}";
        // Goal reached: praise instead of the "click or Space" hint, until the next round starts.
        hint.Text = Done ? praise : L.T("TapOrSpace");
        hint.ForeColor = Done ? Theme.Accent : Theme.Muted;
        SetFont(hint, Done ? Theme.UI(12f, FontStyle.Bold) : Theme.UI(8.5f));
        list.Invalidate();
        ring.Invalidate();
    }

    /// Swap a label's font and free the old one. A font equal to the current one is ignored by the setter
    /// (the label keeps the old object), so in that case the new one is freed instead — never the one in use.
    static void SetFont(Label l, Font f)
    {
        if (l.Font.Equals(f)) { f.Dispose(); return; }
        var old = l.Font;
        l.Font = f;
        old.Dispose();
    }

    bool Done => Target > 0 && n >= Target;

    static readonly string[] PraiseKeys = ["Praise1", "Praise2", "Praise3"];
    string praise = "";

    void Count()
    {
        ResetIfNewDay();
        if (Target > 0 && n >= Target) n = 0;
        n++;
        s.TasbihCounts[idx]++;
        Data.Save(s);
        if (Target > 0 && n == Target)
        {
            SystemSounds.Asterisk.Play();
            praise = L.T(PraiseKeys[Random.Shared.Next(PraiseKeys.Length)]); // a little variety
        }
        UpdateView();
    }

    // Space counts from anywhere in the window (before buttons can treat it as a click).
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Bit 30 of lParam = key was already down: ignore auto-repeat so holding Space counts once.
        if (keyData == Keys.Space) { if ((msg.LParam.ToInt64() & (1L << 30)) == 0) Count(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    class Ring : Control
    {
        readonly TasbihForm f;

        public Ring(TasbihForm owner)
        {
            f = owner;
            Size = new Size(Theme.Dp(200), Theme.Dp(200));
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) f.Count(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            float th = Theme.Dp(12), pad = th / 2 + 2;
            var r = new RectangleF(pad, pad, Width - 2 * pad, Height - 2 * pad);
            using (var track = new Pen(Theme.Card, th)) g.DrawEllipse(track, r);
            var sweep = f.Target > 0 ? 360f * f.Current / f.Target : f.Current % 100 * 3.6f;
            if (f.Current > 0)
                using (var arc = new Pen(Theme.Accent, th) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(arc, r, -90, Math.Max(sweep, 1));
            using var big = Theme.UI(36f, FontStyle.Bold);
            using var small = Theme.UI(11f);
            TextRenderer.DrawText(g, f.Current.ToString(), big, new Rectangle(0, 0, Width, Height - Theme.Dp(20)), f.Done ? Theme.Accent : Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, f.Done ? $"✓ {f.Target}" : f.Target > 0 ? $"/ {f.Target}" : "∞", small, new Rectangle(0, Height / 2 + Theme.Dp(18), Width, Theme.Dp(24)), f.Done ? Theme.Accent : Theme.Muted,
                TextFormatFlags.HorizontalCenter);
        }
    }
}
