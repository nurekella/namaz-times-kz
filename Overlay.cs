namespace NamazTimes;

/// Optional gentle full-screen reminder at prayer time, for people who miss the corner notification.
/// Dims the screen with the prayer name; closes on click, Esc or after 15 seconds.
public class PrayerOverlay : Form
{
    public PrayerOverlay(string title, string subtitle)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen!.Bounds;
        BackColor = Color.FromArgb(10, 16, 38);
        Opacity = 0.93;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        Cursor = Cursors.Hand;

        var icon = new Label { Text = "🕌", Font = new Font("Segoe UI Emoji", 54f * Theme.UiScale), ForeColor = Color.White, AutoSize = true, Anchor = AnchorStyles.None };
        var head = new Label { Text = title, Font = Theme.UI(40f, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Anchor = AnchorStyles.None };
        var sub = new Label { Text = subtitle, Font = Theme.UI(16f), ForeColor = Theme.Muted, AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.Dp(8), 0, Theme.Dp(28)) };
        var close = Theme.Button(L.T("OverlayClose"), primary: true);
        close.Anchor = AnchorStyles.None;
        var column = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Anchor = AnchorStyles.None, BackColor = Color.Transparent };
        column.Controls.AddRange([icon, head, sub, close]);
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        host.Controls.Add(column);
        Controls.Add(host);

        close.Click += (_, _) => Close();
        foreach (Control c in new Control[] { host, icon, head, sub }) c.Click += (_, _) => Close();
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        var timer = new System.Windows.Forms.Timer { Interval = 15_000 };
        timer.Tick += (_, _) => { timer.Dispose(); Close(); };
        timer.Start();
    }
}
