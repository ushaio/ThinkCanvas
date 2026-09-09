using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThinkCanvas;

public static class ScreenCapture
{
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(nint target, int x, int y, int width, int height,
        nint source, int sourceX, int sourceY, uint operation);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();

    public static Int32Rect GetDesktopBounds() => new(GetSystemMetrics(76), GetSystemMetrics(77),
        GetSystemMetrics(78), GetSystemMetrics(79));

    public static BitmapSource GrabDesktop(Int32Rect bounds)
    {
        DwmFlush();
        var source = GetDC(0);
        if (source == 0) throw new Win32Exception("无法读取桌面。");
        nint target = 0, bitmap = 0, previous = 0;
        try
        {
            target = CreateCompatibleDC(source);
            bitmap = CreateCompatibleBitmap(source, bounds.Width, bounds.Height);
            if (target == 0 || bitmap == 0) throw new Win32Exception("无法创建截图缓冲区。");
            previous = SelectObject(target, bitmap);
            if (!BitBlt(target, 0, 0, bounds.Width, bounds.Height, source, bounds.X, bounds.Y, 0x40CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            if (previous != 0) SelectObject(target, previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (target != 0) DeleteDC(target);
            ReleaseDC(0, source);
        }
    }

    public static BitmapSource Compose(Int32Rect bounds, BitmapSource? desktop, Color background,
        Matrix inkTransform, Action<DrawingContext> drawInk)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var rect = new Rect(0, 0, bounds.Width, bounds.Height);
            if (desktop is null) context.DrawRectangle(new SolidColorBrush(background), null, rect);
            else context.DrawImage(desktop, rect);
            context.PushTransform(new MatrixTransform(inkTransform));
            drawInk(context);
            context.Pop();
        }
        var result = new RenderTargetBitmap(bounds.Width, bounds.Height, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }
}
