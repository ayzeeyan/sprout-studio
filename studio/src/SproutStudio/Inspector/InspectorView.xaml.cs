using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SproutStudio.Inspector;

public sealed partial class InspectorView : UserControl
{
    public InspectorView() => InitializeComponent();

    public TabView Tabs => InspectorTabs;
    public Button CollapseButton => CollapsePaneButton;
    public ListView Outline => OutlineItems;
    public TextBlock OutlineEmpty => OutlineEmptyText;
    public TextBlock HoverTitle => HoverTitleText;
    public TextBlock HoverContents => HoverContentsText;
    public ListView Completions => CompletionItems;
    public Button PreviousSnapshot => PreviousSnapshotButton;
    public Button NextSnapshot => NextSnapshotButton;
    public TextBlock DebugPosition => DebugPositionText;
    public TextBox WatchName => WatchNameBox;
    public Button AddWatch => AddWatchButton;
    public ListView Variables => VariableItems;
    public ListView CallStack => CallStackItems;
    public ListView Breakpoints => BreakpointItems;
    public Canvas Canvas => CanvasSurface;
    public TextBlock CanvasEmpty => CanvasEmptyText;

    public void SetOutlineEmpty(bool empty) =>
        OutlineEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

    public void SetVariablesEmpty(bool empty) =>
        VariablesEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

    public void SelectDebugger() => InspectorTabs.SelectedIndex = 2;
}
