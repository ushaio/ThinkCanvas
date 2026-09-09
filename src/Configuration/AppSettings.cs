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
    public bool UseSolidBackground { get; init; }
    public string BackgroundColor { get; init; } = "#FFFFFF";

    public void Validate()
    {
        if (ToggleShortcut is null || CaptureShortcut is null ||
            !ToggleShortcut.IsValid || !CaptureShortcut.IsValid)
            throw new ArgumentException("快捷键需要包含 Ctrl、Alt 或 Shift，以及一个普通按键。");
        if (ToggleShortcut == CaptureShortcut)
            throw new ArgumentException("截图与模式切换不能使用相同的快捷键。");
        ParseBackgroundColor(BackgroundColor);
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
