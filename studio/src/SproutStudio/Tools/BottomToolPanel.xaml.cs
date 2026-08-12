using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SproutStudio.Tools;

public sealed partial class BottomToolPanel : UserControl
{
    public BottomToolPanel() => InitializeComponent();

    public TabView Tabs => ToolTabs;
    public Button CollapseButton => CollapsePaneButton;
    public ListView Problems => ProblemItems;
    public ListView Console => ConsoleItems;
    public ListView BuildOutput => BuildItems;

    public void SetProblemsEmpty(bool empty) =>
        ProblemsEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

    public void SetConsoleEmpty(bool empty) =>
        ConsoleEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

    public void SetBuildEmpty(bool empty) =>
        BuildEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
}
