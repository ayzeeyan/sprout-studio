using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SproutStudio.Views.Dialogs;

public sealed partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(
        string title,
        string message,
        string acceptLabel,
        bool showCancel = true)
        : this()
    {
        Title = title;
        this.FindControl<TextBlock>("Heading")!.Text = title;
        this.FindControl<TextBlock>("Message")!.Text = message;
        this.FindControl<Button>("AcceptButton")!.Content = acceptLabel;
        this.FindControl<Button>("CancelButton")!.IsVisible = showCancel;
    }

    private void Accept_Click(object? sender, RoutedEventArgs args) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs args) => Close(false);
}
