using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ThinkCanvas;

public partial class SettingsWindow : Window
{
    private readonly OverlayWindow _overlay;
    private Shortcut _capture;
    private Shortcut _toggle;
    private Shortcut _eraser;

    public SettingsWindow(OverlayWindow overlay)
    {
        _overlay = overlay;
        _capture = overlay.Settings.CaptureShortcut;
        _toggle = overlay.Settings.ToggleShortcut;
        _eraser = overlay.Settings.EraserShortcut;
        InitializeComponent();
        CaptureShortcutBox.Text = _capture.ToString();
        ToggleShortcutBox.Text = _toggle.ToString();
        EraserShortcutBox.Text = _eraser.ToString();
        StartWithWindowsBox.IsChecked = overlay.Settings.StartWithWindows;
        SolidBackgroundBox.IsChecked = overlay.Settings.UseSolidBackground;
        ColorBox.Text = overlay.Settings.BackgroundColor;
    }

    private void Shortcut_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Tab or Key.Escape) return;
        e.Handled = true;
        var shortcut = new Shortcut(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
        if (!shortcut.IsValid) return;
        if (sender == CaptureShortcutBox) _capture = shortcut;
        else if (sender == ToggleShortcutBox) _toggle = shortcut;
        else _eraser = shortcut;
        ((TextBox)sender).Text = shortcut.ToString();
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
            EraserShortcut = _eraser,
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            UseSolidBackground = SolidBackgroundBox.IsChecked == true,
            BackgroundColor = ColorBox.Text.Trim().ToUpperInvariant()
        };
        if (!_overlay.TryApplySettings(settings, out var error)) { ErrorText.Text = error; return; }
        DialogResult = true;
    }
}
