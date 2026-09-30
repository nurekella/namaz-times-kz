using System.Text.Json;

namespace NamazTimes;

/// Morning and evening adhkar to read (not a counter): Arabic, transliteration, meaning, how many times and the source.
/// Texts live in adhkar.json (kk/ru/en) for review.
public class AdhkarForm : Form
{
    static readonly Lazy<List<JsonElement>> All = new(() =>
        JsonSerializer.Deserialize<List<JsonElement>>(typeof(AdhkarForm).Assembly.GetManifestResourceStream("adhkar.json")!)!);

    static string Tr(JsonElement e, string field) => e.GetProperty(field).GetProperty(L.Lang is "kk" or "en" ? L.Lang : "ru").GetString()!;

    readonly Panel scroll;
    readonly TableLayoutPanel body;
    bool morning;

    /// Morning adhkar until Dhuhr, evening ones after (unless told which).
    public AdhkarForm(bool? morningAdhkar = null)
    {
        morning = morningAdhkar ?? DateTime.Now.Hour < 12;
        Theme.Apply(this);
        Text = L.T("Adhkar");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(760), Theme.Dp(720));
        Padding = new Padding(Theme.Dp(12));

        var which = new Segmented([L.T("AdhkarMorning"), L.T("AdhkarEvening")], morning ? 0 : 1) { Location = Point.Empty };
        var bar = new Panel { Dock = DockStyle.Top, Height = Theme.Dp(46) };
        bar.Controls.Add(which);
        body = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, Padding = new Padding(0, 0, Theme.Dp(8), Theme.Dp(12)) };
        scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
        scroll.Controls.Add(body);
        scroll.HandleCreated += (_, _) => Theme.DarkScrollbars(scroll);
        Controls.AddRange([scroll, bar]);

        which.Changed += i => { morning = i == 0; Fill(); };
        scroll.Resize += (_, _) => Fill(); // re-wrap to the new width
        Load += (_, _) => Fill();
    }

    void Fill()
    {
        int w = Math.Max(Theme.Dp(240), scroll.ClientSize.Width - Theme.Dp(40));
        body.SuspendLayout();
        foreach (Control c in body.Controls) c.Dispose();
        body.Controls.Clear();
        foreach (var e in All.Value.Where(e => e.GetProperty("when").GetString() is "both" || e.GetProperty("when").GetString() == (morning ? "morning" : "evening")))
        {
            var card = new TableLayoutPanel
            {
                AutoSize = true, ColumnCount = 1, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, Theme.Dp(12)),
                Padding = new Padding(Theme.Dp(16), Theme.Dp(12), Theme.Dp(16), Theme.Dp(14)),
            };
            int inner = w - Theme.Dp(32);
            Label Lbl(string text, Font font, Color color, int after) => new()
            {
                Text = text, Font = font, ForeColor = color, AutoSize = true, MaximumSize = new Size(inner, 0),
                Margin = new Padding(0, 0, 0, Theme.Dp(after)), UseMnemonic = false,
            };
            var times = e.GetProperty("n").GetInt32();
            card.Controls.Add(Lbl($"{Tr(e, "title")}  ·  {string.Format(L.T("TimesN"), times)}", Theme.UI(11f, FontStyle.Bold), Theme.Accent, 8));
            var ar = Lbl(e.GetProperty("ar").GetString()!, new Font(Name99.ArabicFont, 19f * Theme.UiScale), Theme.Text, 8);
            ar.RightToLeft = RightToLeft.Yes;
            ar.MinimumSize = new Size(inner, 0);
            ar.TextAlign = ContentAlignment.TopRight;
            card.Controls.Add(ar);
            card.Controls.Add(Lbl(Tr(e, "t"), Theme.UI(10f, FontStyle.Italic), Theme.Muted, 8));
            card.Controls.Add(Lbl(Tr(e, "m"), Theme.UI(10.5f), Theme.Text, 8));
            card.Controls.Add(Lbl(Tr(e, "ref"), Theme.UI(9f), NamesForm.Gold, 0));
            body.Controls.Add(card);
        }
        body.ResumeLayout();
        scroll.AutoScrollPosition = Point.Empty;
    }
}
