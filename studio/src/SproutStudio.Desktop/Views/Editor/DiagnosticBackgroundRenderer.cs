using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SproutStudio.Core;
using SproutStudio.Editor;

namespace SproutStudio.Views.Editor;

internal sealed class DiagnosticBackgroundRenderer : IBackgroundRenderer
{
    private static readonly IPen ErrorPen = new Pen(
        new SolidColorBrush(Color.FromRgb(242, 105, 116)),
        1.25);
    private static readonly IPen WarningPen = new Pen(
        new SolidColorBrush(Color.FromRgb(240, 190, 72)),
        1.25);
    private readonly List<DiagnosticDecoration> _decorations = [];

    public KnownLayer Layer => KnownLayer.Selection;

    public void Update(string source, IEnumerable<StudioDiagnostic> diagnostics)
    {
        _decorations.Clear();
        foreach (StudioDiagnostic diagnostic in diagnostics)
        {
            int start = SourceTextCoordinates.Utf8OffsetToUtf16Index(source, diagnostic.Span.Start);
            int end = SourceTextCoordinates.Utf8OffsetToUtf16Index(source, diagnostic.Span.End);
            start = Math.Clamp(start, 0, source.Length);
            end = Math.Clamp(end, start, source.Length);
            if (end == start && start < source.Length)
            {
                end++;
            }
            _decorations.Add(new DiagnosticDecoration(
                new TextSegment { StartOffset = start, EndOffset = end },
                diagnostic.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
                    ? WarningPen
                    : ErrorPen));
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_decorations.Count == 0 || textView.Document is null)
        {
            return;
        }
        textView.EnsureVisualLines();
        foreach (DiagnosticDecoration decoration in _decorations)
        {
            foreach (Rect rectangle in BackgroundGeometryBuilder.GetRectsForSegment(
                textView,
                decoration.Segment,
                false))
            {
                DrawSquiggle(drawingContext, decoration.Pen, rectangle);
            }
        }
    }

    private static void DrawSquiggle(DrawingContext context, IPen pen, Rect rectangle)
    {
        double left = rectangle.Left;
        double right = Math.Max(left + 3, rectangle.Right);
        double baseline = rectangle.Bottom - 1.5;
        bool rising = true;
        while (left < right)
        {
            double next = Math.Min(left + 2.5, right);
            context.DrawLine(
                pen,
                new Point(left, rising ? baseline : baseline - 2),
                new Point(next, rising ? baseline - 2 : baseline));
            rising = !rising;
            left = next;
        }
    }

    private sealed record DiagnosticDecoration(TextSegment Segment, IPen Pen);
}
