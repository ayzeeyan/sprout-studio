using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using SproutStudio.Services;

namespace SproutStudio.Views.Shared;

/// <summary>
/// Renders read-only code with the same syntax colors as the editor, as lightweight selectable
/// text (no editor instance) for lessons, documentation, concept examples, and the Learn panel.
/// </summary>
public static class CodeHighlighter
{
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");
    private static readonly IBrush DefaultForeground = new SolidColorBrush(Color.Parse("#E9EFEB"));

    public static SelectableTextBlock Create(string code, string? language, double fontSize = 12.5)
    {
        var block = new SelectableTextBlock
        {
            FontFamily = MonoFont,
            FontSize = fontSize,
            LineHeight = Math.Round(fontSize * 1.6),
            TextWrapping = TextWrapping.NoWrap,
            Foreground = DefaultForeground,
        };
        Apply(block, code, language);
        return block;
    }

    /// <summary>Fills a text block with highlighted runs; falls back to plain text for unknown languages.</summary>
    public static void Apply(SelectableTextBlock block, string code, string? language)
    {
        ArgumentNullException.ThrowIfNull(block);
        code ??= string.Empty;
        IHighlightingDefinition? definition = SyntaxHighlightingRegistry.For(NormalizeLanguage(language));
        block.Inlines?.Clear();
        if (definition is null || code.Length == 0)
        {
            block.Text = code;
            return;
        }

        block.Text = null;
        var inlines = new InlineCollection();
        var document = new TextDocument(code);
        var highlighter = new DocumentHighlighter(document, definition);
        for (int lineNumber = 1; lineNumber <= document.LineCount; lineNumber++)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            HighlightedLine highlighted = highlighter.HighlightLine(lineNumber);
            // Sections are ordered outer-to-inner, so later sections override earlier ones.
            var colors = new HighlightingColor?[line.Length];
            foreach (HighlightedSection section in highlighted.Sections)
            {
                int start = Math.Max(0, section.Offset - line.Offset);
                int end = Math.Min(line.Length, section.Offset + section.Length - line.Offset);
                for (int index = start; index < end; index++)
                {
                    colors[index] = section.Color;
                }
            }

            int runStart = 0;
            for (int index = 1; index <= line.Length; index++)
            {
                if (index == line.Length || !ReferenceEquals(colors[index], colors[runStart]))
                {
                    inlines.Add(CreateRun(document.GetText(line.Offset + runStart, index - runStart), colors[runStart]));
                    runStart = index;
                }
            }
            if (lineNumber < document.LineCount)
            {
                inlines.Add(new LineBreak());
            }
        }
        block.Inlines = inlines;
    }

    private static Run CreateRun(string text, HighlightingColor? color)
    {
        var run = new Run(text);
        if (color is null)
        {
            return run;
        }
        if (color.Foreground?.GetBrush(null) is { } brush)
        {
            run.Foreground = brush;
        }
        if (color.FontWeight is { } weight)
        {
            run.FontWeight = weight;
        }
        if (color.FontStyle is { } style)
        {
            run.FontStyle = style;
        }
        return run;
    }

    /// <summary>Maps Markdown info strings and catalog names ("py", "Sprout", "c++") to registry keys.</summary>
    public static string? NormalizeLanguage(string? language)
    {
        string value = (language ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "" or "text" or "plaintext" or "console" or "output" or "powershell" or "shell" or "bash" => null,
            "spr" => "sprout",
            "py" => "python",
            "rs" => "rust",
            "golang" => "go",
            "js" => "javascript",
            "ts" => "typescript",
            "c++" or "cc" => "cpp",
            "c#" or "cs" => "csharp",
            "md" => "markdown",
            _ => value,
        };
    }
}
