using Microsoft.UI.Xaml.Controls;

namespace SproutStudio.Shell;

public sealed partial class StatusBar : UserControl
{
    public StatusBar() => InitializeComponent();

    public TextBlock Task => TaskText;
    public TextBlock Caret => CaretText;
    public TextBlock LanguageLabel => LanguageText;
    public TextBlock Branch => BranchText;
    public ProgressRing Progress => OperationProgress;
}
