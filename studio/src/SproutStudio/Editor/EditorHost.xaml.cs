using Microsoft.UI.Xaml.Controls;

namespace SproutStudio.Editor;

public sealed partial class EditorHost : UserControl
{
    public EditorHost() => InitializeComponent();

    public TabView Tabs => DocumentTabs;
    public TextBlock Breadcrumb => BreadcrumbPathText;
    public Button FormatButton => FormatDocumentButton;
    public Button DefinitionButton => GoToDefinitionButton;
    public Button BreakpointButton => ToggleBreakpointButton;
    public TextBlock LineNumbers => LineNumbersText;
    public TextBox TextEditor => SourceEditor;
    public TextBlock Minimap => MinimapText;
    public Border CompletionPanel => CompletionPopup;
    public ListView Completions => CompletionItems;
}
