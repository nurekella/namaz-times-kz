namespace NamazTimes;

/// Missed prayers (qada) tracker: a counter per prayer (plus Witr, wajib in the Hanafi madhab).
/// "Made up" lowers a counter, "+" adds a missed one; a period (days/months/years) can be added to all at once.
public class QadaForm : Form
{
    static readonly string[] Keys = ["Fajr", "Dhuhr", "Asr", "Maghrib", "Isha", "Witr"];
    static readonly int[] PeriodDays = [1, 30, 365]; // ponytail: a year is counted as 365 days; switch to 354 if a scholar advises lunar years

    readonly Settings s;
    readonly Dictionary<string, Label> counts = [];
    readonly Label total;

    public QadaForm(Settings settings)
    {
        s = settings;
        Theme.Apply(this);
        Text = L.T("Qada");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // Counters: one row = name, count, "made up" (−1), "+" (+1)
        void AddRow(TableLayoutPanel g, string key, string label)
        {
            var name = Theme.Label(label, Theme.Text, 11f);
            name.Margin = new Padding(0, Theme.Dp(10), Theme.Dp(24), Theme.Dp(10));
            var count = new Label
            {
                AutoSize = false, Size = new Size(Theme.Dp(90), Theme.Dp(34)), TextAlign = ContentAlignment.MiddleRight,
                Font = Theme.UI(16f, FontStyle.Bold), ForeColor = Theme.Text, Margin = new Padding(0, 0, Theme.Dp(16), 0), Anchor = AnchorStyles.None,
            };
            counts[key] = count;
            var madeUp = Theme.Button("✓ " + L.T("QadaMadeUp"), primary: true);
            madeUp.Anchor = AnchorStyles.None;
            madeUp.Margin = new Padding(0, Theme.Dp(4), Theme.Dp(8), Theme.Dp(4));
            madeUp.Click += (_, _) => Change(key, -1);
            var add = Theme.Button("+");
            add.Anchor = AnchorStyles.Left;
            add.Click += (_, _) => Change(key, +1);
            g.Controls.AddRange([name, count, madeUp, add]);
        }
        var list = Theme.Section(L.T("Qada"), out var g, 4);
        foreach (var key in Keys) AddRow(g, key, key == "Witr" ? L.T("Witr") : L.Name(Enum.Parse<P>(key)));
        total = Theme.Label("", Theme.Accent, 11.5f, FontStyle.Bold);
        total.Margin = new Padding(0, Theme.Dp(12), 0, Theme.Dp(2));
        g.Controls.Add(total);
        g.SetColumnSpan(total, 4);

        // Add a whole period to every prayer at once
        var bulk = Theme.Section(L.T("QadaAddPeriod"), out var b, 3);
        var amount = new NumericUpDown
        {
            Minimum = 1, Maximum = 36500, Value = 1, Width = Theme.Dp(90), Font = Theme.UI(11f),
            BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Left,
            Margin = new Padding(0, Theme.Dp(4), Theme.Dp(10), Theme.Dp(4)),
        };
        var unit = new Segmented([L.T("UnitDays"), L.T("UnitMonths"), L.T("UnitYears")], 0) { Margin = new Padding(0, 0, Theme.Dp(10), 0) };
        var addAll = Theme.Button(L.T("QadaAddAll"));
        addAll.Anchor = AnchorStyles.Left;
        addAll.Click += (_, _) =>
        {
            var n = (int)amount.Value * PeriodDays[unit.Selected];
            if (MessageBox.Show(this, string.Format(L.T("QadaConfirm"), n), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            foreach (var key in Keys) s.Qada[key] = s.Qada.GetValueOrDefault(key) + n;
            Save();
        };
        b.Controls.AddRange([amount, unit, addAll]);

        var hint = Theme.Label(L.T("QadaHint"), Theme.Muted, 9f);
        hint.MaximumSize = new Size(Theme.Dp(560), 0);
        hint.Margin = new Padding(0, Theme.Dp(4), 0, 0);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(Theme.Dp(16)) };
        // Missed fasting days are counted separately: one day made up for each day missed.
        var fasting = Theme.Section(L.T("QadaFast"), out var fg, 4);
        AddRow(fg, "Fast", L.T("QadaFastDays"));
        root.Controls.AddRange([list, bulk, fasting, hint]);
        Controls.Add(root);
        UpdateCounts();
    }

    void Change(string key, int delta)
    {
        s.Qada[key] = Math.Max(0, s.Qada.GetValueOrDefault(key) + delta);
        Save();
    }

    void Save()
    {
        Data.Save(s);
        UpdateCounts();
    }

    void UpdateCounts()
    {
        foreach (var (key, label) in counts)
        {
            var n = s.Qada.GetValueOrDefault(key);
            label.Text = n.ToString("N0", L.Culture);
            label.ForeColor = n == 0 ? Theme.Muted : Theme.Text;
        }
        total.Text = string.Format(L.T("QadaTotal"), Keys.Sum(k => s.Qada.GetValueOrDefault(k)).ToString("N0", L.Culture));

    }
}
