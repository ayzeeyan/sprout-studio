using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SproutStudio.Editor;
using SproutStudio.ViewModels;
using SproutStudio.Views.Modes;

namespace SproutStudio.Views.Search;

public sealed partial class SearchModeView : UserControl, IStudioModeView
{
    private readonly ProjectSearchService _search = new();
    private MainWindowViewModel? _workspace;
    private CancellationTokenSource? _searchCancellation;

    public SearchModeView()
    {
        InitializeComponent();
    }

    public Task ActivateAsync(MainWindowViewModel workspace, CancellationToken cancellationToken = default)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        DataContext = workspace;
        bool hasProject = workspace.CurrentProject is not null;
        this.FindControl<Button>("SearchButton")!.IsEnabled = hasProject;
        this.FindControl<TextBlock>("SearchStatus")!.Text = hasProject
            ? $"Root: {workspace.CurrentProject!.RootPath}"
            : "Open a project to search its files.";
        return Task.CompletedTask;
    }

    private async void Search_Click(object? sender, RoutedEventArgs args) => await SearchAsync();

    private async void QueryBox_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            args.Handled = true;
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        string query = this.FindControl<TextBox>("QueryBox")!.Text?.Trim() ?? string.Empty;
        string? root = _workspace?.CurrentProject?.RootPath;
        if (root is null || query.Length == 0)
        {
            this.FindControl<TextBlock>("SearchStatus")!.Text = root is null
                ? "Open a project before searching."
                : "Enter text to search for.";
            return;
        }

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _searchCancellation.Token;
        Button button = this.FindControl<Button>("SearchButton")!;
        button.IsEnabled = false;
        this.FindControl<TextBlock>("SearchStatus")!.Text = "Searching…";
        try
        {
            IReadOnlyList<ProjectSearchResult> results = await _search.SearchAsync(
                root,
                new ProjectSearchQuery(
                    query,
                    this.FindControl<TextBox>("FilterBox")!.Text ?? string.Empty,
                    this.FindControl<CheckBox>("MatchCaseBox")!.IsChecked == true,
                    this.FindControl<CheckBox>("WholeWordBox")!.IsChecked == true),
                cancellationToken);
            ListBox list = this.FindControl<ListBox>("ResultList")!;
            list.ItemsSource = results;
            list.IsVisible = results.Count > 0;
            this.FindControl<StackPanel>("EmptyState")!.IsVisible = results.Count == 0;
            this.FindControl<TextBlock>("EmptyTitle")!.Text = results.Count == 0
                ? "No matches found"
                : string.Empty;
            this.FindControl<TextBlock>("EmptyDescription")!.Text = results.Count == 0
                ? "Try a broader query or change the file filter."
                : string.Empty;
            this.FindControl<TextBlock>("ResultSummary")!.Text = $"{results.Count} result{(results.Count == 1 ? string.Empty : "s")}";
            this.FindControl<TextBlock>("SearchStatus")!.Text = results.Count >= 500
                ? "Showing the first 500 matches."
                : "Search complete.";
        }
        catch (OperationCanceledException)
        {
            // A newer query replaced this search.
        }
        catch (Exception exception)
        {
            this.FindControl<TextBlock>("SearchStatus")!.Text = exception.Message;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void ResultList_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_workspace is not null
            && this.FindControl<ListBox>("ResultList")?.SelectedItem is ProjectSearchResult result)
        {
            await _workspace.OpenFileLocationAsync(result.FilePath, result.Line, result.Column);
        }
    }
}
