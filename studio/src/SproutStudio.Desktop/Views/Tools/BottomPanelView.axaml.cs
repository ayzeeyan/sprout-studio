using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using SproutStudio.ViewModels;

namespace SproutStudio.Views.Tools;

public sealed partial class BottomPanelView : UserControl
{
    public BottomPanelView() => InitializeComponent();

    private void Problems_DoubleTapped(object? sender, TappedEventArgs args)
    {
        if (args.Source is Control { DataContext: ProblemItemViewModel problem }
            && DataContext is MainWindowViewModel viewModel
            && viewModel.NavigateProblemCommand.CanExecute(problem))
        {
            viewModel.NavigateProblemCommand.Execute(problem);
            args.Handled = true;
        }
    }

    private async void ReplInput_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter
            && DataContext is MainWindowViewModel viewModel
            && viewModel.RunReplCommand.CanExecute(null))
        {
            args.Handled = true;
            await viewModel.RunReplCommand.ExecuteAsync(null);
            this.FindControl<TextBox>("ReplInputBox")?.Focus();
            Dispatcher.UIThread.Post(
                () => this.FindControl<ScrollViewer>("ReplOutputScroll")?.ScrollToEnd(),
                DispatcherPriority.Background);
        }
    }
}
