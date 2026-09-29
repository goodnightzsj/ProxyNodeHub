using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>
/// 现代按钮控件 - 支持圆角、Ghost 样式、悬停效果
/// </summary>
public class ModernButton : Control
{
    public Color BaseColor { get; set; } = Theme.Stamp;
    public int CornerRadius { get; set; } = 2;

    /// <summary>描边样式 (次级操作): 墨色细边 + 墨色文字, hover 反白</summary>
    public bool Ghost { get; set; }

    private bool _hover, _pressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = Fonts.Ui9;
        Size = new Size(110, 32);
        ForeColor = Theme.Paper;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var radius = Math.Min(CornerRadius, Math.Min(Width, Height) / 2 - 1);
        if (radius < 1) radius = 0;

        using var path = radius > 0 ? RoundedPath(rect, radius) : SharpPath(rect);

        if (Ghost)
        {
            // 墨色描边按钮: hover 变墨底纸字 (印刷反转)
            using var bg = new SolidBrush(BackColor);
            g.FillPath(bg, path);
            if (_hover)
            {
                using var ink = new SolidBrush(Theme.Ink);
                g.FillPath(ink, path);
            }
            using (var pen = new Pen(Theme.Ink, 1f))
                g.DrawPath(pen, path);
            TextRenderer.DrawText(g, Text, Font, rect,
                !Enabled ? Theme.InkLow : (_hover ? Theme.Paper : Theme.Ink),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }

        var c = BaseColor;
        if (_pressed) c = ControlPaint.Dark(c, 0.1f);
        else if (_hover) c = ControlPaint.Light(c, 0.12f);

        using var brush = new SolidBrush(c);
        g.FillPath(brush, path);

        TextRenderer.DrawText(g, Text, Font, rect, Enabled ? ForeColor : Theme.InkLow,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath SharpPath(Rectangle r)
    {
        var path = new GraphicsPath();
        path.AddRectangle(r);
        return path;
    }

    /// <summary>创建圆角矩形路径</summary>
    public static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}