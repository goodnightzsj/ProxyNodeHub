using System.Drawing;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>双缓冲 DataGridView — 用 SetStyle 替代反射, Native AOT 安全</summary>
public class BufferedGridView : DataGridView
{
    public BufferedGridView()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }
}
