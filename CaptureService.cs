using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThinkCanvas;

public sealed class CaptureService(OverlayWindow overlay, ToolbarWindow toolbar)
{
    public async Task CaptureAsync()
    {
        if (overlay.IsBusy) return;
        var previousMode = overlay.Mode;
        overlay.IsBusy = true;
        try
        {
            overlay.SetMode(OverlayMode.Passthrough);
            // Translate the existing ink coordinates into physical desktop pixels.
            var origin = overlay.PointToScreen(new Point());
            var dpi = VisualTreeHelper.GetDpi(overlay);
            var bounds = ScreenCapture.GetDesktopBounds();
            var transform = new Matrix(dpi.DpiScaleX, 0, 0, dpi.DpiScaleY,
                origin.X - bounds.X, origin.Y - bounds.Y);
            toolbar.Hide();
            overlay.Hide();
            await Task.Delay(160);

            var settings = overlay.Settings;
            var desktop = settings.UseSolidBackground ? null : ScreenCapture.GrabDesktop(bounds);
            var frame = ScreenCapture.Compose(bounds, desktop,
                AppSettings.ParseBackgroundColor(settings.BackgroundColor), transform, overlay.DrawInk);
            var selectionWindow = new RegionCaptureWindow(frame, bounds);
            if (selectionWindow.ShowDialog() != true || selectionWindow.Selection is not { } region) return;
            var image = new CroppedBitmap(frame, region);
            image.Freeze();
            new CaptureResultWindow(image).ShowDialog();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"截图失败：{exception.Message}", "ThinkCanvas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            overlay.Show();
            toolbar.Show();
            overlay.IsBusy = false;
            overlay.SetMode(previousMode);
        }
    }
}
