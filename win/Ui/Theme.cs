using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SmsLink.Ui;

/// <summary>统一的配色、字体和圆角绘制工具，界面各处都用它，避免风格漂移。</summary>
public static class Theme
{
    public static bool IsDark { get; private set; }

    public static Color Brand => IsDark ? Color.FromArgb(0x81, 0x8C, 0xFF) : Color.FromArgb(0x4F, 0x46, 0xE5);
    public static Color BrandDark => IsDark ? Color.FromArgb(0xA5, 0xAD, 0xFF) : Color.FromArgb(0x43, 0x38, 0xCA);
    public static Color BrandHover => IsDark ? Color.FromArgb(0x92, 0x9B, 0xFF) : Color.FromArgb(0x5B, 0x53, 0xE8);
    public static Color BrandSoft => IsDark ? Color.FromArgb(0x2A, 0x2E, 0x52) : Color.FromArgb(0xEE, 0xF0, 0xFE);
    public static Color Bg => IsDark ? Color.FromArgb(0x12, 0x14, 0x19) : Color.FromArgb(0xF4, 0xF5, 0xF8);
    public static Color Card => IsDark ? Color.FromArgb(0x1B, 0x1E, 0x25) : Color.White;
    public static Color Input => IsDark ? Color.FromArgb(0x22, 0x26, 0x2F) : Color.White;
    public static Color Line => IsDark ? Color.FromArgb(0x35, 0x3A, 0x46) : Color.FromArgb(0xE6, 0xE8, 0xEF);
    public static Color Text => IsDark ? Color.FromArgb(0xF0, 0xF2, 0xF7) : Color.FromArgb(0x1C, 0x1F, 0x26);
    public static Color Muted => IsDark ? Color.FromArgb(0xA6, 0xAD, 0xBB) : Color.FromArgb(0x6B, 0x72, 0x80);
    public static Color Ok => IsDark ? Color.FromArgb(0x4A, 0xD4, 0x7D) : Color.FromArgb(0x16, 0xA3, 0x4A);
    public static Color Warn => IsDark ? Color.FromArgb(0xFB, 0xB9, 0x42) : Color.FromArgb(0xF5, 0x9E, 0x0B);
    public static Color Danger => IsDark ? Color.FromArgb(0xFF, 0x7B, 0x7B) : Color.FromArgb(0xDC, 0x26, 0x26);
    public static Color OnBrand => Color.White;
    public static Color Bubble => IsDark ? Color.FromArgb(0x20, 0x23, 0x2B) : Color.White;
    public static Color CodeBubble => IsDark ? Color.FromArgb(0x28, 0x2D, 0x4C) : Color.FromArgb(0xEC, 0xEF, 0xFE);
    public static Color CodeLine => IsDark ? Color.FromArgb(0x4B, 0x54, 0x88) : Color.FromArgb(0xD8, 0xDD, 0xFB);
    public static Color GhostHover => IsDark ? Color.FromArgb(0x2B, 0x2F, 0x39) : Color.FromArgb(0xF7, 0xF8, 0xFB);
    public static Color GhostPressed => IsDark ? Color.FromArgb(0x35, 0x3A, 0x46) : Color.FromArgb(0xEC, 0xED, 0xF3);
    public static Color Disabled => IsDark ? Color.FromArgb(0x25, 0x28, 0x30) : Color.FromArgb(0xF1, 0xF2, 0xF6);
    public static Color DisabledText => IsDark ? Color.FromArgb(0x72, 0x78, 0x84) : Color.FromArgb(0xB0, 0xB4, 0xBE);
    public static Color DangerSoft => IsDark ? Color.FromArgb(0x3A, 0x25, 0x29) : Color.White;
    public static Color DangerHover => IsDark ? Color.FromArgb(0x4A, 0x29, 0x2E) : Color.FromArgb(0xFE, 0xF2, 0xF2);
    public static Color DangerPressed => IsDark ? Color.FromArgb(0x55, 0x2D, 0x33) : Color.FromArgb(0xFC, 0xE8, 0xE8);
    public static Color DangerLine => IsDark ? Color.FromArgb(0x75, 0x3D, 0x46) : Color.FromArgb(0xFA, 0xCB, 0xCB);

    public static void Apply(bool dark) => IsDark = dark;

    public static void ApplyButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = Input;
        button.ForeColor = Text;
        button.FlatAppearance.BorderColor = Line;
        button.FlatAppearance.MouseOverBackColor = GhostHover;
        button.FlatAppearance.MouseDownBackColor = GhostPressed;
    }

    public static void ApplyMenu(ContextMenuStrip menu)
    {
        menu.BackColor = Card;
        menu.ForeColor = Text;
        menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
        foreach (ToolStripItem item in menu.Items)
        {
            item.BackColor = Card;
            item.ForeColor = item.Enabled ? Text : DisabledText;
        }
    }

    public static void ApplyTitleBar(Form form)
    {
        try
        {
            var dark = IsDark ? 1 : 0;
            if (DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));
            }
        }
        catch
        {
            // 旧版 Windows 不支持深色标题栏，忽略。
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Card;
        public override Color ImageMarginGradientBegin => Card;
        public override Color ImageMarginGradientMiddle => Card;
        public override Color ImageMarginGradientEnd => Card;
        public override Color MenuItemSelected => GhostHover;
        public override Color MenuItemBorder => Line;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Line;
    }

    private static readonly string UiFamily = PickFamily("Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI");
    private static readonly string MonoFamily = PickFamily("Consolas", "Cascadia Mono", "Courier New");

    private static string PickFamily(params string[] candidates)
    {
        var installed = FontFamily.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in candidates)
        {
            if (installed.Contains(name)) return name;
        }
        return FontFamily.GenericSansSerif.Name;
    }

    public static Font Ui(float size, FontStyle style = FontStyle.Regular) =>
        new(UiFamily, size, style, GraphicsUnit.Point);

    public static Font Mono(float size, FontStyle style = FontStyle.Bold) =>
        new(MonoFamily, size, style, GraphicsUnit.Point);

    public static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var r = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>四个角可以给不同半径——聊天气泡就是把左下角收小一点。</summary>
    public static GraphicsPath CornerPath(Rectangle rect, int topLeft, int topRight, int bottomRight, int bottomLeft)
    {
        var path = new GraphicsPath();
        var limit = Math.Max(1, Math.Min(rect.Width, rect.Height) / 2);
        int R(int value) => Math.Max(0, Math.Min(value, limit));

        var tl = R(topLeft);
        var tr = R(topRight);
        var br = R(bottomRight);
        var bl = R(bottomLeft);

        if (tl > 0) path.AddArc(rect.X, rect.Y, tl * 2, tl * 2, 180, 90);
        else path.AddLine(rect.X, rect.Y, rect.X, rect.Y);
        if (tr > 0) path.AddArc(rect.Right - tr * 2, rect.Y, tr * 2, tr * 2, 270, 90);
        if (br > 0) path.AddArc(rect.Right - br * 2, rect.Bottom - br * 2, br * 2, br * 2, 0, 90);
        if (bl > 0) path.AddArc(rect.X, rect.Bottom - bl * 2, bl * 2, bl * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillPath(Graphics g, GraphicsPath path, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokePath(Graphics g, GraphicsPath path, Color color, float width = 1f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void FillRounded(Graphics g, Rectangle rect, int radius, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedPath(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRounded(Graphics g, Rectangle rect, int radius, Color color, float width = 1f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedPath(rect, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void FillCircle(Graphics g, Rectangle rect, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, rect);
    }

    public static void Prepare(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    }
}
