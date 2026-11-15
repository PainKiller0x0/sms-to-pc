namespace SmsLink.Ui;

/// <summary>扁平圆角按钮：WinForms 默认按钮太"90 年代"，这个自己画，只有三种风格。</summary>
public sealed class FlatButton : Control
{
    public enum Look
    {
        Primary,
        Ghost,
        Danger,
    }

    private bool _hover;
    private bool _pressed;

    public Look Style { get; set; } = Look.Ghost;
    public int CornerRadius { get; set; } = 10;

    public FlatButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        Cursor = Cursors.Hand;
        Font = Theme.Ui(9.5f);
        Height = 38;
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        var (back, border, fore) = Resolve();
        Theme.FillRounded(e.Graphics, rect, CornerRadius, back);
        if (border != Color.Empty) Theme.StrokeRounded(e.Graphics, rect, CornerRadius, border);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            rect,
            fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private (Color Back, Color Border, Color Fore) Resolve()
    {
        if (!Enabled) return (Theme.Disabled, Theme.Line, Theme.DisabledText);

        switch (Style)
        {
            case Look.Primary:
                var back = _pressed ? Theme.BrandDark
                    : _hover ? Theme.BrandHover
                    : Theme.Brand;
                return (back, Color.Empty, Theme.OnBrand);

            case Look.Danger:
                var dangerBack = _pressed ? Theme.DangerPressed
                    : _hover ? Theme.DangerHover
                    : Theme.DangerSoft;
                return (dangerBack, Theme.DangerLine, Theme.Danger);

            default:
                var ghostBack = _pressed ? Theme.GhostPressed
                    : _hover ? Theme.GhostHover
                    : Theme.Card;
                return (ghostBack, Theme.Line, Theme.Text);
        }
    }
}
