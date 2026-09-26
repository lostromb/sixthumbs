using System.Windows;
using System.Windows.Media;
using Sixthumbs.Core;

namespace Sixthumbs.App;

public sealed class GamepadPreview : FrameworkElement
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(Xbox360State),
        typeof(GamepadPreview),
        new FrameworkPropertyMetadata(default(Xbox360State), FrameworkPropertyMetadataOptions.AffectsRender));

    public Xbox360State State
    {
        get => (Xbox360State)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var state = State;
        var body = new Rect(w * 0.12, h * 0.18, w * 0.76, h * 0.64);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(42, 46, 54)), new Pen(Brushes.Gray, 1), body, 18, 18);

        DrawStick(dc, w * 0.32, h * 0.62, state.LeftX, state.LeftY, state.GetButton(Xbox360Control.LeftStick));
        DrawStick(dc, w * 0.62, h * 0.62, state.RightX, state.RightY, state.GetButton(Xbox360Control.RightStick));

        DrawButton(dc, w * 0.72, h * 0.40, "A", state.GetButton(Xbox360Control.A), Color.FromRgb(80, 200, 80));
        DrawButton(dc, w * 0.80, h * 0.32, "B", state.GetButton(Xbox360Control.B), Color.FromRgb(220, 80, 80));
        DrawButton(dc, w * 0.64, h * 0.32, "X", state.GetButton(Xbox360Control.X), Color.FromRgb(80, 140, 230));
        DrawButton(dc, w * 0.72, h * 0.24, "Y", state.GetButton(Xbox360Control.Y), Color.FromRgb(230, 210, 70));

        DrawShoulder(dc, w * 0.22, h * 0.12, "LB", state.GetButton(Xbox360Control.LeftShoulder));
        DrawShoulder(dc, w * 0.62, h * 0.12, "RB", state.GetButton(Xbox360Control.RightShoulder));
        DrawTrigger(dc, w * 0.22, h * 0.04, state.LeftTrigger);
        DrawTrigger(dc, w * 0.62, h * 0.04, state.RightTrigger);

        DrawDpad(dc, w * 0.32, h * 0.36, state);
        DrawSmall(dc, w * 0.44, h * 0.36, "Bk", state.GetButton(Xbox360Control.Back));
        DrawSmall(dc, w * 0.52, h * 0.36, "St", state.GetButton(Xbox360Control.Start));
        DrawSmall(dc, w * 0.48, h * 0.28, "G", state.GetButton(Xbox360Control.Guide));
    }

    private static void DrawStick(DrawingContext dc, double x, double y, short ax, short ay, bool click)
    {
        const double r = 22;
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 30, 36)), new Pen(Brushes.DimGray, 1), new Point(x, y), r, r);
        var nx = ax / 32768.0;
        var ny = ay / 32768.0;
        var fill = click ? Brushes.Orange : Brushes.LightGray;
        dc.DrawEllipse(fill, null, new Point(x + nx * 12, y + ny * 12), 8, 8);
    }

    private static void DrawButton(DrawingContext dc, double x, double y, string label, bool on, Color color)
    {
        var brush = new SolidColorBrush(on ? color : Color.FromRgb(70, 74, 82));
        dc.DrawEllipse(brush, null, new Point(x, y), 10, 10);
        var text = Ft(label, 10);
        dc.DrawText(text, new Point(x - text.Width / 2, y - text.Height / 2));
    }

    private static void DrawShoulder(DrawingContext dc, double x, double y, string label, bool on)
    {
        var rect = new Rect(x, y, 70, 16);
        dc.DrawRoundedRectangle(new SolidColorBrush(on ? Colors.Orange : Color.FromRgb(70, 74, 82)), null, rect, 4, 4);
        var text = Ft(label, 10);
        dc.DrawText(text, new Point(x + 8, y));
    }

    private static void DrawTrigger(DrawingContext dc, double x, double y, byte value)
    {
        var rect = new Rect(x, y, 70, 8);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)), null, rect);
        dc.DrawRectangle(Brushes.SteelBlue, null, new Rect(x, y, 70 * (value / 255.0), 8));
    }

    private static void DrawDpad(DrawingContext dc, double x, double y, Xbox360State state)
    {
        DrawPadDir(dc, x, y - 16, state.GetButton(Xbox360Control.DpadUp));
        DrawPadDir(dc, x, y + 16, state.GetButton(Xbox360Control.DpadDown));
        DrawPadDir(dc, x - 16, y, state.GetButton(Xbox360Control.DpadLeft));
        DrawPadDir(dc, x + 16, y, state.GetButton(Xbox360Control.DpadRight));
    }

    private static void DrawPadDir(DrawingContext dc, double x, double y, bool on)
    {
        dc.DrawRectangle(on ? Brushes.Orange : new SolidColorBrush(Color.FromRgb(70, 74, 82)), null, new Rect(x - 7, y - 7, 14, 14));
    }

    private static void DrawSmall(DrawingContext dc, double x, double y, string label, bool on)
    {
        dc.DrawEllipse(on ? Brushes.Orange : new SolidColorBrush(Color.FromRgb(70, 74, 82)), null, new Point(x, y), 8, 8);
        var text = Ft(label, 8);
        dc.DrawText(text, new Point(x - text.Width / 2, y - text.Height / 2));
    }

    private static FormattedText Ft(string text, double size) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, Brushes.White, 1.25);
}
