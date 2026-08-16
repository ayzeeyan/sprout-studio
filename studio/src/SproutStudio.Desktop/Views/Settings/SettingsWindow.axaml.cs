using Avalonia.Controls;
using Avalonia.Interactivity;
using SproutStudio.Models;

namespace SproutStudio.Views.Settings;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
        : this(new StudioSettingsState())
    {
    }

    public SettingsWindow(StudioSettingsState settings)
    {
        InitializeComponent();
        StudioSettingsState value = StudioSettingsState.Sanitize(settings);
        this.FindControl<NumericUpDown>("FontSizeBox")!.Value = (decimal)value.EditorFontSize;
        this.FindControl<CheckBox>("WordWrapBox")!.IsChecked = value.WordWrap;
        this.FindControl<CheckBox>("WhitespaceBox")!.IsChecked = value.ShowWhitespace;
    }

    private void Save_Click(object? sender, RoutedEventArgs args)
    {
        double fontSize = (double)(this.FindControl<NumericUpDown>("FontSizeBox")!.Value ?? 14);
        Close(StudioSettingsState.Sanitize(new StudioSettingsState(
            EditorFontSize: fontSize,
            WordWrap: this.FindControl<CheckBox>("WordWrapBox")!.IsChecked == true,
            ShowWhitespace: this.FindControl<CheckBox>("WhitespaceBox")!.IsChecked == true)));
    }

    private void Cancel_Click(object? sender, RoutedEventArgs args) => Close(null);
}
