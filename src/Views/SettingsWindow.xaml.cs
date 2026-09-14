using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ThinkCanvas;

public partial class SettingsWindow : Window
{
    private readonly OverlayWindow _overlay;
    private Shortcut? _capture;
    private Shortcut? _toggle;
    private Shortcut? _eraser;
    private Shortcut? _cycle;

    public SettingsWindow(OverlayWindow overlay)
    {
        _overlay = overlay;
        _capture = overlay.Settings.CaptureShortcut;
        _toggle = overlay.Settings.ToggleShortcut;
        _eraser = overlay.Settings.EraserShortcut;
        _cycle = overlay.Settings.CycleShortcut;
        InitializeComponent();
        CaptureShortcutBox.Text = ShortcutText(_capture);
        ToggleShortcutBox.Text = ShortcutText(_toggle);
        EraserShortcutBox.Text = ShortcutText(_eraser);
        CycleShortcutBox.Text = ShortcutText(_cycle);
        CycleMouseBox.IsChecked = overlay.Settings.CycleIncludeMouse;
        CycleWritingBox.IsChecked = overlay.Settings.CycleIncludeWriting;
        CycleEraserBox.IsChecked = overlay.Settings.CycleIncludeEraser;
        StartWithWindowsBox.IsChecked = overlay.Settings.StartWithWindows;
        AskBeforeExitBox.IsChecked = overlay.Settings.AskBeforeExit;
        SolidBackgroundBox.IsChecked = overlay.Settings.UseSolidBackground;
        ColorBox.Text = overlay.Settings.BackgroundColor;
    }

    private static string ShortcutText(Shortcut? shortcut) => shortcut?.ToString() ?? "未设置";

    private void Shortcut_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Tab or Key.Escape) return;
        e.Handled = true;
        Shortcut? shortcut = null;
        if (e.Key is not (Key.Delete or Key.Back))
        {
            shortcut = new Shortcut(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
            if (!shortcut.IsValid) return;
        }
        if (sender == CaptureShortcutBox) _capture = shortcut;
        else if (sender == ToggleShortcutBox) _toggle = shortcut;
        else if (sender == CycleShortcutBox) _cycle = shortcut;
        else _eraser = shortcut;
        ((TextBox)sender).Text = ShortcutText(shortcut);
        ErrorText.Text = "";
    }

    private void ClearShortcut_OnClick(object sender, RoutedEventArgs e)
    {
        var box = (string)((Button)sender).Tag switch
        {
            "CaptureShortcutBox" => CaptureShortcutBox,
            "ToggleShortcutBox" => ToggleShortcutBox,
            "CycleShortcutBox" => CycleShortcutBox,
            _ => EraserShortcutBox
        };
        if (box == CaptureShortcutBox) _capture = null;
        else if (box == ToggleShortcutBox) _toggle = null;
        else if (box == CycleShortcutBox) _cycle = null;
        else _eraser = null;
        box.Text = ShortcutText(null);
        ErrorText.Text = "";
    }

    private void Swatch_OnClick(object sender, RoutedEventArgs e) => ColorBox.Text = (string)((Button)sender).Tag;

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Color_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (ColorPreview is null) return;
        try { ColorPreview.Background = new SolidColorBrush(AppSettings.ParseBackgroundColor(ColorBox.Text)); }
        catch (ArgumentException) { ColorPreview.Background = Brushes.Transparent; }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var settings = new AppSettings
        {
            CaptureShortcut = _capture, ToggleShortcut = _toggle,
            EraserShortcut = _eraser, CycleShortcut = _cycle,
            CycleIncludeMouse = CycleMouseBox.IsChecked == true,
            CycleIncludeWriting = CycleWritingBox.IsChecked == true,
            CycleIncludeEraser = CycleEraserBox.IsChecked == true,
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            AskBeforeExit = AskBeforeExitBox.IsChecked == true,
            ExitAction = _overlay.Settings.ExitAction,
            UseSolidBackground = SolidBackgroundBox.IsChecked == true,
            BackgroundColor = ColorBox.Text.Trim().ToUpperInvariant()
        };
        if (!_overlay.TryApplySettings(settings, out var error)) { ErrorText.Text = error; return; }
        DialogResult = true;
    }
}
