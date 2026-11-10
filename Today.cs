using System.Drawing.Drawing2D;

namespace NamazTimes;

/// Home page, in Wintor's layout: today's times as a calm table (a progress bar for each period, coloured by state:
/// done green, running blue, still to come empty), and a details card under it.
public class TodayForm : Form
{
    readonly Settings s;
    readonly TimesTable table;
    readonly DetailsCard details;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };

    public TodayForm(Settings settings)
    {
        s = settings;
        Theme.Apply(this, '');
        Text = L.T("TodayCap");
        ClientSize = new Size(Theme.Dp(1000), Theme.Dp(700));
        table = new TimesTable(s) { Dock = DockStyle.Top };
        details = new DetailsCard(s) { Dock = DockStyle.Fill };
        var gap = new Panel { Dock = DockStyle.Top, Height = Theme.Dp(12) };
        Controls.AddRange([details, gap, table]);
        timer.Tick += (_, _) => { table.Refresh(); details.Invalidate(); };
        timer.Start();
        Disposed += (_, _) => timer.Dispose();
        table.Refresh();
    }

    internal static Day? Get(Settings s, DateOnly d) => Data.GetDay(s.City, d);

    /// Time span in words: "2 сағ 05 мин", "12 мин", "<1 мин".
    internal static string Span(TimeSpan t)
    {
        if (t < TimeSpan.FromMinutes(1)) return "<" + string.Format(L.T("Min"), 1);
        var h = (int)t.TotalHours;
        var m = t.Minutes + (t.Seconds > 0 ? 1 : 0);
        if (m == 60) { h++; m = 0; }
        return h > 0 ? string.Format(L.T("HM"), h, m.ToString("00")) : string.Format(L.T("Min"), m);
    }

    /// The periods of today: each shown time and the moment the next one starts.
    internal static List<(P P, DateTime Start, DateTime End)> Periods(Settings s, DateTime now)
    {
        var date = DateOnly.FromDateTime(now);
        var list = (Data.Times(date, d => Get(s, d), s) ?? []).Where(x => !s.Hidden.Contains(x.P)).ToList();
        var tomorrow = (Data.Times(date.AddDays(1), d => Get(s, d), s) ?? []).Where(x => !s.Hidden.Contains(x.P)).ToList();
        var result = new List<(P, DateTime, DateTime)>();
        for (int i = 0; i < list.Count; i++)
        {
            var end = i + 1 < list.Count ? list[i + 1].At : tomorrow.Count > 0 ? tomorrow[0].At : list[i].At.AddHours(6);
            result.Add((list[i].P, list[i].At, end));
        }
        return result;
    }

    /// The table card: №, prayer, time, progress, %, status, time left.
    class TimesTable : Control
    {
        readonly Settings s;
        List<(P P, DateTime Start, DateTime End)> rows = [];
        int hot = -1;

        public TimesTable(Settings s)
        {
            this.s = s;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.UI(10.5f);
        }

        int RowH => Theme.Dp(40);
        int HeadH => Theme.Dp(40);

        public new void Refresh()
        {
            rows = Periods(s, DateTime.Now);
            var h = HeadH + Math.Max(1, rows.Count) * RowH + Theme.Dp(8);
            if (Height != h) Height = h;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var h = e.Y < HeadH ? -1 : (e.Y - HeadH) / RowH;
            if (h >= rows.Count) h = -1;
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundRect(card, Theme.Dp(Theme.CardRadius)))
            {
                using var fill = new SolidBrush(Theme.Card);
                g.FillPath(fill, path);
                using var pen = new Pen(Theme.CardStroke);
                g.DrawPath(pen, path);
            }

            // columns: fixed ones around a flexible progress bar
            int pad = Theme.Dp(16), x0 = Theme.Dp(4);
            int wNo = Theme.Dp(40), wTime = Theme.Dp(80), wPct = Theme.Dp(60);
            int rest = Math.Max(Theme.Dp(300), Width - x0 * 2 - pad * 2 - wNo - wTime - wPct);
            int wName = rest * 22 / 100, wStatus = rest * 16 / 100, wLeft = rest * 18 / 100, wBar = rest - wName - wStatus - wLeft;
            int cNo = x0 + pad, cName = cNo + wNo, cTime = cName + wName, cBar = cTime + wTime, cPct = cBar + wBar, cStatus = cPct + wPct, cLeft = cStatus + wStatus;

            using var head = Theme.UI(9.5f);
            const TextFormatFlags L_ = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            const TextFormatFlags R_ = L_ | TextFormatFlags.Right;
            void H(string t, int x, int w, bool right = false) =>
                TextRenderer.DrawText(g, t, head, new Rectangle(x, 0, w - Theme.Dp(8), HeadH), Theme.Muted, right ? R_ : L_);
            H("#", cNo, wNo - Theme.Dp(4), true);
            H(L.T("PrayerCol"), cName + Theme.Dp(8), wName);
            H(L.T("TimeCol"), cTime, wTime, true);
            H(L.T("ProgressCol"), cBar + Theme.Dp(8), wBar);
            H(L.T("StatusCol"), cStatus + Theme.Dp(8), wStatus);
            H(L.T("Left"), cLeft, wLeft, true);
            using (var line = new Pen(Theme.CardStroke)) g.DrawLine(line, x0, HeadH - 1, Width - x0, HeadH - 1);

            var now = DateTime.Now;
            if (rows.Count == 0)
            {
                TextRenderer.DrawText(g, L.T("NoData"), Font, new Rectangle(cName, HeadH, Width - cName, RowH), Theme.Muted, L_);
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var (p, start, end) = rows[i];
                var y = HeadH + i * RowH + Theme.Dp(4);
                var running = start <= now && now < end;
                var done = now >= end;
                var cell = new RectangleF(x0, y + Theme.Dp(2), Width - x0 * 2, RowH - Theme.Dp(4));
                if (running || i == hot)
                {
                    using var rp = Theme.RoundRect(cell, Theme.Dp(Theme.ControlRadius));
                    using var rb = new SolidBrush(Theme.Line);
                    g.FillPath(rb, rp);
                }
                if (running)
                {
                    using var pill = Theme.RoundRect(new RectangleF(cell.X, cell.Y + (cell.Height - Theme.Dp(16)) / 2f, Theme.Dp(3), Theme.Dp(16)), Theme.Dp(1));
                    using var ab = new SolidBrush(Theme.Accent);
                    g.FillPath(ab, pill);
                }
                var row = new Rectangle(0, y, Width, RowH);
                var fg = Theme.Text;
                TextRenderer.DrawText(g, (i + 1).ToString(), Font, row with { X = cNo, Width = wNo - Theme.Dp(12) }, fg, R_);
                TextRenderer.DrawText(g, L.Name(p), Font, row with { X = cName + Theme.Dp(8), Width = wName - Theme.Dp(8) }, fg, L_);
                TextRenderer.DrawText(g, L.Time(start), Font, row with { X = cTime, Width = wTime - Theme.Dp(8) }, fg, R_);

                // progress bar of the period
                var k = done ? 1.0 : running ? (now - start) / (end - start) : 0.0;
                var bar = new RectangleF(cBar + Theme.Dp(8), y + (RowH - Theme.Dp(16)) / 2f, wBar - Theme.Dp(16), Theme.Dp(16));
                using (var tp = Theme.RoundRect(bar, Theme.Dp(4)))
                using (var tb = new SolidBrush(Theme.Track)) g.FillPath(tb, tp);
                if (k > 0)
                    using (var fp = Theme.RoundRect(bar with { Width = Math.Max(Theme.Dp(8), (float)(bar.Width * k)) }, Theme.Dp(4)))
                    using (var fb = new SolidBrush(done ? Theme.Green : Theme.Blue)) g.FillPath(fb, fp);
                using var small = Theme.UI(9f);
                TextRenderer.DrawText(g, done ? "100%" : running ? (k * 100).ToString(k < 0.1 ? "0.0" : "0", L.Culture) + "%" : "0%",
                    small, row with { X = cPct, Width = wPct - Theme.Dp(4) }, Theme.Muted, R_);
                TextRenderer.DrawText(g, done ? L.T("Passed") : running ? L.T("NowCap") : L.T("Upcoming"), Font,
                    row with { X = cStatus + Theme.Dp(8), Width = wStatus - Theme.Dp(8) }, fg, L_);
                var left = running ? Span(end - now) : !done ? Span(start - now) : "";
                TextRenderer.DrawText(g, left, Font, row with { X = cLeft, Width = wLeft - Theme.Dp(8) }, fg, R_);
            }
        }
    }

    /// Details under the table, like Wintor's details pane: a title row, then label/value pairs in four columns.
    class DetailsCard : Control
    {
        readonly Settings s;

        public DetailsCard(Settings s)
        {
            this.s = s;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.UI(9.5f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundRect(card, Theme.Dp(Theme.CardRadius)))
            {
                using var fill = new SolidBrush(Theme.Card);
                g.FillPath(fill, path);
                using var pen = new Pen(Theme.CardStroke);
                g.DrawPath(pen, path);
            }
            var now = DateTime.Now;
            var date = DateOnly.FromDateTime(now);
            const TextFormatFlags F = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            int pad = Theme.Dp(20);

            // title row: what this is on the left, the city on the right (Wintor shows the torrent's name there)
            using var title = Theme.UI(10.5f, FontStyle.Bold);
            Icons.Draw(g, '', new RectangleF(pad, Theme.Dp(14), Theme.Dp(16), Theme.Dp(16)), Theme.Text);
            using var titleFont = Theme.UI(10.5f);
            TextRenderer.DrawText(g, L.T("TodayCap"), titleFont, new Rectangle(pad + Theme.Dp(24), Theme.Dp(4), Theme.Dp(300), Theme.Dp(36)), Theme.Text, F);
            TextRenderer.DrawText(g, s.City.Title, title, new Rectangle(Width / 2, Theme.Dp(4), Width / 2 - pad, Theme.Dp(36)), Theme.Text, F | TextFormatFlags.Right);
            using (var accent = new SolidBrush(Theme.Accent))
            using (var u = Theme.RoundRect(new RectangleF(pad + Theme.Dp(24), Theme.Dp(36), Theme.Dp(16), Theme.Dp(3)), Theme.Dp(1))) g.FillPath(accent, u);
            using (var line = new Pen(Theme.CardStroke)) g.DrawLine(line, 1, Theme.Dp(44), Width - 2, Theme.Dp(44));

            var times = Data.Times(date, d => Get(s, d), s);
            DateTime? T(P p) => times?.Where(x => x.P == p).Select(x => (DateTime?)x.At).FirstOrDefault();
            var next = Data.Next(now, d => Get(s, d), p => !s.Hidden.Contains(p) && p is not (P.Tahajjud or P.Duha), s);
            var (hy, hm, hd) = Hijri.Of(date, s.HijriAdjust);
            var nm = Name99.OfDay(date);

            var sections = new List<(string Title, (string K, string V)[] Pairs)>
            {
                (L.T("TimeSection"), [
                    (L.T("NextPrayer"), next is { } n ? $"{L.Name(n.P)} · {L.Time(n.At)}" : "—"),
                    (L.T("TimeLeft"), next is { } n2 ? Span(n2.At - now) : "—"),
                    (L.T("Suhoor"), T(P.Fajr) is { } f ? L.Time(f) : "—"),
                    (L.T("Iftar"), T(P.Maghrib) is { } m ? L.Time(m) : "—"),
                    (L.Name(P.Duha), T(P.Duha) is { } d ? L.Time(d) : "—"),
                    (L.Name(P.Tahajjud), T(P.Tahajjud) is { } t ? L.Time(t) : "—"),
                    (L.T("AsrMethod"), L.T(s.Hanafi ? "Hanafi" : "OtherMadhabs")),
                    (L.T("Jumuah"), NextFriday(date)),
                ]),
                (L.T("General"), [
                    (L.T("Date"), date.ToString("d MMMM yyyy, dddd", L.Culture)),
                    (L.T("HijriCol"), Hijri.Format(date, s.HijriAdjust)),
                    (L.T("City"), s.City.Title),
                    (L.T("NameOfDay"), $"{nm.N}. {nm.Translit} — {nm.Meaning}"),
                ]),
            };

            int y = Theme.Dp(56);
            int cols = Width >= Theme.Dp(1000) ? 4 : Width >= Theme.Dp(700) ? 3 : 2;
            int colW = (Width - pad * 2) / cols, lineH = Theme.Dp(24);
            var keyW = Math.Min(colW * 55 / 100, sections.SelectMany(x => x.Pairs).Max(x => TextRenderer.MeasureText(g, x.K, Font).Width) + Theme.Dp(12));
            using var bold = Theme.UI(10.5f, FontStyle.Bold);
            foreach (var (head, pairs) in sections)
            {
                if (y + lineH > Height - Theme.Dp(8)) break;
                TextRenderer.DrawText(g, head, bold, new Rectangle(pad, y, Width - pad * 2, Theme.Dp(28)), Theme.Text, F);
                y += Theme.Dp(32);
                // the name of the day is long: it gets the rest of its row
                for (int i = 0; i < pairs.Length; i++)
                {
                    var col = i % cols;
                    var rowY = y + i / cols * lineH;
                    if (rowY + lineH > Height - Theme.Dp(8)) break;
                    var x = pad + col * colW;
                    var valueW = i == pairs.Length - 1 && col < cols - 1 ? Width - pad - x - keyW : colW - keyW - Theme.Dp(8);
                    TextRenderer.DrawText(g, pairs[i].K, Font, new Rectangle(x, rowY, keyW - Theme.Dp(8), lineH), Theme.Muted, F);
                    TextRenderer.DrawText(g, pairs[i].V, Font, new Rectangle(x + keyW, rowY, valueW, lineH), Theme.Text, F);
                }
                y += (pairs.Length + cols - 1) / cols * lineH + Theme.Dp(12);
            }
        }

        string NextFriday(DateOnly date)
        {
            var friday = date.AddDays(((int)DayOfWeek.Friday - (int)date.DayOfWeek + 7) % 7);
            var dhuhr = Data.Times(friday, d => Get(s, d), s)?.Where(x => x.P == P.Dhuhr).Select(x => (DateTime?)x.At).FirstOrDefault();
            return (friday == date ? L.T("Today") : friday.ToString("d MMMM", L.Culture)) + (dhuhr is { } t ? " · " + L.Time(t) : "");
        }
    }
}
