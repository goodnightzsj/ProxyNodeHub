using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>
/// UI 度量与间距刻度。
///
/// 集中放置的原因：此前多处手写坐标与宽度（head.Width - 92、10000px 假 Label），
/// 在构造期这些值还是 0，控件被推到负坐标或溢出容器。这里统一提供测量函数，
/// 让宽度由内容决定。
/// </summary>
internal static class UiMetrics
{
    // ── 间距刻度（4 的倍数，全项目统一）──
    public const int SpaceXs = 4;
    public const int SpaceSm = 8;
    public const int SpaceMd = 12;
    public const int SpaceLg = 16;
    public const int SpaceXl = 24;

    // ── 控件尺寸 ──
    public const int ButtonHeight = 26;
    public const int RowHeight = 26;

    /// <summary>测量文本在指定字体下的像素宽度。</summary>
    public static int TextWidth(string text, Font font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return (int)Math.Ceiling((double)TextRenderer.MeasureText(g, text, font).Width);
        }
        catch
        {
            // 拿不到 DC 时按字符数估算，宁可偏宽也不要被裁切
            return text.Length * (font?.Height ?? 12) / 2 + 8;
        }
    }

    /// <summary>
    /// 让按钮宽度自适应文字。
    ///
    /// 必须能给按钮自己的字体、也允许收缩：只用 Grow（Math.Max）的话，
    /// 按钮永远只会变宽，文字短了也不会收回去；而拿新建的字体去量，
    /// 和实际渲染用的可能不是同一个实例，量出来对不上。
    /// </summary>
    public static void FitWidth(ModernButton btn, string? text = null, int extra = 0)
    {
        var t = text ?? btn.Text ?? "";
        var f = btn.Font ?? Fonts.Ui9;
        btn.Width = TextWidth(t, f) + 26 + extra;
    }

    /// <summary>Label 安全宽度：按 MaximumSize 换行并长高，避免文字被裁掉半句。</summary>
    public static Label WrapLabel(string text, Font font, Color fore, int maxWidth)
    {
        return new Label
        {
            Text = text,
            Font = font,
            ForeColor = fore,
            AutoSize = false,
            MaximumSize = new Size(maxWidth, 0),
            Width = maxWidth,
            // 高度由文字行数决定；MeasureText 会按 MaximumSize 换行
            Height = MeasureHeight(text, font, maxWidth),
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
    }

    public static int MeasureHeight(string text, Font font, int width)
    {
        if (string.IsNullOrEmpty(text)) return font.Height + 2;
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);

            // TextRenderer 不把 \n 当换行（会画成方框），所以按段量再累加。
            // 每段内部仍可能折行，交给 WordBreak 处理。
            var total = 0;
            foreach (var seg in text.Replace("\r\n", "\n").Split('\n'))
            {
                var sz = TextRenderer.MeasureText(g, seg, font,
                    new Size(Math.Max(20, width), int.MaxValue), TextFormatFlags.WordBreak);
                total += Math.Max(sz.Height, font.Height);
            }
            return total + 2;
        }
        catch
        {
            var lines = Math.Max(1, (int)Math.Ceiling(text.Length / (double)Math.Max(1, width / 2)));
            return lines * (font.Height + 2) + 2;
        }
    }

    /// <summary>
    /// 标题块：主标题 + 副标题，纵向排列。
    ///
    /// 必须用容器而不是在 Panel 里手写 Location —— 手写绝对 Y 时，
    /// 15pt 主标题实际占 20px 高，副标题放在 y=21 会被压掉 1px 且视觉上
    /// 完全糊在一起（三个弹窗都犯过这个错）。
    /// </summary>
    public static Control TitleBlock(string title, string subtitle, Font? titleFont = null)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2,
            BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
        };
        t.Rows(SizeType.AutoSize, SizeType.AutoSize);

        t.Controls.Add(new Label
        {
            Text = title, Font = titleFont ?? Fonts.SerifTitle, ForeColor = Theme.Ink,
            AutoSize = true, Margin = new Padding(0, 0, 0, 2), Padding = new Padding(0),
        }, 0, 0);

        t.Controls.Add(new Label
        {
            Text = subtitle, Font = Fonts.Mono8, ForeColor = Theme.InkMid,
            AutoSize = true, Margin = new Padding(0, 0, 0, 0), Padding = new Padding(0),
        }, 0, 1);

        return t;
    }

    /// <summary>
    /// 会折行且高度自适应的 Label，用于 Dock=Top 的纵向堆叠。
    ///
    /// 单靠 AutoSize=true 不够：Dock=Top 会先把宽度拉伸，而 AutoSize 的
    /// preferred size 是在拉伸之前算的，于是文字折成两行、高度仍停在单行，
    /// 第二行被静默切掉（测速参数页 11 条说明就这么被切）。所以这里在
    /// Layout 里按实际宽度重算高度。
    /// </summary>
    /// <summary>
    /// 会折行且高度自适应的 Label，用于 Dock=Top 的纵向堆叠。
    ///
    /// 单靠 AutoSize=true 不够：Dock=Top 会先把宽度拉伸，而 AutoSize 的
    /// preferred size 是在拉伸之前算的，于是文字折成两行、高度仍停在单行，
    /// 第二行被静默切掉（测速参数页 11 条说明就这么被切）。所以这里在
    /// Layout 里按实际宽度重算高度。
    ///
    /// 宽度策略分两种宿主：
    ///   · Panel/FlowLayoutPanel 内 —— Anchor 左右，宽度由父容器给
    ///   · TableLayoutPanel 内   —— Dock=Fill；Anchor 在单元格里不生效，
    ///                              控件会退回按内容算宽从而超出单元格
    /// </summary>
    public static Label WrapTop(string text, Font font, Color fore, int topMargin = 0,
        bool inGrid = false)
    {
        var lbl = new Label
        {
            Text = text, Font = font, ForeColor = fore,
            AutoSize = false,
            Dock = inGrid ? DockStyle.Fill : DockStyle.None,
            Anchor = inGrid ? AnchorStyles.None
                            : AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, topMargin, 0, 0), Padding = new Padding(0),
            UseMnemonic = false,
        };

        void Fix(object? s, LayoutEventArgs e)
        {
            if (lbl.Parent is not Control p) return;

            // inGrid：Dock=Fill 会把宽度撑到单元格宽，此刻 lbl.Width 即真实宽度。
            // Fill 期间 ClientSize 可能还是旧值（例如单元格刚重建时为 0/100），
            // 所以这里读 lbl.Width 而不是 p.ClientSize.Width。
            var w = lbl.Width > 0 ? lbl.Width : p.ClientSize.Width;
            w = Math.Max(80, w - lbl.Margin.Horizontal);
            var need = MeasureHeight(text, font, w);
            if (lbl.Height != need) lbl.Height = need;
        }

        lbl.ParentChanged += (s, e) =>
        {
            Fix(s, new LayoutEventArgs(lbl, "Width"));
            if (lbl.Parent != null) lbl.Parent.Layout += Fix;
        };
        return lbl;
    }

    /// <summary>
    /// 把一组可变高度的行铺进可滚动容器。
    ///
    /// 为什么不用 AutoSize / Dock 自动布局：
    /// 「AutoSize 面板 + Percent 列」会形成循环 sizing —— 面板宽度要靠内容算，
    /// 内容宽度又靠面板给，结果所有子项收缩到内容最小宽度。表现为文字
    /// 堆成小块、只占左边一小条、右边大片空白（测速参数页与内核页都犯过这个错）。
    ///
    /// 这里改为完全显式：每行给定容器宽度，由行自己安排子控件并回报高度。
    /// 行若有自己的摆法，把 <c>row.Tag = (Action&lt;int&gt;)w =&gt; ...</c> 挂上去即可；
    /// 没有就退回读首选尺寸。
    /// </summary>
    public static void PlaceRows(Panel scroller, IEnumerable<Control> rows,
        int padX, int padTop, int gap)
    {
        var list = rows.ToList();
        if (list.Count == 0) return;

        // 每行：左右拉伸 + 显式宽度
        foreach (var r in list)
        {
            r.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            r.Dock = DockStyle.None;
        }

        int HeightOf(Control r, int width)
        {
            if (r.Tag is Action<int> lay)
            {
                lay(width);
                return r.Height > 0 ? r.Height : r.PreferredSize.Height;
            }
            r.PerformLayout();
            var h = r.PreferredSize.Height;
            return h > 0 ? h : r.Height;
        }

        void Layout()
        {
            var w = Math.Max(200, scroller.ClientSize.Width - padX * 2);
            int y = padTop;
            foreach (var r in list)
            {
                // 左右都要让开：只按 padX 算宽度、不挪 Left 的话，行会从左
                // 边缘 x=0 开始画，左边贴死、右边空出一截 —— 两鬓不对称。
                r.Left = padX;
                r.Width = w;
                var h = HeightOf(r, w);
                r.Height = h;
                r.Top = y;
                y += h + gap;
            }
        }

        scroller.Resize += (s, e) => Layout();
        scroller.Layout += (s, e) => Layout();
        Layout();
    }


    /// <summary>
    /// 让 Label 的宽度跟随给定基准宽度，并据实重算高度。
    /// 用于固定尺寸对话框（FixedDialog）里不能靠 Resize 传播的场合。
    /// </summary>
    public static void FitLabelWidth(Label lbl, int width)
    {
        lbl.Width = width;
        lbl.Height = MeasureHeight(lbl.Text, lbl.Font, width);
    }

    /// <summary>水平分隔线，负 Margin 让它顶到容器左右边缘。</summary>
    public static Control Rule(int sideInset)
        => new Panel
        {
            Dock = DockStyle.Top, Height = 1, BackColor = Theme.Hairline,
            Margin = new Padding(-sideInset, 7, -sideInset, 7),
        };

    /// <summary>
    /// 让面板在拿到真实宽度时，按 <see cref="Panel.Tag"/> 上的摆法（Action&lt;int&gt;）重排一次。
    ///
    /// 为什么不能挂在父容器的 Layout 上：父容器的 Layout 可能在给子控件分配
    /// 宽度之前就触发，那时子控件 Width 还是 0，摆法被跳过，之后再也不跑 ——
    /// 表现为所有 Label 停在默认 100px 宽、文字折成好几行。
    /// SizeChanged 才是「宽度已经落定」的信号。
    ///
    /// 也不在构造时同步跑一次：那时父容器往往还没布局，面板停在 Panel 默认的
    /// 200px。按 200 排出来的总高是虚高的，靠它收窗体高度（KernelDialog.FitForm）
    /// 就会把窗体撑过头，之后宽度对了也缩不回来。只等真正的 SizeChanged。
    /// </summary>
    public static void AutoLayout(this Panel box)
    {
        void Go(object? s, EventArgs e)
        {
            if (box.Width <= 0 || box.Tag is not Action<int> lay) return;
            lay(box.Width);
        }

        box.SizeChanged += Go;
        box.Layout += Go;
    }

    /// <summary>
    /// 给 Panel 去掉 AutoScroll 的横向滚动条 —— 只保留纵向。
    /// </summary>
    public static void VerticalScrollOnly(Panel p)
    {
        p.AutoScroll = true;
        p.HorizontalScroll.Enabled = false;
        p.HorizontalScroll.Visible = false;
        // WinForms 没有直接的"仅纵向"属性，靠子控件不超宽来保证
    }
}
