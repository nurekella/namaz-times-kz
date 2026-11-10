namespace NamazTimes;

/// Settings page, laid out like Windows 11 Settings: groups of setting cards in one column
/// (title and hint on the left, the control on the right). Every change applies at once — no Save button.
public class SettingsForm : Form
{
    readonly Settings s;
    readonly Action<bool> changed; // true = language/theme/scale changed, so windows need rebuilding
    readonly TableLayoutPanel body;

    public SettingsForm(Settings settings, Action<bool> changed, Action? testAlert = null, Func<IWin32Window, Task>? checkUpdates = null)
    {
        s = settings;
        this.changed = changed;
        Theme.Apply(this, '');
        Text = L.T("Settings");
        ClientSize = new Size(Theme.Dp(900), Theme.Dp(760));

        body = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, Padding = new Padding(Theme.Dp(8), 0, Theme.Dp(8), Theme.Dp(24)) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
        scroll.Controls.Add(body);
        scroll.HandleCreated += (_, _) => Theme.DarkScrollbars(scroll);
        Controls.Add(scroll);
        // Content keeps a comfortable reading width, centred when the window is wide.
        scroll.Resize += (_, _) =>
        {
            var side = Math.Max(Theme.Dp(8), (scroll.ClientSize.Width - Theme.Dp(1000)) / 2);
            body.Padding = new Padding(side, 0, side, Theme.Dp(24));
        };

        // General
        Header(L.T("General"));
        var lang = new Segmented(L.Languages.Select(l => l.Name), Array.FindIndex(L.Languages, l => l.Code == s.Lang));
        lang.Changed += i => Set(() => s.Lang = L.Languages[i].Code, rebuild: true);
        Card('', L.T("Language"), null, lang);
        var cityBtn = Theme.Button(s.City.Title + "  ›");
        cityBtn.Click += (_, _) =>
        {
            using var f = new CityPicker();
            if (f.ShowDialog(this) != DialogResult.OK || f.Selected == null) return;
            cityBtn.Text = f.Selected.Title + "  ›";
            Set(() => s.City = f.Selected);
        };
        Card('', L.T("City"), null, cityBtn);
        var madhab = new Segmented([L.T("Hanafi"), L.T("OtherMadhabs")], s.Hanafi ? 0 : 1);
        madhab.Changed += i => Set(() => s.Hanafi = i == 0);
        Card('', L.T("AsrMethod"), null, madhab);
        var timeFormat = new Segmented([L.T("H24"), L.T("H12")], s.Hour12 ? 1 : 0);
        timeFormat.Changed += i => Set(() => s.Hour12 = i == 1);
        Card('', L.T("TimeFormat"), null, timeFormat);
        var fasting = new Segmented([L.T("FastAuto"), L.T("FastAlways"), L.T("FastOff")], (int)s.Fasting);
        fasting.Changed += i => Set(() => s.Fasting = (FastingMode)i);
        Card('', L.T("FastTimes"), null, fasting);
        var hijri = new Stepper(s.HijriAdjust, -2, 2, 1, v => v > 0 ? $"+{v}" : v.ToString());
        hijri.ValueChanged += v => Set(() => s.HijriAdjust = v);
        Card('', L.T("HijriAdjust"), null, hijri);

        // Appearance
        Header(L.T("Appearance"));
        var themeMode = new Segmented([L.T("ThemeSystem"), L.T("ThemeDark"), L.T("ThemeLight")], s.ThemeMode);
        themeMode.Changed += i => Set(() => s.ThemeMode = i, rebuild: true);
        Card('', L.T("ThemeMode"), null, themeMode);
        var uiScale = new Stepper(s.UiScale, 80, 160, 10, v => v + "%");
        uiScale.ValueChanged += v => Set(() => s.UiScale = v, rebuild: true);
        Card('', L.T("WindowScale"), null, uiScale);

        // Notifications
        Header(L.T("Notifications"));
        Card('', L.T("NotifyOn"), null, Switch(!s.Muted, v => s.Muted = !v));
        string MinOrOff(int v) => v == 0 ? L.T("Off") : string.Format(L.T("Min"), v);
        var remind = new Stepper(s.RemindBefore, 0, 60, 5, MinOrOff);
        remind.ValueChanged += v => Set(() => s.RemindBefore = v);
        Card('', L.T("RemindBefore"), null, remind);
        var jumuahMin = new Stepper(s.JumuahBefore, 15, 180, 15, MinOrOff);
        jumuahMin.ValueChanged += v => Set(() => s.JumuahBefore = v);
        var jumuahRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        jumuahRow.Controls.AddRange([jumuahMin, Switch(s.Jumuah, v => s.Jumuah = v)]);
        Card('', L.T("JumuahRemind"), null, jumuahRow);
        var test = Theme.Button(L.T("TestAlert"));
        test.Click += (_, _) => testAlert?.Invoke();
        Card('', L.T("TestAlert"), null, test);

        // Prayer times: show / notify / offset for each
        Header(L.T("Prayers"));
        foreach (var p in Enum.GetValues<P>())
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            var off = new Stepper(s.Offsets.GetValueOrDefault(p), -30, 30, 1, v => v > 0 ? $"+{v}" : v.ToString());
            off.ValueChanged += v => Set(() => { if (v == 0) s.Offsets.Remove(p); else s.Offsets[p] = v; });
            row.Controls.AddRange([
                Labelled(L.T("Show"), Switch(!s.Hidden.Contains(p), v => { if (v) s.Hidden.Remove(p); else s.Hidden.Add(p); })),
                Labelled(L.T("Notify"), Switch(s.Alerts.Contains(p), v => { if (v) s.Alerts.Add(p); else s.Alerts.Remove(p); })),
                Labelled(L.T("Offset"), off),
            ]);
            Card('', L.Name(p), null, row);
        }

        // Reminders
        Header(L.T("Reminders"));
        Card('', L.T("RemFasts"), null, Switch(s.RemindSunnahFasts, v => s.RemindSunnahFasts = v));
        Card('', L.T("RemHolidays"), null, Switch(s.RemindHolidays, v => s.RemindHolidays = v));
        Card('', L.T("RemKahf"), null, Switch(s.RemindKahf, v => s.RemindKahf = v));
        Card('', L.T("RemAdhkar"), null, Switch(s.RemindAdhkar, v => s.RemindAdhkar = v));
        Card('', L.T("RemFullscreen"), null, Switch(s.FullscreenAlert, v => s.FullscreenAlert = v));

        // Widget
        Header(L.T("Widget"));
        var zoom = new Stepper(s.Zoom, 70, 250, 10, v => v + "%");
        zoom.ValueChanged += v => Set(() => s.Zoom = v);
        Card('', L.T("Size"), L.T("ResizeHint"), zoom);
        var opacity = new Stepper(s.Opacity, 30, 100, 10, v => v + "%");
        opacity.ValueChanged += v => Set(() => s.Opacity = v);
        Card('', L.T("Opacity"), null, opacity);
        Card('', L.T("ShowNameOfDay"), null, Switch(s.ShowNameOfDay, v => s.ShowNameOfDay = v));
        Card('', L.T("TopMost"), null, Switch(s.TopMost, v => s.TopMost = v));
        var autoStart = new Toggle(Widget.AutoStart);
        autoStart.CheckedChanged += (_, _) => Widget.AutoStart = autoStart.Checked;
        Card('', L.T("AutoStart"), null, autoStart);

        // About this app: version, updates, backup
        Header(L.T("About"));
        var check = Theme.Button("↻ " + L.T("CheckUpdates"));
        check.Click += async (_, _) => { check.Enabled = false; try { await (checkUpdates?.Invoke(this) ?? Task.CompletedTask); } finally { check.Enabled = true; } };
        Card('', $"Namaz Times KZ {Updates.Current.ToString(3)}", "muftyat.kz", check);
        var export = Theme.Button("⭳ " + L.T("Export"));
        var import = Theme.Button("⭱ " + L.T("Import"));
        export.Click += (_, _) =>
        {
            using var d = new SaveFileDialog { FileName = "NamazTimes-settings.json", Filter = "JSON|*.json" };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            Data.Export(s, d.FileName);
            MessageBox.Show(this, L.T("Exported"), Text);
        };
        import.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "JSON|*.json" };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            if (!Data.Import(d.FileName, s)) { MessageBox.Show(this, L.T("ImportFailed"), Text); return; }
            Changed(rebuild: true); // the page is rebuilt from the imported values
        };
        var backup = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        backup.Controls.AddRange([export, import]);
        Card('', L.T("Backup"), null, backup);

        // Credit line
        var credit = new Control { Size = new Size(Theme.Dp(360), Theme.Dp(28)), Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.Dp(16), 0, 0) };
        credit.Paint += (_, e) =>
        {
            const string text = "Made by Nurbol Khamzauly with ";
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
            using var font = Theme.UI(9f);
            using var heartFont = new Font("Segoe UI Symbol", 10f * Theme.UiScale); // monochrome glyph, so it takes the red colour
            int tw = TextRenderer.MeasureText(e.Graphics, text, font, Size.Empty, flags).Width;
            int hw = TextRenderer.MeasureText(e.Graphics, "❤", heartFont, Size.Empty, flags).Width;
            int x = (credit.Width - tw - hw) / 2;
            TextRenderer.DrawText(e.Graphics, text, font, new Rectangle(x, 0, tw, credit.Height), Theme.Muted, flags);
            TextRenderer.DrawText(e.Graphics, "❤", heartFont, new Rectangle(x + tw, 0, hw, credit.Height), Color.FromArgb(229, 57, 70), flags);
        };
        body.Controls.Add(credit);
    }

    /// Apply a change: update the setting, save, and let the widget pick it up.
    void Set(Action apply, bool rebuild = false)
    {
        apply();
        Changed(rebuild);
    }

    // Rebuilding disposes this page, so never do it inside the control's own event.
    void Changed(bool rebuild) => BeginInvoke(() => changed(rebuild));

    Toggle Switch(bool value, Action<bool> apply)
    {
        var t = new Toggle(value) { Anchor = AnchorStyles.Right };
        t.CheckedChanged += (_, _) => Set(() => apply(t.Checked));
        return t;
    }

    /// A small caption to the left of a control (used in the prayer rows).
    static FlowLayoutPanel Labelled(string text, Control c)
    {
        var l = Theme.Label(text, Theme.Muted, 9f);
        l.Margin = new Padding(Theme.Dp(14), Theme.Dp(8), Theme.Dp(6), 0);
        c.Margin = new Padding(0, Theme.Dp(3), 0, Theme.Dp(3));
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        p.Controls.AddRange([l, c]);
        return p;
    }

    void Header(string text)
    {
        var l = Theme.Label(text, Theme.Text, 10.5f, FontStyle.Bold);
        l.Margin = new Padding(Theme.Dp(2), Theme.Dp(body.Controls.Count == 0 ? 4 : 22), 0, Theme.Dp(8));
        body.Controls.Add(l);
    }

    /// One Windows-Settings card: glyph, title (and hint) on the left, the control on the right.
    void Card(char glyph, string title, string? hint, Control control)
    {
        var card = new TableLayoutPanel
        {
            ColumnCount = 3, AutoSize = true, Dock = DockStyle.Top, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, Theme.Dp(4)),
            Padding = new Padding(Theme.Dp(16), Theme.Dp(10), Theme.Dp(16), Theme.Dp(10)), MinimumSize = new Size(0, Theme.Dp(66)),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var icon = new Label
        {
            Text = glyph.ToString(), Font = new Font("Segoe MDL2 Assets", 13f * Theme.UiScale), ForeColor = Theme.Text, AutoSize = true,
            Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, Theme.Dp(16), 0),
        };
        var texts = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        var t = Theme.Label(title, Theme.Text, 10.5f);
        t.Margin = Padding.Empty;
        texts.Controls.Add(t);
        if (hint != null)
        {
            var h = Theme.Label(hint, Theme.Muted, 8.5f);
            h.Margin = new Padding(0, Theme.Dp(2), 0, 0);
            texts.Controls.Add(h);
        }
        control.Anchor = AnchorStyles.Right;
        card.Controls.Add(icon, 0, 0);
        card.Controls.Add(texts, 1, 0);
        card.Controls.Add(control, 2, 0);
        body.Controls.Add(Theme.Round(card));
    }
}

public class CityPicker : Form
{
    public City? Selected { get; private set; }

    public CityPicker()
    {
        Theme.Apply(this);
        Text = L.T("ChooseCity");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(Theme.Dp(520), Theme.Dp(520));
        Padding = new Padding(Theme.Dp(12));

        var queryBox = new SearchBox(L.T("SearchHint")) { Dock = DockStyle.Top };
        var query = queryBox.Box;
        var list = new ListBox
        {
            Dock = DockStyle.Fill, IntegralHeight = false, BackColor = Theme.Card, ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None, Font = Theme.UI(10f),
        };
        list.HandleCreated += (_, _) => Theme.DarkScrollbars(list);
        var choose = Theme.Button(L.T("Choose"), primary: true);
        var cancel = Theme.Button(L.T("Cancel"));
        cancel.DialogResult = DialogResult.Cancel; CancelButton = cancel; AcceptButton = choose;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, Theme.Dp(8), 0, 0) };
        buttons.Controls.AddRange([cancel, choose]);
        var gap = new Panel { Dock = DockStyle.Top, Height = Theme.Dp(8) };
        Controls.AddRange([list, gap, queryBox, buttons]);

        var all = Data.Cities.Value
            .Select(c => (Label: string.Join(" · ", new[] { c[0], c[2], c[1] }.Where(x => x != "")), Key: Data.Fold(c[0]), Row: c))
            .ToList();
        List<string[]> shown = [];
        void Filter()
        {
            var q = Data.Fold(query.Text.Trim());
            var hits = q == "" ? all : all.Where(c => c.Key.Contains(q)).OrderBy(c => !c.Key.StartsWith(q)).ToList();
            shown = hits.Select(h => h.Row).ToList();
            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(hits.Select(h => (object)h.Label).ToArray());
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            list.EndUpdate();
        }
        void Pick()
        {
            if (list.SelectedIndex < 0) return;
            var r = shown[list.SelectedIndex];
            Selected = new City(r[0], r[3], r[4]);
            DialogResult = DialogResult.OK;
        }
        query.TextChanged += (_, _) => Filter();
        query.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Down or Keys.Up && list.Items.Count > 0)
            {
                list.SelectedIndex = Math.Clamp(list.SelectedIndex + (e.KeyCode == Keys.Down ? 1 : -1), 0, list.Items.Count - 1);
                e.Handled = true;
            }
        };
        list.DoubleClick += (_, _) => Pick();
        choose.Click += (_, _) => Pick();
        Shown += (_, _) => query.Focus();
        Filter();
    }
}
