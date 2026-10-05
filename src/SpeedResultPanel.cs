using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>
/// 详情面板里的「测速」页：上半是检测结果，下半是内核实时日志。
///
/// 320px 的窄栏放不下 10 列宽表，所以节点用纵向卡片列表 ——
/// 一个节点三行：名称+速度、协议地址、解锁标签。
/// </summary>
public sealed class SpeedResultPanel : UserControl
{
    /// <summary>结果区标题行高度。</summary>
    private const int HeadH = 34;
    /// <summary>一张节点卡片高度（三行 + 间距）。</summary>
    private const int CardH = 57;
    /// <summary>结果区最高占多少，再高就内部滚动，别把日志挤没了。</summary>
    private const int MaxTopH = 300;

    private Panel _listHost = null!;
    private Panel _top = null!;
    private RichTextBox _txtLog = null!;
    private Label _lblSummary = null!;
    private CheckBox _chkOnlyPassed = null!;

    private List<SpeedTestNode> _nodes = new();

    public SpeedResultPanel()
    {
        BackColor = Theme.PaperHi;
        Font = Fonts.Ui9;
        DoubleBuffered = true;
        Build();
    }

    private void Build()
    {
        // ── 上半：结果 ──
        // 高度不再写死 300：没有结果时也得占 300px，勾选框下面就空一大块，
        // 日志被挤到最底下。改成跟着内容走，见 Render() 末尾的 AdjustTop()。
        var top = new Panel { Dock = DockStyle.Top, Height = HeadH, BackColor = Theme.PaperHi };
        _top = top;

        var head = new Panel { Dock = DockStyle.Top, Height = HeadH, BackColor = Theme.PaperHi,
            Padding = new Padding(10, 5, 8, 0) };

        // 右侧勾选框：用 RightToLeft 的 FlowLayout 靠右，不手算坐标 ——
        // 手算 head.Width 在构造期还是 0，会把控件推到负坐标。
        var headFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0),
        };
        _chkOnlyPassed = new CheckBox
        {
            Text = "仅看通过", Font = Fonts.Mono8, ForeColor = Theme.InkMid,
            AutoSize = true, FlatStyle = FlatStyle.Flat, Checked = false,
            Margin = new Padding(0, 3, 0, 0), Padding = new Padding(0),
        };
        _chkOnlyPassed.CheckedChanged += (s, e) => Render();
        headFlow.Controls.Add(_chkOnlyPassed);
        head.Controls.Add(headFlow);

        head.Controls.Add(new Label
        {
            Text = "检测结果", Font = Fonts.Ui9Bold, ForeColor = Theme.Ink,
            AutoSize = true, Location = new Point(10, 7)
        });
        _lblSummary = new Label
        {
            Text = "", Font = Fonts.Mono8, ForeColor = Theme.InkMid,
            AutoSize = true, Location = new Point(70, 9)
        };
        head.Controls.Add(_lblSummary);
        top.Controls.Add(head);

        _listHost = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.PaperHi,
            Padding = new Padding(0, 2, 0, 0),
        };
        top.Controls.Add(_listHost);
        Controls.Add(top);

        // ── 下半：日志 ──
        var bottom = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PaperHi };
        var logHead = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Theme.PaperHi,
            Padding = new Padding(10, 5, 8, 0) };

        var logFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0),
        };
        var clear = new LinkLabel
        {
            Text = "清空", Font = Fonts.Mono8, ForeColor = Theme.InkMid, AutoSize = true,
            LinkBehavior = LinkBehavior.NeverUnderline, TabStop = false,
            BackColor = Color.Transparent, Margin = new Padding(0, 3, 0, 0),
        };
        clear.LinkClicked += (s, e) => _txtLog.Clear();
        logFlow.Controls.Add(clear);
        logHead.Controls.Add(logFlow);

        logHead.Controls.Add(new Label
        {
            Text = "内核实时日志", Font = Fonts.Ui9Bold, ForeColor = Theme.Ink,
            AutoSize = true, Location = new Point(10, 7)
        });

        _txtLog = new RichTextBox
        {
            Dock = DockStyle.Fill, BackColor = Theme.PaperHi, ForeColor = Theme.InkMid,
            Font = Fonts.Mono85, BorderStyle = BorderStyle.None, ReadOnly = true,
            // WordWrap=false 时每行都超出窄栏宽度，只有纵向滚动条的话右边
            // 半句永远看不到，也滚不动。两个方向都给。
            WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both,
            DetectUrls = false, HideSelection = false,
        };
        bottom.Controls.Add(_txtLog);
        bottom.Controls.Add(logHead);
        Controls.Add(bottom);
    }

    // ══════════════════ 结果 ══════════════════

    public void SetNodes(List<SpeedTestNode> nodes)
    {
        _nodes = nodes ?? new List<SpeedTestNode>();
        Render();
    }

    private void Render()
    {
        if (_listHost == null || _listHost.IsDisposed) return;

        _listHost.SuspendLayout();
        try
        {
            _listHost.Controls.Clear();

            var show = _chkOnlyPassed != null && _chkOnlyPassed.Checked
                ? _nodes.Where(n => n.Passed)
                : _nodes.AsEnumerable();

            var ordered = show
                .OrderByDescending(n => n.Passed)
                .ThenByDescending(n => n.Speed)
                .ToList();

            _lblSummary.Text = _nodes.Count == 0 ? "" :
                $"{_nodes.Where(n => n.Passed).Count()}/{_nodes.Count} 通过";

            if (ordered.Count == 0)
            {
                _listHost.Controls.Add(new Label
                {
                    Text = _nodes.Count == 0 ? "尚未测速" : "没有符合过滤条件的节点",
                    Font = Fonts.Mono8, ForeColor = Theme.InkLow,
                    AutoSize = true, Location = new Point(10, 10),
                });
                AdjustTop(0);
                return;
            }

            int y = 0;
            foreach (var n in ordered)
            {
                var card = new Panel
                {
                    Location = new Point(0, y), Height = CardH, BackColor = Theme.Paper,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                };
                var inner = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3,
                    BackColor = Color.Transparent, Padding = new Padding(8, 4, 8, 4),
                };
                inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                inner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                var dotColor = n.Passed ? Theme.Live : n.Speed == 0 ? Theme.Stamp : Theme.Ochre;
                var dot = new Label { Text = "●", Font = Fonts.Mono9, ForeColor = dotColor,
                    AutoSize = true, Dock = DockStyle.Left, Margin = new Padding(0, 3, 3, 0) };

                var name = new Label
                {
                    Text = string.IsNullOrEmpty(n.BaseName) ? n.Name : n.BaseName,
                    Font = Fonts.Ui9, ForeColor = n.Passed ? Theme.Ink : Theme.InkLow,
                    AutoSize = true, Dock = DockStyle.Left,
                };
                var nameRow = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                    AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0),
                };
                nameRow.Controls.Add(dot);
                nameRow.Controls.Add(name);
                inner.Controls.Add(nameRow, 0, 0);

                var speed = new Label
                {
                    Text = n.Passed ? (n.Speed >= 1024 ? $"{n.Speed / 1024.0:F1} MB/s" : $"{n.Speed} KB/s")
                          : n.Speed == 0 ? "✕ 测活失败" : "○ 未达标",
                    Font = Fonts.Mono9, ForeColor = dotColor,
                    AutoSize = true, Dock = DockStyle.Right, Margin = new Padding(0, 2, 0, 0),
                };
                inner.Controls.Add(speed, 1, 0);

                var meta = new Label
                {
                    Text = $"{n.Type} · {n.Server}:{n.Port}" +
                           (n.Tls ? " · tls" : "") + (n.Udp ? " · udp" : "") +
                           (string.IsNullOrEmpty(n.Country) ? "" : $" · {n.Country}") +
                           (string.IsNullOrEmpty(n.IpRisk) ? "" : $" · {n.IpRisk}"),
                    Font = Fonts.Mono8, ForeColor = Theme.InkMid,
                    AutoSize = false, Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 0),
                };
                inner.Controls.Add(meta, 0, 1);
                inner.SetColumnSpan(meta, 2);

                var tags = "";
                if (n.Media.Count > 0)
                {
                    tags = string.Join(" ", n.Media.Select(m =>
                        m.Tag.Length > 0 ? SpeedTestPanel.ShortPlatform(m.Platform) + "✓"
                                         : SpeedTestPanel.ShortPlatform(m.Platform) + "—"));
                }
                var tagLab = new Label
                {
                    Text = tags.Length == 0 ? "—" : tags,
                    Font = Fonts.Mono8, ForeColor = Theme.InkMid,
                    AutoSize = false, Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 0),
                };
                inner.Controls.Add(tagLab, 0, 2);
                inner.SetColumnSpan(tagLab, 2);

                card.Controls.Add(inner);
                _listHost.Controls.Add(card);
                y += CardH + 1;
            }

            AdjustTop(ordered.Count);
        }
        finally { _listHost.ResumeLayout(); }
    }

    /// <summary>
    /// 结果区高度跟着内容走：一条结果都没有时只剩标题行，勾选框下面不会
    /// 空一大块；结果多了封顶在 MaxTopH，超出部分由内部滚动接管，
    /// 日志区始终有位置。
    /// </summary>
    private void AdjustTop(int shown)
    {
        if (_top == null || _top.IsDisposed) return;
        var want = shown == 0
            ? HeadH
            : Math.Min(MaxTopH, HeadH + 4 + shown * (CardH + 1));
        if (_top.Height != want) _top.Height = want;
    }

    // ══════════════════ 日志 ══════════════════

    public void AppendLog(string line, Color color)
    {
        if (_txtLog == null || _txtLog.IsDisposed) return;
        try
        {
            if (line == "\u0001CLEAR") { _txtLog.Clear(); return; }

            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.SelectionColor = Theme.InkLow;
            _txtLog.AppendText($"{DateTime.Now:HH:mm:ss.fff}  ");
            _txtLog.SelectionColor = color;
            _txtLog.AppendText(line + "\n");
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.ScrollToCaret();
        }
        catch { }
    }

    /// <summary>控件挂载时回填此前累积的日志。</summary>
    public void Backfill(string tail)
    {
        if (string.IsNullOrEmpty(tail)) return;
        try
        {
            _txtLog.Clear();
            foreach (var l in tail.Split('\n'))
            {
                if (l.Length == 0) continue;
                _txtLog.SelectionStart = _txtLog.TextLength;
                _txtLog.SelectionColor = l.Contains("WARN") ? Theme.Ochre
                    : l.Contains("ERROR") ? Theme.Stamp : Theme.InkMid;
                _txtLog.AppendText(l + "\n");
            }
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.ScrollToCaret();
        }
        catch { }
    }
}
