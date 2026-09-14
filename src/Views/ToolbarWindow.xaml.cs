using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ThinkCanvas;

public partial class ToolbarWindow : Window
{
    private readonly OverlayWindow _overlay;
    private readonly CaptureService _captureService;
    private bool _positionInitialized;

    public ToolbarWindow(OverlayWindow overlay)
    {
        _overlay = overlay;
        InitializeComponent();
        _captureService = new CaptureService(overlay, this);
        _overlay.CaptureRequested += OnCaptureRequested;
        Closed += (_, _) =>
        {
            _overlay.CaptureRequested -= OnCaptureRequested;
            _overlay.PressureChanged -= OnPressureChanged;
            _overlay.StrokesChanged -= OnStrokesChanged;
        };
        Loaded += OnLoaded;
        _overlay.PressureChanged += OnPressureChanged;
        _overlay.StrokesChanged += OnStrokesChanged;
        MaxWidth = Math.Max(220, SystemParameters.WorkArea.Width - 24);
        SetMode(_overlay.Mode);
        SizeChanged += (_, _) =>
        {
            if (_positionInitialized && IsVisible)
                NativeMethods.ClampToWorkArea(new WindowInteropHelper(this).Handle);
        };
    }

    public void SetMode(OverlayMode mode)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetMode(mode));
            return;
        }

        var writing = mode == OverlayMode.Writing;
        var erasing = mode == OverlayMode.Erasing;
        ToggleButton.IsChecked = writing;
        EraserButton.IsChecked = erasing;
        OperateButton.IsChecked = mode == OverlayMode.Passthrough;
        if (!writing)
        {
            PressureBar.Value = 0;
            PressureText.Text = "--";
            ClosePopups();
        }
        SetHotkeyAvailable(_overlay.ToggleHotkeyAvailable);
    }

    public void EnsureAboveOverlay()
    {
        Dispatcher.InvokeAsync(() =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != 0)
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // An owned toolbar stays above its overlay even when drawing changes z-order.
        Owner = _overlay;
        if (!_positionInitialized)
        {
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - ActualWidth) / 2;
            Top = SystemParameters.WorkArea.Bottom - ActualHeight - 18;
            _positionInitialized = true;
        }
        SetHotkeyAvailable(_overlay.ToggleHotkeyAvailable);
    }

    public void SetHotkeyAvailable(bool available)
    {
        Dispatcher.InvokeAsync(() =>
        {
            ToggleButton.ToolTip = HotkeyText("切换模式", _overlay.Settings.ToggleShortcut, _overlay.ToggleHotkeyAvailable);
            OperateButton.ToolTip = ToggleButton.ToolTip;
            CaptureButton.ToolTip = HotkeyText("截图", _overlay.Settings.CaptureShortcut, _overlay.CaptureHotkeyAvailable);
            EraserButton.ToolTip = HotkeyText("橡皮擦", _overlay.Settings.EraserShortcut, _overlay.EraserHotkeyAvailable);
        });
    }

    private static string HotkeyText(string action, Shortcut? shortcut, bool registered) =>
        shortcut is null ? $"{action}（未设置快捷键）" :
        registered ? $"{action} ({shortcut})" : $"{action}（快捷键已被占用）";

    private void OnPressureChanged(float? pressure)
    {
        Dispatcher.InvokeAsync(() =>
        {
            PressureText.Text = pressure is { } value ? $"{value:P0}" : "--";
            PressureText.ToolTip = pressure.HasValue ? "笔压" : "当前输入未提供压力";
            PressureBar.Value = pressure ?? 0;
        });
    }

    private void DragHandle_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        WidthPopup.IsOpen = ClearPopup.IsOpen = false;
        DragMove();
        NativeMethods.ClampToWorkArea(new WindowInteropHelper(this).Handle);
        e.Handled = true;
    }

    private void ToggleButton_OnClick(object sender, RoutedEventArgs e) => _overlay.SetMode(OverlayMode.Writing);
    private void EraserButton_OnClick(object sender, RoutedEventArgs e) => _overlay.SetMode(OverlayMode.Erasing);
    private void OperateButton_OnClick(object sender, RoutedEventArgs e) => _overlay.SetMode(OverlayMode.Passthrough);
    private void RedButton_OnClick(object sender, RoutedEventArgs e) => _overlay.SetColor(Colors.Red);
    private void BlueButton_OnClick(object sender, RoutedEventArgs e) => _overlay.SetColor(Color.FromRgb(23, 105, 210));
    private void BlackButton_OnClick(object sender, RoutedEventArgs e) => _overlay.SetColor(Color.FromRgb(37, 41, 46));
    private void ClearButton_OnClick(object sender, RoutedEventArgs e) => ClearPopup.IsOpen = !ClearPopup.IsOpen;
    private void CancelClear_OnClick(object sender, RoutedEventArgs e) => ClearPopup.IsOpen = false;
    private void ConfirmClear_OnClick(object sender, RoutedEventArgs e) { ClearPopup.IsOpen = false; _overlay.Clear(); }
    private void OnStrokesChanged() => ClearButton.IsEnabled = _overlay.HasStrokes;
    private void WidthButton_OnClick(object sender, RoutedEventArgs e) => WidthPopup.IsOpen = !WidthPopup.IsOpen;
    private void WidthSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WidthDot is null || WidthValue is null) return;
        _overlay.SetWidth(e.NewValue);
        WidthDot.Width = WidthDot.Height = e.NewValue;
        WidthValue.Text = $"{e.NewValue:0} px";
        WidthButton.ToolTip = $"笔宽 {e.NewValue:0} px";
    }
    private void ClosePopups() => WidthPopup.IsOpen = ClearPopup.IsOpen = false;
    private async void OnCaptureRequested()
    {
        ClosePopups();
        await _captureService.CaptureAsync();
    }
    private void CaptureButton_OnClick(object sender, RoutedEventArgs e) => OnCaptureRequested();
    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_overlay.IsBusy) return;
        ClosePopups();
        var previous = _overlay.Mode;
        _overlay.IsBusy = true;
        _overlay.SetMode(OverlayMode.Passthrough);
        _overlay.SuspendShortcuts();
        try { new SettingsWindow(_overlay) { Owner = this }.ShowDialog(); }
        finally { _overlay.ResumeShortcuts(); _overlay.IsBusy = false; _overlay.SetMode(previous); }
    }
    private void ExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        ClosePopups();
        var settings = _overlay.Settings;
        if (!settings.AskBeforeExit)
        {
            ExecuteExit(settings.ExitAction == "Tray");
            return;
        }
        if (_overlay.IsBusy) return;
        var dialog = new ExitDialog { Owner = this };
        _overlay.IsBusy = true;
        bool? confirmed;
        try { confirmed = dialog.ShowDialog(); }
        finally { _overlay.IsBusy = false; }
        if (confirmed != true) return;
        if (dialog.RememberChoice)
            _overlay.TryUpdateExitPreference(false, dialog.MinimizeToTray ? "Tray" : "Exit");
        ExecuteExit(dialog.MinimizeToTray);
    }

    private void ExecuteExit(bool minimizeToTray)
    {
        if (minimizeToTray)
            ((App)Application.Current).EnterTrayMode();
        else
            Application.Current.Shutdown();
    }
}
