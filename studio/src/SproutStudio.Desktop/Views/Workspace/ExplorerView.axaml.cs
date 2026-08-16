using Avalonia.Controls;
using Avalonia.Input;
using SproutStudio.ViewModels;

namespace SproutStudio.Views.Workspace;

public sealed partial class ExplorerView : UserControl
{
    public ExplorerView() => InitializeComponent();

    public void FocusFilter() => this.FindControl<TextBox>("WorkspaceFilterBox")?.Focus();

    private void WorkspaceTree_DoubleTapped(object? sender, TappedEventArgs args)
    {
        if (args.Source is Control { DataContext: WorkspaceNodeViewModel item }
            && DataContext is MainWindowViewModel viewModel
            && viewModel.OpenWorkspaceItemCommand.CanExecute(item))
        {
            viewModel.OpenWorkspaceItemCommand.Execute(item);
            args.Handled = true;
        }
    }
}
