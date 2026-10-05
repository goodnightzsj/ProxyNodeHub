using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>
/// 参数设置弹窗。两个页面可切换：
///   · 表单 —— 常用控件的快捷方式，改动写回 config.yaml
///   · YAML —— config.yaml 原文编辑，内核任何配置项都在这里
///
/// 之所以必须留 YAML 页：内核升级会新增配置项，我们不做控件也不会挡住它。
/// 表单页只覆盖常用键，其余全在 YAML 页。
/// </summary>
public sealed class SpeedTestSettingsDialog : Form
{
    private SpeedTestRunner _runner;
    private int _tab;                     // 0 = 表单, 1 = YAML
    private bool _dirtyYaml;

    private Panel _formPage = null!, _yamlPage = null!;
    private TextBox _yamlBox = null!;
    private ModernButton _btnForm = null!, _btnYaml = null!;

    public SpeedTestSettingsDialog(SpeedTestRunner runner)
    {
        _runner = runner;
        Text = "测速参数";
        Size = new Size(1040, 780);
        MinimumSize = new Size(880, 560);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Paper;
        ForeColor = Theme.Ink;
        Font = Fonts.Ui9;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        KeyPreview = true;

        var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Paper };
        BuildTabs(root);
        BuildFormPage(root);
        BuildYamlPage(root);
        Controls.Add(root);

        // 底部按钮
        // 底部条：左（提示）/ 右（按钮）两个面板分开停靠。
        // 绝不用 10000px 宽假 Label 当弹性间隔 —— FlowLayoutPanel 会被它顶到换行，
        // 第二行的三个按钮落到容器高度之外，整个消失。
        var foot = new Panel
        {
            Dock = DockStyle.Bottom, Height = 42, BackColor = Theme.Paper,
            Padding = new Padding(14, 7, 14, 0),
        };

        var footLeft = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, AutoSize = true, BackColor = Color.Transparent,
            WrapContents = false, Padding = new Padding(0),
        };
        footLeft.Controls.Add(new Label
        {
            Text = "改动写回 config.yaml · 请勿删除未知键", Font = Fonts.Mono8,
            ForeColor = Theme.InkLow, AutoSize = true, Margin = new Padding(0, 8, 0, 0),
        });
        foot.Controls.Add(footLeft);

        var footRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, BackColor = Color.Transparent,
            WrapContents = false, Padding = new Padding(0), FlowDirection = FlowDirection.RightToLeft,
        };

        var btnOpen = new ModernButton { Text = "打开配置目录", Ghost = true, Height = 26, Margin = new Padding(0, 0, 6, 0) };
        UiMetrics.FitWidth(btnOpen);
        btnOpen.Click += (s, e) => Common.OpenDir(SpeedTestConfig.ConfigDir);
        var btnCancel = new ModernButton { Text = "关闭", Ghost = true, Size = new Size(72, 26), Margin = new Padding(0, 0, 6, 0) };
        btnCancel.Click += (s, e) => Close();
        var btnOk = new ModernButton { Text = "保存", Size = new Size(72, 26), Margin = new Padding(0) };
        btnOk.Click += (s, e) => Save();
        // RightToLeft：添加顺序即视觉从右往左
        footRight.Controls.Add(btnOk);
        footRight.Controls.Add(btnCancel);
        footRight.Controls.Add(btnOpen);
        foot.Controls.Add(footRight);

        Controls.Add(foot);

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.Control && e.KeyCode == Keys.S) Save();
        };

        ApplyTab();
    }

    // ══════════════════ 页签 ══════════════════

    private void BuildTabs(Panel host)
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Theme.PaperHi };
        bar.Paint += (s, e) =>
        {
            using var p = new Pen(Theme.Hairline);
            e.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Height = 38, BackColor = Color.Transparent,
            Padding = new Padding(12, 6, 0, 0), WrapContents = false,
        };

        _btnForm = new ModernButton { Text = "表单", Ghost = true, BackColor = Theme.PaperHi,
            Size = new Size(88, 26), CornerRadius = 0 };
        _btnForm.Click += (s, e) => ApplyTab(0);

        _btnYaml = new ModernButton { Text = "YAML", Ghost = true, BackColor = Theme.PaperHi,
            Size = new Size(88, 26), CornerRadius = 0 };
        _btnYaml.Click += (s, e) => ApplyTab(1);

        flow.Controls.Add(_btnForm);
        flow.Controls.Add(_btnYaml);
        bar.Controls.Add(flow);
        host.Controls.Add(bar);
    }

    private void ApplyTab(int which)
    {
        _tab = which;

        // 表单 → YAML：如果表单改过，先把磁盘内容刷进 YAML 页
        if (which == 1) LoadYamlFromDisk();

        _formPage.Visible = which == 0;
        _yamlPage.Visible = which == 1;

        Common.StyleTab(_btnForm, which == 0);
        Common.StyleTab(_btnYaml, which == 1);
    }

    private void ApplyTab() => ApplyTab(_tab);

    private void LoadYamlFromDisk()
    {
        try
        {
            _yamlBox.Text = SpeedTestConfig.ReadConfig();
            _dirtyYaml = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "读取 config.yaml 失败：\n" + ex.Message, "测速参数",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ══════════════════ 表单页 ══════════════════

    private void BuildFormPage(Panel host)
    {
        _formPage = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Theme.Paper, AutoScroll = true,
            Padding = new Padding(0),
        };

        // 单一板块：所有参数按分组堆在一列里，每个参数三行
        //（标签+控件 / 绑定键 / 一句解释）。不再分四块 —— 四块在窄一点
        // 的窗口里就会挤压重叠，而且同一组的参数被拆到不同块里反而难找。
        // 纵向堆叠必须用 TableLayoutPanel 单列：Panel 配多个 Dock=Top 子项时
        // 是按"后进先出"逆序堆叠的，整个参数表会被颠倒。
        // 先把所有条目收集成列表，一次性设定 RowCount 后按索引放入 ——
        // 不能在布局过程中动态增行（AutoSize + 指定单元格会抛 ArgumentException）。
        var items = new List<Control>();

        Group(items, "并发与节奏");
        Param(items, "并发线程数", "concurrent", "同时测活多少个节点。越高越快，但带宽竞争会让测速结果偏低。",
            Num(CfgInt("concurrent", 20), 1, 200));
        Param(items, "测速并发", "speed-concurrent", "专门用于下载测速的并发。建议明显小于上面的并发数，否则互相抢带宽。",
            Num(CfgInt("speed-concurrent", 8), 1, 100));
        Param(items, "检查间隔（分钟）", "check-interval", "内核每隔多久自动重测一轮。填 1 表示每次都测。",
            Num(CfgInt("check-interval", 120), 1, 1440));

        Group(items, "阈值与流量");
        Param(items, "超时（毫秒）", "timeout", "单个节点测活的等待上限。网络差就调大，否则会误判为不可用。",
            Num(CfgInt("timeout", 5000), 500, 30000));
        Param(items, "测速下限（KB/s）", "min-speed", "低于这个速度的节点被丢弃。想多留些节点就调低。",
            Num(CfgInt("min-speed", 512), 0, 100000));
        Param(items, "测速时间（秒）", "download-timeout", "每个节点最多下载多久。调小可省时间但速度估得更粗。",
            Num(CfgInt("download-timeout", 8), 1, 60));
        Param(items, "单节点流量（MB）", "download-mb", "每个节点最多下载多少流量。这是控制总消耗的主要旋钮。",
            Num(CfgInt("download-mb", 3), 0, 500));
        Param(items, "总速度限制（MB/s）", "total-speed-limit", "整个测速过程的下载上限。0 为不限；担心占满宽带就设个值。",
            Num(CfgInt("total-speed-limit", 0), 0, 1000));

        Group(items, "测试地址");
        Param(items, "测活地址", "alive-test-url", "用来判断节点是否可通的地址。想过滤掉 CF 节点可换成 https://www.cloudflare.com",
            Txt(CfgStr("alive-test-url", "http://gstatic.com/generate_204")));
        Param(items, "测速地址", "speed-test-url", "下载测速用的文件地址。留空用内核默认值。",
            Txt(CfgStr("speed-test-url", "")));

        Group(items, "节点筛选");
        Param(items, "只测协议（逗号分隔）", "node-type", "只测这些协议的节点，其余跳过。留空表示全部。", Txt(
            CfgList("node-type", "ss,vmess,vless,trojan,hysteria2")));
        Param(items, "节点前缀", "node-prefix", "给所有节点名加统一前缀，便于在客户端里认出是 ProxyNodeHub 产出的。",
            Txt(CfgStr("node-prefix", "")));
        Param(items, "启用流媒体检测", "media-check", "检测 Netflix / Disney / OpenAI 等解锁情况。耗时明显变长。",
            Chk(CfgBool("media-check", false)));
        Param(items, "检测平台", "platforms", "流媒体检测要查哪些平台，逗号分隔。", Txt(
            CfgList("platforms", "iprisk,tiktok,youtube,netflix,disney,openai,gemini,claude,spotify")));
        Param(items, "节点地址查询", "rename-node", "按节点 IP 查询归属地并写进节点名。关掉可稍快。",
            Chk(CfgBool("rename-node", true)));

        Group(items, "历史节点");
        Param(items, "历史节点过期（天）", "keep-days", "内核按「天」保留可用节点的天数。按「回数」保留由 ProxyNodeHub 自己实现，回数在测速页的订阅来源弹窗里调。",
            Num(CfgInt("keep-days", 7), 0, 365));

        Group(items, "结果交付");
        Param(items, "保存方法", "save-method", "测速结果存到哪里。local 只存本地；gist/r2/webdav/s3 的凭据在 YAML 页填。",
            Cmb(CfgStr("save-method", "local"), "local", "gist", "r2", "webdav", "s3", "worker"));
        Param(items, "输出目录", "output-dir", "结果文件存放目录，相对内核工作目录。",
            Txt(CfgStr("output-dir", "output/")));
        Param(items, "GitHub Proxy", "github-proxy", "内核拉订阅时走的加速镜像。国内建议填一个。",
            Cmb(CfgStr("github-proxy", ""), "", "https://ghfast.top/", "https://gh-proxy.com/", "https://ghproxy.net/"));
        Param(items, "可用率阈值", "success-rate", "低于这个可用率就把订阅链接打印出来，用于排查质量差的订阅。0 为不提示。",
            Num(CfgInt("success-rate", 0), 0, 100));

        Group(items, "订阅拉取");
        Param(items, "重试次数", "sub-urls-retry", "拉订阅失败后的重试次数。GitHub raw 偶发 5xx，重试能提高成功率。",
            Num(CfgInt("sub-urls-retry", 3), 0, 20));
        Param(items, "拉取并发", "sub-urls-concurrent", "同时拉多少个订阅链接。",
            Num(CfgInt("sub-urls-concurrent", 20), 1, 100));

        Group(items, "其他");
        Param(items, "Sub-Store 端口", "sub-store-port", "订阅格式转换服务端口。不需要转换可置空。",
            Txt(CfgStr("sub-store-port", ":8299")));
        Param(items, "允许 IPv6", "ipv6", "是否允许节点走 IPv6。",
            Chk(CfgBool("ipv6", true)));

        // 行直接放进可滚动的 _formPage，由 PlaceRows 统一给宽/定位。
        // 不用嵌套的 AutoSize TableLayoutPanel —— 那会循环 sizing，
        // 把所有子项压到内容最小宽，文字堆成小块不占满面板。
        _formPage.Controls.AddRange(items.ToArray());
        // 留白是排版的一部分，不是剩下的地方：
        // 左右各 32（两鬓）、顶部 24、行间 10。原先 20/10/2，
        // 文字几乎贴到左边缘和上边缘，一整页挤在一起。
        UiMetrics.PlaceRows(_formPage, items, padX: 32, padTop: 24, gap: 10);

        host.Controls.Add(_formPage);
    }

    // ══════════════════ 单列参数行 ══════════════════

    /// <summary>分组标题。整块共用一个板块，分组只作文字提示，不再套边框。</summary>
    private void Group(List<Control> items, string title)
    {
        var row = new Panel
        {
            BackColor = Color.Transparent, Padding = new Padding(0),
            Margin = new Padding(0),
        };

        // 左侧 3px 印章红竖线：分组唯一的重色，替代"给整块套边框"。
        // 边框在窄窗口下会挤压内容，而竖线只是视觉锚点，不占横向空间。
        var bar = new Panel
        {
            Width = 3, BackColor = Theme.Stamp,
            Margin = new Padding(0), Padding = new Padding(0),
        };
        var lbl = new Label
        {
            Text = title, Font = Fonts.Mono8, ForeColor = Theme.Stamp,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };
        var rule = new Panel
        {
            Height = 1, BackColor = Theme.Hairline,
            Margin = new Padding(0), Padding = new Padding(0),
        };

        row.Controls.Add(bar);
        row.Controls.Add(lbl);
        row.Controls.Add(rule);

        // 全都要通栏/定位明确：宽度由行宽给，不能用 AutoSize 去猜
        void Layout(int w)
        {
            const int top = 22;      // 组标题上方的气口
            const int barH = 12;

            bar.Location = new Point(0, top + 1);
            bar.Size = new Size(3, barH);

            lbl.Location = new Point(9, top);
            lbl.Width = w - 9;
            lbl.Height = UiMetrics.MeasureHeight(title, Fonts.Mono8, lbl.Width);

            rule.Location = new Point(0, lbl.Bottom + 8);
            rule.Width = w;

            row.Height = rule.Bottom + 8;
        }

        row.Tag = (Action<int>)Layout;
        items.Add(row);
    }

    /// <summary>
    /// 一个参数 = 两行：第一行 标签 + 控件（控件紧跟标签），
    /// 第二行 绑定键（灰）+ 一句解释，解释一路写到面板右边缘。
    ///
    /// 全部显式摆放，不碰 AutoSize：
    /// AutoSize 容器只按内容算自己的尺寸，子项永远拿不到容器的真实宽度 ——
    /// 于是解释文字按「几个字」的宽度折成四五行，右边留一大片空白。
    /// 这里由行的摆法直接拿 PlaceRows 给的行宽，解释文字的宽度 =
    /// 行宽 - 键名宽 - 间距，装不下才折行。
    /// </summary>
    private void Param(List<Control> items, string label, string key, string help, Control ctl)
    {
        var row = new Panel
        {
            BackColor = Color.Transparent, Padding = new Padding(0),
            Margin = new Padding(0, 7, 0, 7),
        };

        var lblName = new Label
        {
            Text = label, Font = Fonts.Ui9, ForeColor = Theme.Ink,
            AutoSize = true, Margin = new Padding(0), Padding = new Padding(0),
            MaximumSize = new Size(400, 0),
        };
        var lblKey = new Label
        {
            Text = key, Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = true, Margin = new Padding(0), Padding = new Padding(0),
        };
        var lblHelp = new Label
        {
            Text = help, Font = Fonts.Mono8, ForeColor = Theme.InkMid,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };

        ctl.Margin = new Padding(0);
        if (ctl is NumericUpDown) ctl.Width = 88;
        else if (ctl is ComboBox) ctl.Width = 200;
        else ctl.Width = 320;

        if (ctl is NumericUpDown n) Bind(n, key);
        else if (ctl is ComboBox c) Bind(c, key);
        else if (ctl is CheckBox b) Bind(b, key);
        else if (ctl is TextBox t) Bind(t, key);

        row.Controls.Add(lblName);
        row.Controls.Add(ctl);
        row.Controls.Add(lblKey);
        row.Controls.Add(lblHelp);

        void Layout(int w)
        {
            lblName.Location = new Point(0, 4);
            // 控件紧跟标签，不贴右边缘 —— 面板上千像素宽，甩到右边会空一大块
            ctl.Location = new Point(lblName.Width + 10, 2);

            int y2 = Math.Max(lblName.Height, ctl.Height) + 5;
            lblKey.Location = new Point(0, y2);
            lblHelp.Location = new Point(lblKey.Width + 10, y2 - 1);

            var helpW = Math.Max(80, w - lblKey.Width - 10);
            lblHelp.Width = helpW;
            lblHelp.Height = UiMetrics.MeasureHeight(help, Fonts.Mono8, helpW);

            row.Height = lblHelp.Bottom + 5;
        }

        // PlaceRows 每轮给行宽时会调这个
        row.Tag = (Action<int>)Layout;

        items.Add(row);
    }

    private void BuildYamlPage(Panel host)
    {
        _yamlPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Paper, Visible = false,
            Padding = new Padding(14, 10, 14, 10) };

        // 提示用 WrapTop 拿"按宽度自算高度"，但必须改成 Dock=Top。
        // 锚左右（Anchor）在这个面板里没有把宽度带上去：面板从不可见的 200px
        // 变成可见时的 1024px，Label 的 Width 一直停在默认的 100 ——
        // 于是 50 来字的提示被折成五窄行，右边整片空白。
        // Dock=Top 由布局引擎保证拿到父面板的客户区宽度，高度由 WrapTop 自己算。
        var hint = UiMetrics.WrapTop(
            "config.yaml 原文。内核的任何配置项都在这里，包括本表单没有控件的键 —— 直接改即可，未知键不影响运行。",
            Fonts.Mono8, Theme.InkLow, 0);
        hint.Dock = DockStyle.Top;

        _yamlBox = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both,
            Font = Fonts.Mono95, BackColor = Theme.PaperHi,
            ForeColor = Theme.Ink, BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false, AcceptsTab = true,
        };
        _yamlBox.TextChanged += (s, e) => _dirtyYaml = true;

        _yamlPage.Controls.Add(_yamlBox);
        _yamlPage.Controls.Add(hint);
        host.Controls.Add(_yamlPage);
    }

    // ══════════════════ 保存 ══════════════════

    private void Save()
    {
        try
        {
            if (_tab == 1)
            {
                if (!_dirtyYaml) { Close(); return; }
                // 只做最基本的健全性检查，不做 YAML 解析 ——
                // 解析会让内核新版键被我们误判为非法。
                var text = _yamlBox.Text ?? "";
                if (text.Trim().Length == 0)
                {
                    var r = MessageBox.Show(this, "config.yaml 是空的，内核将使用全部默认值。\n确定保存？",
                        "测速参数", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.OK) return;
                }
                if (text.Contains('\t'))
                {
                    MessageBox.Show(this, "config.yaml 含有 Tab 字符，YAML 不允许用 Tab 缩进。\n请改成空格。",
                        "测速参数", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SpeedTestConfig.WriteConfig(text);
                _dirtyYaml = false;
            }

            // 内核只在启动时读配置，这里给出明确提示，避免用户以为立即生效
            MessageBox.Show(this,
                "已保存到 config.yaml。\n\n注意：内核在启动时读取配置，改动将在下一次「开始测速」时生效。",
                "测速参数", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：\n" + ex.Message, "测速参数",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ══════════════════ 表单控件（从 SpeedTestPanel 迁移） ══════════════════





    private int CfgInt(string key, int def)
        => int.TryParse(SpeedTestConfig.GetScalar(SpeedTestConfig.ReadConfig(), key), out var v) ? v : def;

    private bool CfgBool(string key, bool def)
        => bool.TryParse(SpeedTestConfig.GetScalar(SpeedTestConfig.ReadConfig(), key), out var v) ? v : def;

    private string CfgStr(string key, string def)
    {
        var v = SpeedTestConfig.GetScalar(SpeedTestConfig.ReadConfig(), key);
        return string.IsNullOrEmpty(v) ? def : v;
    }

    /// <summary>
    /// 读 YAML 列表键（node-type / platforms），取子行里的 "- 项" 拼成逗号串。
    /// GetScalar 遇到列表返回 null，所以这类键必须单独读，否则表单永远显示
    /// 硬编码的默认值、写回时还会把用户的列表覆盖掉。
    /// </summary>
    private static bool IsListKey(string key) => key is "node-type" or "platforms";

    private string CfgList(string key, string def)
    {
        var block = SpeedTestConfig.GetBlock(SpeedTestConfig.ReadConfig(), key);
        if (string.IsNullOrWhiteSpace(block)) return def;
        var items = block.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("- "))
            .Select(l => l[2..].Trim().Trim('"', '\''))
            .Where(l => l.Length > 0);
        var joined = string.Join(",", items);
        return joined.Length == 0 ? def : joined;
    }

    private NumericUpDown Num(int val, int min, int max, int width = 56)
        => new()
        {
            Width = width, Font = Fonts.Mono9, Minimum = min, Maximum = max, Value = val,
            TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.PaperHi, ForeColor = Theme.Ink, Margin = new Padding(0),
        };

    private ComboBox Cmb(string selected, params string[] items)
    {
        var c = new ComboBox
        {
            Width = 118, Font = Fonts.Ui9, DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat, BackColor = Theme.PaperHi, ForeColor = Theme.Ink,
            Margin = new Padding(0),
        };
        c.Items.AddRange(items);
        var i = c.Items.IndexOf(selected);
        c.SelectedIndex = i >= 0 ? i : (c.Items.Count > 0 ? 0 : -1);
        return c;
    }

    private TextBox Txt(string val, int width = 118)
        => new()
        {
            Text = val, Width = width, Font = Fonts.Mono9, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.PaperHi, ForeColor = Theme.Ink, Margin = new Padding(0),
        };

    private CheckBox Chk(bool on) => new()
    {
        Checked = on, AutoSize = true, FlatStyle = FlatStyle.Flat,
        BackColor = Theme.PaperHi, ForeColor = Theme.Ink, Margin = new Padding(0),
    };

    /// <summary>控件失焦即写回 config.yaml。</summary>
    private void BindWrite(Control ctl)
    {
        if (ctl.Tag is not string k || string.IsNullOrEmpty(k)) return;
        try
        {
            var yaml = SpeedTestConfig.ReadConfig();
            if (ctl is NumericUpDown n) yaml = SpeedTestConfig.SetScalar(yaml, k, (int)n.Value);
            else if (ctl is ComboBox c && c.SelectedItem is string s) yaml = SpeedTestConfig.SetScalar(yaml, k, s);
            else if (ctl is CheckBox b) yaml = SpeedTestConfig.SetScalar(yaml, k, b.Checked);
            // 列表键必须写回 YAML 列表，写成标量字符串内核认不出来
            else if (ctl is TextBox t && IsListKey(k))
                yaml = SpeedTestConfig.SetStringList(yaml, k,
                    (t.Text ?? "").Split(new[] { ',', '，', ' ', '\t' },
                        StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            else if (ctl is TextBox tb) yaml = SpeedTestConfig.SetScalar(yaml, k, tb.Text.Trim());
            SpeedTestConfig.WriteConfig(yaml);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"写回 {k} 失败：\n{ex.Message}", "测速参数",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Bind(Control ctl, string key)
    {
        ctl.Tag = key;
        ctl.Leave += (s, e) => BindWrite(ctl);
        // 下拉/勾选即时生效，数字步进等失焦（避免边打边写）
        if (ctl is ComboBox) ((ComboBox)ctl).SelectionChangeCommitted += (s, e) => BindWrite(ctl);
        if (ctl is CheckBox b) b.CheckedChanged += (s, e) => BindWrite(ctl);
    }





}
