using System.Windows;

namespace ThinkCanvas;

public partial class App : Application
{
    private OverlayWindow? _overlay;
    private ToolbarWindow? _toolbar;

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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _toolbar?.Close();
        _overlay?.Close();
        base.OnExit(e);
    }
}
