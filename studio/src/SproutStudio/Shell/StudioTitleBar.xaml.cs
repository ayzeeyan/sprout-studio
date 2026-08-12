using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SproutStudio.Shell;

public sealed partial class StudioTitleBar : UserControl
{
    public StudioTitleBar() => InitializeComponent();

    public FrameworkElement DragRegion => DragRegionRoot;

    public string Context
    {
        get => ContextText.Text;
        set => ContextText.Text = value;
    }
}
