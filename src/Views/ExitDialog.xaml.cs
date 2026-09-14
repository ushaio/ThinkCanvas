using System.Windows;
using System.Windows.Input;

namespace ThinkCanvas;

public partial class ExitDialog : Window
{
    /// <summary>true 表示最小化到托盘，false 表示退出程序。仅在 ShowDialog 返回 true 时有效。</summary>
    public bool MinimizeToTray { get; private set; }

    public bool RememberChoice => DontAskBox.IsChecked == true;

    public ExitDialog() => InitializeComponent();

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void TrayButton_OnClick(object sender, RoutedEventArgs e)
    {
        MinimizeToTray = true;
        DialogResult = true;
    }

    private void ExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        MinimizeToTray = false;
        DialogResult = true;
    }
}
