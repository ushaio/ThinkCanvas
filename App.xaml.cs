using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using WF = System.Windows.Forms;

namespace ThinkCanvas;

public partial class App : Application
{
    private OverlayWindow? _overlay;
    private ToolbarWindow? _toolbar;
    private WF.NotifyIcon? _trayIcon;
    private Icon? _trayIconSource;
    private bool _inTray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppSettings settings;
        try { settings = SettingsStore.Load(); }
        catch (Exception exception)
        {
            MessageBox.Show($"无法读取设置，已使用默认配置：{exception.Message}", "ThinkCanvas");
            settings = new AppSettings();
        }
        _overlay = new OverlayWindow(settings);
        _toolbar = new ToolbarWindow(_overlay);
        _overlay.Toolbar = _toolbar;

        _overlay.Show();
        _toolbar.Show();
        _overlay.SetMode(OverlayMode.Passthrough);
        CreateTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is { } trayIcon)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }
        _trayIconSource?.Dispose();
        _toolbar?.Close();
        _overlay?.Close();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        _trayIconSource = TryExtractAppIcon() ?? BuildTrayIcon();
        _trayIcon = new WF.NotifyIcon
        {
            Icon = _trayIconSource,
            Text = "ThinkCanvas",
            Visible = false
        };
        _trayIcon.MouseClick += OnTrayIconMouseClick;
        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("显示悬浮工具栏", null, (_, _) => ExitTrayMode());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("退出 ThinkCanvas", null, (_, _) => Shutdown());
        _trayIcon.ContextMenuStrip = menu;
    }

    private void OnTrayIconMouseClick(object? sender, WF.MouseEventArgs e)
    {
        if (e.Button == WF.MouseButtons.Left)
            ExitTrayMode();
    }

    /// <summary>隐藏悬浮工具栏并挂起快捷键，仅保留托盘图标用于恢复。</summary>
    public void EnterTrayMode()
    {
        if (_inTray || _toolbar is null || _overlay is null) return;
        _inTray = true;
        _overlay.SetMode(OverlayMode.Passthrough);
        _overlay.SuspendShortcuts();
        _toolbar.Hide();
        _trayIcon!.Visible = true;
    }

    public void ExitTrayMode()
    {
        if (!_inTray || _toolbar is null || _overlay is null) return;
        _inTray = false;
        _trayIcon!.Visible = false;
        _toolbar.Show();
        _overlay.ResumeShortcuts();
        _toolbar.EnsureAboveOverlay();
    }

    /// <summary>从 exe 内嵌图标提取托盘图标，与资源管理器显示保持一致；提取失败时由调用方回退到运行时绘制。</summary>
    private static Icon? TryExtractAppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            return string.IsNullOrEmpty(exePath) ? null : Icon.ExtractAssociatedIcon(exePath);
        }
        catch (Exception exception) when (exception is ArgumentException or System.IO.IOException or
            System.ComponentModel.Win32Exception or InvalidOperationException)
        { return null; }
    }

    private static Icon BuildTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var path = new GraphicsPath();
            var radius = 8;
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(32 - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(32 - radius * 2, 32 - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, 32 - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            using var brush = new SolidBrush(Color.FromArgb(33, 116, 95));
            graphics.FillPath(brush, path);
            using var font = new Font("Segoe MDL2 Assets", 15f);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            graphics.DrawString("\uE70F", font, Brushes.White, new RectangleF(0, 1, 32, 32), format);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }
}
