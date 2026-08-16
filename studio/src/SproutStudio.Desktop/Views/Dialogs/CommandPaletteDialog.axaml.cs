using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using SproutStudio.Models;

namespace SproutStudio.Views.Dialogs;

public sealed partial class CommandPaletteDialog : Window
{
    private IReadOnlyList<CommandPaletteItem> _allCommands = [];

    public CommandPaletteDialog()
    {
        InitializeComponent();
        Opened += (_, _) => this.FindControl<TextBox>("QueryBox")?.Focus();
    }

    public CommandPaletteDialog(IReadOnlyList<CommandPaletteItem> commands)
        : this()
    {
        _allCommands = commands ?? throw new ArgumentNullException(nameof(commands));
        Refresh(string.Empty);
    }

    private void QueryBox_TextChanged(object? sender, TextChangedEventArgs args) =>
        Refresh(this.FindControl<TextBox>("QueryBox")?.Text ?? string.Empty);

    private void QueryBox_KeyDown(object? sender, KeyEventArgs args)
    {
        ListBox list = this.FindControl<ListBox>("CommandList")!;
        if (args.Key == Key.Down)
        {
            list.SelectedIndex = Math.Min(list.ItemCount - 1, list.SelectedIndex + 1);
            if (list.SelectedItem is { } selected)
            {
                list.ScrollIntoView(selected);
            }
            args.Handled = true;
        }
        else if (args.Key == Key.Up)
        {
            list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1);
            if (list.SelectedItem is { } selected)
            {
                list.ScrollIntoView(selected);
            }
            args.Handled = true;
        }
        else if (args.Key == Key.Enter)
        {
            AcceptSelection();
            args.Handled = true;
        }
        else if (args.Key == Key.Escape)
        {
            Close((string?)null);
            args.Handled = true;
        }
    }

    private void CommandList_DoubleTapped(object? sender, TappedEventArgs args) => AcceptSelection();

    private void Refresh(string query)
    {
        string needle = query.Trim().TrimStart('>');
        CommandPaletteItem[] matches = _allCommands
            .Where(item => string.IsNullOrWhiteSpace(needle)
                || item.Label.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToArray();
        ListBox list = this.FindControl<ListBox>("CommandList")!;
        list.ItemsSource = new ObservableCollection<CommandPaletteItem>(matches);
        list.SelectedIndex = matches.Length == 0 ? -1 : 0;
    }

    private void AcceptSelection()
    {
        if (this.FindControl<ListBox>("CommandList")?.SelectedItem is CommandPaletteItem command)
        {
            Close(command.Id);
        }
    }
}
