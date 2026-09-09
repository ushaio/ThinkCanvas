using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkCanvas;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nuint extraInfo);
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            GetCursorPos(out var originalCursor);
            Window? target = null;
            OverlayWindow? overlay = null;
            ToolbarWindow? toolbar = null;
            var result = 0;
            try
            {
                TestPressureRendering();
                TestCapturePixelsAndSettings();
                target = new Window
                {
                    Title = "ThinkCanvas input test", Left = 80, Top = 80,
                    Width = 420, Height = 260, Background = Brushes.White,
                    Topmost = true
                };
                target.Show();
                overlay = new OverlayWindow(new AppSettings
                {
                    ToggleShortcut = new Shortcut(Key.F20, ModifierKeys.Control | ModifierKeys.Alt),
                    CaptureShortcut = new Shortcut(Key.F21, ModifierKeys.Control | ModifierKeys.Alt),
                    EraserShortcut = new Shortcut(Key.F19, ModifierKeys.Control | ModifierKeys.Alt)
                });
                toolbar = new ToolbarWindow(overlay);
                overlay.Toolbar = toolbar;
                overlay.Show();
                toolbar.Show();
                await Task.Delay(500);
                var screenPoint = target.PointToScreen(new Point(100, 100));
                var probe = new NativePoint { X = (int)screenPoint.X, Y = (int)screenPoint.Y };
                var targetHandle = new WindowInteropHelper(target).Handle;
                var overlayHandle = new WindowInteropHelper(overlay).Handle;
                TestHotkeyRollback(overlay, targetHandle);
                Assert(WindowFromPoint(probe) == targetHandle, "Operation mode reaches underlying window");
                Assert(((RadioButton)toolbar.FindName("OperateButton")).IsChecked == true,
                    "Operation segment reflects initial mode");
                Assert(!((Button)toolbar.FindName("ClearButton")).IsEnabled, "Empty canvas disables clear");
                ((Slider)toolbar.FindName("WidthSlider")).Value = 10;

                overlay.SetMode(OverlayMode.Writing);
                await Task.Delay(500);
                var canvas = (Canvas)overlay.FindName("DrawingCanvas");
                var writingBackground = canvas.Background;
                canvas.Background = Brushes.Transparent;
                await Task.Delay(200);
                Assert(WindowFromPoint(probe) != overlayHandle,
                    "Regression reproduced: alpha-zero blank pixels bypass the overlay");
                canvas.Background = writingBackground;
                await Task.Delay(200);
                Assert(WindowFromPoint(probe) == overlayHandle, "Writing mode receives input on blank pixels");
                SetCursorPos(probe.X, probe.Y);
                mouse_event(0x0002, 0, 0, 0, 0);
                await Task.Delay(100);
                for (var i = 1; i <= 8; i++)
                {
                    SetCursorPos(probe.X + i * 10, probe.Y + i * 4);
                    await Task.Delay(25);
                }
                mouse_event(0x0004, 0, 0, 0, 0);
                await Task.Delay(200);
                Assert(canvas.Children.OfType<PressureStrokeVisual>().Any(visual => visual.Strokes[0].StylusPoints.Count >= 3),
                    "Actual mouse drag creates a stroke through Windows input routing");
                Assert(canvas.Children.OfType<PressureStrokeVisual>().Last().Strokes[0].DrawingAttributes.Width == 10,
                    "Width slider changes new stroke width");
                Assert(((RadioButton)toolbar.FindName("ToggleButton")).IsChecked == true, "Writing segment follows mode changes");
                ((Button)toolbar.FindName("ClearButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(((Popup)toolbar.FindName("ClearPopup")).IsOpen && overlay.HasStrokes, "Clear requests confirmation without deleting ink");
                ((Popup)toolbar.FindName("ClearPopup")).IsOpen = false;
                ((RadioButton)toolbar.FindName("EraserButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(100);
                SetCursorPos(probe.X + 40, probe.Y + 16);
                mouse_event(0x0002, 0, 0, 0, 0);
                mouse_event(0x0004, 0, 0, 0, 0);
                await Task.Delay(100);
                Assert(overlay.Mode == OverlayMode.Erasing &&
                    ((RadioButton)toolbar.FindName("EraserButton")).IsChecked == true,
                    "Eraser button selects erasing mode");
                Assert(!overlay.HasStrokes && !((Button)toolbar.FindName("ClearButton")).IsEnabled,
                    "Eraser removes a hit stroke and updates toolbar state");
                overlay.SetMode(OverlayMode.Writing);
                ((Slider)toolbar.FindName("WidthSlider")).Value = 4;

                var frame = new RenderTargetBitmap((int)toolbar.ActualWidth, (int)toolbar.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                frame.Render(toolbar);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(frame));
                using (var output = File.Create("toolbar-smoke.png"))
                    encoder.Save(output);
                var border = (Border)toolbar.Content;
                var panel = (WrapPanel)border.Child;
                foreach (FrameworkElement child in panel.Children)
                {
                    if (child is Popup) continue;
                    var rect = child.TransformToAncestor(toolbar).TransformBounds(new Rect(child.RenderSize));
                    Assert(rect.Right <= toolbar.ActualWidth && rect.Bottom <= toolbar.ActualHeight,
                        $"Toolbar {child.GetType().Name} fits ({rect.Right:0}/{toolbar.ActualWidth:0})");
                }
                var originalMaxWidth = toolbar.MaxWidth;
                toolbar.MaxWidth = 320;
                toolbar.UpdateLayout();
                await Task.Delay(100);
                SaveWindowImage(toolbar, "toolbar-compact-smoke.png");
                foreach (var button in Descendants(toolbar).OfType<ButtonBase>())
                {
                    var rect = button.TransformToAncestor(toolbar).TransformBounds(new Rect(button.RenderSize));
                    Assert(rect.Right <= toolbar.ActualWidth + 1 && rect.Bottom <= toolbar.ActualHeight + 1,
                        "Compact toolbar keeps controls inside window");
                }
                toolbar.MaxWidth = originalMaxWidth;
                toolbar.UpdateLayout();
                await Task.Delay(100);
                var toolbarPoint = toolbar.PointToScreen(new Point(25, 25));
                Assert(WindowFromPoint(new NativePoint { X = (int)toolbarPoint.X, Y = (int)toolbarPoint.Y })
                    == new WindowInteropHelper(toolbar).Handle, "Toolbar remains above the writing overlay");

                SetCursorPos(probe.X, probe.Y);
                mouse_event(0x0002, 0, 0, 0, 0);
                await Task.Delay(100);
                overlay.Clear();
                Assert(!canvas.IsMouseCaptured, "Clear releases input during an active stroke");
                mouse_event(0x0004, 0, 0, 0, 0);
                await Task.Delay(100);

                mouse_event(0x0002, 0, 0, 0, 0);
                await Task.Delay(100);
                SetCursorPos(probe.X + 30, probe.Y + 20);
                await Task.Delay(100);
                mouse_event(0x0004, 0, 0, 0, 0);
                await Task.Delay(100);
                Assert(canvas.Children.OfType<PressureStrokeVisual>().Any(visual => visual.Strokes[0].StylusPoints.Count >= 2),
                    "Drawing resumes after clearing an active stroke");
                overlay.SetMode(OverlayMode.Passthrough);
                await Task.Delay(300);
                Assert(WindowFromPoint(probe) == targetHandle, "Operation mode restores passthrough after drawing");
                await TestScreenshotWorkflow(overlay, toolbar, probe);
                var settingsWindow = new SettingsWindow(overlay);
                settingsWindow.Show();
                await Task.Delay(100);
                SaveWindowImage(settingsWindow, "settings-smoke.png");
                settingsWindow.Close();
                ((Button)toolbar.FindName("ClearButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var clearPopup = (Popup)toolbar.FindName("ClearPopup");
                var confirmation = Descendants(clearPopup.Child).OfType<Button>().Single(button => Equals(button.Content, "清空"));
                confirmation.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(canvas.Children.Count == 0 && !clearPopup.IsOpen, "Confirming clear removes strokes and dismisses popup");
                Assert(!((Button)toolbar.FindName("ClearButton")).IsEnabled, "Clear disables after removing strokes");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                result = 1;
            }
            finally
            {
                mouse_event(0x0004, 0, 0, 0, 0);
                toolbar?.Close();
                overlay?.Close();
                target?.Close();
                SetCursorPos(originalCursor.X, originalCursor.Y);
                app.Shutdown(result);
            }
        };
        return app.Run();
    }

    private static void TestPressureRendering()
    {
        var stroke = new ThinkCanvas.Stroke { Width = 12, HasPressure = true };
        for (int x = 20; x <= 180; x += 2)
            stroke.Points.Add(new StrokePoint(new Point(x, 32), x < 100 ? 0.1f : 0.9f, x));
        var visual = new PressureStrokeVisual(stroke);
        visual.Measure(new Size(200, 64));
        visual.Arrange(new Rect(0, 0, 200, 64));
        var bitmap = new RenderTargetBitmap(200, 64, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[200 * 64 * 4];
        bitmap.CopyPixels(pixels, 200 * 4, 0);
        int Thickness(int x) => Enumerable.Range(0, 64).Count(y => pixels[(y * 200 + x) * 4 + 3] > 32);
        Assert(Thickness(50) > 0 && Thickness(150) > Thickness(50) * 2,
            $"Pressure changes actual pixels within a stroke ({Thickness(50)}px -> {Thickness(150)}px)");

        var mouseStroke = new ThinkCanvas.Stroke { Width = 12, HasPressure = false };
        mouseStroke.Points.AddRange(stroke.Points);
        var mouseVisual = new PressureStrokeVisual(mouseStroke);
        Assert(mouseVisual.Strokes[0].DrawingAttributes.IgnorePressure,
            "Inputs without pressure retain constant width");
    }

    private static void SaveWindowImage(Window window, string path)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void TestCapturePixelsAndSettings()
    {
        var bounds = new Int32Rect(-1920, -200, 120, 80);
        var blue = Color.FromRgb(30, 100, 200);
        var image = ScreenCapture.Compose(bounds, null, blue, new Matrix(2, 0, 0, 2, 10, 10),
            dc => dc.DrawRectangle(Brushes.Red, null, new Rect(5, 5, 10, 5)));
        var pixels = new byte[120 * 80 * 4];
        image.CopyPixels(pixels, 120 * 4, 0);
        Assert(pixels[0] == 200 && pixels[1] == 100 && pixels[2] == 30 && pixels[3] == 255,
            "Solid background export uses exact opaque custom color");
        int ink = (25 * 120 + 25) * 4;
        Assert(pixels[ink + 2] == 255 && pixels[ink + 1] == 0,
            "Ink is transformed into physical pixels over solid background");
        var region = RegionCaptureWindow.ToPixelRect(new Point(50, 30), new Point(10, 5), new Size(60, 40), 120, 80);
        Assert(region == new Int32Rect(20, 10, 80, 50), "Reverse drag with scaled coordinates selects correct pixels");
        var clipped = RegionCaptureWindow.ToPixelRect(new Point(-5, -5), new Point(90, 60), new Size(60, 40), 120, 80);
        Assert(clipped == new Int32Rect(0, 0, 120, 80), "Selection outside screen is clamped");
        var desktopImage = ScreenCapture.Compose(bounds, image, Colors.White, Matrix.Identity, _ => { });
        desktopImage.CopyPixels(pixels, 120 * 4, 0);
        Assert(pixels[0] == 200, "Desktop background is retained when solid fill is disabled");
        var settings = new AppSettings { UseSolidBackground = true, BackgroundColor = "#123456", StartWithWindows = true };
        settings.Validate();
        var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(settings));
        Assert(restored == settings, "Shortcuts and capture background survive JSON round trip");
        var path = Path.Combine(AppContext.BaseDirectory, "settings-test-" + Guid.NewGuid() + ".json");
        try
        {
            SettingsStore.Save(settings, path);
            Assert(SettingsStore.Load(path) == settings, "Settings save and reload from disk");
            var changed = settings with { BackgroundColor = "#ABCDEF" };
            SettingsStore.Save(changed, path);
            Assert(SettingsStore.Load(path) == changed, "Existing settings are replaced atomically");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        try { (settings with { ToggleShortcut = settings.CaptureShortcut }).Validate(); throw new Exception("Duplicate shortcuts accepted"); }
        catch (ArgumentException) { Console.WriteLine("PASS: Duplicate shortcuts rejected"); }
        try { (settings with { EraserShortcut = settings.CaptureShortcut }).Validate(); throw new Exception("Duplicate eraser shortcut accepted"); }
        catch (ArgumentException) { Console.WriteLine("PASS: Duplicate eraser shortcut rejected"); }
        try { (settings with { BackgroundColor = "#00FFFFFF" }).Validate(); throw new Exception("Transparent background accepted"); }
        catch (ArgumentException) { Console.WriteLine("PASS: Invalid background rejected"); }
    }

    private static void TestHotkeyRollback(OverlayWindow overlay, nint target)
    {
        var previous = overlay.Settings;
        Assert(RegisterHotKey(target, 73, 3, (uint)KeyInterop.VirtualKeyFromKey(Key.F22)), "Reserve competing shortcut");
        try
        {
            var success = overlay.TryApplySettings(previous with
            {
                CaptureShortcut = new Shortcut(Key.F22, ModifierKeys.Control | ModifierKeys.Alt)
            }, out var error);
            Assert(!success && error.Length > 0 && overlay.Settings == previous &&
                overlay.CaptureHotkeyAvailable && overlay.ToggleHotkeyAvailable && overlay.EraserHotkeyAvailable,
                "Shortcut conflict restores all previous bindings without saving");
        }
        finally { UnregisterHotKey(target, 73); }
    }

    private static async Task<T> WaitForWindow<T>() where T : Window
    {
        for (int i = 0; i < 60; i++)
        {
            var window = Application.Current.Windows.OfType<T>().FirstOrDefault(w => w.IsVisible);
            if (window is not null) return window;
            await Task.Delay(50);
        }
        throw new TimeoutException(typeof(T).Name + " did not open");
    }

    private static async Task TestScreenshotWorkflow(OverlayWindow overlay, ToolbarWindow toolbar, NativePoint probe)
    {
        var mode = overlay.Mode;
        toolbar.Left -= 40;
        toolbar.Top -= 30;
        var toolbarPosition = new Point(toolbar.Left, toolbar.Top);
        ((Button)toolbar.FindName("CaptureButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var selection = await WaitForWindow<RegionCaptureWindow>();
        await Task.Delay(200);
        Assert(!toolbar.IsVisible && !overlay.IsVisible, "Capture hides toolbar and overlay");
        SetCursorPos(probe.X - 20, probe.Y - 20);
        mouse_event(0x0002, 0, 0, 0, 0);
        await Task.Delay(100);
        SetCursorPos(probe.X + 100, probe.Y + 70);
        await Task.Delay(100);
        mouse_event(0x0004, 0, 0, 0, 0);
        var result = await WaitForWindow<CaptureResultWindow>();
        var image = (BitmapSource)((Image)result.FindName("PreviewImage")).Source;
        Assert(image.PixelWidth is >= 119 and <= 121 && image.PixelHeight is >= 89 and <= 91,
            $"Screenshot button and actual drag produce a correctly sized preview ({image.PixelWidth} x {image.PixelHeight})");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var stream = File.Create("capture-smoke.png")) encoder.Save(stream);
        result.Close();
        var pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(pixels, image.PixelWidth * 4, 0);
        Assert(Enumerable.Range(0, image.PixelWidth * image.PixelHeight)
            .Any(i => pixels[i * 4 + 2] > 180 && pixels[i * 4 + 1] < 100 && pixels[i * 4] < 100),
            "Captured region includes red ink pixels");
        await Task.Delay(200);
        Assert(!overlay.IsBusy && toolbar.IsVisible && overlay.IsVisible && overlay.Mode == mode,
            "Closing capture restores toolbar and previous mode");
        Assert(Math.Abs(toolbar.Left - toolbarPosition.X) < 2 && Math.Abs(toolbar.Top - toolbarPosition.Y) < 2,
            "Capture retains manually placed toolbar position");
        ((Button)toolbar.FindName("CaptureButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var cancel = await WaitForWindow<RegionCaptureWindow>();
        cancel.Close();
        await Task.Delay(200);
        Assert(!overlay.IsBusy && toolbar.IsVisible, "Cancelling selection restores application");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
