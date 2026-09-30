using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SproutStudio.Editor;
using ShapePath = Avalonia.Controls.Shapes.Path;

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
        string languageKey = string.IsNullOrWhiteSpace(LanguageKey) ? "sprout" : LanguageKey;
        LanguageDescriptor language = LanguageRegistry.TryGet(languageKey, out LanguageDescriptor? found)
            ? found!
            : LanguageRegistry.Get(DocumentLanguage.PlainText);
        Border? surface = this.FindControl<Border>("IconSurface");
        TextBlock? glyph = this.FindControl<TextBlock>("IconGlyph");
        ShapePath? path = this.FindControl<ShapePath>("IconPath");
        if (surface is null || glyph is null || path is null)
        {
            return;
        }

        surface.Background = AccentBrushes.TryGetValue(language.Key, out IBrush? brush) ? brush : Brushes.DimGray;
        if (!string.IsNullOrEmpty(language.IconPathData))
        {
            try
            {
                path.Data = Geometry.Parse(language.IconPathData);
                path.Fill = Brushes.White;
                path.Margin = new Thickness(1);
                path.Stretch = Stretch.Uniform;
                path.IsVisible = true;
                glyph.IsVisible = false;
            }
            catch
            {
                path.IsVisible = false;
                glyph.IsVisible = true;
                glyph.Text = language.IconGlyph;
                glyph.FontSize = 12 * language.IconScale;
                glyph.Foreground = Brushes.White;
            }
        }
        else
        {
            path.IsVisible = false;
            glyph.IsVisible = true;
            glyph.Text = language.IconGlyph;
            glyph.FontSize = 12 * language.IconScale;
            glyph.Foreground = Brushes.White;
        }
        ToolTip.SetTip(this, language.DisplayName);
    }
}
