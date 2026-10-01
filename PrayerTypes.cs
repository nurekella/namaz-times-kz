using System.Text.Json;

namespace NamazTimes;

/// Voluntary and special prayers: ruling, time, rak'ahs, how to pray, and an ayah / hadith as evidence.
/// Texts live in prayers.json (kk/ru/en) so they can be reviewed without touching code.
public class PrayerTypesForm : Form
{
    static readonly Lazy<List<JsonElement>> All = new(() =>
        JsonSerializer.Deserialize<List<JsonElement>>(typeof(PrayerTypesForm).Assembly.GetManifestResourceStream("prayers.json")!)!);

    /// Picks the current language from an object like {"kk": …, "ru": …, "en": …}.
    static string Tr(JsonElement e) => e.GetProperty(L.Lang is "kk" or "en" ? L.Lang : "ru").GetString()!;

    readonly ListBox list;
    readonly Panel scroll;
    readonly TableLayoutPanel body;

    public PrayerTypesForm()
    {
        Theme.Apply(this, '\uE736');
        Text = L.T("PrayerTypes");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(960), Theme.Dp(680));
        Padding = new Padding(Theme.Dp(12));

        list = new ListBox
        {
            Dock = DockStyle.Left, Width = Theme.Dp(270), BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.Dp(42), IntegralHeight = false, Font = Theme.UI(10.5f),
        };
        list.Items.AddRange(All.Value.Select(p => (object)Tr(p)).ToArray());
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(selected ? Theme.Line : Theme.Card)) e.Graphics.FillRectangle(bg, e.Bounds);
            if (selected) using (var bar = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y + Theme.Dp(8), Theme.Dp(3), e.Bounds.Height - Theme.Dp(16));
            TextRenderer.DrawText(e.Graphics, list.Items[e.Index].ToString(), list.Font,
                new Rectangle(e.Bounds.X + Theme.Dp(14), e.Bounds.Y, e.Bounds.Width - Theme.Dp(18), e.Bounds.Height),
                selected ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        list.HandleCreated += (_, _) => Theme.DarkScrollbars(list);

        body = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, Padding = new Padding(Theme.Dp(8), 0, Theme.Dp(8), Theme.Dp(12)) };
        scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
        scroll.Controls.Add(body);
        scroll.HandleCreated += (_, _) => Theme.DarkScrollbars(scroll);
        var gap = new Panel { Dock = DockStyle.Left, Width = Theme.Dp(12) };
        var note = Theme.Label(L.T("PrayerTypesNote"), Theme.Muted, 8.5f);
        note.Dock = DockStyle.Bottom;
        note.Margin = Padding.Empty;
        note.Padding = new Padding(0, Theme.Dp(8), 0, 0);
        Controls.AddRange([scroll, gap, list, note]);

        list.SelectedIndexChanged += (_, _) => ShowPrayer();
        scroll.Resize += (_, _) => ShowPrayer(); // re-wrap text to the new width
        Load += (_, _) => list.SelectedIndex = 0;
    }

    void ShowPrayer()
    {
        if (list.SelectedIndex < 0) return;
        var p = All.Value[list.SelectedIndex];
        int w = Math.Max(Theme.Dp(200), scroll.ClientSize.Width - Theme.Dp(48));

        Label Lbl(string text, Font font, Color color, Padding margin) => new()
        {
            Text = text, Font = font, ForeColor = color, AutoSize = true, MaximumSize = new Size(w, 0), Margin = margin, UseMnemonic = false,
        };
        Padding After(int px) => new(0, 0, 0, Theme.Dp(px));

        body.SuspendLayout();
        foreach (Control c in body.Controls) c.Dispose();
        body.Controls.Clear();

        body.Controls.Add(Lbl(Tr(p), Theme.UI(17f, FontStyle.Bold), Theme.Text, After(2)));
        body.Controls.Add(Lbl(Tr(p.GetProperty("status")), Theme.UI(10f, FontStyle.Bold), Theme.Accent, After(14)));
        foreach (var (key, field) in new[] { ("TimeLbl", "time"), ("Rakats", "rakats"), ("HowToPray", "how") })
        {
            body.Controls.Add(Lbl(L.T(key), Theme.UI(9f), Theme.Muted, After(2)));
            body.Controls.Add(Lbl(Tr(p.GetProperty(field)), Theme.UI(10.5f), Theme.Text, After(12)));
        }

        // Evidence blocks: ayah (Arabic + meaning) and hadith, each on a card.
        void Evidence(string heading, JsonElement e, bool arabic)
        {
            var card = new TableLayoutPanel
            {
                AutoSize = true, ColumnCount = 1, BackColor = Theme.Card, Margin = After(12),
                Padding = new Padding(Theme.Dp(14), Theme.Dp(10), Theme.Dp(14), Theme.Dp(12)),
            };
            int inner = w - Theme.Dp(28);
            Label In(string text, Font font, Color color, int after) { var l = Lbl(text, font, color, After(after)); l.MaximumSize = new Size(inner, 0); return l; }
            card.Controls.Add(In($"{heading} · {Tr(e.GetProperty("ref"))}", Theme.UI(9f, FontStyle.Bold), Names99Gold, 6));
            if (arabic)
            {
                var ar = In(e.GetProperty("ar").GetString()!, new Font(Name99.ArabicFont, 20f * Theme.UiScale), Theme.Text, 6);
                ar.RightToLeft = RightToLeft.Yes;
                ar.MinimumSize = new Size(inner, 0);
                ar.TextAlign = ContentAlignment.TopRight;
                card.Controls.Add(ar);
            }
            card.Controls.Add(In(Tr(e), Theme.UI(10.5f, arabic ? FontStyle.Italic : FontStyle.Regular), Theme.Text, 0));
            body.Controls.Add(Theme.Round(card));
        }
        if (p.TryGetProperty("ayah", out var ayah)) Evidence(L.T("Ayah"), ayah, true);
        Evidence(L.T("Hadith"), p.GetProperty("hadith"), false);

        body.ResumeLayout();
        scroll.AutoScrollPosition = Point.Empty;
    }

    static Color Names99Gold => NamesForm.Gold;
}
