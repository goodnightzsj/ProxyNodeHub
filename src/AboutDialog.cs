using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ProxyNodeHub;

/// <summary>
/// 关于对话框 - DOSSIER 纸面情报卷宗风格
/// </summary>
public class AboutDialog : Form
{
    public AboutDialog()
    {
        Text = "关于 ProxyNodeHub";
        Size = new Size(480, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Paper;
        ForeColor = Theme.Ink;
        Font = Fonts.Ui9;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        KeyPreview = true;

        BuildContent();

        Load += (s, e) => Win32Interop.EnableDarkTitle(this, false);
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };
    }

    private void BuildContent()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 28, 32, 28),
            RowCount = 4,
            BackColor = Theme.Paper
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 品牌
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f)); // 发丝线（含间距）
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 内容
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 按钮

        // ── 品牌行 ──
        var brandPanel = new Panel { Dock = DockStyle.Fill, Height = 48, BackColor = Color.Transparent };
        var brandName = new Label
        {
            Text = "ProxyNodeHub",
            Font = Fonts.SerifBrand,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(0, 4),
            BackColor = Color.Transparent
        };
        var brandTag = new Label
        {
            Text = "NODE INTELLIGENCE · 节点情报台",
            Font = Fonts.Mono8,
            ForeColor = Theme.InkMid,
            AutoSize = true,
            Location = new Point(0, 28),
            BackColor = Color.Transparent
        };
        brandPanel.Controls.Add(brandName);
        brandPanel.Controls.Add(brandTag);
        root.Controls.Add(brandPanel, 0, 0);

        // ── 发丝线 ──
        var rule = new Panel { Dock = DockStyle.Fill, Height = 2, BackColor = Theme.Rule, Margin = new Padding(0, 12, 0, 12) };
        root.Controls.Add(rule, 0, 1);

        // ── 内容区 ──
        var contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        // Logo
        var logoBox = new PictureBox
        {
            Size = new Size(48, 48),
            Location = new Point(0, 4),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "ProxyNodeHub.logo.png";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null) logoBox.Image = Image.FromStream(stream);
        }
        catch { }

        var versionLabel = new Label
        {
            Text = "版本",
            Font = Fonts.Ui9Bold,
            ForeColor = Theme.InkMid,
            AutoSize = true,
            Location = new Point(64, 8),
            BackColor = Color.Transparent
        };
        var versionValue = new Label
        {
            Text = "v0.0.1_beta",
            Font = Fonts.Mono9,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(120, 8),
            BackColor = Color.Transparent
        };

        var authorLabel = new Label
        {
            Text = "作者",
            Font = Fonts.Ui9Bold,
            ForeColor = Theme.InkMid,
            AutoSize = true,
            Location = new Point(64, 28),
            BackColor = Color.Transparent
        };
        var authorValue = new NoFocusLinkLabel
        {
            Text = "Zoyaya",
            Font = Fonts.Mono9,
            LinkColor = Theme.Ink,
            ActiveLinkColor = Theme.Stamp,
            VisitedLinkColor = Theme.Ink,
            LinkBehavior = LinkBehavior.NeverUnderline,
            AutoSize = true,
            Location = new Point(120, 28),
            BackColor = Color.Transparent,
            TabStop = false
        };
        authorValue.LinkClicked += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = "https://github.com/wanvfx", UseShellExecute = true }); }
            catch { }
        };

        var descLabel = new Label
        {
            Text = "GitHub 免费节点仓库监控与聚合工具\r\n帮助用户发现活跃的节点仓库、分析活跃度、去重节点，并提供订阅链接。",
            Font = Fonts.Ui9,
            ForeColor = Theme.InkMid,
            AutoSize = true,
            MaximumSize = new Size(340, 0),
            Location = new Point(0, 60),
            BackColor = Color.Transparent
        };

        contentPanel.Controls.AddRange(new Control[] { logoBox, versionLabel, versionValue, authorLabel, authorValue, descLabel });
        root.Controls.Add(contentPanel, 0, 2);

        // ── 关闭按钮 ──
        var btnClose = new ModernButton
        {
            Text = "关 闭",
            Ghost = true,
            BackColor = Theme.Paper,
            Size = new Size(88, 32),
            Dock = DockStyle.Right
        };
        btnClose.Click += (s, e) => Close();
        root.Controls.Add(btnClose, 0, 3);

        Controls.Add(root);
    }
}

/// <summary>
/// 无焦点虚线的链接标签
/// </summary>
internal class NoFocusLinkLabel : LinkLabel
{
    protected override bool ShowFocusCues => false;
}
