using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SproutStudio.Core;

namespace SproutStudio.Views.Inspector;

public sealed class CanvasPreviewControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<CanvasCommand>> CommandsProperty =
        AvaloniaProperty.Register<CanvasPreviewControl, IReadOnlyList<CanvasCommand>>(
            nameof(Commands),
            []);

    static CanvasPreviewControl() =>
        AffectsRender<CanvasPreviewControl>(CommandsProperty);

    public IReadOnlyList<CanvasCommand> Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Rect surface = new(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, surface);
        foreach (CanvasCommand command in Commands)
        {
            IBrush brush = BrushFor(command.Color);
            switch (command.Kind.ToLowerInvariant())
            {
                case "clear":
                    context.FillRectangle(brush, surface);
                    break;
                case "rectangle":
                    context.FillRectangle(
                        brush,
                        new Rect(
                            command.X ?? 0,
                            command.Y ?? 0,
                            Math.Max(0, command.Width ?? 0),
                            Math.Max(0, command.Height ?? 0)));
                    break;
                case "circle":
                    double radius = Math.Max(0, command.Radius ?? 0);
                    context.DrawEllipse(brush, null, new Point(command.X ?? 0, command.Y ?? 0), radius, radius);
                    break;
            }
        }
    }

    private static IBrush BrushFor(string value) =>
        Color.TryParse(value, out Color color)
            ? new SolidColorBrush(color)
            : value.ToLowerInvariant() switch
            {
                "green" => new SolidColorBrush(Color.Parse("#49C979")),
                "red" => new SolidColorBrush(Color.Parse("#F07178")),
                "blue" => new SolidColorBrush(Color.Parse("#61AFEF")),
                "yellow" => new SolidColorBrush(Color.Parse("#E6BC55")),
                "white" => Brushes.White,
                "black" => Brushes.Black,
                _ => new SolidColorBrush(Color.Parse("#BAC2BB")),
            };
}
