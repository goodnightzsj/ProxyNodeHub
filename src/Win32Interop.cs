using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>
/// Win32 API 互操作 - 深色标题栏、圆角窗口
/// </summary>
public static class Win32Interop
{
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);

    /// <summary>启用深色标题栏</summary>
    public static void EnableDarkTitle(Form form, bool dark = true)
    {
        try { int v = dark ? 1 : 0; DwmSetWindowAttribute(form.Handle, 20, ref v, sizeof(int)); }
        catch { }
    }

    /// <summary>启用窗口圆角</summary>
    public static void EnableRoundedCorners(Form form)
    {
        try { int round = 2; DwmSetWindowAttribute(form.Handle, 33, ref round, sizeof(int)); }
        catch { }
    }
}