using System.Drawing.Drawing2D;
using System.Text.Json;

namespace NamazTimes;

/// Line icons from the free Hugeicons set (Stroke Rounded, MIT, https://hugeicons.com), the same family Wintor uses.
/// icons.json holds SVG path data on a 24×24 grid (tools/icons.mjs); here it is parsed into GraphicsPaths once and
/// stroked at 1.5 grid units with round caps, so icons stay crisp at any size and take any colour.
/// The rest of the app still names icons by their old Segoe Fluent code point; Map translates them.
public static class Icons
{
    static readonly Dictionary<string, string[][]> Data =
        JsonSerializer.Deserialize<Dictionary<string, string[][]>>(typeof(Icons).Assembly.GetManifestResourceStream("icons.json")!)!;
    static readonly Dictionary<string, (bool Fill, GraphicsPath Path)[]> Parsed = [];
    static readonly Dictionary<(string, int, int), Bitmap> Bitmaps = [];

    /// Segoe Fluent Icons code point → Hugeicons name.
    static readonly Dictionary<char, string> Map = new()
    {
        [''] = "Quran01", [''] = "Tasbih", [''] = "Dua", [''] = "Allah",
        [''] = "PrayerRug01", [''] = "History", [''] = "Zakat", [''] = "Calendar03",
        [''] = "MoonStar", [''] = "News01", [''] = "InformationCircle", [''] = "Settings01",
        [''] = "Mosque01", [''] = "Mosque02", [''] = "Clock01", [''] = "Time04", [''] = "Clock01",
        [''] = "Location01", [''] = "Moon02", [''] = "ArrowDown01", [''] = "ArrowUp01",
        [''] = "ArrowLeft01", [''] = "ArrowRight01", [''] = "Add01", [''] = "MoreHorizontal",
        [''] = "Pin", [''] = "Search01", [''] = "Refresh", [''] = "Tick02", [''] = "Maximize01",
        [''] = "VolumeHigh", [''] = "VolumeMute02", [''] = "VolumeHigh", [''] = "Translate",
        [''] = "SunMoon", [''] = "Droplet", [''] = "Power", [''] = "NotificationOff01",
        [''] = "Notification01", [''] = "AppWindow", [''] = "ArrowReloadHorizontal",
        [''] = "Download01", [''] = "Upload01", [''] = "LinkSquare02", [''] = "Archive02",
        [''] = "Bug01", [''] = "Menu01", [''] = "Cancel01", [''] = "Bookmark02",
        [''] = "TextFont", [''] = "ZoomIn",
    };

    public static bool Has(char glyph) => Map.ContainsKey(glyph);

    /// Draws the icon for an old glyph code (or does nothing if it has none) centred in box.
    public static void Draw(Graphics g, char glyph, RectangleF box, Color color)
    {
        if (Map.TryGetValue(glyph, out var name)) Draw(g, name, box, color);
    }

    /// Draws a Hugeicons icon by name, as large as fits in box and centred.
    public static void Draw(Graphics g, string name, RectangleF box, Color color)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var k = Math.Min(box.Width, box.Height) / 24f;
        g.TranslateTransform(box.X + (box.Width - 24 * k) / 2, box.Y + (box.Height - 24 * k) / 2);
        g.ScaleTransform(k, k);
        using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        foreach (var (fill, path) in Paths(name))
            if (fill) g.FillPath(brush, path); else g.DrawPath(pen, path);
        g.Restore(state);
    }

    /// A cached bitmap of the icon (for menus and window icons).
    public static Bitmap Bitmap(char glyph, int size, Color color) =>
        Map.TryGetValue(glyph, out var name) ? Bitmap(name, size, color) : new Bitmap(size, size);

    public static Bitmap Bitmap(string name, int size, Color color)
    {
        var key = (name, size, color.ToArgb());
        if (Bitmaps.TryGetValue(key, out var b)) return b;
        b = new Bitmap(size, size);
        using (var g = Graphics.FromImage(b)) Draw(g, name, new RectangleF(0, 0, size, size), color);
        return Bitmaps[key] = b;
    }

    static (bool, GraphicsPath)[] Paths(string name)
    {
        if (Parsed.TryGetValue(name, out var p)) return p;
        return Parsed[name] = Data[name].Select(e => (e[0] == "f", Svg.Parse(e[1]))).ToArray();
    }
}

/// Minimal SVG path-data parser (M L H V C S Q T A Z, absolute and relative) into a GraphicsPath.
static class Svg
{
    public static GraphicsPath Parse(string d)
    {
        var path = new GraphicsPath(FillMode.Winding);
        int i = 0;
        char cmd = 'M';
        PointF cur = default, start = default, lastCtrl = default;
        char last = ' ';

        void Skip() { while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++; }
        bool NumAhead() { Skip(); return i < d.Length && (char.IsDigit(d[i]) || d[i] is '-' or '+' or '.'); }
        float Num()
        {
            Skip();
            int s = i;
            if (i < d.Length && d[i] is '-' or '+') i++;
            bool dot = false;
            while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot)))
            {
                if (d[i] == '.') dot = true;
                i++;
            }
            if (i < d.Length && d[i] is 'e' or 'E')
            {
                i++;
                if (i < d.Length && d[i] is '-' or '+') i++;
                while (i < d.Length && char.IsDigit(d[i])) i++;
            }
            return float.Parse(d.AsSpan(s, i - s), System.Globalization.CultureInfo.InvariantCulture);
        }
        bool Flag() { Skip(); return d[i++] == '1'; }
        PointF Pt(bool rel) { var x = Num(); var y = Num(); return rel ? new(cur.X + x, cur.Y + y) : new(x, y); }

        while (true)
        {
            Skip();
            if (i >= d.Length) break;
            if (char.IsLetter(d[i])) cmd = d[i++];
            else if (cmd is 'M') cmd = 'L';
            else if (cmd is 'm') cmd = 'l';
            var rel = char.IsLower(cmd);
            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                    cur = start = Pt(rel);
                    path.StartFigure();
                    break;
                case 'L': { var p = Pt(rel); path.AddLine(cur, p); cur = p; break; }
                case 'H': { var x = Num(); var p = new PointF(rel ? cur.X + x : x, cur.Y); path.AddLine(cur, p); cur = p; break; }
                case 'V': { var y = Num(); var p = new PointF(cur.X, rel ? cur.Y + y : y); path.AddLine(cur, p); cur = p; break; }
                case 'C':
                {
                    var c1 = Pt(rel); var c2 = Pt(rel); var p = Pt(rel);
                    path.AddBezier(cur, c1, c2, p); lastCtrl = c2; cur = p; break;
                }
                case 'S':
                {
                    var c1 = last is 'C' or 'S' ? new PointF(2 * cur.X - lastCtrl.X, 2 * cur.Y - lastCtrl.Y) : cur;
                    var c2 = Pt(rel); var p = Pt(rel);
                    path.AddBezier(cur, c1, c2, p); lastCtrl = c2; cur = p; break;
                }
                case 'Q':
                {
                    var q = Pt(rel); var p = Pt(rel);
                    Quad(path, cur, q, p); lastCtrl = q; cur = p; break;
                }
                case 'T':
                {
                    var q = last is 'Q' or 'T' ? new PointF(2 * cur.X - lastCtrl.X, 2 * cur.Y - lastCtrl.Y) : cur;
                    var p = Pt(rel);
                    Quad(path, cur, q, p); lastCtrl = q; cur = p; break;
                }
                case 'A':
                {
                    var rx = Num(); var ry = Num(); var rot = Num(); var large = Flag(); var sweep = Flag(); var p = Pt(rel);
                    Arc(path, cur, rx, ry, rot, large, sweep, p); cur = p; break;
                }
                case 'Z':
                    path.CloseFigure();
                    cur = start;
                    break;
                default: return path; // unknown command: keep what we have
            }
            last = char.ToUpperInvariant(cmd);
            if (last == 'Z' && !NumAhead()) continue;
        }
        return path;
    }

    static void Quad(GraphicsPath path, PointF p0, PointF q, PointF p) =>
        path.AddBezier(p0, new PointF(p0.X + 2f / 3 * (q.X - p0.X), p0.Y + 2f / 3 * (q.Y - p0.Y)),
            new PointF(p.X + 2f / 3 * (q.X - p.X), p.Y + 2f / 3 * (q.Y - p.Y)), p);

    // SVG endpoint arc → centre form → cubic Béziers of at most 90° each (SVG 1.1, appendix F.6.5).
    static void Arc(GraphicsPath path, PointF p0, float rx, float ry, float rotDeg, bool large, bool sweep, PointF p1)
    {
        if (p0 == p1) return;
        if (rx == 0 || ry == 0) { path.AddLine(p0, p1); return; }
        double Rx = Math.Abs(rx), Ry = Math.Abs(ry), phi = rotDeg * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
        double dx = (p0.X - p1.X) / 2.0, dy = (p0.Y - p1.Y) / 2.0;
        double x1 = cos * dx + sin * dy, y1 = -sin * dx + cos * dy;
        var lambda = x1 * x1 / (Rx * Rx) + y1 * y1 / (Ry * Ry);
        if (lambda > 1) { Rx *= Math.Sqrt(lambda); Ry *= Math.Sqrt(lambda); }
        double num = Rx * Rx * Ry * Ry - Rx * Rx * y1 * y1 - Ry * Ry * x1 * x1, den = Rx * Rx * y1 * y1 + Ry * Ry * x1 * x1;
        var coef = Math.Sqrt(Math.Max(0, num / den)) * (large == sweep ? -1 : 1);
        double cxp = coef * Rx * y1 / Ry, cyp = -coef * Ry * x1 / Rx;
        double cx = cos * cxp - sin * cyp + (p0.X + p1.X) / 2.0, cy = sin * cxp + cos * cyp + (p0.Y + p1.Y) / 2.0;
        static double Angle(double ux, double uy, double vx, double vy) => Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        var t1 = Angle(1, 0, (x1 - cxp) / Rx, (y1 - cyp) / Ry);
        var dt = Angle((x1 - cxp) / Rx, (y1 - cyp) / Ry, (-x1 - cxp) / Rx, (-y1 - cyp) / Ry);
        if (!sweep && dt > 0) dt -= 2 * Math.PI;
        else if (sweep && dt < 0) dt += 2 * Math.PI;
        var n = (int)Math.Ceiling(Math.Abs(dt) / (Math.PI / 2) - 1e-9);
        var delta = dt / n;
        var k = 4.0 / 3 * Math.Tan(delta / 4);
        PointF At(double a) => new((float)(cx + Rx * Math.Cos(a) * cos - Ry * Math.Sin(a) * sin), (float)(cy + Rx * Math.Cos(a) * sin + Ry * Math.Sin(a) * cos));
        (double X, double Y) Tan(double a) => (-Rx * Math.Sin(a) * cos - Ry * Math.Cos(a) * sin, -Rx * Math.Sin(a) * sin + Ry * Math.Cos(a) * cos);
        for (int s = 0; s < n; s++)
        {
            double a1 = t1 + s * delta, a2 = a1 + delta;
            PointF e1 = s == 0 ? p0 : At(a1), e2 = s == n - 1 ? p1 : At(a2);
            var (d1x, d1y) = Tan(a1);
            var (d2x, d2y) = Tan(a2);
            path.AddBezier(e1, new PointF((float)(e1.X + k * d1x), (float)(e1.Y + k * d1y)),
                new PointF((float)(e2.X - k * d2x), (float)(e2.Y - k * d2y)), e2);
        }
    }
}
