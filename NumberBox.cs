using System.Drawing.Drawing2D;
using System.Globalization;

namespace NamazTimes;

/// Windows 11 NumberBox: a Fluent text field (4 px corners, underline that turns accent while typing)
/// with inline ▲▼ spin buttons. Arrow keys and the mouse wheel step the value too; typed text is
/// checked and clamped when you press Enter or leave the field. Drop-in for the NumericUpDown we used.
public class NumberBox : Panel
{
    readonly TextBox box;
    decimal value, min, max = 100, step = 1;
    int decimals, hotSpin = -1;
    bool thousands;
    public event EventHandler? ValueChanged;

    public NumberBox()
    {
        DoubleBuffered = true;
        Height = Theme.Dp(32);
        Width = Theme.Dp(120);
        BackColor = Theme.CtlFill;
        Padding = new Padding(Theme.Dp(10), Theme.Dp(7), SpinW + Theme.Dp(4), 0);
        box = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.CtlFill, ForeColor = Theme.Text, Font = Theme.UI(10.5f) };
        Controls.Add(box);
        Center();
        Cursor = Cursors.IBeam;
        box.GotFocus += (_, _) => Invalidate();
        box.LostFocus += (_, _) => { Commit(); Invalidate(); };
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Commit(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { Step(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Step(-1); e.Handled = true; }
        };
        box.MouseWheel += (_, e) => Step(Math.Sign(e.Delta));
        Display();
    }

    int SpinW => Theme.Dp(60);
    Rectangle SpinRect(int i) => SpinRect(Size, i);

    /// The ▲ (0) and ▼ (1) buttons inside a field of the given size; shared with Stepper.
    public static Rectangle SpinRect(Size size, int i) =>
        new(size.Width - Theme.Dp(60) + i * Theme.Dp(28), Theme.Dp(4), Theme.Dp(26), size.Height - Theme.Dp(8));

    public decimal Minimum { get => min; set { min = value; Value = this.value; } }
    public decimal Maximum { get => max; set { max = value; Value = this.value; } }
    public decimal Increment { get => step; set => step = value; }
    public int DecimalPlaces { get => decimals; set { decimals = value; Display(); } }
    public bool ThousandsSeparator { get => thousands; set { thousands = value; Display(); } }
    public HorizontalAlignment TextAlign { get => box.TextAlign; set => box.TextAlign = value; }
    // Accepted for compatibility with NumericUpDown initialisers; the look comes from the theme.
    public new BorderStyle BorderStyle { get; set; }

    public decimal Value
    {
        get => value;
        set
        {
            var v = Math.Round(Math.Clamp(value, min, max), decimals);
            var changed = v != this.value;
            this.value = v;
            Display();
            if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); box.Font = Font; Center(); }

    // Keep the text vertically centred in the 32 px field whatever the font size.
    void Center() => Padding = new Padding(Theme.Dp(10), Math.Max(0, (Height - box.PreferredHeight) / 2), SpinW + Theme.Dp(4), 0);
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); box.ForeColor = ForeColor; }

    void Display() => box.Text = value.ToString((thousands ? "N" : "F") + decimals, CultureInfo.CurrentCulture);

    void Step(int dir) { Commit(); Value += dir * step; box.SelectionStart = box.TextLength; }

    void Commit()
    {
        var text = box.Text.Replace(" ", "").Replace(" ", "").Replace(" ", "");
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var v)
            || decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out v))
            Value = v;
        else Display(); // not a number: put the last good value back
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var h = SpinRect(0).Contains(e.Location) ? 0 : SpinRect(1).Contains(e.Location) ? 1 : -1;
        Cursor = h >= 0 ? Cursors.Hand : Cursors.IBeam;
        if (h != hotSpin) { hotSpin = h; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hotSpin = -1; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (SpinRect(0).Contains(e.Location)) Step(1);
        else if (SpinRect(1).Contains(e.Location)) Step(-1);
        else box.Focus();
    }

    protected override void OnPaintBackground(PaintEventArgs e) => PaintField(e.Graphics, this, box.Focused, hotSpin);

    /// Fluent field: fill, hairline border, underline (accent while focused), and the two spin buttons.
    public static void PaintField(Graphics g, Control c, bool focused, int hotSpin)
    {
        g.Clear(c.Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int rad = Theme.Dp(Theme.ControlRadius), w = c.Width, h = c.Height;
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, w - 1.5f, h - 1.5f), rad);
        using (var b = new SolidBrush(Theme.CtlFill)) g.FillPath(b, path);
        using (var p = new Pen(Theme.CtlStroke)) g.DrawPath(p, path);
        using (var u = new Pen(focused ? Theme.Accent : Theme.Muted, focused ? Theme.Dp(2) : 1))
            g.DrawLine(u, rad, h - (focused ? Theme.Dp(1) : 1), w - rad, h - (focused ? Theme.Dp(1) : 1));
        using var icons = new Font(Theme.IconFamily, 7.5f * Theme.UiScale);
        string[] glyphs = ["\uE70E", "\uE70D"]; // ChevronUp, ChevronDown
        for (int i = 0; i < 2; i++)
        {
            var r = SpinRect(c.Size, i);
            if (i == hotSpin)
                using (var hp = Theme.RoundRect(r, rad))
                using (var hb = new SolidBrush(Theme.Line)) g.FillPath(hb, hp);
            TextRenderer.DrawText(g, glyphs[i], icons, r, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
