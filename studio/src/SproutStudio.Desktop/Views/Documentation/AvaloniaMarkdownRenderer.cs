using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdInline = Markdig.Syntax.Inlines.Inline;

namespace SproutStudio.Views.Documentation;

public sealed record RenderedDocumentationHeading(
    DocumentationHeading Heading,
    Control Target);

public sealed record MarkdownRenderResult(
    Control Content,
    IReadOnlyList<RenderedDocumentationHeading> Headings);

public sealed class AvaloniaMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();
    private static readonly IBrush TextPrimary = Brush("#EEF2EE");
    private static readonly IBrush TextSecondary = Brush("#BAC2BB");
    private static readonly IBrush TextMuted = Brush("#7E8980");
    private static IBrush Accent => SproutStudio.Services.StudioThemeService.AccentBrush;
    private static readonly IBrush Panel = Brush("#171A18");
    private static readonly IBrush Raised = Brush("#1D211E");
    private static readonly IBrush Border = Brush("#313732");
    private readonly Func<string, Task> _linkActivated;

    public AvaloniaMarkdownRenderer(Func<string, Task> linkActivated)
    {
        _linkActivated = linkActivated ?? throw new ArgumentNullException(nameof(linkActivated));
    }

    public MarkdownRenderResult Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);
        var content = new StackPanel
        {
            Spacing = 4,
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var headings = new List<RenderedDocumentationHeading>();
        var usedAnchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Block block in document)
        {
            Control? control = RenderBlock(block, headings, usedAnchors);
            if (control is not null)
            {
                content.Children.Add(control);
            }
        }
        return new MarkdownRenderResult(content, headings);
    }

    private Control? RenderBlock(
        Block block,
        List<RenderedDocumentationHeading> headings,
        HashSet<string> usedAnchors)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading, headings, usedAnchors),
            ParagraphBlock paragraph => RenderParagraph(paragraph),
            FencedCodeBlock fenced => RenderCodeBlock(fenced, fenced.Info?.ToString() ?? string.Empty),
            CodeBlock code => RenderCodeBlock(code, string.Empty),
            ListBlock list => RenderList(list, headings, usedAnchors),
            QuoteBlock quote => RenderQuote(quote, headings, usedAnchors),
            ThematicBreakBlock => new Border
            {
                Height = 1,
                Background = Border,
                Margin = new Thickness(0, 14, 0, 14),
            },
            Table table => RenderTable(table),
            HtmlBlock html => RenderUnsupportedHtml(html),
            _ => null,
        };
    }

    private Control RenderHeading(
        HeadingBlock heading,
        List<RenderedDocumentationHeading> headings,
        HashSet<string> usedAnchors)
    {
        string title = ExtractInlineText(heading.Inline).Trim();
        string anchor = UniqueAnchor(Slug(title), usedAnchors);
        var text = new TextBlock
        {
            Text = title,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = heading.Level <= 2 ? FontWeight.SemiBold : FontWeight.Medium,
            FontSize = heading.Level switch
            {
                1 => 27,
                2 => 20,
                3 => 16,
                _ => 14,
            },
            Foreground = heading.Level == 1 ? TextPrimary : TextSecondary,
            Margin = new Thickness(0, heading.Level == 1 ? 4 : 18, 0, heading.Level == 1 ? 12 : 6),
        };
        headings.Add(new RenderedDocumentationHeading(
            new DocumentationHeading(anchor, title, heading.Level),
            text));
        return text;
    }

    private Control RenderParagraph(ParagraphBlock paragraph)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            LineHeight = 21,
            Foreground = TextSecondary,
            Margin = new Thickness(0, 2, 0, 8),
        };
        InlineCollection inlines = text.Inlines ?? new InlineCollection();
        text.Inlines = inlines;
        AddInlines(inlines, paragraph.Inline);
        return text;
    }

    private Control RenderCodeBlock(CodeBlock block, string language)
    {
        string code = block.Lines.ToString().TrimEnd('\r', '\n');
        var copyButton = new Button
        {
            Content = "Copy",
            MinHeight = 24,
            Padding = new Thickness(8, 0),
            FontSize = 11,
        };
        copyButton.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(copyButton)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(code);
                copyButton.Content = "Copied";
            }
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Height = 34,
            Background = Panel,
        };
        header.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(language) ? "Code" : HumanizeLanguage(language),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Accent,
            Margin = new Thickness(12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(copyButton, 1);
        copyButton.Margin = new Thickness(0, 4, 6, 4);
        header.Children.Add(copyButton);
        var codeText = new SelectableTextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12.5,
            LineHeight = 20,
            Foreground = TextPrimary,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(12, 10, 12, 12),
        };
        var body = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = codeText,
        };
        var grid = new Grid { RowDefinitions = new RowDefinitions("34,Auto") };
        grid.Children.Add(header);
        Grid.SetRow(body, 1);
        grid.Children.Add(body);
        return new Border
        {
            Background = Raised,
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 5, 0, 12),
            Child = grid,
        };
    }

    private Control RenderList(
        ListBlock list,
        List<RenderedDocumentationHeading> headings,
        HashSet<string> usedAnchors)
    {
        var panel = new StackPanel
        {
            Spacing = 3,
            Margin = new Thickness(6, 2, 0, 10),
        };
        int ordinal = int.TryParse(list.OrderedStart, out int orderedStart)
            ? Math.Max(1, orderedStart)
            : 1;
        foreach (ListItemBlock item in list)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*") };
            row.Children.Add(new TextBlock
            {
                Text = list.IsOrdered ? $"{ordinal++}." : "•",
                Foreground = Accent,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 8, 0),
            });
            var body = new StackPanel { Spacing = 2 };
            foreach (Block child in item)
            {
                Control? rendered = RenderBlock(child, headings, usedAnchors);
                if (rendered is not null)
                {
                    body.Children.Add(rendered);
                }
            }
            Grid.SetColumn(body, 1);
            row.Children.Add(body);
            panel.Children.Add(row);
        }
        return panel;
    }

    private Control RenderQuote(
        QuoteBlock quote,
        List<RenderedDocumentationHeading> headings,
        HashSet<string> usedAnchors)
    {
        var panel = new StackPanel { Spacing = 2 };
        foreach (Block child in quote)
        {
            Control? rendered = RenderBlock(child, headings, usedAnchors);
            if (rendered is not null)
            {
                panel.Children.Add(rendered);
            }
        }
        return new Border
        {
            BorderBrush = Accent,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Background = Panel,
            Padding = new Thickness(14, 10),
            Margin = new Thickness(0, 5, 0, 12),
            Child = panel,
        };
    }

    private Control RenderTable(Table table)
    {
        int columns = table.OfType<TableRow>().Select(row => row.Count).DefaultIfEmpty(1).Max();
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(',', Enumerable.Repeat("*", columns))),
            Margin = new Thickness(0, 5, 0, 12),
        };
        int rowIndex = 0;
        foreach (TableRow row in table.OfType<TableRow>())
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            int columnIndex = 0;
            foreach (TableCell cell in row.OfType<TableCell>())
            {
                var text = new TextBlock
                {
                    Text = ExtractBlockText(cell).Trim(),
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = row.IsHeader ? FontWeight.SemiBold : FontWeight.Normal,
                    Foreground = row.IsHeader ? TextPrimary : TextSecondary,
                };
                var border = new Border
                {
                    Background = row.IsHeader ? Panel : Raised,
                    BorderBrush = Border,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(10, 7),
                    Child = text,
                };
                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, columnIndex++);
                grid.Children.Add(border);
            }
            rowIndex++;
        }
        return new Border
        {
            BorderBrush = Border,
            BorderThickness = new Thickness(1, 1, 0, 0),
            CornerRadius = new CornerRadius(5),
            Child = grid,
        };
    }

    private static Control RenderUnsupportedHtml(HtmlBlock html) => new TextBlock
    {
        Text = html.Lines.ToString(),
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        Foreground = TextMuted,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 10),
    };

    private void AddInlines(InlineCollection destination, ContainerInline? source)
    {
        if (source is null)
        {
            return;
        }
        foreach (MdInline inline in source)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    destination.Add(new Run(literal.Content.ToString()));
                    break;
                case CodeInline code:
                    destination.Add(new Run(code.Content)
                    {
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        Foreground = Accent,
                        Background = Raised,
                    });
                    break;
                case EmphasisInline emphasis:
                    var span = emphasis.DelimiterCount >= 2 ? (Span)new Bold() : new Italic();
                    AddInlines(span.Inlines, emphasis);
                    destination.Add(span);
                    break;
                case LineBreakInline:
                    destination.Add(new LineBreak());
                    break;
                case LinkInline link:
                    AddLink(destination, link);
                    break;
                case ContainerInline container:
                    AddInlines(destination, container);
                    break;
            }
        }
    }

    private void AddLink(InlineCollection destination, LinkInline link)
    {
        string label = ExtractInlineText(link);
        if (link.IsImage)
        {
            destination.Add(new Run($"[Image: {label}]") { Foreground = TextMuted });
            return;
        }
        string url = link.GetDynamicUrl is null ? link.Url ?? string.Empty : link.GetDynamicUrl();
        var button = new Button
        {
            Content = label.Length == 0 ? url : label,
            Tag = url,
            MinHeight = 20,
            Padding = new Thickness(3, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Accent,
            FontSize = 13.5,
        };
        button.Click += Link_Click;
        destination.Add(button);
    }

    private async void Link_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string url } && !string.IsNullOrWhiteSpace(url))
        {
            await _linkActivated(url);
        }
    }

    private static string ExtractBlockText(ContainerBlock block) => string.Join(
        " ",
        block.Select(child => child is LeafBlock leaf
            ? ExtractInlineText(leaf.Inline)
            : child is ContainerBlock container
                ? ExtractBlockText(container)
                : string.Empty));

    private static string ExtractInlineText(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }
        var parts = new List<string>();
        foreach (MdInline inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    parts.Add(literal.Content.ToString());
                    break;
                case CodeInline code:
                    parts.Add(code.Content);
                    break;
                case LineBreakInline:
                    parts.Add(" ");
                    break;
                case ContainerInline nested:
                    parts.Add(ExtractInlineText(nested));
                    break;
            }
        }
        return string.Concat(parts);
    }

    private static string Slug(string value)
    {
        string slug = new(value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }
        return slug.Trim('-');
    }

    private static string UniqueAnchor(string preferred, ISet<string> used)
    {
        string baseAnchor = preferred.Length == 0 ? "section" : preferred;
        string candidate = baseAnchor;
        int suffix = 2;
        while (!used.Add(candidate))
        {
            candidate = $"{baseAnchor}-{suffix++}";
        }
        return candidate;
    }

    private static string HumanizeLanguage(string value) => value.Trim().ToLowerInvariant() switch
    {
        "sprout" => "Sprout",
        "rust" => "Rust",
        "python" or "py" => "Python",
        "powershell" or "ps1" => "PowerShell",
        "toml" => "TOML",
        "json" => "JSON",
        "ebnf" => "EBNF",
        "text" => "Text",
        _ => value.Trim(),
    };

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
