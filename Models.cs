using System.Windows;
using System.Windows.Media;

namespace ThinkCanvas;

public enum OverlayMode
{
    Passthrough,
    Writing
}

public readonly record struct StrokePoint(Point Position, float Pressure, long Timestamp);

public sealed class Stroke
{
    public List<StrokePoint> Points { get; } = [];
    public Color Color { get; init; } = Colors.Red;
    public double Width { get; init; } = 4;
    public bool HasPressure { get; init; }
}
