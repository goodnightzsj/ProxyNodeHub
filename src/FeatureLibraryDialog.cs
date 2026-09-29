using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProxyNodeHub;

public class FeatureLibraryDialog : Form
{
    private TextBox _txtImport = null!;
    private Label _lblRuleCount = null!;
    private Panel _rulesList = null!;

    public FeatureLibraryDialog()
    {
        Text = "特征库管理";
        Size = new Size(640, 600);
        MinimumSize = new Size(540, 500);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Theme.Paper;
        ForeColor = Theme.Ink;
        Font = Fonts.Ui9;

        BuildUi();
        LoadRules();

        Load += (s, e) => { Win32Interop.EnableDarkTitle(this, false); Win32Interop.EnableRoundedCorners(this); };
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    private void BuildUi()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 20),
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Theme.Paper
        };
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // ═══ 第一行: 默认特征库 ═══
        var defaultCard = CreateCard(Theme.InkMid);
        defaultCard.Controls.Add(MakeTitle("默认特征库", "内置 · 不可修改", Theme.InkMid));
        var defaultInfo = new Label
        {
            Text = "L1 特征库学习  L2 已知映射  L3 文件树探测\nL4 常见路径    L5 README 解析  L6 兜底候选",
            ForeColor = Theme.InkMid,
            Font = Fonts.Mono85,
            AutoSize = true,
            Location = new Point(20, 32),
            BackColor = Theme.PaperHi
        };
        defaultCard.Controls.Add(defaultInfo);
        defaultCard.Height = 72;
        main.Controls.Add(defaultCard, 0, 0);

        // ═══ 第二行: 自定义特征库 (填充剩余空间) ═══
        var customCard = CreateCard(Theme.Stamp);
        var customHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Theme.PaperHi };
        customHeader.Controls.Add(MakeTitle("自定义特征库", "", Theme.Stamp));
        _lblRuleCount = new Label
        {
            Text = "0 条规则",
            ForeColor = Theme.Stamp,
            Font = Fonts.Mono85,
            AutoSize = true,
            Location = new Point(160, 8),
            BackColor = Theme.PaperHi
        };
        customHeader.Controls.Add(_lblRuleCount);
        customCard.Controls.Add(customHeader);

        // 规则列表 (可滚动)
        _rulesList = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Theme.PaperHi,
            Padding = new Padding(0, 4, 0, 4)
        };
        customCard.Controls.Add(_rulesList);

        // 导入区
        var importPanel = new Panel { Dock = DockStyle.Top, Height = 168, BackColor = Theme.PaperHi, Padding = new Padding(20, 8, 16, 8) };
        importPanel.Controls.Add(new Label
        {
            Text = "导入特征码 (粘贴 JSON):",
            ForeColor = Theme.Ink,
            Font = Fonts.Ui9Bold,
            AutoSize = true,
            Location = new Point(20, 8),
            BackColor = Theme.PaperHi
        });
        _txtImport = new TextBox
        {
            Multiline = true,
            Height = 96,
            Width = 554,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Theme.PaperDeep,
            ForeColor = Theme.Ink,
            Font = Fonts.Mono85,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(20, 28)
        };
        importPanel.Controls.Add(_txtImport);

        // 按钮行
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 40, BackColor = Theme.PaperHi,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Padding = new Padding(16, 4, 16, 0)
        };
        var btnImport = new ModernButton { Text = "📥 导入", BaseColor = Theme.Stamp, ForeColor = Theme.Paper, Size = new Size(80, 32) };
        btnImport.Click += (s, e) => ImportFeatureCode();
        var btnExport = new ModernButton { Text = "📤 导出", Ghost = true, BackColor = Theme.PaperHi, Size = new Size(80, 32) };
        btnExport.Click += (s, e) => ExportFeatureCode();
        var btnReset = new ModernButton { Text = "🔄 重置", Ghost = true, BackColor = Theme.PaperHi, Size = new Size(80, 32) };
        btnReset.Click += (s, e) => ResetCustom();
        btnPanel.Controls.AddRange(new Control[] { btnImport, btnExport, btnReset });
        importPanel.Controls.Add(btnPanel);

        customCard.Controls.Add(importPanel);
        main.Controls.Add(customCard, 0, 1);

        // ═══ 第三行: 特征库文档 ═══
        var docCard = CreateCard(Theme.Stamp);
        docCard.Controls.Add(MakeTitle("特征库文档", "复制给 AI", Theme.Stamp));
        docCard.Controls.Add(new Label
        {
            Text = "复制给 AI 生成特征码，粘贴到导入框即可",
            ForeColor = Theme.InkMid,
            Font = Fonts.Ui9,
            AutoSize = true,
            Location = new Point(20, 32),
            BackColor = Theme.PaperHi
        });
        var btnCopyDoc = new ModernButton
        {
            Text = "📋 复制文档", BaseColor = Theme.Stamp, ForeColor = Theme.Paper,
            Size = new Size(130, 32), Location = new Point(20, 56)
        };
        btnCopyDoc.Click += (s, e) =>
        {
            try
            {
                Clipboard.SetText(CustomFeatureLibrary.GetDocument());
                btnCopyDoc.Text = "✓ 已复制";
                var t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += (s2, e2) => { btnCopyDoc.Text = "📋 复制文档"; t.Stop(); t.Dispose(); };
                t.Start();
            }
            catch { }
        };
        docCard.Controls.Add(btnCopyDoc);
        docCard.Height = 100;
        main.Controls.Add(docCard, 0, 2);

        Controls.Add(main);
    }

    private Panel CreateCard(Color accentColor)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.PaperHi,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0)
        };
        card.Paint += (s, e) =>
        {
            using var border = new Pen(Theme.Hairline, 1f);
            e.Graphics.DrawRectangle(border, 0, 0, card.Width - 1, card.Height - 1);
            using var accent = new SolidBrush(accentColor);
            e.Graphics.FillRectangle(accent, 0, 0, 4, card.Height);
        };
        return card;
    }

    private Label MakeTitle(string title, string badge, Color badgeColor)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Theme.PaperHi };
        var lblTitle = new Label
        {
            Text = title,
            Font = Fonts.Ui11Bold,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(16, 6),
            BackColor = Theme.PaperHi
        };
        panel.Controls.Add(lblTitle);
        if (!string.IsNullOrEmpty(badge))
        {
            var lblBadge = new Label
            {
                Text = badge,
                Font = Fonts.Mono85,
                ForeColor = badgeColor,
                AutoSize = true,
                Location = new Point(lblTitle.Right + 12, 8),
                BackColor = Theme.PaperHi
            };
            panel.Controls.Add(lblBadge);
        }
        // We return a Label but added to panel - this is a workaround
        // Actually let's just return the title label and handle badge separately
        return lblTitle;
    }

    private void LoadRules()
    {
        var pack = CustomFeatureLibrary.Pack;
        _lblRuleCount.Text = $"{pack.Rules.Count} 条规则";
        _rulesList.SuspendLayout();
        _rulesList.Controls.Clear();

        if (pack.Rules.Count == 0)
        {
            _rulesList.Controls.Add(new Label
            {
                Text = "暂无自定义规则",
                ForeColor = Theme.InkMid,
                AutoSize = true,
                Font = Fonts.Ui9,
                Location = new Point(4, 4),
                BackColor = Theme.PaperHi
            });
            _rulesList.Height = 28;
        }
        else
        {
            int ry = 2;
            foreach (var rule in pack.Rules.Take(3))
            {
                var item = new Panel
                {
                    Width = 540, Height = 28,
                    Location = new Point(4, ry),
                    BackColor = Theme.PaperDeep
                };
                item.Paint += (s, e) =>
                {
                    using var pen = new Pen(Theme.Hairline, 1f);
                    e.Graphics.DrawRectangle(pen, 0, 0, item.Width - 1, item.Height - 1);
                };
                var lbl = new Label
                {
                    Text = $"● {rule.Description}",
                    ForeColor = Theme.Ink,
                    AutoSize = true,
                    Location = new Point(6, 5),
                    Font = Fonts.Ui9,
                    BackColor = Theme.PaperDeep
                };
                var btnDel = new ModernButton
                {
                    Text = "✕", Ghost = true, BackColor = Theme.PaperDeep,
                    Size = new Size(24, 20), Dock = DockStyle.Right
                };
                var ruleId = rule.Id;
                btnDel.Click += (s, e) => DeleteRule(ruleId);
                item.Controls.Add(lbl);
                item.Controls.Add(btnDel);
                _rulesList.Controls.Add(item);
                ry += 32;
            }
            if (pack.Rules.Count > 3)
            {
                _rulesList.Controls.Add(new Label
                {
                    Text = $"... 还有 {pack.Rules.Count - 3} 条",
                    ForeColor = Theme.InkMid,
                    AutoSize = true,
                    Font = Fonts.Mono85,
            Location = new Point(4, ry),
                    BackColor = Theme.PaperHi
                });
            }
            _rulesList.Height = ry + 8;
        }

        _rulesList.ResumeLayout(true);
    }

    private void ImportFeatureCode()
    {
        var json = _txtImport.Text.Trim();
        if (string.IsNullOrEmpty(json))
        {
            MessageBox.Show("请先粘贴特征码 JSON", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var (ok, error, pack) = CustomFeatureLibrary.Import(json);
        if (!ok || pack == null)
        {
            MessageBox.Show($"导入失败:\n{error}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var existing = CustomFeatureLibrary.Pack;
        var existingIds = existing.Rules.Select(r => r.Id).ToHashSet();
        int added = 0;
        foreach (var rule in pack.Rules)
        {
            if (existingIds.Contains(rule.Id)) continue;
            existing.Rules.Add(rule);
            added++;
        }
        CustomFeatureLibrary.Save(existing);
        LoadRules();
        _txtImport.Clear();
        MessageBox.Show($"成功导入 {added} 条规则", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportFeatureCode()
    {
        var pack = CustomFeatureLibrary.Pack;
        if (pack.Rules.Count == 0)
        {
            MessageBox.Show("没有可导出的规则", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(pack, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Clipboard.SetText(json);
            MessageBox.Show("特征码已复制到剪贴板", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch { }
    }

    private void ResetCustom()
    {
        if (MessageBox.Show("确定要清空所有自定义规则?", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        CustomFeatureLibrary.Reset();
        LoadRules();
    }

    private void DeleteRule(string ruleId)
    {
        var pack = CustomFeatureLibrary.Pack;
        pack.Rules.RemoveAll(r => r.Id == ruleId);
        CustomFeatureLibrary.Save(pack);
        LoadRules();
    }
}
