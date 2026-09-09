using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThinkCanvas;

public sealed class RegionCaptureWindow : Window
{
    private readonly SelectionSurface _surface;
    public Int32Rect? Selection { get; private set; }

    public RegionCaptureWindow(BitmapSource image, Int32Rect bounds)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        Cursor = Cursors.Cross;
        _surface = new SelectionSurface(image);
        Content = _surface;
        SourceInitialized += (_, _) => NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle,
            NativeMethods.HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0);
        Loaded += (_, _) => Activate();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
        };
        MouseRightButtonDown += (_, _) => DialogResult = false;
        _surface.Completed += rect => { Selection = rect; DialogResult = true; };
    }

    public static Int32Rect ToPixelRect(Point start, Point end, Size surfaceSize, int pixelWidth, int pixelHeight)
    {
        var scaleX = pixelWidth / surfaceSize.Width;
        var scaleY = pixelHeight / surfaceSize.Height;
        var left = (int)Math.Clamp(Math.Floor(Math.Min(start.X, end.X) * scaleX), 0, pixelWidth);
        var top = (int)Math.Clamp(Math.Floor(Math.Min(start.Y, end.Y) * scaleY), 0, pixelHeight);
        var right = (int)Math.Clamp(Math.Ceiling(Math.Max(start.X, end.X) * scaleX), left, pixelWidth);
        var bottom = (int)Math.Clamp(Math.Ceiling(Math.Max(start.Y, end.Y) * scaleY), top, pixelHeight);
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private sealed class SelectionSurface(BitmapSource image) : FrameworkElement
    {
        private Point? _start;
        private Point _end;
        public event Action<Int32Rect>? Completed;

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var full = new Rect(RenderSize);
            dc.DrawImage(image, full);
            var mask = new GeometryGroup { FillRule = FillRule.EvenOdd };
            mask.Children.Add(new RectangleGeometry(full));
            if (_start is { } start)
                mask.Children.Add(new RectangleGeometry(new Rect(start, _end)));
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), null, mask);
            if (_start is { } origin)
                dc.DrawRectangle(null, new Pen(Brushes.DodgerBlue, 1), new Rect(origin, _end));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _start = _end = e.GetPosition(this);
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_start is null || !IsMouseCaptured) return;
            var point = e.GetPosition(this);
            _end = new Point(Math.Clamp(point.X, 0, ActualWidth), Math.Clamp(point.Y, 0, ActualHeight));
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_start is not { } start) return;
            var rect = ToPixelRect(start, e.GetPosition(this), RenderSize, image.PixelWidth, image.PixelHeight);
            _start = null;
            ReleaseMouseCapture();
            InvalidateVisual();
            if (rect.Width >= 2 && rect.Height >= 2) Completed?.Invoke(rect);
            e.Handled = true;
        }
    }
}
