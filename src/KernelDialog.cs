using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>
/// 内核管理弹窗：版本信息、安装位置（默认/自定义）、自动更新开关。
///
/// 纵向排版一律显式堆叠，不用 TableLayoutPanel 的行：
/// 1) 分隔线原先占的是 SizeType.Absolute 行，而 TableLayoutExt.Rows 给非
///    Percent 行一律赋 0 高度 —— 线就画到了相邻行的内容上，「安装位置」那行
///    被一条灰线横穿，就是这么来的。
/// 2) 行高全是 AutoSize 时，剩余空间既不会被吸收也不会把窗体撑到贴合内容，
///    底部于是空一大块。这里改成内容自己算总高，再一次性把窗体高度收到贴合。
/// </summary>
public sealed class KernelDialog : Form
{
    public bool RequestedManual { get; private set; }
    public bool Changed { get; private set; }

    private TextBox _txtPath = null!;
    private CheckBox _chkAuto = null!;
    private Label _lblDefaultHint = null!;
    private Label _lblVersion = null!;
    private Label _lblLatestTag = null!;
    private ModernButton _btnCheck = null!;
    private Panel? _pathBox;
    private Action<int>? _pathLayout;
    private Panel? _infoBox;
    private Action<int>? _infoLayout;
    private Panel? _contentBox;
    private Action<int>? _contentLayout;
    private readonly System.Threading.CancellationTokenSource _cts = new();
    private readonly Func<string, string> _proxy;
    private bool _shown;
    private bool _busy;

    /// <param name="proxy">拉 GitHub 用的加速前缀。直连不通的机器必须传，
    /// 否则「检查更新」会一直卡在查询上。</param>
    public KernelDialog(Func<string, string>? proxy = null)
    {
        _proxy = proxy ?? (u => u);
        Text = "subs-check 内核";
        // 定 ClientSize 而不是 Size。Size 要等句柄建好才生效，之前那段时间
        // 窗体按默认尺寸（约 300px 宽）布局了一次，内容按窄宽度折行、总高虚高，
        // FitForm 照这个虚高把窗体撑到 730 多，底部空一大块。
        ClientSize = new Size(604, 461);          // ≈ 620x500 含标题栏与边框
        MinimumSize = new Size(560, 460);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Paper;
        ForeColor = Theme.Ink;
        Font = Fonts.Ui9;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        KeyPreview = true;

        var settings = AppSettings.Load();
        var local = SubCheckKernel.LocalVersion();
        var inset = 22;

        var root = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Theme.Paper,
            Padding = new Padding(inset, 18, inset, 14),
        };

        // 内容容器：所有板块按序显式堆在里面
        var content = new Panel { Dock = DockStyle.Top, BackColor = Color.Transparent };

        var title = UiMetrics.TitleBlock("subs-check 内核",
            "GPL-3.0 · Go 二进制 · 约 55 MB");
        var info = BuildInfo(local);
        var path = BuildPath(settings);
        var auto = BuildAutoUpdate(settings);
        var note = BuildNote();
        var btns = BuildButtons(local);

        Control Rule() => new Panel
        {
            Height = 1, BackColor = Theme.Hairline,
            Margin = new Padding(0), Padding = new Padding(0),
        };

        var r1 = Rule(); var r2 = Rule(); var r3 = Rule();

        // 板块 + 前后的间距。间距由这里统一管，不再靠 Margin 溢出到相邻行
        var items = new (Control Ctl, int Before, int After)[]
        {
            (title, 0,  14),
            (r1,    0,  14),
            (info,  0,  14),
            (r2,    0,  14),
            (path,  0,  10),
            (auto,  0,  12),
            (r3,    0,  12),
            (note,  0,  12),
            (btns,  6,  0),
        };

        foreach (var it in items) content.Controls.Add(it.Ctl);
        root.Controls.Add(content);

        _contentBox = content;
        _contentLayout = w =>
        {
            int y = 0;
            foreach (var it in items)
            {
                y += it.Before;
                var c = it.Ctl;
                // 每块（含分隔线）都通栏：宽度由容器给，别用 Dock 去抢
                c.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                c.Dock = DockStyle.None;
                c.Location = new Point(0, y);
                c.Width = w;

                if (c.Tag is Action<int> lay) lay(w);   // 自带动法的板块
                else c.PerformLayout();                 // AutoSize 板块按内容算高

                var h = c.Tag is Action<int> ? c.Height : c.PreferredSize.Height;
                if (h <= 0) h = c.Height;
                c.Height = h;
                y += h + it.After;
            }
            content.Height = y;
            FitForm(y);
        };
        content.Tag = _contentLayout;
        content.AutoLayout();

        // 显示之前允许反复收高度：窗体在拿到最终宽度前会被布局好几轮
        // （先是默认宽度、再是设定宽度），中途那几次内容按窄宽度折行、
        // 总高虚高。显示之后就定型，不再动，免得切换默认/自定义时窗体忽大忽小。
        // 一打开就把最新版本号查出来填到标注里，不用先手点「检查更新」。
        // 只查不比、不下载；失败就留着空标注，不打扰用户。
        Shown += async (s, e) => await ProbeLatestAsync();

        // 显示那一刻把最小高度锁成收好的高度：之后不能再被压得比内容还矮，
        // 否则底部又会被切掉。放在这里而不是 FitForm 里 —— FitForm 会被布局
        // 调好几轮，中途按窄宽度算出的虚高会先把最小高度顶上去，再也缩不回来。
        Shown += (s, e) =>
        {
            _shown = true;
            MinimumSize = new Size(MinimumSize.Width, Height);
        };

        Controls.Add(root);

        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        FormClosed += (s, e) => { try { _cts.Cancel(); } catch { } };
    }

    /// <summary>
    /// 把窗体高度收到贴合内容，消掉底部那一大片空白。
    ///
    /// 显示前可以反复调：窗体在拿到最终宽度前会被布局好几轮，中途那几次
    /// 内容按窄宽度折行、总高虚高，只认第一次就会把窗体撑过头。
    /// 显示之后一概不碰 —— 否则切换默认/自定义时提示文字长度变化会连带动窗体忽大忽小。
    /// </summary>
    private void FitForm(int contentHeight)
    {
        if (_shown || contentHeight <= 0) return;
        // 窗体高 = 内容 + 上下内边距 + 标题栏与边框
        var need = contentHeight + 18 + 14 + (Height - ClientSize.Height);
        if (need > 0 && Height != need) Height = need;
    }

    // ══════════════════ 版本信息 ══════════════════

    private Panel BuildInfo(string? local)
    {
        // 三列：键名 / 值 / 「最新版本」标注。标注单独一列，
        // 直接改值Label 的 Text 会把版本号和标注糊成一片、也无法分别上色。
        var box = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0),
        };

        var t = new TableLayoutPanel
        {
            ColumnCount = 3, AutoSize = true,
            BackColor = Color.Transparent, Margin = new Padding(0),
            Dock = DockStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        }
        .Columns(TableLayoutExt.AutoSize_, TableLayoutExt.AutoSize_, TableLayoutExt.Fill_)
        .Rows(TableLayoutExt.AutoSize_, TableLayoutExt.AutoSize_, TableLayoutExt.AutoSize_);

        Row(t, 0, "本地版本", local == null ? "未安装" : SubCheckKernel.Display(local),
            local == null ? Theme.Ochre : Theme.Live, out var ver);
        _lblVersion = ver;
        Row(t, 1, "发布地址", "github.com/beck-8/subs-check", Theme.InkMid, out _);
        Row(t, 2, "许可", "GNU General Public License v3.0", Theme.InkMid, out _);

        // 「最新版本 vX.Y.Z」标注，紧跟在版本号后面；检查前是空的
        _lblLatestTag = new Label
        {
            Text = "", Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = true, Margin = new Padding(0, 4, 0, 3), Padding = new Padding(0),
        };
        t.Controls.Add(_lblLatestTag, 2, 0);

        box.Controls.Add(t);
        _infoBox = box;
        _infoLayout = w => { t.Width = w; box.Height = t.PreferredSize.Height; };
        box.Tag = _infoLayout;
        return box;
    }

    private void Row(TableLayoutPanel t, int r, string k, string v, Color c, out Label value)
    {
        t.Controls.Add(new Label
        {
            Text = k, Font = Fonts.Mono8, ForeColor = Theme.InkMid,
            AutoSize = true, Margin = new Padding(0, 3, 12, 3), Padding = new Padding(0),
        }, 0, r);
        value = new Label
        {
            Text = v, Font = Fonts.Mono9, ForeColor = c,
            AutoSize = true, Margin = new Padding(0, 3, 0, 3), Padding = new Padding(0),
        };
        t.Controls.Add(value, 1, r);
    }

    // ══════════════════ 安装位置 ══════════════════

    private Panel BuildPath(AppSettings settings)
    {
        // 全部显式摆放，不碰 AutoSize。
        // 之前用 AutoSize 的 TableLayoutPanel + WrapTop(Dock=Fill)，提示 Label
        // 在 AutoSize 容器里只按内容算宽（"默认位置。修改后" 七八个字就折行），
        // 右边大片空白。这里宽度由行宽直接给，路径框和提示都吃满。
        var box = new Panel
        {
            // 左右锚定：宽度跟着容器走，高度仍由摆法自己算
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent,
            Margin = new Padding(0), Padding = new Padding(0),
        };

        var lblTitle = new Label
        {
            Text = "安装位置", Font = Fonts.Ui9Bold, ForeColor = Theme.Ink,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };
        var btnDefault = new ModernButton { Text = "默认", Ghost = true, Size = new Size(56, 24), Margin = new Padding(0, 0, 6, 0) };
        var btnCustom = new ModernButton { Text = "自定义", Ghost = true, Size = new Size(64, 24), Margin = new Padding(0) };
        btnDefault.Click += (s, e) => ApplyPath("");
        btnCustom.Click += (s, e) => PickFolder();

        // 路径输入框：吃掉整行宽度，绝不写死像素宽导致溢出边框
        _txtPath = new TextBox
        {
            Font = Fonts.Mono9, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.PaperHi, ForeColor = Theme.Ink,
            Text = string.IsNullOrWhiteSpace(settings.KernelPath)
                ? SpeedTestConfig.KernelDirDefault
                : settings.KernelPath,
            Dock = DockStyle.None,
        };

        _lblDefaultHint = new Label
        {
            Text = "默认位置。修改后下次启动测速时生效。",
            Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };

        box.Controls.Add(lblTitle);
        box.Controls.Add(btnDefault);
        box.Controls.Add(btnCustom);
        box.Controls.Add(_txtPath);
        box.Controls.Add(_lblDefaultHint);
        _pathBox = box;
        _pathLayout = w =>
        {
            // 标题只占自己文字的宽度，按钮跟在文字右边。
            // 若把标题拉成整行宽，按钮会被排到面板外面去。
            lblTitle.Location = new Point(0, 3);
            lblTitle.Width = UiMetrics.TextWidth("安装位置", Fonts.Ui9Bold);
            lblTitle.Height = UiMetrics.MeasureHeight("安装位置", Fonts.Ui9Bold, lblTitle.Width);

            btnDefault.Location = new Point(lblTitle.Right + 12, 0);
            btnCustom.Location = new Point(btnDefault.Right + 6, 0);

            _txtPath.Location = new Point(0, btnCustom.Bottom + 5);
            _txtPath.Width = w;

            _lblDefaultHint.Location = new Point(0, _txtPath.Bottom + 5);
            _lblDefaultHint.Width = w;
            _lblDefaultHint.Height = UiMetrics.MeasureHeight(
                _lblDefaultHint.Text, Fonts.Mono8, w);

            box.Height = _lblDefaultHint.Bottom + 2;
        };
        box.Tag = _pathLayout;
        SyncHint();

        return box;
    }

    private void ApplyPath(string p)
    {
        var s = AppSettings.Load();
        s.KernelPath = p;
        s.Save();
        _txtPath.Text = string.IsNullOrWhiteSpace(p) ? SpeedTestConfig.KernelDirDefault : p;
        SyncHint();
        Changed = true;
    }

    private void PickFolder()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "选择 subs-check.exe 的存放目录",
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(_txtPath.Text) ? _txtPath.Text : SpeedTestConfig.KernelDirDefault,
        };
        if (fbd.ShowDialog(this) != DialogResult.OK) return;

        var p = fbd.SelectedPath;
        // 该目录已有内核则直接采用；否则提醒用户但仍允许（下次安装会放这里）
        if (!File.Exists(Path.Combine(p, "subs-check.exe")))
        {
            var r = MessageBox.Show(this,
                "该目录下还没有 subs-check.exe。\n\n选「是」仍使用此目录（下次安装会放这里），\n选「否」返回重新选择。",
                "安装位置", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.OK) return;
        }
        ApplyPath(p);
    }

    private void SyncHint()
    {
        var custom = AppSettings.Load().KernelPath;
        var isCustom = !string.IsNullOrWhiteSpace(custom);
        _lblDefaultHint.Text = isCustom
            ? "自定义位置。点击「默认」可恢复。"
            : "默认位置。修改后下次启动测速时生效。";
        // 文案长度变了要重跑一遍行布局，高度与行高都按新文案重算
        _pathLayout?.Invoke(_pathBox?.Width ?? _lblDefaultHint.Width);
    }

    // ══════════════════ 检查更新状态行 ══════════════════

    /// <summary>
    /// 把检查更新的状态写进版本那一行（第三列），不再单独占一行。
    ///
    /// 单独一行是行不通的：状态文字是点了「检查更新」之后才出现的，
    /// 而窗体高度在显示那一刻就收好并锁死了，内容突然多出一行只会把
    /// 底部的按钮顶出可视区。版本行的高度由信息表自己算，与状态文字
    /// 无关，往里填文字不会改动任何布局。
    /// </summary>
    private void SetStatus(string text, Color color)
    {
        if (_lblLatestTag == null) return;
        _lblLatestTag.Text = text;
        _lblLatestTag.ForeColor = color;
        // 表格是 AutoSize 的，文字长了会自己重算这一行的高度
        _infoLayout?.Invoke(_infoBox?.Width ?? 0);
        _contentLayout?.Invoke(_contentBox?.Width ?? 0);
    }

    // ══════════════════ 自动更新 ══════════════════

    private Panel BuildAutoUpdate(AppSettings settings)
    {
        // 显式摆放，不碰 AutoSize。AutoSize 容器只按内容算宽，
        // 提示文字会被压成左侧一小条、右边大片空白 —— 正是内核页的老问题。
        var box = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Theme.PaperHi, BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 9, 12, 10), Margin = new Padding(0),
        };

        _chkAuto = new CheckBox
        {
            Text = "自动更新并安装", Font = Fonts.Ui9Bold, ForeColor = Theme.Ink,
            Checked = settings.KernelAutoUpdate, AutoSize = false,
            FlatStyle = FlatStyle.Flat, Margin = new Padding(0), Padding = new Padding(0),
        };
        _chkAuto.CheckedChanged += (s, e) =>
        {
            var st = AppSettings.Load();
            st.KernelAutoUpdate = _chkAuto.Checked;
            st.Save();
            Changed = true;
        };

        var hint1 = new Label
        {
            Text = "网络畅通时自动检测内核新版本，有新版本即自动下载安装，无需手动下载。",
            Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };
        var hint2 = new Label
        {
            Text = "每次最多检查一次；安装失败不影响使用当前版本。",
            Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };

        box.Controls.Add(_chkAuto);
        box.Controls.Add(hint1);
        box.Controls.Add(hint2);
        box.Tag = (Action<int>)(w =>
        {
            var inner = Math.Max(80, w - box.Padding.Horizontal);

            _chkAuto.Location = new Point(box.Padding.Left, box.Padding.Top);
            _chkAuto.Width = inner;
            _chkAuto.Height = UiMetrics.MeasureHeight(_chkAuto.Text, Fonts.Ui9Bold, inner);

            hint1.Location = new Point(box.Padding.Left, _chkAuto.Bottom + 5);
            hint1.Width = inner;
            hint1.Height = UiMetrics.MeasureHeight(hint1.Text, Fonts.Mono8, inner);

            hint2.Location = new Point(box.Padding.Left, hint1.Bottom + 2);
            hint2.Width = inner;
            hint2.Height = UiMetrics.MeasureHeight(hint2.Text, Fonts.Mono8, inner);

            box.Height = hint2.Bottom + box.Padding.Bottom;
        });

        return box;
    }

    // ══════════════════ 说明 ══════════════════

    private Panel BuildNote()
    {
        var text1 = "内核会真实建立代理连接并下载测速数据。随本程序分发时已一并附上其许可证与源码获取地址，以满足 GPL-3.0 的义务。";
        var text2 = "首次下载约 55MB。国内网络下可能较慢或中断，中断后进度会保留，下次继续；也可用「从本地 zip 安装」手动指定。";

        // 与自动更新框同一套做法：宽度由容器给，装不下才折行。
        var box = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent, Margin = new Padding(0),
        };

        var l1 = new Label
        {
            Text = text1, Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };
        var l2 = new Label
        {
            Text = text2, Font = Fonts.Mono8, ForeColor = Theme.InkLow,
            AutoSize = false, Margin = new Padding(0), Padding = new Padding(0),
        };

        box.Controls.Add(l1);
        box.Controls.Add(l2);
        box.Tag = (Action<int>)(w =>
        {
            l1.Location = new Point(0, 0);
            l1.Width = w;
            l1.Height = UiMetrics.MeasureHeight(text1, Fonts.Mono8, w);

            l2.Location = new Point(0, l1.Bottom + 3);
            l2.Width = w;
            l2.Height = UiMetrics.MeasureHeight(text2, Fonts.Mono8, w);

            box.Height = l2.Bottom + 2;
        });

        return box;
    }

    // ══════════════════ 按钮 ══════════════════

    private Control BuildButtons(string? local)
    {
        var f = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Top, AutoSize = true,
            BackColor = Color.Transparent, WrapContents = false, Margin = new Padding(0),
        };

        var btnDir = new ModernButton { Text = "打开目录", Ghost = true, Size = new Size(84, 26), Margin = new Padding(0, 0, 6, 0) };
        btnDir.Click += (s, e) => Common.OpenDir(SpeedTestConfig.KernelPath);

        var btnManual = new ModernButton { Text = "从本地 zip 安装", Ghost = true, Size = new Size(126, 26), Margin = new Padding(0, 0, 6, 0) };
        btnManual.Click += (s, e) => { RequestedManual = true; Close(); };

        _btnCheck = new ModernButton
        {
            Text = local == null ? "下载并安装" : "检查更新", Size = new Size(104, 26),
            Margin = new Padding(0),
        };
        // 检查/安装都在本页进行，不再一按就关窗 —— 用户在页面上看不到过程
        _btnCheck.Click += async (s, e) => await CheckOrInstallAsync();

        // RightToLeft：添加顺序即视觉从右往左
        f.Controls.Add(_btnCheck);
        f.Controls.Add(btnManual);
        f.Controls.Add(btnDir);
        return f;
    }

    // ══════════════════ 检查更新 / 安装 ══════════════════

    /// <summary>
    /// 先查当前版本，再比最新版：
    ///   · 已是最新 → 只标注 [最新版本 vX]，不下载
    ///   · 有新版   → 标注 [有新版本 vX]，问一句再下载
    ///   · 未安装   → 直接下载
    /// 全程不关窗体，进度与结果写在状态行里。
    /// </summary>
    private async Task CheckOrInstallAsync()
    {
        if (_busy) return;
        _busy = true;
        _btnCheck.Enabled = false;
        var original = _btnCheck.Text;
        try
        {
            _btnCheck.Text = "检查中…";
            SetStatus("检查中…", Theme.InkLow);

            var local = SubCheckKernel.LocalVersion();
            var rel = await SubCheckKernel.FetchLatestAsync(_proxy, _cts.Token);
            if (rel == null)
            {
                SetStatus("查询失败，可改用「从本地 zip 安装」", Theme.Stamp);
                return;
            }

            // 已是最新：只标注，不下载
            if (local != null && !SubCheckKernel.IsNewer(rel.Version, local))
            {
                TagLatest($"最新版本 {Ver(rel.Version)}", Theme.Live);
                SetStatus($"已是最新 {Ver(rel.Version)}", Theme.Live);
                _btnCheck.Text = "检查更新";
                return;
            }

            var isNew = local != null;
            TagLatest(isNew ? $"有新版本 {Ver(rel.Version)}" : $"可安装 {Ver(rel.Version)}",
                      isNew ? Theme.Ochre : Theme.InkLow);

            if (isNew)
            {
                var r = MessageBox.Show(this,
                    $"发现新版本 {Ver(rel.Version)}（当前 {Ver(local)}）。\n\n" +
                    $"下载约 {rel.SizeBytes / 1048576.0:F1} MB，是否现在下载并安装？\n" +
                    "安装失败会自动回滚到当前版本。",
                    "内核更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.OK)
                {
                    SetStatus("已取消下载", Theme.InkLow);
                    return;
                }
            }

            SetStatus($"下载中 · {rel.SizeBytes / 1048576.0:F0} MB", Theme.InkLow);
            _btnCheck.Text = "下载中…";

            var ok = await SubCheckKernel.InstallAsync(rel, p =>
            {
                if (IsHandleCreated)
                    BeginInvoke(() => SetStatus(
                        $"{p.Phase} {p.Percent:F0}% · {Ver(rel.Version)}", Theme.InkLow));
            }, _cts.Token, _proxy);

            if (!ok)
            {
                SetStatus("安装失败，已保留当前版本", Theme.Stamp);
                return;
            }

            var now = SubCheckKernel.LocalVersion() ?? rel.Version;
            _lblVersion.Text = Ver(now);
            _lblVersion.ForeColor = Theme.Live;
            TagLatest($"最新版本 {Ver(now)}", Theme.Live);
            SetStatus($"已是最新 {Ver(now)}", Theme.Live);
            _btnCheck.Text = "检查更新";
            Changed = true;
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消", Theme.InkLow);
        }
        catch (Exception)
        {
            SetStatus("操作失败", Theme.Stamp);
        }
        finally
        {
            _busy = false;
            _btnCheck.Enabled = true;
            if (_btnCheck.Text is "检查中…" or "下载中…") _btnCheck.Text = original;
        }
    }

    /// <summary>版本号显示收口到 SubCheckKernel.Display，本地不再自己拼 v。</summary>
    private static string Ver(string? v) => SubCheckKernel.Display(v);

    /// <summary>
    /// 打开弹窗时用：只查最新版本并填标注，不下载、不弹窗。
    /// 查询失败就什么都不显示 —— 用户没要求检查，不该被错误提示打扰。
    /// </summary>
    private async Task ProbeLatestAsync()
    {
        try
        {
            var local = SubCheckKernel.LocalVersion();
            var rel = await SubCheckKernel.FetchLatestAsync(_proxy, _cts.Token);
            if (rel == null) return;

            if (local == null)
                TagLatest($"可安装 {Ver(rel.Version)}", Theme.InkLow);
            else if (!SubCheckKernel.IsNewer(rel.Version, local))
                TagLatest($"已是最新 {Ver(rel.Version)}", Theme.Live);
            else
                TagLatest($"有新版本 {Ver(rel.Version)}", Theme.Ochre);
        }
        catch { /* 探测失败不影响弹窗使用 */ }
    }

    private void TagLatest(string text, Color color) => SetStatus(text, color);

}
