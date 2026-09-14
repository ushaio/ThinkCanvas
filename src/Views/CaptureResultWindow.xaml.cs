using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ThinkCanvas;

public partial class CaptureResultWindow : Window
{
    private readonly BitmapSource _desktopFrame;
    private readonly Int32Rect _region;
    private readonly Int32Rect _bounds;
    private readonly Matrix _inkTransform;
    private readonly Action<DrawingContext> _drawInk;
    private Color _customColor;
    private bool _initialized;

    public CaptureResultWindow(BitmapSource desktopFrame, Int32Rect region, Int32Rect bounds,
        Matrix inkTransform, Action<DrawingContext> drawInk, Color customColor, bool useSolidBackground)
    {
        InitializeComponent();
        _desktopFrame = desktopFrame;
        _region = region;
        _bounds = bounds;
        _inkTransform = inkTransform;
        _drawInk = drawInk;
        _customColor = customColor;
        KeepBackgroundRadio.IsChecked = !useSolidBackground;
        CustomBackgroundRadio.IsChecked = useSolidBackground;
        CustomColorPanel.Visibility = useSolidBackground ? Visibility.Visible : Visibility.Collapsed;
        CustomColorBox.Text = $"#{customColor.R:X2}{customColor.G:X2}{customColor.B:X2}";
        PreviewImage.Source = CurrentImage();
        StatusText.Text = $"{_region.Width} × {_region.Height}";
        _initialized = true;
    }

    private BitmapSource CurrentImage()
    {
        if (KeepBackgroundRadio.IsChecked == true)
            return new CroppedBitmap(_desktopFrame, _region);
        var frame = ScreenCapture.Compose(_bounds, null, _customColor, _inkTransform, _drawInk);
        return new CroppedBitmap(frame, _region);
    }

    private void BackgroundMode_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        CustomColorPanel.Visibility = CustomBackgroundRadio.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
        RefreshPreview();
    }

    private void Swatch_OnClick(object sender, RoutedEventArgs e) => CustomColorBox.Text = (string)((Button)sender).Tag;

    private void CustomColor_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        try { _customColor = (Color)ColorConverter.ConvertFromString(CustomColorBox.Text.Trim()); }
        catch (FormatException) { return; }
        RefreshPreview();
    }

    private void RefreshPreview() => PreviewImage.Source = CurrentImage();

    private void TitleBar_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void Copy_OnClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetImage(CurrentImage()); StatusText.Text = "已复制"; }
        catch (ExternalException) { StatusText.Text = "剪贴板正忙，请重试。"; }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png", DefaultExt = ".png", AddExtension = true,
            FileName = $"ThinkCanvas-{DateTime.Now:yyyyMMdd-HHmmss}.png"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(CurrentImage()));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            StatusText.Text = "已保存";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { StatusText.Text = $"保存失败：{exception.Message}"; }
    }
}
