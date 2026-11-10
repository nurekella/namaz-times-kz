using System.Drawing.Drawing2D;

namespace NamazTimes;

/// Home page, in Wintor's layout: today's times as a calm table (a progress bar for each period, coloured by state:
/// done green, running blue, still to come empty), and under it a details pane with tabs — today, this week, Ramadan.
public class TodayForm : Form
{
    readonly Settings s;
    readonly TimesTable table;
    readonly DetailsCard details;
    readonly WeekCard week;
    readonly RamadanCard ramadan;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };

    public TodayForm(Settings settings)
    {
        s = settings;
        Theme.Apply(this, '');
        Text = L.T("TodayCap");
        ClientSize = new Size(Theme.Dp(1000), Theme.Dp(700));
        table = new TimesTable(s) { Dock = DockStyle.Top };
        var gap = new Panel { Dock = DockStyle.Top, Height = Theme.Dp(8), BackColor = Theme.Bg };
        var tabs = new TabBar { Dock = DockStyle.Top };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        details = new DetailsCard(s) { Dock = DockStyle.Fill };
        week = new WeekCard(s) { Dock = DockStyle.Fill, Visible = false };
        ramadan = new RamadanCard(s) { Dock = DockStyle.Fill, Visible = false };
        host.Controls.AddRange([details, week, ramadan]);
        tabs.Changed += i =>
        {
            details.Visible = i == 0;
            week.Visible = i == 1;
            ramadan.Visible = i == 2;
        };
        Controls.AddRange([host, tabs, gap, table]);
        timer.Tick += (_, _) => { table.Refresh(); details.Invalidate(); week.Invalidate(); ramadan.Invalidate(); };
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

    /// One line under the Ramadan title: days until it starts, or the day number, days left, and the suhoor/iftar countdown.
    internal static string RamadanStatus(Settings s, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var (_, start, end) = Hijri.RamadanAround(today, s.HijriAdjust);
        DateTime? At(DateOnly d, P p) => Data.Times(d, day => Data.GetDay(s.City, day), s)?.Where(t => t.P == p).Select(t => (DateTime?)t.At).FirstOrDefault();
        if (today < start)
        {
            var line = string.Format(L.T("StartsIn"), start.DayNumber - today.DayNumber);
            if (today.AddDays(1) == start && At(start, P.Fajr) is { } f && now < f)
                line += " · " + L.T("UntilSuhoor") + " " + Span(f - now);
            return line;
        }
        var line2 = string.Format(L.T("DayN"), Hijri.Of(today, s.HijriAdjust).D)
            + " · " + string.Format(L.T("EndsIn"), end.DayNumber - today.DayNumber);
        var fajr = At(today, P.Fajr);
        var maghrib = At(today, P.Maghrib);
        var count = fajr is { } f2 && now < f2 ? L.T("UntilSuhoor") + " " + Span(f2 - now)
            : maghrib is { } m && now < m ? L.T("UntilIftar") + " " + Span(m - now)
            : today.AddDays(1) < end && At(today.AddDays(1), P.Fajr) is { } f3 ? L.T("UntilSuhoor") + " " + Span(f3 - now)
            : null;
        return count == null ? line2 : line2 + " · " + count;
    }

    static void PaintCard(Graphics g, Control c)
    {
        g.Clear(c.Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var card = new RectangleF(0.5f, 0.5f, c.Width - 1.5f, c.Height - 1.5f);
        using var path = Theme.RoundRect(card, Theme.Dp(Theme.CardRadius));
        using var fill = new SolidBrush(Theme.Card);
        g.FillPath(fill, path);
        using var pen = new Pen(Theme.CardStroke);
        g.DrawPath(pen, path);
    }

    /// Text tabs above the details pane. The chosen one has a short accent underline, as in Wintor.
    class TabBar : Control
    {
        static readonly string[] Keys = ["TodayCap", "Week", "HM9"];
        public int Selected { get; private set; }
        public event Action<int>? Changed;
        int hot = -1;
        Rectangle[] hits = [];

        public TabBar()
        {
            Height = Theme.Dp(36);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
            Font = Theme.UI(10.5f);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            hits = new Rectangle[Keys.Length];
            int x = 0;
            for (int i = 0; i < Keys.Length; i++)
            {
                var text = L.T(Keys[i]);
                int w = TextRenderer.MeasureText(g, text, Font).Width + Theme.Dp(24);
                var r = new Rectangle(x, 0, w, Height);
                hits[i] = r;
                var on = i == Selected;
                TextRenderer.DrawText(g, text, Font, r, on || i == hot ? Theme.Text : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                if (on)
                {
                    int uw = Math.Min(Theme.Dp(28), w - Theme.Dp(12));
                    var u = new RectangleF(r.X + (r.Width - uw) / 2f, Height - Theme.Dp(4), uw, Theme.Dp(3));
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = Theme.RoundRect(u, Theme.Dp(1));
                    using var b = new SolidBrush(Theme.Accent);
                    g.FillPath(b, path);
                }
                x += w;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = -1;
            for (int i = 0; i < hits.Length; i++) if (hits[i].Contains(e.Location)) h = i;
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < hits.Length; i++)
                if (hits[i].Contains(e.Location) && i != Selected)
                {
                    Selected = i;
                    Invalidate();
                    Changed?.Invoke(i);
                    return;
                }
        }
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
            PaintCard(g, this);

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
            PaintCard(g, this);
            var now = DateTime.Now;
            var date = DateOnly.FromDateTime(now);
            const TextFormatFlags F = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            int pad = Theme.Dp(20);

            // title row: what this is on the left, the city on the right (Wintor shows the torrent's name there)
            using var title = Theme.UI(10.5f, FontStyle.Bold);
            Icons.Draw(g, '', new RectangleF(pad, Theme.Dp(14), Theme.Dp(16), Theme.Dp(16)), Theme.Text);
            using var titleFont = Theme.UI(10.5f);
            TextRenderer.DrawText(g, date.ToString("d MMMM yyyy", L.Culture), titleFont, new Rectangle(pad + Theme.Dp(24), Theme.Dp(4), Width / 2 - pad, Theme.Dp(36)), Theme.Text, F);
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

    /// A card taller than its place: the wheel and the thumb scroll the part under the fixed header.
    abstract class ScrollCard : Control
    {
        protected int scroll;
        int fixedH, scrolledH, hot = -1;
        bool drag;
        int dragY, dragScroll;

        protected ScrollCard(Settings s)
        {
            S = s;
            Font = Theme.UI(10.5f);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected Settings S { get; }
        protected int Hot => hot;
        protected int Scroll => scroll;
        protected virtual int FixedHeight => 0;
        protected abstract int ScrolledHeight { get; }
        protected abstract void Prepare();
        protected virtual void DrawFixed(Graphics g, int width) { }
        protected abstract void DrawScrolled(Graphics g, int width, int origin);
        protected virtual int HitRow(int mouseY) => -1;

        void Measure()
        {
            Prepare();
            fixedH = FixedHeight;
            scrolledH = ScrolledHeight;
            scroll = Math.Clamp(scroll, 0, MaxScroll);
            AfterMeasure();
            scroll = Math.Clamp(scroll, 0, MaxScroll);
        }

        /// Runs after the content is measured. Used once, to bring today into view.
        protected virtual void AfterMeasure() { }

        int MaxScroll => Math.Max(0, scrolledH - Math.Max(1, Height - fixedH));

        Rectangle Thumb()
        {
            int max = MaxScroll;
            if (max <= 0 || Height <= fixedH + Theme.Dp(16)) return Rectangle.Empty;
            int trackTop = fixedH + Theme.Dp(6);
            int trackH = Height - trackTop - Theme.Dp(6);
            int view = Math.Max(1, Height - fixedH);
            int h = Math.Max(Theme.Dp(24), (int)(trackH * (view / (float)(view + max))));
            h = Math.Min(h, trackH);
            int y = trackTop + (int)((trackH - h) * (scroll / (float)max));
            return new Rectangle(Width - Theme.Dp(10), y, Theme.Dp(4), h);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Measure();
            var next = Math.Clamp(scroll - Math.Sign(e.Delta) * Theme.Dp(64), 0, MaxScroll);
            if (next == scroll) return;
            scroll = next;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Measure();
            var t = Thumb();
            var grab = t.IsEmpty ? Rectangle.Empty : new Rectangle(t.X - Theme.Dp(8), t.Y, t.Width + Theme.Dp(16), t.Height);
            if (!grab.Contains(e.Location)) return;
            drag = true;
            dragY = e.Y;
            dragScroll = scroll;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag)
            {
                Measure();
                int max = MaxScroll;
                int trackTop = fixedH + Theme.Dp(6);
                int trackH = Math.Max(1, Height - trackTop - Theme.Dp(6));
                int view = Math.Max(1, Height - fixedH);
                int h = Math.Max(Theme.Dp(24), (int)(trackH * (view / (float)(view + max))));
                h = Math.Min(h, trackH);
                int usable = Math.Max(1, trackH - h);
                scroll = Math.Clamp(dragScroll + (int)((e.Y - dragY) / (float)usable * max), 0, max);
                Invalidate();
                return;
            }
            int row = HitRow(e.Y);
            if (row != hot) { hot = row; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            drag = false;
            Capture = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!drag && hot != -1) { hot = -1; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 4 || Height < 4) return;
            Measure();
            var g = e.Graphics;
            PaintCard(g, this);
            int width = Width - Theme.Dp(16);
            g.SetClip(new Rectangle(Theme.Dp(1), Theme.Dp(1), Width - Theme.Dp(2), Math.Max(1, fixedH - Theme.Dp(1))));
            DrawFixed(g, width);
            g.SetClip(new Rectangle(Theme.Dp(1), fixedH, Width - Theme.Dp(2), Math.Max(1, Height - fixedH - Theme.Dp(1))));
            DrawScrolled(g, width, fixedH - scroll);
            g.ResetClip();
            var thumb = Thumb();
            if (thumb.IsEmpty) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Theme.RoundRect(thumb, Math.Min(Theme.Dp(2), thumb.Width / 2f));
            using var b = new SolidBrush(Theme.Faded);
            g.FillPath(b, path);
        }
    }

    /// This week, Monday to Sunday: the prayers that are shown, without Tahajjud and Duha.
    class WeekCard : ScrollCard
    {
        readonly record struct Line(DateOnly Date, bool Today, bool Friday, string Label, string[] Cells);
        Line[] lines = [];
        P[] prayers = [];

        public WeekCard(Settings s) : base(s) { }

        protected override int ScrolledHeight => prayers.Length == 0 ? Theme.Dp(80) : Theme.Dp(40) + lines.Length * Theme.Dp(36) + Theme.Dp(8);

        protected override void Prepare()
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var start = Hijri.WeekStart(today);
            prayers = Enum.GetValues<P>().Where(p => p is not (P.Tahajjud or P.Duha) && !S.Hidden.Contains(p)).ToArray();
            lines = Enumerable.Range(0, 7).Select(i =>
            {
                var d = start.AddDays(i);
                var times = Data.Times(d, day => Data.GetDay(S.City, day), S);
                string Cell(P p) => times?.Where(t => t.P == p).Select(t => L.Time(t.At)).FirstOrDefault() ?? "—";
                return new Line(d, d == today, d.DayOfWeek == DayOfWeek.Friday, d.ToString("ddd, d MMM", L.Culture), prayers.Select(Cell).ToArray());
            }).ToArray();
        }

        protected override int HitRow(int mouseY)
        {
            int rel = mouseY - Theme.Dp(40) + Scroll;
            if (rel < 0) return -1;
            int i = rel / Theme.Dp(36);
            return i < lines.Length ? i : -1;
        }

        protected override void DrawScrolled(Graphics g, int width, int origin)
        {
            const TextFormatFlags L_ = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            const TextFormatFlags R_ = L_ | TextFormatFlags.Right;
            int pad = Theme.Dp(16), head = Theme.Dp(40), rowH = Theme.Dp(36);
            if (prayers.Length == 0 || lines.Length == 0)
            {
                TextRenderer.DrawText(g, L.T("NoData"), Font, new Rectangle(pad, origin, width - pad, Theme.Dp(48)), Theme.Muted, L_);
                return;
            }
            int dateW = Theme.Dp(140);
            int colW = Math.Max(1, Math.Max(0, width - pad * 2 - dateW) / prayers.Length);
            int yHead = origin;
            using (var headFont = Theme.UI(9.5f))
            {
                TextRenderer.DrawText(g, L.T("Date"), headFont, new Rectangle(pad, yHead, dateW, head), Theme.Muted, L_);
                int cellW = Math.Max(1, colW - Theme.Dp(8));
                for (int c = 0; c < prayers.Length; c++)
                    TextRenderer.DrawText(g, L.Name(prayers[c]), headFont, new Rectangle(pad + dateW + c * colW, yHead, cellW, head), Theme.Muted, R_);
            }
            using var line = new Pen(Theme.CardStroke);
            g.DrawLine(line, Theme.Dp(8), yHead + head - 1, width - Theme.Dp(4), yHead + head - 1);
            for (int i = 0; i < lines.Length; i++)
            {
                var row = lines[i];
                int y = origin + head + i * rowH;
                if (y + rowH < 0 || y > Height) continue;
                var cell = new RectangleF(Theme.Dp(4), y + Theme.Dp(2), Width - Theme.Dp(18), rowH - Theme.Dp(4));
                if (row.Today || i == Hot)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var rp = Theme.RoundRect(cell, Theme.Dp(Theme.ControlRadius));
                    using var rb = new SolidBrush(Theme.Line);
                    g.FillPath(rb, rp);
                }
                if (row.Today)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var pill = Theme.RoundRect(new RectangleF(cell.X, cell.Y + (cell.Height - Theme.Dp(16)) / 2f, Theme.Dp(3), Theme.Dp(16)), Theme.Dp(1));
                    using var ab = new SolidBrush(Theme.Accent);
                    g.FillPath(ab, pill);
                }
                var fg = row.Today ? Theme.Text : row.Friday ? Theme.Accent : Theme.Text;
                int cellW = Math.Max(1, colW - Theme.Dp(8));
                TextRenderer.DrawText(g, row.Label, Font, new Rectangle(pad, y, dateW, rowH), fg, L_);
                for (int c = 0; c < prayers.Length; c++)
                    TextRenderer.DrawText(g, row.Cells[c], Font, new Rectangle(pad + dateW + c * colW, y, cellW, rowH), Theme.Text, R_);
            }
        }
    }

    /// Suhoor and iftar for the current Ramadan, or the next one when this year's has ended.
    class RamadanCard : ScrollCard
    {
        readonly record struct FastDay(int N, bool Today, string Date, string Suhoor, string Iftar);
        FastDay[] days = [];
        string title = "", range = "", status = "";

        public RamadanCard(Settings s) : base(s) { }
        bool jumped;

        protected override void AfterMeasure()
        {
            if (jumped) return;
            jumped = true;
            int i = Array.FindIndex(days, d => d.Today);
            if (i > 0) scroll = (i - 1) * Theme.Dp(36); // leave the day before today in view
        }

        protected override int FixedHeight => Theme.Dp(104);
        protected override int ScrolledHeight => Math.Max(Theme.Dp(48), days.Length * Theme.Dp(36) + Theme.Dp(8));

        protected override void Prepare()
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var (y, start, end) = Hijri.RamadanAround(today, S.HijriAdjust);
            title = $"{L.T("HM9")} {y}";
            var last = end.AddDays(-1);
            var fmt = start.Year == last.Year ? "d MMMM" : "d MMMM yyyy";
            range = start.ToString(fmt, L.Culture) + " — " + last.ToString(fmt, L.Culture);
            status = RamadanStatus(S, now);
            var list = new List<FastDay>();
            for (var d = start; d < end; d = d.AddDays(1))
            {
                var times = Data.Times(d, day => Data.GetDay(S.City, day), S);
                string At(P p) => times?.Where(t => t.P == p).Select(t => L.Time(t.At)).FirstOrDefault() ?? "—";
                list.Add(new(Hijri.Of(d, S.HijriAdjust).D, d == today, d.ToString("d MMMM, ddd", L.Culture), At(P.Fajr), At(P.Maghrib)));
            }
            days = list.ToArray();
        }

        protected override int HitRow(int mouseY)
        {
            int rel = mouseY - FixedHeight + Scroll;
            if (rel < 0) return -1;
            int i = rel / Theme.Dp(36);
            return i < days.Length ? i : -1;
        }

        protected override void DrawFixed(Graphics g, int width)
        {
            const TextFormatFlags F = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            int pad = Theme.Dp(16);
            using var titleFont = Theme.UI(10.5f, FontStyle.Bold);
            using var small = Theme.UI(9.5f);
            TextRenderer.DrawText(g, title, titleFont, new Rectangle(pad, Theme.Dp(8), width / 2, Theme.Dp(26)), Theme.Text, F);
            TextRenderer.DrawText(g, range, small, new Rectangle(width / 2, Theme.Dp(8), width / 2 - pad, Theme.Dp(26)), Theme.Muted, F | TextFormatFlags.Right);
            TextRenderer.DrawText(g, status, small, new Rectangle(pad, Theme.Dp(34), width - pad * 2, Theme.Dp(24)), Theme.Text, F);
            using var line = new Pen(Theme.CardStroke);
            g.DrawLine(line, Theme.Dp(8), Theme.Dp(62), width - Theme.Dp(4), Theme.Dp(62));
            var (x0, x1, x2, x3, w0, w1, w2) = Cols(width);
            TextRenderer.DrawText(g, "#", small, new Rectangle(x0, Theme.Dp(66), w0, Theme.Dp(36)), Theme.Muted, F | TextFormatFlags.Right);
            TextRenderer.DrawText(g, L.T("Date"), small, new Rectangle(x1, Theme.Dp(66), w1, Theme.Dp(36)), Theme.Muted, F);
            TextRenderer.DrawText(g, L.T("Suhoor"), small, new Rectangle(x2, Theme.Dp(66), w2 - Theme.Dp(8), Theme.Dp(36)), Theme.Muted, F | TextFormatFlags.Right);
            TextRenderer.DrawText(g, L.T("Iftar"), small, new Rectangle(x3, Theme.Dp(66), w2 - Theme.Dp(8), Theme.Dp(36)), Theme.Muted, F | TextFormatFlags.Right);
        }

        protected override void DrawScrolled(Graphics g, int width, int origin)
        {
            const TextFormatFlags F = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            int rowH = Theme.Dp(36);
            var (x0, x1, x2, x3, w0, w1, w2) = Cols(width);
            if (days.Length == 0)
            {
                TextRenderer.DrawText(g, L.T("NoData"), Font, new Rectangle(Theme.Dp(16), origin, width, rowH), Theme.Muted, F);
                return;
            }
            for (int i = 0; i < days.Length; i++)
            {
                var d = days[i];
                int y = origin + i * rowH;
                if (y + rowH < FixedHeight || y > Height) continue;
                var cell = new RectangleF(Theme.Dp(4), y + Theme.Dp(2), Width - Theme.Dp(18), rowH - Theme.Dp(4));
                if (d.Today || i == Hot)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var rp = Theme.RoundRect(cell, Theme.Dp(Theme.ControlRadius));
                    using var rb = new SolidBrush(Theme.Line);
                    g.FillPath(rb, rp);
                }
                if (d.Today)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var pill = Theme.RoundRect(new RectangleF(cell.X, cell.Y + (cell.Height - Theme.Dp(16)) / 2f, Theme.Dp(3), Theme.Dp(16)), Theme.Dp(1));
                    using var ab = new SolidBrush(Theme.Accent);
                    g.FillPath(ab, pill);
                }
                TextRenderer.DrawText(g, d.N.ToString(), Font, new Rectangle(x0, y, w0 - Theme.Dp(8), rowH), Theme.Text, F | TextFormatFlags.Right);
                TextRenderer.DrawText(g, d.Date, Font, new Rectangle(x1, y, w1 - Theme.Dp(8), rowH), Theme.Text, F);
                TextRenderer.DrawText(g, d.Suhoor, Font, new Rectangle(x2, y, w2 - Theme.Dp(8), rowH), Theme.Text, F | TextFormatFlags.Right);
                TextRenderer.DrawText(g, d.Iftar, Font, new Rectangle(x3, y, w2 - Theme.Dp(8), rowH), Theme.Text, F | TextFormatFlags.Right);
            }
        }

        static (int X0, int X1, int X2, int X3, int W0, int W1, int W2) Cols(int width)
        {
            int pad = Theme.Dp(16), numW = Theme.Dp(44);
            int rest = Math.Max(0, width - pad * 2 - numW);
            int timeW = Math.Max(Theme.Dp(88), rest / 4);
            int dateW = Math.Max(0, rest - timeW * 2);
            int x = pad;
            return (x, x + numW, x + numW + dateW, x + numW + dateW + timeW, numW, dateW, timeW);
        }
    }
}
