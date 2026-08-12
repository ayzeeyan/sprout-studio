using Microsoft.UI.Xaml.Controls;

namespace SproutStudio.Shell;

public sealed partial class MainToolbar : UserControl
{
    public MainToolbar() => InitializeComponent();

    public Button ProjectSelector => ProjectSelectorButton;
    public TextBlock ProjectName => ProjectNameText;
    public AppBarButton NewDocument => NewDocumentButton;
    public AppBarButton OpenDocument => OpenDocumentButton;
    public AppBarButton SaveDocument => SaveDocumentButton;
    public AppBarButton CreateProject => NewProjectButton;
    public AppBarButton OpenProject => OpenProjectButton;
    public ComboBox SyntaxProfile => SyntaxProfileBox;
    public AppBarButton Check => CheckButton;
    public AppBarButton Run => RunButton;
    public AppBarButton Debug => DebugButton;
    public AppBarButton Build => BuildButton;
}
