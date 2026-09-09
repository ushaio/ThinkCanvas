using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ThinkCanvas;

public partial class CaptureResultWindow : Window
{
    private readonly BitmapSource _image;
    public CaptureResultWindow(BitmapSource image)
    {
        InitializeComponent();
        _image = image;
        PreviewImage.Source = image;
        StatusText.Text = $"{image.PixelWidth} × {image.PixelHeight}";
    }

    private void Copy_OnClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetImage(_image); StatusText.Text = "已复制"; }
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
            encoder.Frames.Add(BitmapFrame.Create(_image));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            StatusText.Text = "已保存";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { StatusText.Text = $"保存失败：{exception.Message}"; }
    }
}
