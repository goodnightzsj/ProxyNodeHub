using System.Drawing;

namespace ProxyNodeHub;

/// <summary>
/// DOSSIER 视觉系统 — 纸面情报卷宗
/// 暖纸基底 + 墨色文字 + 印章红单强调。
/// </summary>
public static class Theme
{
    // ── 纸面基底 (暖染, 非纯白) ──
    public static readonly Color Paper = Color.FromArgb(244, 241, 234);
    public static readonly Color PaperDeep = Color.FromArgb(234, 230, 220);
    public static readonly Color PaperHi = Color.FromArgb(251, 249, 244);

    // ── 墨色文字 ──
    public static readonly Color Ink = Color.FromArgb(43, 40, 35);
    public static readonly Color InkMid = Color.FromArgb(110, 104, 92);
    public static readonly Color InkLow = Color.FromArgb(163, 156, 141);

    // ── 发丝线 ──
    public static readonly Color Hairline = Color.FromArgb(216, 210, 196);
    public static readonly Color Rule = Color.FromArgb(43, 40, 35);

    // ── 印章红 (唯一 accent) ──
    public static readonly Color Stamp = Color.FromArgb(191, 69, 32);
    public static readonly Color StampHi = Color.FromArgb(217, 91, 50);
    public static readonly Color StampDeep = Color.FromArgb(160, 48, 40);

    // ── 语义色 ──
    public static readonly Color Live = Color.FromArgb(78, 125, 70);
    public static readonly Color Ochre = Color.FromArgb(184, 134, 45);

    // ── 网格 ──
    public static readonly Color GridRow = Color.FromArgb(251, 249, 244);
    public static readonly Color GridRowAlt = Color.FromArgb(244, 241, 234);
    public static readonly Color GridSel = Color.FromArgb(233, 223, 205);
}

