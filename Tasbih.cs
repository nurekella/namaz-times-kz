using System.Drawing.Drawing2D;
using System.Media;

namespace NamazTimes;

public record Dhikr(string Ar, string Kk, string Ru, string En, string Mkk, string Mru, string Men)
{
    public string Translit => L.Lang switch { "kk" => Kk, "en" => En, _ => Ru };
    public string Meaning => L.Lang switch { "kk" => Mkk, "en" => Men, _ => Mru };

    public static readonly Dhikr[] All =
    [
        new("سُبْحَانَ ٱللَّٰهِ", "Субханаллаһ", "Субханаллах", "SubhanAllah",
            "Аллаһ барлық кемшіліктен пәк", "Пречист Аллах", "Glory be to Allah"),
        new("ٱلْحَمْدُ لِلَّٰهِ", "Әлхамдулиллаһ", "Альхамдулиллях", "Alhamdulillah",
            "Барлық мақтау Аллаһқа тән", "Хвала Аллаху", "All praise is due to Allah"),
        new("ٱللَّٰهُ أَكْبَرُ", "Аллаһу әкбар", "Аллаху акбар", "Allahu Akbar",
            "Аллаһ ең Ұлы", "Аллах велик", "Allah is the Greatest"),
        new("ٱللَّٰهُمَّ صَلِّ عَلَىٰ مُحَمَّدٍ", "Салауат", "Салават", "Salawat",
            "Аллаһым, Мұхаммедке салауат айта гөр", "О Аллах, благослови Мухаммада", "O Allah, send blessings upon Muhammad"),
        new("أَسْتَغْفِرُ ٱللَّٰهَ", "Истиғфар", "Истигфар", "Istighfar",
            "Аллаһтан кешірім сұраймын", "Прошу прощения у Аллаха", "I seek Allah's forgiveness"),
    ];
}

/// Digital prayer beads. Counts per dhikr are kept for the current day in settings.
public class TasbihForm : Form
{
    static readonly int[] Targets = [33, 99, 100, 0]; // 0 = no limit
    readonly Settings s;
    readonly Label ar, translit, meaning, totals;
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

        int w = Theme.Dp(360);
        Label Centered(Font f, Color c, int h) => new()
        {
            AutoSize = false, Width = w, Height = Theme.Dp(h), TextAlign = ContentAlignment.MiddleCenter, Font = f, ForeColor = c,
            Margin = Padding.Empty, UseMnemonic = false,
        };
        var chips = new Segmented(Dhikr.All.Select(d => d.Translit), 0) { WrapContents = true, MaximumSize = new Size(w, 0), Anchor = AnchorStyles.None };
        ar = Centered(new Font(Name99.ArabicFont, 24f), Theme.Text, 56);
        ar.RightToLeft = RightToLeft.Yes;
        translit = Centered(Theme.UI(12f, FontStyle.Bold), Theme.Text, 26);
        meaning = Centered(Theme.UI(9.5f), Theme.Muted, 40);
        ring = new Ring(this) { Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.Dp(8), 0, Theme.Dp(4)) };
        var hint = Centered(Theme.UI(8.5f), Theme.Muted, 22);
        hint.Text = L.T("TapOrSpace");
        var target = new Segmented(Targets.Select(t => t == 0 ? "∞" : t.ToString()), Math.Max(0, Array.IndexOf(Targets, s.TasbihTarget)));
        var reset = Theme.Button("↺ " + L.T("Reset"));
        var targetRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.Dp(4), 0, Theme.Dp(8)) };
        target.Margin = new Padding(0, 0, Theme.Dp(8), 0);
        targetRow.Controls.AddRange([target, reset]);
        totals = new Label
        {
            AutoSize = false, Width = w, Height = Theme.Dp(130), BackColor = Theme.Card, ForeColor = Theme.Text,
            Font = Theme.UI(9.5f), Padding = new Padding(Theme.Dp(12), Theme.Dp(8), Theme.Dp(12), Theme.Dp(8)), Margin = Padding.Empty,
        };

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(Theme.Dp(14)) };
        root.Controls.AddRange([chips, ar, translit, meaning, ring, hint, targetRow, totals]);
        Controls.Add(root);

        chips.Changed += i => { idx = i; n = 0; UpdateView(); };
        target.Changed += i => { s.TasbihTarget = Targets[i]; n = 0; Data.Save(s); ring.Invalidate(); };
        reset.Click += (_, _) => { n = 0; ring.Invalidate(); };
        UpdateView();
    }

    int Target => s.TasbihTarget;
    int Current => n;

    void ResetIfNewDay()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (s.TasbihDate == today && s.TasbihCounts.Length == Dhikr.All.Length) return;
        s.TasbihDate = today;
        s.TasbihCounts = new int[Dhikr.All.Length];
    }

    void UpdateView()
    {
        var d = Dhikr.All[idx];
        ar.Text = d.Ar;
        translit.Text = d.Translit;
        meaning.Text = d.Meaning;
        var lines = Dhikr.All.Select((x, i) => (x, c: s.TasbihCounts[i])).Where(t => t.c > 0).Select(t => $"{t.x.Translit}: {t.c}");
        totals.Text = $"{L.T("TodayCap")}: {s.TasbihCounts.Sum()}\n" + string.Join("\n", lines);
        ring.Invalidate();
    }

    void Count()
    {
        ResetIfNewDay();
        if (Target > 0 && n >= Target) n = 0;
        n++;
        s.TasbihCounts[idx]++;
        Data.Save(s);
        if (Target > 0 && n == Target) SystemSounds.Asterisk.Play();
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
            TextRenderer.DrawText(g, f.Current.ToString(), big, new Rectangle(0, 0, Width, Height - Theme.Dp(20)), Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, f.Target > 0 ? $"/ {f.Target}" : "∞", small, new Rectangle(0, Height / 2 + Theme.Dp(18), Width, Theme.Dp(24)), Theme.Muted,
                TextFormatFlags.HorizontalCenter);
        }
    }
}
