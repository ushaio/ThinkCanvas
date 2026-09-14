using System.IO;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;

namespace ThinkCanvas;

public sealed record Shortcut(Key Key, ModifierKeys Modifiers)
{
    public bool IsValid => KeyInterop.VirtualKeyFromKey(Key) is >= 0x30 and <= 0xFE &&
        Key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System) &&
        Modifiers != ModifierKeys.None &&
        (Modifiers & ~(ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt)) == 0;

    public override string ToString() => string.Join("+", new[]
    {
        Modifiers.HasFlag(ModifierKeys.Control) ? "Ctrl" : null,
        Modifiers.HasFlag(ModifierKeys.Alt) ? "Alt" : null,
        Modifiers.HasFlag(ModifierKeys.Shift) ? "Shift" : null,
        Key.ToString()
    }.Where(part => part != null));
}

public sealed record AppSettings
{
    public Shortcut ToggleShortcut { get; init; } = new(Key.A, ModifierKeys.Control | ModifierKeys.Shift);
    public Shortcut CaptureShortcut { get; init; } = new(Key.S, ModifierKeys.Control | ModifierKeys.Shift);
    public Shortcut EraserShortcut { get; init; } = new(Key.E, ModifierKeys.Control | ModifierKeys.Shift);
    public bool StartWithWindows { get; init; }
    public bool UseSolidBackground { get; init; }
    public string BackgroundColor { get; init; } = "#FFFFFF";
    public bool AskBeforeExit { get; init; } = true;

    /// <summary>AskBeforeExit 为 false 时点击退出按钮直接执行的操作："" 占位、"Tray" 最小化到托盘、"Exit" 退出程序。</summary>
    public string ExitAction { get; init; } = "";

    public void Validate()
    {
        if (ToggleShortcut is null || CaptureShortcut is null || EraserShortcut is null ||
            !ToggleShortcut.IsValid || !CaptureShortcut.IsValid || !EraserShortcut.IsValid)
            throw new ArgumentException("快捷键需要包含 Ctrl、Alt 或 Shift，以及一个普通按键。");
        if (new[] { ToggleShortcut, CaptureShortcut, EraserShortcut }.Distinct().Count() != 3)
            throw new ArgumentException("截图、模式切换与橡皮擦不能使用相同的快捷键。");
        ParseBackgroundColor(BackgroundColor);
        if (ExitAction is not ("" or "Tray" or "Exit"))
            throw new ArgumentException("退出方式设置无效。");
    }

    public static Color ParseBackgroundColor(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length != 7 || text[0] != '#' ||
            !text.AsSpan(1).ToArray().All(Uri.IsHexDigit))
            throw new ArgumentException("背景颜色必须为 #RRGGBB，例如 #FFFFFF。");
        return (Color)ColorConverter.ConvertFromString(text);
    }
}

public static class SettingsStore
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThinkCanvas", "settings.json");

    public static AppSettings Load(string? filePath = null)
    {
        var path = filePath ?? FilePath;
        if (!File.Exists(path))
            return new AppSettings();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
            ?? throw new InvalidDataException("设置文件为空。");
        settings.Validate();
        return settings;
    }

    public static void Save(AppSettings settings, string? filePath = null)
    {
        var path = Path.GetFullPath(filePath ?? FilePath);
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
