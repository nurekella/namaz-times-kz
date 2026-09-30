namespace NamazTimes;

public class SettingsForm : Form
{
    public SettingsForm(Settings s)
    {
        Text = "Namaz Times KZ — " + L.T("Settings").TrimEnd('…');
        Icon = new(typeof(Widget).Assembly.GetManifestResourceStream("app.ico")!);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 10f);

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Padding = new Padding(12), Dock = DockStyle.Fill };
        Label Lbl(string text, bool bold = false) => new()
        {
            Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6),
            Font = bold ? new Font(Font, FontStyle.Bold) : Font,
        };
        void Row(Control a, Control? b = null, int span = 1)
        {
            grid.Controls.Add(a);
            if (b != null) { grid.Controls.Add(b); grid.SetColumnSpan(b, span); }
        }

        var lang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        lang.Items.AddRange(L.Languages.Select(l => l.Name).ToArray<object>());
        lang.SelectedIndex = Math.Max(0, Array.FindIndex(L.Languages, l => l.Code == s.Lang));
        Row(Lbl(L.T("Language")), lang, 2);

        var city = s.City;
        var cityBtn = new Button { Text = $"{city.Title} — {L.T("Change")}", AutoSize = true };
        cityBtn.Click += (_, _) =>
        {
            using var f = new CityPicker();
            if (f.ShowDialog(this) == DialogResult.OK && f.Selected != null)
            {
                city = f.Selected;
                cityBtn.Text = $"{city.Title} — {L.T("Change")}";
            }
        };
        Row(Lbl(L.T("City")), cityBtn, 2);

        Row(Lbl(L.T("Prayer"), true));
        Row(Lbl(L.T("Show"), true));
        Row(Lbl(L.T("Notify"), true));
        var checks = Enum.GetValues<P>().Select(p =>
        {
            var show = new CheckBox { Checked = !s.Hidden.Contains(p), AutoSize = true, Anchor = AnchorStyles.None };
            var notify = new CheckBox { Checked = s.Alerts.Contains(p), AutoSize = true, Anchor = AnchorStyles.None };
            Row(Lbl(L.Name(p)), show);
            grid.Controls.Add(notify);
            return (p, show, notify);
        }).ToList();

        var remind = new NumericUpDown { Minimum = 0, Maximum = 120, Value = Math.Clamp(s.RemindBefore, 0, 120), Width = 70 };
        Row(Lbl(L.T("RemindBefore")), remind, 2);

        var opacity = new TrackBar { Minimum = 30, Maximum = 100, TickFrequency = 10, Value = Math.Clamp(s.Opacity, 30, 100), Width = 160, AutoSize = false, Height = 32 };
        Row(Lbl(L.T("Opacity")), opacity, 2);

        var topMost = new CheckBox { Text = L.T("TopMost"), Checked = s.TopMost, AutoSize = true };
        Row(topMost); grid.SetColumnSpan(topMost, 3);
        var autoStart = new CheckBox { Text = L.T("AutoStart"), Checked = Widget.AutoStart, AutoSize = true };
        Row(autoStart); grid.SetColumnSpan(autoStart, 3);

        var save = new Button { Text = L.T("Save"), AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = L.T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.AddRange([cancel, save]);
        Row(buttons); grid.SetColumnSpan(buttons, 3);
        AcceptButton = save; CancelButton = cancel;

        save.Click += (_, _) =>
        {
            s.Lang = L.Languages[lang.SelectedIndex].Code;
            s.City = city;
            s.Hidden = checks.Where(c => !c.show.Checked).Select(c => c.p).ToHashSet();
            s.Alerts = checks.Where(c => c.notify.Checked).Select(c => c.p).ToHashSet();
            s.RemindBefore = (int)remind.Value;
            s.Opacity = opacity.Value;
            s.TopMost = topMost.Checked;
            if (autoStart.Checked != Widget.AutoStart) Widget.AutoStart = autoStart.Checked;
        };

        Controls.Add(grid);
    }
}

public class CityPicker : Form
{
    public City? Selected { get; private set; }
    readonly TextBox query = new() { Dock = DockStyle.Fill, PlaceholderText = L.T("SearchHint") };
    readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    List<(string Title, City City)> found = [];

    public CityPicker()
    {
        Text = L.T("ChooseCity");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(440, 360);
        Font = new Font("Segoe UI", 10f);
        var search = new Button { Text = L.T("Search"), Dock = DockStyle.Right, Width = 90 };
        var top = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(8, 6, 8, 0) };
        top.Controls.Add(query); top.Controls.Add(search);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        body.Controls.Add(list);
        Controls.Add(body); Controls.Add(top);
        AcceptButton = search;

        search.Click += async (_, _) =>
        {
            if (query.Text.Trim().Length < 2) return;
            search.Enabled = false;
            try
            {
                found = await Data.SearchCities(query.Text.Trim());
                list.DataSource = found.Count > 0 ? found.Select(f => f.Title).ToList() : [L.T("NotFound")];
            }
            catch { MessageBox.Show(this, L.T("NetError"), Text); }
            finally { search.Enabled = true; }
        };
        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= found.Count) return;
            Selected = found[list.SelectedIndex].City;
            DialogResult = DialogResult.OK;
        };
    }
}
