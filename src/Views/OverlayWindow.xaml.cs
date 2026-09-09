using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;

namespace ThinkCanvas;

public partial class OverlayWindow : Window
{
    private enum InputSource
    {
        None,
        NativePen,
        Stylus,
        Mouse
    }

    private readonly List<Stroke> _strokes = [];
    private Stroke? _activeStroke;
    private PressureStrokeVisual? _activeVisual;
    private InputSource _activeInput;
    private nint _hwnd;
    private OverlayMode _mode;
    private bool _toggleHotkeyRegistered;
    private bool _captureHotkeyRegistered;
    private bool _eraserHotkeyRegistered;
    private bool _escapeHotkeyRegistered;
    private double _width = 4;
    private Color _color = Colors.Red;
    public ToolbarWindow? Toolbar { get; set; }
    public event Action<float?>? PressureChanged;
    public bool ToggleHotkeyAvailable => _toggleHotkeyRegistered;
    public bool CaptureHotkeyAvailable => _captureHotkeyRegistered;
    public bool EraserHotkeyAvailable => _eraserHotkeyRegistered;
    public AppSettings Settings { get; private set; }
    public OverlayMode Mode => _mode;
    public bool IsBusy { get; set; }
    public event Action? CaptureRequested;
    public event Action? StrokesChanged;
    public bool HasStrokes => _strokes.Count != 0;

    public OverlayWindow(AppSettings? settings = null)
    {
        Settings = settings ?? new AppSettings();
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) => UnregisterHotkeys();
        PreviewStylusDown += OnStylusDown;
        PreviewStylusMove += OnStylusMove;
        PreviewStylusUp += OnStylusUp;
        PreviewMouseLeftButtonDown += OnMouseDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseUp;
        DrawingCanvas.LostMouseCapture += (_, _) =>
        {
            if (_activeInput == InputSource.Mouse)
                FinishStroke();
        };
        DrawingCanvas.LostStylusCapture += (_, _) =>
        {
            if (_activeInput == InputSource.Stylus)
                FinishStroke();
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        RegisterHotkeys();
        SetMode(OverlayMode.Passthrough);
    }

    public void SetMode(OverlayMode mode)
    {
        if (_mode != mode)
            FinishStroke();
        _mode = mode;
        // Layered windows skip alpha-zero pixels before WPF input hit testing.
        // Keep blank pixels hittable while writing; the tint is only 1/255 black.
        DrawingCanvas.Background = mode != OverlayMode.Passthrough
            ? new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))
            : Brushes.Transparent;
        DrawingCanvas.Cursor = mode != OverlayMode.Passthrough ? Cursors.Cross : null;
        if (_hwnd == 0)
            return;

        var style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        if (mode == OverlayMode.Passthrough)
            style |= NativeMethods.WS_EX_TRANSPARENT;
        else
            style &= ~NativeMethods.WS_EX_TRANSPARENT;

        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);

        UpdateEscapeHotkey(mode != OverlayMode.Passthrough);
        Toolbar?.SetMode(mode);
        Toolbar?.EnsureAboveOverlay();
    }

    public void Clear()
    {
        FinishStroke();
        _strokes.Clear();
        DrawingCanvas.Children.Clear();
        StrokesChanged?.Invoke();
    }

    public void ToggleMode() => SetMode(_mode == OverlayMode.Writing ? OverlayMode.Passthrough : OverlayMode.Writing);

    public void SetColor(Color color) => _color = color;
    public void SetWidth(double width) => _width = Math.Clamp(width, 1, 20);

    private void RegisterHotkeys()
    {
        _toggleHotkeyRegistered = RegisterShortcut(1, Settings.ToggleShortcut);
        _captureHotkeyRegistered = RegisterShortcut(3, Settings.CaptureShortcut);
        _eraserHotkeyRegistered = RegisterShortcut(4, Settings.EraserShortcut);
        Toolbar?.SetHotkeyAvailable(_toggleHotkeyRegistered);
    }

    private bool RegisterShortcut(int id, Shortcut shortcut) => NativeMethods.RegisterHotKey(_hwnd, id,
        (uint)shortcut.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(shortcut.Key));

    public void SuspendShortcuts()
    {
        NativeMethods.UnregisterHotKey(_hwnd, 1);
        NativeMethods.UnregisterHotKey(_hwnd, 3);
        NativeMethods.UnregisterHotKey(_hwnd, 4);
        _toggleHotkeyRegistered = _captureHotkeyRegistered = _eraserHotkeyRegistered = false;
    }

    public void ResumeShortcuts()
    {
        SuspendShortcuts();
        RegisterHotkeys();
    }

    public bool TryApplySettings(AppSettings settings, out string error)
    {
        var previous = Settings;
        try { settings.Validate(); }
        catch (ArgumentException exception) { error = exception.Message; return false; }

        NativeMethods.UnregisterHotKey(_hwnd, 1);
        NativeMethods.UnregisterHotKey(_hwnd, 3);
        NativeMethods.UnregisterHotKey(_hwnd, 4);
        _toggleHotkeyRegistered = RegisterShortcut(1, settings.ToggleShortcut);
        _captureHotkeyRegistered = RegisterShortcut(3, settings.CaptureShortcut);
        _eraserHotkeyRegistered = RegisterShortcut(4, settings.EraserShortcut);
        error = !_toggleHotkeyRegistered ? $"切换快捷键 {settings.ToggleShortcut} 已被占用。" :
            !_captureHotkeyRegistered ? $"截图快捷键 {settings.CaptureShortcut} 已被占用。" :
            !_eraserHotkeyRegistered ? $"橡皮擦快捷键 {settings.EraserShortcut} 已被占用。" : "";
        if (error.Length == 0)
        {
            try
            {
                StartupManager.SetEnabled(settings.StartWithWindows);
                SettingsStore.Save(settings);
            }
            catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { error = $"无法保存设置：{exception.Message}"; }
        }
        if (error.Length != 0)
        {
            NativeMethods.UnregisterHotKey(_hwnd, 1);
            NativeMethods.UnregisterHotKey(_hwnd, 3);
            NativeMethods.UnregisterHotKey(_hwnd, 4);
            try { StartupManager.SetEnabled(previous.StartWithWindows); }
            catch { }
            Settings = previous;
            RegisterHotkeys();
            return false;
        }
        Settings = settings;
        Toolbar?.SetHotkeyAvailable(true);
        return true;
    }

    public void DrawInk(DrawingContext context)
    {
        foreach (var visual in DrawingCanvas.Children.OfType<PressureStrokeVisual>())
            foreach (var stroke in visual.Strokes)
                stroke.Draw(context);
    }

    private void UnregisterHotkeys()
    {
        if (_hwnd == 0)
            return;
        if (_toggleHotkeyRegistered)
            NativeMethods.UnregisterHotKey(_hwnd, 1);
        if (_captureHotkeyRegistered)
            NativeMethods.UnregisterHotKey(_hwnd, 3);
        if (_eraserHotkeyRegistered)
            NativeMethods.UnregisterHotKey(_hwnd, 4);
        if (_escapeHotkeyRegistered)
            NativeMethods.UnregisterHotKey(_hwnd, 2);
    }

    private void UpdateEscapeHotkey(bool shouldRegister)
    {
        if (_hwnd == 0 || _escapeHotkeyRegistered == shouldRegister)
            return;
        if (shouldRegister)
            _escapeHotkeyRegistered = NativeMethods.RegisterHotKey(
                _hwnd, 2, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_ESCAPE);
        else
        {
            NativeMethods.UnregisterHotKey(_hwnd, 2);
            _escapeHotkeyRegistered = false;
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_HOTKEY:
                if (IsBusy)
                {
                    handled = true;
                    break;
                }
                if (wParam.ToInt32() == 1)
                    ToggleMode();
                else if (wParam.ToInt32() == 2)
                    SetMode(OverlayMode.Passthrough);
                else if (wParam.ToInt32() == 3)
                    CaptureRequested?.Invoke();
                else if (wParam.ToInt32() == 4)
                    SetMode(OverlayMode.Erasing);
                handled = true;
                break;
            case NativeMethods.WM_NCHITTEST when _mode == OverlayMode.Passthrough:
                handled = true;
                return new nint(NativeMethods.HTTRANSPARENT);
            case NativeMethods.WM_POINTERDOWN:
                if (_mode != OverlayMode.Passthrough)
                    handled = HandlePointer(wParam, true, false);
                break;
            case NativeMethods.WM_POINTERUPDATE:
                if (_mode != OverlayMode.Passthrough)
                    handled = HandlePointer(wParam, false, false);
                break;
            case NativeMethods.WM_POINTERUP:
                if (_mode != OverlayMode.Passthrough)
                    handled = HandlePointer(wParam, false, true);
                break;
        }
        return 0;
    }

    private bool HandlePointer(nint wParam, bool down, bool up)
    {
        var pointerId = (uint)(wParam.ToInt64() & 0xFFFF);
        if (!NativeMethods.GetPointerType(pointerId, out var type) || type != NativeMethods.PT_PEN)
            return false;
        if (!NativeMethods.GetPointerPenInfo(pointerId, out var info))
            return false;

        // A native pen message owns the stroke. WPF stylus/mouse promotion is
        // suppressed by marking this message handled in WndProc.
        if (_activeInput is not (InputSource.None or InputSource.NativePen))
            return true;

        var point = info.PointerInfo.PtPixelLocation;
        var hasPressure = (info.PenMask & 0x00000001) != 0;
        var pressure = hasPressure ? Math.Clamp(info.Pressure / 1024f, 0f, 1f) : 0.5f;
        var local = PointFromScreen(new Point(point.X, point.Y));
        var penPoint = new StrokePoint(local, pressure, Stopwatch.GetTimestamp());

        if (down)
        {
            if (_mode == OverlayMode.Writing)
                StartStroke(penPoint, InputSource.NativePen, hasPressure);
            else
                StartErasing(penPoint.Position, InputSource.NativePen);
        }
        else if (_activeInput == InputSource.NativePen)
        {
            if (_mode == OverlayMode.Writing)
                AppendPoint(penPoint);
            else
                EraseAt(penPoint.Position);
        }

        if (up && _activeInput == InputSource.NativePen)
            FinishStroke();
        return true;
    }

    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        if (_mode == OverlayMode.Passthrough || _activeInput != InputSource.None)
            return;

        var points = e.GetStylusPoints(DrawingCanvas);
        if (points.Count == 0)
            return;
        var hasPressure = e.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus &&
            e.StylusDevice.TabletDevice.TabletHardwareCapabilities.HasFlag(TabletHardwareCapabilities.SupportsPressure);
        if (_mode == OverlayMode.Writing)
            StartStroke(ToStrokePoint(points[0]), InputSource.Stylus, hasPressure);
        else
            StartErasing(points[0].ToPoint(), InputSource.Stylus);
        for (var i = 1; i < points.Count; i++)
        {
            if (_mode == OverlayMode.Writing)
                AppendPoint(ToStrokePoint(points[i]));
            else
                EraseAt(points[i].ToPoint());
        }
        DrawingCanvas.CaptureStylus();
        e.Handled = true;
    }

    private void OnStylusMove(object sender, StylusEventArgs e)
    {
        if (_mode == OverlayMode.Passthrough || _activeInput != InputSource.Stylus)
            return;
        foreach (var point in e.GetStylusPoints(DrawingCanvas))
        {
            if (_mode == OverlayMode.Writing)
                AppendPoint(ToStrokePoint(point));
            else
                EraseAt(point.ToPoint());
        }
        e.Handled = true;
    }

    private void OnStylusUp(object sender, StylusEventArgs e)
    {
        if (_activeInput != InputSource.Stylus)
            return;
        foreach (var point in e.GetStylusPoints(DrawingCanvas))
        {
            if (_mode == OverlayMode.Writing)
                AppendPoint(ToStrokePoint(point));
            else
                EraseAt(point.ToPoint());
        }
        FinishStroke();
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_mode == OverlayMode.Passthrough || _activeInput != InputSource.None)
            return;
        var point = e.GetPosition(DrawingCanvas);
        if (_mode == OverlayMode.Writing)
            StartStroke(new StrokePoint(point, 0.5f, Stopwatch.GetTimestamp()), InputSource.Mouse);
        else
            StartErasing(point, InputSource.Mouse);
        DrawingCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_mode == OverlayMode.Passthrough || _activeInput != InputSource.Mouse || e.LeftButton != MouseButtonState.Pressed)
            return;
        var point = e.GetPosition(DrawingCanvas);
        if (_mode == OverlayMode.Writing)
            AppendPoint(new StrokePoint(point, 0.5f, Stopwatch.GetTimestamp()));
        else
            EraseAt(point);
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeInput != InputSource.Mouse)
            return;
        var point = e.GetPosition(DrawingCanvas);
        if (_mode == OverlayMode.Writing)
            AppendPoint(new StrokePoint(point, 0.5f, Stopwatch.GetTimestamp()));
        else
            EraseAt(point);
        FinishStroke();
        e.Handled = true;
    }

    private static StrokePoint ToStrokePoint(StylusPoint point) =>
        new(new Point(point.X, point.Y), point.PressureFactor, Stopwatch.GetTimestamp());

    private void StartStroke(StrokePoint point, InputSource source, bool hasPressure = false)
    {
        _activeInput = source;
        _activeStroke = new Stroke { Color = _color, Width = _width, HasPressure = hasPressure };
        _activeStroke.Points.Add(point);
        _strokes.Add(_activeStroke);
        PressureChanged?.Invoke(hasPressure ? point.Pressure : null);
        _activeVisual = new PressureStrokeVisual(_activeStroke);
        DrawingCanvas.Children.Add(_activeVisual);
        StrokesChanged?.Invoke();
    }

    private void StartErasing(Point point, InputSource source)
    {
        _activeInput = source;
        PressureChanged?.Invoke(null);
        EraseAt(point);
    }

    private void EraseAt(Point point)
    {
        var changed = false;
        for (var i = DrawingCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (DrawingCanvas.Children[i] is not PressureStrokeVisual visual ||
                visual.Strokes.HitTest(point, 18).Count == 0)
                continue;
            DrawingCanvas.Children.RemoveAt(i);
            _strokes.RemoveAt(i);
            changed = true;
        }
        if (changed)
            StrokesChanged?.Invoke();
    }

    private void AppendPoint(StrokePoint point)
    {
        if (_activeStroke is null)
            return;
        _activeStroke.Points.Add(point);
        PressureChanged?.Invoke(_activeStroke.HasPressure ? point.Pressure : null);
        _activeVisual?.Append(point);
    }

    private void FinishStroke()
    {
        _activeStroke = null;
        _activeVisual = null;
        _activeInput = InputSource.None;
        if (DrawingCanvas.IsMouseCaptured)
            DrawingCanvas.ReleaseMouseCapture();
        if (DrawingCanvas.IsStylusCaptured)
            DrawingCanvas.ReleaseStylusCapture();
    }

}
