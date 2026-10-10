using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace SproutStudio.Views.Editor;

/// <summary>Draws faint vertical guides at each indentation level of the visible lines.</summary>
internal sealed class IndentGuideRenderer : IBackgroundRenderer
{
    private readonly int _indentSize;

    public IndentGuideRenderer(int indentSize = 4)
    {
        _indentSize = Math.Max(1, indentSize);
    }

    public bool IsEnabled { get; set; } = true;

    public IPen Pen { get; set; } = new Pen(new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF)), 1);

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || !textView.VisualLinesValid || textView.Document is not { } document)
        {
            return;
        }

        double characterWidth = textView.WideSpaceWidth;
        if (characterWidth <= 0)
        {
            return;
        }

        int previousDepth = 0;
        foreach (VisualLine visualLine in textView.VisualLines)
        {
            DocumentLine line = visualLine.FirstDocumentLine;
            int depth = IndentDepth(document, line);
            // Blank lines inherit the surrounding depth so guides stay continuous.
            if (depth < 0)
            {
                depth = previousDepth;
            }
            previousDepth = depth;

            double top = visualLine.VisualTop - textView.VerticalOffset;
            double bottom = top + visualLine.Height;
            for (int level = 0; level < depth; level++)
            {
                double x = Math.Round((level * _indentSize * characterWidth) - textView.HorizontalOffset) + 0.5;
                drawingContext.DrawLine(Pen, new Point(x, top), new Point(x, bottom));
            }
        }
    }

    /// <summary>Indentation levels for a line, or -1 when the line is blank.</summary>
    private int IndentDepth(TextDocument document, DocumentLine line)
    {
        int spaces = 0;
        for (int offset = line.Offset; offset < line.EndOffset; offset++)
        {
            char character = document.GetCharAt(offset);
            if (character == ' ')
            {
                spaces++;
            }
            else if (character == '\t')
            {
                spaces += _indentSize - (spaces % _indentSize);
            }
            else
            {
                return spaces / _indentSize;
            }
        }
        return -1;
    }
}
