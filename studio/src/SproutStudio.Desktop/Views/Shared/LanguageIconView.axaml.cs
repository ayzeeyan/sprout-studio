using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SproutStudio.Editor;

namespace SproutStudio.Views.Shared;

public sealed partial class LanguageIconView : UserControl
{
    public static readonly StyledProperty<string?> LanguageKeyProperty =
        AvaloniaProperty.Register<LanguageIconView, string?>(nameof(LanguageKey), "sprout");

    private static readonly IReadOnlyDictionary<string, IBrush> AccentBrushes =
        LanguageRegistry.All.ToDictionary(
            language => language.Key,
            language => (IBrush)new SolidColorBrush(Color.Parse(language.Accent)),
            StringComparer.OrdinalIgnoreCase);

    public LanguageIconView()
    {
        InitializeComponent();
        UpdateIcon();
    }

    public string? LanguageKey
    {
        get => GetValue(LanguageKeyProperty);
        set => SetValue(LanguageKeyProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LanguageKeyProperty)
        {
            UpdateIcon();
        }
    }

    private void UpdateIcon()
    {
        LanguageDescriptor language = LanguageRegistry.TryGet(LanguageKey, out LanguageDescriptor? found)
            ? found!
            : LanguageRegistry.Get(DocumentLanguage.PlainText);
        Border? surface = this.FindControl<Border>("IconSurface");
        TextBlock? glyph = this.FindControl<TextBlock>("IconGlyph");
        if (surface is null || glyph is null)
        {
            return;
        }
        surface.Background = AccentBrushes[language.Key];
        glyph.Text = language.IconGlyph;
        glyph.FontSize = 12 * language.IconScale;
        glyph.Foreground = Brushes.White;
        ToolTip.SetTip(this, language.DisplayName);
    }
}
