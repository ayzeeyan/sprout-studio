using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SproutStudio.Services;

/// <summary>A named accent palette that recolors every accent-derived resource in the shell.</summary>
public sealed record StudioAccentTheme(string Id, string DisplayName, Color Accent, Color AccentHover);

/// <summary>
/// Applies an accent theme at runtime by overriding the accent tokens declared in
/// StudioTheme.axaml. Every view references those tokens through DynamicResource, so the
/// whole shell (and Fluent's own accent-based controls) recolors without a restart.
/// </summary>
public static class StudioThemeService
{
    public const string DefaultThemeId = "sprout";

    public static IReadOnlyList<StudioAccentTheme> Themes { get; } =
    [
        new("sprout", "Sprout", Color.Parse("#4CD484"), Color.Parse("#6BE39C")),
        new("ocean", "Ocean", Color.Parse("#4FA8FF"), Color.Parse("#74BBFF")),
        new("amethyst", "Amethyst", Color.Parse("#A78BFA"), Color.Parse("#BBA4FF")),
        new("ember", "Ember", Color.Parse("#FF9F43"), Color.Parse("#FFB46B")),
        new("rose", "Rose", Color.Parse("#F472B6"), Color.Parse("#F891C7")),
    ];

    public static StudioAccentTheme Current { get; private set; } = Themes[0];

    public static StudioAccentTheme Find(string? id) =>
        Themes.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Themes[0];

    public static void Apply(string? id)
    {
        StudioAccentTheme theme = Find(id);
        Current = theme;
        if (Application.Current is not { } application)
        {
            return;
        }

        IResourceDictionary resources = application.Resources;
        Color accent = theme.Accent;
        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorLight1"] = Mix(accent, Colors.White, 0.15);
        resources["SystemAccentColorLight2"] = Mix(accent, Colors.White, 0.30);
        resources["SystemAccentColorLight3"] = Mix(accent, Colors.White, 0.50);
        resources["SystemAccentColorDark1"] = Mix(accent, Colors.Black, 0.12);
        resources["SystemAccentColorDark2"] = Mix(accent, Colors.Black, 0.28);
        resources["SystemAccentColorDark3"] = Mix(accent, Colors.Black, 0.45);

        resources["StudioAccentColor"] = accent;
        resources["StudioAccentBrush"] = new SolidColorBrush(accent);
        resources["StudioAccentHoverBrush"] = new SolidColorBrush(theme.AccentHover);
        resources["StudioAccentPressedBrush"] = new SolidColorBrush(Mix(accent, Colors.Black, 0.15));
        resources["StudioAccentForegroundBrush"] = new SolidColorBrush(Mix(accent, Colors.Black, 0.88));
        resources["StudioAccentSoftBrush"] = new SolidColorBrush(WithAlpha(accent, 0x22));
        resources["StudioAccentSofterBrush"] = new SolidColorBrush(WithAlpha(accent, 0x12));
        resources["StudioAccentBorderBrush"] = new SolidColorBrush(WithAlpha(accent, 0x66));
        resources["StudioSelectedBrush"] = new SolidColorBrush(WithAlpha(accent, 0x26));
        resources["StudioSelectionBrush"] = new SolidColorBrush(WithAlpha(accent, 0x4A));
        resources["StudioCurrentLineBrush"] = new SolidColorBrush(WithAlpha(accent, 0x0C));
        resources["StudioAccentGlowBrush"] = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(WithAlpha(accent, 0x55), 0),
                new GradientStop(WithAlpha(accent, 0x00), 1),
            },
        };
        resources["StudioAccentGradientBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(theme.AccentHover, 0),
                new GradientStop(Mix(accent, Colors.Black, 0.2), 1),
            },
        };
    }

    public static IBrush AccentBrush => new SolidColorBrush(Current.Accent);

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Mix(Color color, Color other, double amount) => Color.FromRgb(
        (byte)Math.Round(color.R + ((other.R - color.R) * amount)),
        (byte)Math.Round(color.G + ((other.G - color.G) * amount)),
        (byte)Math.Round(color.B + ((other.B - color.B) * amount)));
}
