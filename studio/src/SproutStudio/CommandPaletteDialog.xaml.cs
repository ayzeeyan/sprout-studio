using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace SproutStudio;

internal sealed partial class CommandPaletteDialog : UserControl
{
    private readonly IReadOnlyList<StudioCommand> _commands;
    private readonly ObservableCollection<StudioCommand> _filtered = [];
    private ContentDialog? _dialog;
    private StudioCommand? _selected;

    public CommandPaletteDialog(IReadOnlyList<StudioCommand> commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        InitializeComponent();
        CommandList.ItemsSource = _filtered;
        Refresh();
    }

    public async Task<StudioCommand?> ShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        _dialog = new ContentDialog
        {
            Title = "Command Palette",
            Content = this,
            PrimaryButtonText = "Run",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };
        _dialog.Resources["ContentDialogMaxWidth"] = 650d;
        _dialog.Opened += (_, _) => SearchBox.Focus(FocusState.Programmatic);
        ContentDialogResult result = await _dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? CommandList.SelectedItem as StudioCommand
            : _selected;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        string query = SearchBox.Text.Trim();
        IEnumerable<StudioCommand> matches = _commands
            .Where(command => command.CanExecute())
            .Where(command => query.Length == 0
                || command.Display.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(command => command.Display.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(command => command.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.Title, StringComparer.OrdinalIgnoreCase);
        _filtered.Clear();
        foreach (StudioCommand command in matches)
        {
            _filtered.Add(command);
        }
        CommandList.SelectedIndex = _filtered.Count > 0 ? 0 : -1;
    }

    private void CommandList_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is StudioCommand command)
        {
            _selected = command;
            _dialog?.Hide();
        }
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Down && _filtered.Count > 0)
        {
            CommandList.SelectedIndex = Math.Min(_filtered.Count - 1, CommandList.SelectedIndex + 1);
            CommandList.ScrollIntoView(CommandList.SelectedItem);
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Up && _filtered.Count > 0)
        {
            CommandList.SelectedIndex = Math.Max(0, CommandList.SelectedIndex - 1);
            CommandList.ScrollIntoView(CommandList.SelectedItem);
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Enter && CommandList.SelectedItem is StudioCommand command)
        {
            _selected = command;
            _dialog?.Hide();
            args.Handled = true;
        }
    }
}
