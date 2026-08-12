using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SproutStudio.Workspace;

public sealed partial class ExplorerView : UserControl
{
    public ExplorerView() => InitializeComponent();

    public Button CollapseButton => CollapsePaneButton;
    public TextBox SearchBox => WorkspaceFilterBox;
    public TextBlock ProjectName => ProjectNameText;
    public ListView Files => WorkspaceFileList;
    public TextBlock EmptyState => WorkspaceEmptyText;
    public Expander OutlineExpander => OutlineSection;
    public ListView Outline => OutlineList;
    public Expander SourceControlExpander => SourceControlSection;
    public Button RefreshSourceControl => RefreshSourceControlButton;
    public TextBlock GitBranch => BranchText;
    public ListView GitStatus => SourceControlList;

    public void SetWorkspaceEmpty(bool empty) =>
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
}
