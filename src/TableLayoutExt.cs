using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>行列样式的语义化常量，比裸 SizeType 更易读。</summary>
internal static class TableLayoutExt
{
    public const SizeType AutoSize_ = SizeType.AutoSize;
    public const SizeType Fill_ = SizeType.Percent;
    public const SizeType Rule_ = SizeType.Absolute;

    public static TableLayoutPanel Rows(this TableLayoutPanel t, params SizeType[] types)
    {
        t.RowCount = types.Length;
        t.RowStyles.Clear();
        foreach (var s in types)
            t.RowStyles.Add(new RowStyle(s, s == SizeType.Percent ? 100f : 0f));
        return t;
    }

    public static TableLayoutPanel Columns(this TableLayoutPanel t, params SizeType[] types)
    {
        t.ColumnCount = types.Length;
        t.ColumnStyles.Clear();
        foreach (var s in types)
            t.ColumnStyles.Add(new ColumnStyle(s, s == SizeType.Percent ? 100f : 0f));
        return t;
    }
}
