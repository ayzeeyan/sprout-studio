using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace SproutStudio.Shell;

public sealed partial class ActivityBar : UserControl
{
    public ActivityBar()
    {
        InitializeComponent();
        Select("explorer");
    }

    public ToggleButton Explorer => ExplorerButton;
    public ToggleButton Search => SearchButton;
    public ToggleButton SourceControl => SourceControlButton;
    public ToggleButton Academy => AcademyButton;
    public ToggleButton Documentation => DocumentationButton;
    public ToggleButton Packages => PackagesButton;
    public ToggleButton Toolchains => ToolchainsButton;
    public string SelectedKey { get; private set; } = "explorer";

    public void Select(string key)
    {
        (ToggleButton Button, Border Indicator, string Key)[] entries =
        [
            (ExplorerButton, ExplorerIndicator, "explorer"),
            (SearchButton, SearchIndicator, "search"),
            (SourceControlButton, SourceControlIndicator, "source-control"),
            (AcademyButton, AcademyIndicator, "academy"),
            (DocumentationButton, DocumentationIndicator, "documentation"),
            (PackagesButton, PackagesIndicator, "packages"),
            (ToolchainsButton, ToolchainsIndicator, "toolchains"),
        ];

        foreach ((ToggleButton button, Border indicator, string entryKey) in entries)
        {
            bool selected = string.Equals(key, entryKey, StringComparison.Ordinal);
            button.IsChecked = false;
            button.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                selected ? "StudioSelectionBrush" : "StudioSurfaceBrush"];
            button.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                selected ? "StudioAccentBrush" : "StudioTextSecondaryBrush"];
            indicator.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
        SelectedKey = key;
    }
}
