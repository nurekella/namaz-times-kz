namespace NamazTimes;

public class SettingsForm : Form
{
    public SettingsForm(Settings s, Action? testAlert = null, Func<IWin32Window, Task>? checkUpdates = null)
    {
        Theme.Apply(this);
        Text = "Namaz Times KZ — " + L.T("Settings");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        static void Row(TableLayoutPanel g, string label, Control c)
        {
            c.Anchor = AnchorStyles.Left;
            g.Controls.Add(Theme.Label(label));
            g.Controls.Add(c);
        }

        // General
        var general = Theme.Section(L.T("General"), out var g1);
        var lang = new Segmented(L.Languages.Select(l => l.Name), Array.FindIndex(L.Languages, l => l.Code == s.Lang));
        Row(g1, L.T("Language"), lang);
        var city = s.City;
        var cityBtn = Theme.Button(city.Title + "  ›");
        cityBtn.Click += (_, _) =>
        {
            using var f = new CityPicker();
            if (f.ShowDialog(this) == DialogResult.OK && f.Selected != null)
            {
                city = f.Selected;
                cityBtn.Text = city.Title + "  ›";
            }
        };
        Row(g1, L.T("City"), cityBtn);
        var madhab = new Segmented([L.T("Hanafi"), L.T("OtherMadhabs")], s.Hanafi ? 0 : 1);
        Row(g1, L.T("AsrMethod"), madhab);
        var timeFormat = new Segmented([L.T("H24"), L.T("H12")], s.Hour12 ? 1 : 0);
        Row(g1, L.T("TimeFormat"), timeFormat);
        var fasting = new Segmented([L.T("FastAuto"), L.T("FastAlways"), L.T("FastOff")], (int)s.Fasting);
        Row(g1, L.T("FastTimes"), fasting);
        var hijri = new Stepper(s.HijriAdjust, -2, 2, 1, v => v > 0 ? $"+{v}" : v.ToString());
        Row(g1, L.T("HijriAdjust"), hijri);
        var uiScale = new Stepper(s.UiScale, 80, 160, 10, v => v + "%");
        Row(g1, L.T("WindowScale"), uiScale);

        // Notifications
        var notif = Theme.Section(L.T("Notifications"), out var g2);
        var notifyOn = new Toggle(!s.Muted);
        Row(g2, L.T("NotifyOn"), notifyOn);
        string MinOrOff(int v) => v == 0 ? L.T("Off") : string.Format(L.T("Min"), v);
        var remind = new Stepper(s.RemindBefore, 0, 60, 5, MinOrOff);
        Row(g2, L.T("RemindBefore"), remind);
        var jumuah = new Toggle(s.Jumuah);
        var jumuahMin = new Stepper(s.JumuahBefore, 15, 180, 15, MinOrOff);
        var jumuahRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        jumuahRow.Controls.AddRange([jumuah, jumuahMin]);
        Row(g2, L.T("JumuahRemind"), jumuahRow);
        var test = Theme.Button(L.T("TestAlert"));
        test.Click += (_, _) => testAlert?.Invoke();
        g2.Controls.Add(test); g2.SetColumnSpan(test, 2);

        // Widget
        var widget = Theme.Section(L.T("Widget"), out var g3);
        var zoom = new Stepper(s.Zoom, 70, 250, 10, v => v + "%");
        Row(g3, L.T("Size"), zoom);
        var opacity = new Stepper(s.Opacity, 30, 100, 10, v => v + "%");
        Row(g3, L.T("Opacity"), opacity);
        var nameOfDay = new Toggle(s.ShowNameOfDay);
        Row(g3, L.T("ShowNameOfDay"), nameOfDay);
        var topMost = new Toggle(s.TopMost);
        Row(g3, L.T("TopMost"), topMost);
        var autoStart = new Toggle(Widget.AutoStart);
        Row(g3, L.T("AutoStart"), autoStart);
        var hint = Theme.Label(L.T("ResizeHint"), Theme.Muted, 8.5f);
        g3.Controls.Add(hint); g3.SetColumnSpan(hint, 2);

        // Prayers: name | show | notify | offset
        var prayers = Theme.Section(L.T("Prayers"), out var g4, 4);
        foreach (var h in new[] { "", L.T("Show"), L.T("Notify"), L.T("Offset") })
        {
            var l = Theme.Label(h, Theme.Muted, 8.5f);
            l.Anchor = h == "" ? AnchorStyles.Left : AnchorStyles.None;
            g4.Controls.Add(l);
        }
        var rows = Enum.GetValues<P>().Select(p =>
        {
            var show = new Toggle(!s.Hidden.Contains(p));
            var notify = new Toggle(s.Alerts.Contains(p));
            var off = new Stepper(s.Offsets.GetValueOrDefault(p), -30, 30, 1, v => v > 0 ? $"+{v}" : v.ToString());
            g4.Controls.Add(Theme.Label(L.Name(p)));
            g4.Controls.AddRange([show, notify, off]);
            return (p, show, notify, off);
        }).ToList();

        // Layout: two columns of cards + footer
        var left = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = new Padding(0, 0, Theme.Dp(12), 0) };
        left.Controls.AddRange([general, notif, widget]);
        // Reminders besides prayer times
        var reminders = Theme.Section(L.T("Reminders"), out var g5);
        var remFasts = new Toggle(s.RemindSunnahFasts);
        Row(g5, L.T("RemFasts"), remFasts);
        var remHolidays = new Toggle(s.RemindHolidays);
        Row(g5, L.T("RemHolidays"), remHolidays);
        var remKahf = new Toggle(s.RemindKahf);
        Row(g5, L.T("RemKahf"), remKahf);
        var remAdhkar = new Toggle(s.RemindAdhkar);
        Row(g5, L.T("RemAdhkar"), remAdhkar);
        var fullscreen = new Toggle(s.FullscreenAlert);
        Row(g5, L.T("RemFullscreen"), fullscreen);

        var right = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
        right.Controls.AddRange([prayers, reminders]);

        var save = Theme.Button(L.T("Save"), primary: true);
        var cancel = Theme.Button(L.T("Cancel"));
        save.DialogResult = DialogResult.OK; cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = save; CancelButton = cancel;
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right, Margin = Padding.Empty };
        buttons.Controls.AddRange([cancel, save]);
        var version = Theme.Label($"v{Updates.Current.ToString(3)} · muftyat.kz", Theme.Muted, 8.5f);
        var check = Theme.Button("↻ " + L.T("CheckUpdates"));
        check.Click += async (_, _) => { check.Enabled = false; try { await (checkUpdates?.Invoke(this) ?? Task.CompletedTask); } finally { check.Enabled = true; } };
        var about = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        // Backup: export the settings file / import it back (e.g. on a new computer)
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
            DialogResult = DialogResult.OK; // closing as "saved" makes the widget apply the imported settings
            Close();
        };
        about.Controls.AddRange([version, check, export, import]);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(Theme.Dp(16)) };
        root.Controls.Add(left); root.Controls.Add(right);
        root.Controls.Add(about); root.Controls.Add(buttons);

        // Credit line, centred under everything.
        var credit = new Control { Size = new Size(Theme.Dp(360), Theme.Dp(24)), Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.Dp(10), 0, 0) };
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
        root.Controls.Add(credit); root.SetColumnSpan(credit, 2);
        Controls.Add(root);

        // Shown modeless, so DialogResult alone doesn't close the window.
        cancel.Click += (_, _) => Close();

        save.Click += (_, _) =>
        {
            s.Lang = L.Languages[lang.Selected].Code;
            s.City = city;
            s.Hanafi = madhab.Selected == 0;
            s.Hour12 = timeFormat.Selected == 1;
            s.Fasting = (FastingMode)fasting.Selected;
            s.HijriAdjust = hijri.Value;
            s.UiScale = uiScale.Value;
            s.Muted = !notifyOn.Checked;
            s.RemindBefore = remind.Value;
            s.Jumuah = jumuah.Checked;
            s.JumuahBefore = jumuahMin.Value;
            s.Zoom = zoom.Value;
            s.Opacity = opacity.Value;
            s.TopMost = topMost.Checked;
            s.ShowNameOfDay = nameOfDay.Checked;
            s.RemindSunnahFasts = remFasts.Checked;
            s.RemindHolidays = remHolidays.Checked;
            s.RemindKahf = remKahf.Checked;
            s.RemindAdhkar = remAdhkar.Checked;
            s.FullscreenAlert = fullscreen.Checked;
            s.Hidden = rows.Where(r => !r.show.Checked).Select(r => r.p).ToHashSet();
            s.Alerts = rows.Where(r => r.notify.Checked).Select(r => r.p).ToHashSet();
            s.Offsets = rows.Where(r => r.off.Value != 0).ToDictionary(r => r.p, r => r.off.Value);
            if (autoStart.Checked != Widget.AutoStart) Widget.AutoStart = autoStart.Checked;
            Close();
        };
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

        var query = new TextBox
        {
            Dock = DockStyle.Top, PlaceholderText = L.T("SearchHint"), BackColor = Theme.Card, ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle, Font = Theme.UI(11f),
        };
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
        Controls.AddRange([list, gap, query, buttons]);

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
