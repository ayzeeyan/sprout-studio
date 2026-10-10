using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SproutStudio.Models;
using SproutStudio.Services;

namespace SproutStudio.Views.Settings;

public sealed partial class SettingsWindow : Window
{
    private readonly string _originalTheme;
    private string _selectedTheme;
    private bool _saved;

    public SettingsWindow()
        : this(new StudioSettingsState())
    {
    }

    public SettingsWindow(StudioSettingsState settings)
    {
        InitializeComponent();
        StudioSettingsState value = StudioSettingsState.Sanitize(settings);
        _originalTheme = value.AccentTheme;
        _selectedTheme = value.AccentTheme;
        this.FindControl<NumericUpDown>("FontSizeBox")!.Value = (decimal)value.EditorFontSize;
        this.FindControl<ToggleSwitch>("WordWrapBox")!.IsChecked = value.WordWrap;
        this.FindControl<ToggleSwitch>("WhitespaceBox")!.IsChecked = value.ShowWhitespace;
        this.FindControl<ToggleSwitch>("CurrentLineBox")!.IsChecked = value.HighlightCurrentLine;
        this.FindControl<ToggleSwitch>("IndentGuidesBox")!.IsChecked = value.ShowIndentGuides;
        BuildSwatches();
        Closed += (_, _) =>
        {
            // Cancelling (or closing the window) reverts the live preview.
            if (!_saved)
            {
                StudioThemeService.Apply(_originalTheme);
            }
        };
    }

    private void BuildSwatches()
    {
        WrapPanel panel = this.FindControl<WrapPanel>("SwatchPanel")!;
        panel.Children.Clear();
        foreach (StudioAccentTheme theme in StudioThemeService.Themes)
        {
            var preview = new StackPanel { Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            dots.Children.Add(new Border { Width = 22, Height = 22, CornerRadius = new(11), Background = new SolidColorBrush(theme.Accent) });
            dots.Children.Add(new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new(11),
                Background = new SolidColorBrush(Color.FromArgb(0x40, theme.Accent.R, theme.Accent.G, theme.Accent.B)),
            });
            preview.Children.Add(dots);
            preview.Children.Add(new TextBlock { Text = theme.DisplayName, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });

            var button = new Button { Content = preview, Tag = theme.Id };
            button.Classes.Add("swatch");
            button.Classes.Set("selected", string.Equals(theme.Id, _selectedTheme, StringComparison.OrdinalIgnoreCase));
            ToolTip.SetTip(button, $"{theme.DisplayName} accent");
            button.Click += Swatch_Click;
            panel.Children.Add(button);
        }
    }

    private void Swatch_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string id })
        {
            return;
        }
        _selectedTheme = id;
        StudioThemeService.Apply(id);
        foreach (Button swatch in this.FindControl<WrapPanel>("SwatchPanel")!.Children.OfType<Button>())
        {
            swatch.Classes.Set("selected", Equals(swatch.Tag, id));
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs args)
    {
        double fontSize = (double)(this.FindControl<NumericUpDown>("FontSizeBox")!.Value ?? 14);
        _saved = true;
        Close(StudioSettingsState.Sanitize(new StudioSettingsState(
            EditorFontSize: fontSize,
            WordWrap: this.FindControl<ToggleSwitch>("WordWrapBox")!.IsChecked == true,
            ShowWhitespace: this.FindControl<ToggleSwitch>("WhitespaceBox")!.IsChecked == true,
            AccentTheme: _selectedTheme,
            HighlightCurrentLine: this.FindControl<ToggleSwitch>("CurrentLineBox")!.IsChecked == true,
            ShowIndentGuides: this.FindControl<ToggleSwitch>("IndentGuidesBox")!.IsChecked == true)));
    }

    private void Cancel_Click(object? sender, RoutedEventArgs args) => Close(null);
}
