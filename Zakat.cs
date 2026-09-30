namespace NamazTimes;

/// Zakat (2.5% of zakatable wealth above the nisab, held for a lunar year) and fitr sadaqa calculators.
/// Values are in tenge and remembered in settings. Nisab: 85 g gold, 595 g silver, or the amount ДУМК announces.
public class ZakatForm : Form
{
    public const decimal GoldNisabGrams = 85m, SilverNisabGrams = 595m, Rate = 0.025m;

    public static decimal Wealth(ZakatInput z) =>
        z.Cash + z.GoldGrams * z.GoldPrice + z.SilverGrams * z.SilverPrice + z.Goods + z.Receivables - z.Debts;

    public static decimal Nisab(ZakatInput z) => z.NisabBasis switch
    {
        0 => GoldNisabGrams * z.GoldPrice,
        1 => SilverNisabGrams * z.SilverPrice,
        _ => z.NisabManual,
    };

    /// Zakat due, or 0 when wealth is below the nisab (or the nisab is unknown).
    public static decimal Due(ZakatInput z) => Nisab(z) > 0 && Wealth(z) >= Nisab(z) ? Math.Round(Wealth(z) * Rate, 0) : 0;

    readonly Settings s;
    readonly Label wealth, nisab, due, fitrTotal;

    public ZakatForm(Settings settings)
    {
        s = settings;
        var z = s.Zakat;
        Theme.Apply(this);
        Text = L.T("Zakat");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        NumericUpDown Money(decimal value, Action<decimal> set, int decimals = 0)
        {
            var n = new NumericUpDown
            {
                Maximum = 100_000_000_000m, DecimalPlaces = decimals, ThousandsSeparator = true, Value = Math.Clamp(value, 0, 100_000_000_000m),
                Width = Theme.Dp(170), Font = Theme.UI(10.5f), BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Right, Anchor = AnchorStyles.Left, Margin = new Padding(0, Theme.Dp(3), 0, Theme.Dp(3)),
            };
            n.ValueChanged += (_, _) => { set(n.Value); Recalc(); };
            return n;
        }
        static void Row(TableLayoutPanel g, string label, Control c)
        {
            g.Controls.Add(Theme.Label(label));
            g.Controls.Add(c);
        }

        // Zakat
        var zakat = Theme.Section(L.T("ZakatSection"), out var g);
        Row(g, L.T("ZCash"), Money(z.Cash, v => z.Cash = v));
        Row(g, L.T("ZGoldGrams"), Money(z.GoldGrams, v => z.GoldGrams = v, 2));
        Row(g, L.T("ZGoldPrice"), Money(z.GoldPrice, v => z.GoldPrice = v));
        Row(g, L.T("ZSilverGrams"), Money(z.SilverGrams, v => z.SilverGrams = v, 2));
        Row(g, L.T("ZSilverPrice"), Money(z.SilverPrice, v => z.SilverPrice = v));
        Row(g, L.T("ZGoods"), Money(z.Goods, v => z.Goods = v));
        Row(g, L.T("ZReceivables"), Money(z.Receivables, v => z.Receivables = v));
        Row(g, L.T("ZDebts"), Money(z.Debts, v => z.Debts = v));
        var basis = new Segmented([L.T("NisabGold"), L.T("NisabSilver"), L.T("NisabManual")], z.NisabBasis);
        Row(g, L.T("NisabBy"), basis);
        var manual = Money(z.NisabManual, v => z.NisabManual = v);
        Row(g, L.T("NisabAmount"), manual);
        basis.Changed += i => { z.NisabBasis = i; manual.Enabled = i == 2; Recalc(); };
        manual.Enabled = z.NisabBasis == 2;

        wealth = Theme.Label("", Theme.Text, 10.5f);
        nisab = Theme.Label("", Theme.Muted, 10f);
        due = Theme.Label("", Theme.Accent, 14f, FontStyle.Bold);
        foreach (var l in new[] { wealth, nisab, due }) { g.Controls.Add(l); g.SetColumnSpan(l, 2); }

        // Fitr sadaqa
        var fitr = Theme.Section(L.T("Fitr"), out var f);
        Row(f, L.T("FitrAmount"), Money(z.FitrAmount, v => z.FitrAmount = v));
        var people = new Stepper(z.FitrPeople, 1, 50, 1);
        Row(f, L.T("FitrPeople"), people);
        fitrTotal = Theme.Label("", Theme.Accent, 12f, FontStyle.Bold);
        f.Controls.Add(fitrTotal); f.SetColumnSpan(fitrTotal, 2);
        people.ValueChanged += v => { z.FitrPeople = v; Recalc(); };

        var hint = Theme.Label(L.T("ZakatHint"), Theme.Muted, 9f);
        hint.MaximumSize = new Size(Theme.Dp(560), 0);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(Theme.Dp(16)) };
        root.Controls.AddRange([zakat, fitr, hint]);
        Controls.Add(root);
        Recalc();
    }

    void Recalc()
    {
        var z = s.Zakat;
        string T(decimal v) => v.ToString("N0", L.Culture) + " ₸";
        wealth.Text = $"{L.T("ZTotal")}: {T(Wealth(z))}";
        nisab.Text = Nisab(z) > 0 ? $"{L.T("NisabBy")}: {T(Nisab(z))}" : L.T("NisabUnknown");
        due.Text = Nisab(z) <= 0 ? "" : Due(z) > 0 ? $"{L.T("ZDue")}: {T(Due(z))}" : L.T("ZBelowNisab");
        fitrTotal.Text = z.FitrAmount > 0 ? $"{L.T("FitrTotal")}: {T(z.FitrAmount * z.FitrPeople)}" : L.T("FitrUnknown");
        Data.Save(s);
    }
}
