using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;

namespace ThinkCanvas;

public sealed class PressureStrokeVisual : InkPresenter
{
    private readonly System.Windows.Ink.Stroke _inkStroke;

    public PressureStrokeVisual(Stroke stroke)
    {
        IsHitTestVisible = false;
        _inkStroke = new System.Windows.Ink.Stroke(new StylusPointCollection(
            stroke.Points.Select(ToStylusPoint)), new DrawingAttributes
        {
            Color = stroke.Color,
            Width = stroke.Width,
            Height = stroke.Width,
            IgnorePressure = !stroke.HasPressure,
            FitToCurve = false
        });
        Strokes.Add(_inkStroke);
    }

    public void Append(StrokePoint point) => _inkStroke.StylusPoints.Add(ToStylusPoint(point));

    private static StylusPoint ToStylusPoint(StrokePoint point) =>
        new(point.Position.X, point.Position.Y, Math.Clamp(point.Pressure, 0f, 1f));
}
