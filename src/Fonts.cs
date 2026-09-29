using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>
/// 字体系统: 衬线品牌 × 界面无衬线 × 等宽数据。
/// Georgia (Win 自带衬线) 做卷宗气质; Cascadia Mono 做终端数据。
/// </summary>
public static class Fonts
{
    public static readonly string Mono = Pick("Cascadia Mono", "Cascadia Code", "Consolas");
    public static readonly string Ui = Pick("Segoe UI Variable Text", "Segoe UI");
    public static readonly string UiBold = Pick("Segoe UI Semibold", "Segoe UI");
    public static readonly string Serif = Pick("Georgia", "Times New Roman");

    public static readonly Font Mono8 = new(Mono, 8F);
    public static readonly Font Mono85 = new(Mono, 8.5F);
    public static readonly Font Mono9 = new(Mono, 9F);
    public static readonly Font Mono10 = new(Mono, 10F);
    public static readonly Font Ui9 = new(Ui, 9F);
    public static readonly Font Ui9Bold = new(UiBold, 9F);
    public static readonly Font Ui10 = new(Ui, 10F);
    public static readonly Font Ui11Bold = new(UiBold, 11F);
    public static readonly Font SerifBrand = new(Serif, 14F, FontStyle.Bold);
    public static readonly Font SerifTitle = new(Serif, 15F, FontStyle.Bold);
    public static readonly Font SerifItalic = new(Serif, 11F, FontStyle.Italic);

    private static string Pick(params string[] candidates)
    {
        try
        {
            var installed = new HashSet<string>(
                System.Drawing.FontFamily.Families.Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);
            return candidates.FirstOrDefault(c => installed.Contains(c)) ?? candidates[^1];
        }
        catch { return candidates[^1]; }
    }
}