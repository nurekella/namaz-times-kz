using System.Media;
using System.Text.Json;

namespace NamazTimes;

/// Morning and evening adhkar: Arabic, transliteration, meaning, source, and a counter per item
/// (click the card or its button, or press Space for the next unfinished one). Progress is kept per day.
/// Texts live in adhkar.json (kk/ru/en) for review.
public class AdhkarForm : Form
{
    static readonly Lazy<List<JsonElement>> All = new(() =>
        JsonSerializer.Deserialize<List<JsonElement>>(typeof(AdhkarForm).Assembly.GetManifestResourceStream("adhkar.json")!)!);

    static string Tr(JsonElement e, string field) => e.GetProperty(field).GetProperty(L.Lang is "kk" or "en" ? L.Lang : "ru").GetString()!;

    /// One adhkar card on screen, with its counter button.
    record Item(string Key, int Target, TableLayoutPanel Card, Button Counter, List<Label> Texts);

    readonly Settings s;
    readonly Panel scroll;
    readonly TableLayoutPanel body;
    readonly Label progress;
    readonly List<Item> items = [];
    bool morning;
    static readonly Color Faded = Color.FromArgb(96, 105, 120);

    /// Morning adhkar until Dhuhr, evening ones after (unless told which).
    public AdhkarForm(Settings settings, bool? morningAdhkar = null)
    {
        s = settings;
        morning = morningAdhkar ?? DateTime.Now.Hour < 12;
        Theme.Apply(this);
        Text = L.T("Adhkar");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(760), Theme.Dp(720));
        Padding = new Padding(Theme.Dp(12));

        var which = new Segmented([L.T("AdhkarMorning"), L.T("AdhkarEvening")], morning ? 0 : 1) { Margin = new Padding(0, 0, Theme.Dp(16), 0) };
        progress = Theme.Label("", Theme.Accent, 11f, FontStyle.Bold);
        var reset = Theme.Button("↺");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Theme.Dp(50), WrapContents = false };
        top.Controls.AddRange([which, progress, reset]);
        var hint = Theme.Label(L.T("AdhkarCountHint"), Theme.Muted, 8.5f);
        hint.Dock = DockStyle.Bottom;
        hint.Padding = new Padding(0, Theme.Dp(6), 0, 0);
        body = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, Padding = new Padding(0, 0, Theme.Dp(8), Theme.Dp(12)) };
        scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
        scroll.Controls.Add(body);
        scroll.HandleCreated += (_, _) => Theme.DarkScrollbars(scroll);
        Controls.AddRange([scroll, top, hint]);

        which.Changed += i => { morning = i == 0; Fill(resetScroll: true); };
        reset.Click += (_, _) =>
        {
            foreach (var it in items) s.AdhkarCounts.Remove(it.Key);
            Data.Save(s);
            items.ForEach(UpdateItem);
            UpdateProgress();
        };
        scroll.Resize += (_, _) => Fill(resetScroll: false); // re-wrap to the new width
        Load += (_, _) => Fill(resetScroll: true);
    }

    void EnsureToday()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (s.AdhkarDate == today) return;
        s.AdhkarDate = today;
        s.AdhkarCounts = [];
    }

    int CountOf(Item it) => s.AdhkarCounts.GetValueOrDefault(it.Key);

    void Fill(bool resetScroll)
    {
        EnsureToday();
        var keep = scroll.AutoScrollPosition;
        int w = Math.Max(Theme.Dp(240), scroll.ClientSize.Width - Theme.Dp(40));
        body.SuspendLayout();
        foreach (Control c in body.Controls) c.Dispose();
        body.Controls.Clear();
        items.Clear();
        var period = morning ? "morning" : "evening";
        foreach (var (e, index) in All.Value.Select((e, i) => (e, i)))
        {
            var when = e.GetProperty("when").GetString();
            if (when != "both" && when != period) continue;
            var card = new TableLayoutPanel
            {
                AutoSize = true, ColumnCount = 1, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, Theme.Dp(12)),
                Padding = new Padding(Theme.Dp(16), Theme.Dp(12), Theme.Dp(16), Theme.Dp(14)), Cursor = Cursors.Hand,
            };
            int inner = w - Theme.Dp(32);
            var texts = new List<Label>();
            Label Lbl(string text, Font font, Color color, int after, int width)
            {
                var l = new Label
                {
                    Text = text, Font = font, ForeColor = color, AutoSize = true, MaximumSize = new Size(width, 0),
                    Margin = new Padding(0, 0, 0, Theme.Dp(after)), UseMnemonic = false, Tag = color,
                };
                texts.Add(l);
                return l;
            }

            // Header: title on the left, counter button on the right
            var counter = Theme.Button("");
            counter.AutoSize = false;
            counter.Size = new Size(Theme.Dp(104), Theme.Dp(34));
            counter.Anchor = AnchorStyles.Right;
            var header = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, Theme.Dp(8)), Width = inner };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var title = Lbl(Tr(e, "title"), Theme.UI(11f, FontStyle.Bold), Theme.Accent, 0, inner - Theme.Dp(120));
            title.Anchor = AnchorStyles.Left;
            header.Controls.AddRange([title, counter]);
            header.MinimumSize = new Size(inner, 0);
            card.Controls.Add(header);

            var ar = Lbl(e.GetProperty("ar").GetString()!, new Font(Name99.ArabicFont, 19f * Theme.UiScale), Theme.Text, 8, inner);
            ar.RightToLeft = RightToLeft.Yes;
            ar.MinimumSize = new Size(inner, 0);
            ar.TextAlign = ContentAlignment.TopRight;
            card.Controls.Add(ar);
            card.Controls.Add(Lbl(Tr(e, "t"), Theme.UI(10f, FontStyle.Italic), Theme.Muted, 8, inner));
            card.Controls.Add(Lbl(Tr(e, "m"), Theme.UI(10.5f), Theme.Text, 8, inner));
            card.Controls.Add(Lbl(Tr(e, "ref"), Theme.UI(9f), NamesForm.Gold, 0, inner));
            body.Controls.Add(card);

            var item = new Item((morning ? "m" : "e") + index, e.GetProperty("n").GetInt32(), card, counter, texts);
            items.Add(item);
            // The whole card counts, not just the button — a big target, like the tasbih ring.
            foreach (var c in new Control[] { card, header, counter }.Concat(texts)) c.Click += (_, _) => Count(item);
            UpdateItem(item);
        }
        body.ResumeLayout();
        scroll.AutoScrollPosition = resetScroll ? Point.Empty : new Point(-keep.X, -keep.Y);
        UpdateProgress();
    }

    void Count(Item it)
    {
        EnsureToday();
        if (CountOf(it) >= it.Target) return;
        s.AdhkarCounts[it.Key] = CountOf(it) + 1;
        Data.Save(s);
        UpdateItem(it);
        UpdateProgress();
        if (CountOf(it) == it.Target) SystemSounds.Asterisk.Play();
    }

    void UpdateItem(Item it)
    {
        var n = CountOf(it);
        var done = n >= it.Target;
        it.Counter.Text = done ? $"✓ {n} / {it.Target}" : $"{n} / {it.Target}";
        it.Counter.BackColor = done ? Theme.Accent : Theme.Card;
        it.Counter.ForeColor = done ? Theme.Bg : Theme.Text;
        foreach (var l in it.Texts) l.ForeColor = done ? Faded : (Color)l.Tag!; // finished cards fade out
    }

    void UpdateProgress()
    {
        var done = items.Count(it => CountOf(it) >= it.Target);
        progress.Text = string.Format(L.T("AdhkarProgress"), done, items.Count);
        progress.Margin = new Padding(0, Theme.Dp(8), Theme.Dp(12), 0);
    }

    // Space: count the first unfinished adhkar and bring it into view.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData != Keys.Space) return base.ProcessCmdKey(ref msg, keyData);
        if ((msg.LParam.ToInt64() & (1L << 30)) == 0 && items.FirstOrDefault(it => CountOf(it) < it.Target) is { } next)
        {
            Count(next);
            scroll.ScrollControlIntoView(next.Card);
        }
        return true;
    }
}
