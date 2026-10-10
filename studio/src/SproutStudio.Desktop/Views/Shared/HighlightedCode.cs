using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SproutStudio.Views.Shared;

/// <summary>Read-only, selectable code with editor syntax colors, usable directly from XAML.</summary>
public sealed class HighlightedCode : SelectableTextBlock
{
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<HighlightedCode, string?>(nameof(Code));

    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<HighlightedCode, string?>(nameof(Language));

    public HighlightedCode()
    {
        FontFamily = new FontFamily("Cascadia Mono, Consolas");
        FontSize = 11.5;
        LineHeight = 18;
        TextWrapping = TextWrapping.NoWrap;
    }

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty || change.Property == LanguageProperty)
        {
            CodeHighlighter.Apply(this, Code ?? string.Empty, Language);
        }
    }
}
