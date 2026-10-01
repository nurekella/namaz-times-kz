namespace NamazTimes;

static class Grid
{
    public static DataGridView Create()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, BorderStyle = BorderStyle.None,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ScrollBars = ScrollBars.Vertical,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, // row height follows the font, so descenders aren't clipped
            BackgroundColor = Theme.Bg, GridColor = Theme.Line, EnableHeadersVisualStyles = false,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        };
        g.DefaultCellStyle.BackColor = Theme.Bg;
        g.DefaultCellStyle.ForeColor = Theme.Text;
        g.DefaultCellStyle.SelectionBackColor = Theme.Line;
        g.DefaultCellStyle.SelectionForeColor = Theme.Text;
        g.DefaultCellStyle.Padding = new Padding(Theme.Dp(10), Theme.Dp(7), Theme.Dp(10), Theme.Dp(7));
        g.ColumnHeadersDefaultCellStyle.BackColor = Theme.Card;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Card;
        g.ColumnHeadersDefaultCellStyle.Padding = g.DefaultCellStyle.Padding;
        g.HandleCreated += (_, _) => Theme.DarkScrollbars(g);
        return g;
    }

    public static void Highlight(DataGridViewRow r, Color c)
    {
        r.DefaultCellStyle.ForeColor = c;
        r.DefaultCellStyle.Font = Theme.UI(10f, FontStyle.Bold);
    }
}

public class MonthForm : Form
{
    public MonthForm(Settings s)
    {
        Theme.Apply(this, '\uE787');
        Text = L.T("Month") + " — " + s.City.Title;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(1000), Theme.Dp(680));
        Padding = new Padding(Theme.Dp(12));

        var month = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var title = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.UI(13f, FontStyle.Bold), ForeColor = Theme.Text };
        var prev = Theme.Button("‹"); var next = Theme.Button("›"); var today = Theme.Button(char.ToUpper(L.T("Today")[0]) + L.T("Today")[1..]);
        prev.Dock = next.Dock = DockStyle.Left; today.Dock = DockStyle.Right;
        var bar = new Panel { Dock = DockStyle.Top, Height = Theme.Dp(40) };
        bar.Controls.AddRange([title, today, next, prev]);
        var grid = Grid.Create();
        var gap = new Panel { Dock = DockStyle.Top, Height = Theme.Dp(8) };
        Controls.AddRange([grid, gap, bar]);

        var cols = Enum.GetValues<P>().Where(p => !s.Hidden.Contains(p)).ToList();
        grid.Columns.Add("d", L.T("Date"));
        grid.Columns.Add("h", L.T("HijriCol"));
        grid.Columns[0].FillWeight = 130; grid.Columns[1].FillWeight = 220;
        foreach (var p in cols) grid.Columns.Add(p.ToString(), L.Name(p));

        void Fill()
        {
            title.Text = month.ToDateTime(default).ToString("MMMM yyyy", L.Culture);
            grid.Rows.Clear();
            var now = DateOnly.FromDateTime(DateTime.Today);
            for (var d = month; d.Month == month.Month; d = d.AddDays(1))
            {
                var times = Data.Times(d, x => Data.GetDay(s.City, x), s);
                var cells = new List<object> { d.ToString("d MMM, ddd", L.Culture), Hijri.Format(d, s.HijriAdjust) };
                cells.AddRange(cols.Select(p => (object)(times?.Where(t => t.P == p).Select(t => L.Time(t.At)).FirstOrDefault() ?? "—")));
                var r = grid.Rows[grid.Rows.Add(cells.ToArray())];
                if (d == now) Grid.Highlight(r, Theme.Accent);
                else if (d.DayOfWeek == DayOfWeek.Friday) r.DefaultCellStyle.BackColor = Theme.Card;
            }
            grid.ClearSelection();
        }
        prev.Click += (_, _) => { month = month.AddMonths(-1); Fill(); };
        next.Click += (_, _) => { month = month.AddMonths(1); Fill(); };
        today.Click += (_, _) => { month = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1); Fill(); };
        Fill();
    }
}

public class HolidaysForm : Form
{
    public HolidaysForm(Settings s)
    {
        Theme.Apply(this, '\uE734');
        Text = L.T("Holidays");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(900), Theme.Dp(640));
        Padding = new Padding(Theme.Dp(12));

        var grid = Grid.Create();
        grid.Columns.Add("n", L.T("Holiday"));
        grid.Columns.Add("d", L.T("Date"));
        grid.Columns.Add("h", L.T("HijriCol"));
        grid.Columns.Add("l", L.T("Left"));
        grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells; // full holiday name, never "…"
        grid.Columns[1].FillWeight = 300; grid.Columns[2].FillWeight = 170; grid.Columns[3].FillWeight = 80;
        var note = Theme.Label(L.T("HolidayNote"), Theme.Muted, 8.5f);
        note.Dock = DockStyle.Bottom; note.MaximumSize = new Size(Theme.Dp(876), 0); note.Margin = Padding.Empty;
        Controls.AddRange([grid, note]);

        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var h in Hijri.Upcoming(today, s.HijriAdjust))
        {
            var days = h.Date.DayNumber - today.DayNumber;
            var date = h.Date.ToString("d MMMM yyyy, dddd", L.Culture) + (h.Night ? $" ({L.T("Evening")})" : "");
            var hijriDay = h.Night ? h.Date.AddDays(1) : h.Date;
            var r = grid.Rows[grid.Rows.Add(L.T(h.Key), date, Hijri.Format(hijriDay, s.HijriAdjust),
                days == 0 ? L.T("Today") : string.Format(L.T("Days"), days))];
            if (h.Key is "FitrEid" or "AdhaEid") Grid.Highlight(r, Theme.Accent);
        }
        grid.ClearSelection();
    }
}
