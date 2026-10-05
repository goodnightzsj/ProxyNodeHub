using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProxyNodeHub;

public sealed class SpeedSourceDialog : Form
{
    private bool _shown;

    private CheckBox _chkFav = null!, _chkResult = null!, _chkManual = null!;
    private TextBox _txtManual = null!;
    private CheckBox _chkHistory = null!;
    private NumericUpDown _numKeep = null!;

    /// <summary>选中的来源。可多选，调用方自行聚合成 [Flags]。</summary>
    public List<SpeedTestPanel.SpeedSource> Selected { get; private set; } = new();

    /// <summary>手动粘贴的订阅，每行一条。</summary>
    public string ManualUrls { get; private set; } = "";

    public bool WithHistory { get; private set; } = true;
    public int KeepRounds { get; private set; } = 3;

    public SpeedSourceDialog(
        SpeedTestPanel.SpeedSource current,
        string manual,
        bool withHistory,
        int keepRounds,
        Func<int> countFavorites,
        Func<int> countResults)
    {
        Text = "订阅来源";
        Size = new Size(470, 344);
        MinimumSize = new Size(440, 300);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Paper;
        ForeColor = Theme.Ink;
        Font = Fonts.Ui9;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(20, 16, 20, 12), BackColor = Theme.Paper,
        }
        .Rows(SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize);

        root.Controls.Add(Brand(), 0, 0);
        var src = BuildSources(countFavorites, countResults);
        src.AutoLayout();
        root.Controls.Add(src, 0, 1);
        var his = BuildHistory();
        his.AutoLayout();
        root.Controls.Add(his, 0, 2);
        root.Controls.Add(BuildButtons(), 0, 3);
        Controls.Add(root);

        // 窗体高度收到贴合内容。行高全是 AutoSize 时，剩余空间既不会被吸收
        // 也不会把窗体撑开，反过来内容比窗体高就会被压进底部内边距 ——
        // 这个弹窗实测溢出了 10px，确定/取消被切掉一截。
        // 显示前允许反复收敛（宽度定下来之前会被布局好几轮），显示后定型。
        Shown += (s, e) =>
        {
            _shown = true;
            MinimumSize = new Size(MinimumSize.Width, Height);
        };
        root.Layout += (s, e) => FitForm(root);

        _chkFav.Checked = current.HasFlag(SpeedTestPanel.SpeedSource.Favorites);
        _chkResult.Checked = current.HasFlag(SpeedTestPanel.SpeedSource.Results);
        _chkManual.Checked = current.HasFlag(SpeedTestPanel.SpeedSource.Manual);
        if (current == SpeedTestPanel.SpeedSource.None) _chkFav.Checked = true;

        _txtManual.Text = manual;
        _chkHistory.Checked = withHistory;
        _numKeep.Value = Math.Max(_numKeep.Minimum, Math.Min(_numKeep.Maximum, keepRounds));
        _chkHistory.CheckedChanged += (s, e) => { _numKeep.Enabled = _chkHistory.Checked; };
        SyncManual();

        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    /// <summary>
    /// 把窗体高度收到贴合内容。行高全是 AutoSize 时，剩余空间既不会被吸收
    /// 也不会把窗体撑开，反过来内容比窗体高就会被压进底部内边距 ——
    /// 这个弹窗实测溢出 10px，确定/取消被切掉一截。
    ///
    /// 直接量内容真实范围，不去累加 RowStyles：布局过程中行高可能还是旧值。
    /// 显示前允许反复收敛（宽度定下来之前会被布局好几轮），显示后定型，
    /// 免得切换勾选时提示文字长度变化连带动窗体忽大忽小。
    /// </summary>
    private void FitForm(Control root)
    {
        if (_shown) return;

        int bottom = 0;
        void Scan(Control c, int offset)
        {
            foreach (Control ch in c.Controls)
            {
                if (!ch.Visible || ch.Height <= 0) continue;
                if (ch is ScrollableControl sc && sc.AutoScroll) continue;
                var b = offset + ch.Top + ch.Height;
                if (b > bottom) bottom = b;
                Scan(ch, offset + ch.Top);
            }
        }
        Scan(root, root.Top);

        var need = bottom + root.Padding.Bottom + (Height - ClientSize.Height);
        if (need > 0 && Height != need)
            Height = Math.Max(MinimumSize.Height, need);
    }

    // ══════════════════ 标题 ══════════════════

    private Control Brand() => UiMetrics.TitleBlock("订阅来源",
        "测速对象来自哪里 · 可多选 · 决定结果能映射回哪些仓库");

    // ══════════════════ 来源选择 ══════════════════

    private Panel BuildSources(Func<int> countFavorites, Func<int> countResults)
    {
        // 一行三个勾选框：无边框、无 emoji，可单选也可多选。
        // 显式摆放，不碰 AutoSize —— AutoSize 容器只按内容算宽，
        // 勾选框、说明文字、输入框都会被压成左边一小条，右边空一大片。
        var box = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Color.Transparent,
            Margin = new Padding(0), Padding = new Padding(0),
        };

        _chkFav = new CheckBox
        {
            Text = $"收藏仓库 · {countFavorites()}", Font = Fonts.Ui9, ForeColor = Theme.Ink,
            AutoSize = false, FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0), Padding = new Padding(0),
        };
        _chkResult = new CheckBox
        {
            Text = $"搜索结果 · {countResults()}", Font = Fonts.Ui9, ForeColor = Theme.Ink,
            AutoSize = false, FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0), Padding = new Padding(0),
        };
        _chkManual = new CheckBox
        {
            Text = "手动粘贴", Font = Fonts.Ui9, ForeColor = Theme.Ink,
            AutoSize = false, FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0), Padding = new Padding(0),
        };

        foreach (var c in new[] { _chkFav, _chkResult, _chkManual })
        {
            c.CheckedChanged += (s, e) => { SyncManual(); };
        }

        var hint = "勾选「手动粘贴」后，在下面填写订阅链接，每行一条：";
        var lblHint = new Label
        {
            Text = hint, Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };

        _txtManual = new TextBox
        {
            Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
            Font = Fonts.Mono9, BackColor = Theme.PaperHi, ForeColor = Theme.Ink,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.None, Height = 96,
        };

        box.Controls.Add(_chkFav);
        box.Controls.Add(_chkResult);
        box.Controls.Add(_chkManual);
        box.Controls.Add(lblHint);
        box.Controls.Add(_txtManual);
        box.Tag = (Action<int>)(w =>
        {
            int x = 0;
            foreach (var c in new[] { _chkFav, _chkResult, _chkManual })
            {
                c.Location = new Point(x, 2);
                c.Width = UiMetrics.TextWidth(c.Text, Fonts.Ui9) + 22;
                c.Height = 18;
                x = c.Right + 20;
            }

            var yy = _chkFav.Bottom + 8;
            lblHint.Location = new Point(0, yy);
            lblHint.Width = w;
            lblHint.Height = UiMetrics.MeasureHeight(hint, Fonts.Mono8, w);

            _txtManual.Location = new Point(0, lblHint.Bottom + 5);
            _txtManual.Width = w;
            _txtManual.Height = 96;

            box.Height = _txtManual.Bottom + 2;
        });

        SyncManual();
        return box;
    }

    // ══════════════════ 历史节点 ══════════════════

    private Panel BuildHistory()
    {
        // 两行结构：第一行 勾选+步进+「回」，第二行 说明通栏。
        // 同样显式摆放 —— AutoSize 的表格会把说明压成一小条。
        // 不加外框：历史节点是次要信息，边框会和来源区抢注意力。
        var box = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0), Padding = new Padding(0),
        };

        _chkHistory = new CheckBox
        {
            Text = "并入历史节点", Font = Fonts.Ui9, Checked = true,
            AutoSize = false, FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0), Padding = new Padding(0),
        };
        _numKeep = new NumericUpDown
        {
            Width = 48, Font = Fonts.Mono9, Minimum = 1, Maximum = 10, Value = 3,
            TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.PaperHi, Margin = new Padding(0), Padding = new Padding(0),
        };
        var lblHui = new Label
        {
            Text = "回", Font = Fonts.Ui9, ForeColor = Theme.Ink,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };
        var hint = "把最近几回测速通过的节点重新并入本轮队列，未通过的自动淘汰";
        var lblHint = new Label
        {
            Text = hint, Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };

        box.Controls.Add(_chkHistory);
        box.Controls.Add(_numKeep);
        box.Controls.Add(lblHui);
        box.Controls.Add(lblHint);
        box.Tag = (Action<int>)(w =>
        {
            _chkHistory.Location = new Point(0, 2);
            _chkHistory.Width = UiMetrics.TextWidth(_chkHistory.Text, Fonts.Ui9) + 22;
            _chkHistory.Height = 18;

            _numKeep.Location = new Point(_chkHistory.Right + 8, 1);
            lblHui.Location = new Point(_numKeep.Right + 6, 4);
            lblHui.Size = new Size(14, 16);

            lblHint.Location = new Point(0, _chkHistory.Bottom + 7);
            lblHint.Width = w;
            lblHint.Height = UiMetrics.MeasureHeight(hint, Fonts.Mono8, w);

            box.Height = lblHint.Bottom + 4;
        });

        return box;
    }

    // ══════════════════ 按钮 ══════════════════

    private Control BuildButtons()
    {
        // 只放按钮。原来右侧那行「收藏仓库 · 并入历史 3 回」预览去掉了：
        // 选择结果在上层页面已经能看到，这里重复一遍只是噪音。
        var f = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Height = 38, BackColor = Color.Transparent,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 0, 0),
        };

        var cancel = new ModernButton { Text = "取消", Ghost = true, Size = new Size(72, 26), Margin = new Padding(0, 0, 8, 0) };
        cancel.Click += (s, e) => Close();
        var ok = new ModernButton { Text = "确定", Size = new Size(72, 26), Margin = new Padding(0) };
        ok.Click += (s, e) =>
        {
            var picked = new List<SpeedTestPanel.SpeedSource>();
            if (_chkFav.Checked) picked.Add(SpeedTestPanel.SpeedSource.Favorites);
            if (_chkResult.Checked) picked.Add(SpeedTestPanel.SpeedSource.Results);
            if (_chkManual.Checked) picked.Add(SpeedTestPanel.SpeedSource.Manual);
            Selected = picked;
            ManualUrls = _txtManual.Text ?? "";
            WithHistory = _chkHistory.Checked;
            KeepRounds = (int)_numKeep.Value;
            DialogResult = DialogResult.OK;
            Close();
        };
        f.Controls.Add(cancel);
        f.Controls.Add(ok);
        return f;
    }

    private void SyncManual()
    {
        _txtManual.Enabled = _chkManual.Checked;
        _txtManual.BackColor = _chkManual.Checked ? Theme.PaperHi : Theme.PaperDeep;
    }
}
