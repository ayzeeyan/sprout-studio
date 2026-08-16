using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SproutStudio.Services;
using SproutStudio.SourceControl;
using SproutStudio.Toolchains;
using SproutStudio.ViewModels;
using SproutStudio.Views.Modes;

namespace SproutStudio.Views.SourceControl;

public sealed partial class SourceControlModeView : UserControl, IStudioModeView
{
    private readonly GitRepositoryService _git;
    private readonly ObservableCollection<GitChangeRow> _changes = [];
    private readonly ObservableCollection<GitChangeRow> _staged = [];
    private MainWindowViewModel? _workspace;
    private GitRepositoryState? _state;
    private bool _busy;

    public SourceControlModeView()
        : this(new GitRepositoryService())
    {
    }

    internal SourceControlModeView(GitRepositoryService git)
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
        InitializeComponent();
        this.FindControl<ListBox>("ChangesList")!.ItemsSource = _changes;
        this.FindControl<ListBox>("StagedList")!.ItemsSource = _staged;
    }

    public async Task ActivateAsync(MainWindowViewModel workspace, CancellationToken cancellationToken = default)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        DataContext = workspace;
        await RefreshAsync(cancellationToken);
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs args) => await RefreshAsync();

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        string? root = _workspace?.CurrentProject?.RootPath;
        if (root is null)
        {
            RenderNoRepository("Open a project to use Source Control.");
            return;
        }
        await RunOperationAsync("Refreshing repository…", async () =>
        {
            await LoadStateCoreAsync(root, cancellationToken);
        });
    }

    private async Task LoadStateCoreAsync(string root, CancellationToken cancellationToken = default)
    {
        _state = await _git.GetStatusAsync(root, cancellationToken);
        ResetDiff();
        RenderState(_state);
        await _workspace!.RefreshSourceControlStateAsync(cancellationToken);
        ShowStatus(_state.Message);
    }

    private void RenderState(GitRepositoryState state)
    {
        if (!state.IsRepository)
        {
            RenderNoRepository(state.Message);
            return;
        }
        this.FindControl<TextBlock>("RepositoryText")!.Text = state.RepositoryRoot;
        this.FindControl<Border>("BranchBadge")!.IsVisible = true;
        this.FindControl<TextBlock>("BranchText")!.Text = state.BranchSummary;
        _changes.Clear();
        _staged.Clear();
        foreach (GitFileChange change in state.Changes)
        {
            if (change.IsUnstaged)
            {
                _changes.Add(new GitChangeRow(change, false));
            }
            if (change.IsStaged)
            {
                _staged.Add(new GitChangeRow(change, true));
            }
        }
        UpdateCollectionState();
        bool networkEnabled = state.HasRemote && !_busy;
        this.FindControl<Button>("RefreshButton")!.IsEnabled = !_busy;
        this.FindControl<Button>("FetchButton")!.IsEnabled = networkEnabled;
        this.FindControl<Button>("PullButton")!.IsEnabled = networkEnabled;
        this.FindControl<Button>("PushButton")!.IsEnabled = networkEnabled;
    }

    private void RenderNoRepository(string message)
    {
        _state = null;
        _changes.Clear();
        _staged.Clear();
        this.FindControl<TextBlock>("RepositoryText")!.Text = message;
        this.FindControl<Border>("BranchBadge")!.IsVisible = false;
        UpdateCollectionState();
        SetActionButtons(false);
        ResetDiff();
    }

    private void UpdateCollectionState()
    {
        this.FindControl<TextBlock>("ChangesCount")!.Text = _changes.Count.ToString();
        this.FindControl<TextBlock>("StagedCount")!.Text = _staged.Count.ToString();
        this.FindControl<TextBlock>("ChangesEmpty")!.IsVisible = _changes.Count == 0;
        this.FindControl<ListBox>("ChangesList")!.IsVisible = _changes.Count > 0;
        this.FindControl<TextBlock>("StagedEmpty")!.IsVisible = _staged.Count == 0;
        this.FindControl<ListBox>("StagedList")!.IsVisible = _staged.Count > 0;
        this.FindControl<Button>("StageAllButton")!.IsEnabled = _changes.Count > 0 && !_busy;
        this.FindControl<Button>("UnstageAllButton")!.IsEnabled = _staged.Count > 0 && !_busy;
        this.FindControl<Button>("CommitButton")!.IsEnabled = _staged.Count > 0 && !_busy;
        this.FindControl<Button>("CommitPushButton")!.IsEnabled = _staged.Count > 0 && _state?.HasRemote == true && !_busy;
    }

    private async void ChangeList_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (sender is ListBox { SelectedItem: GitChangeRow row } && _state is not null)
        {
            await LoadDiffAsync(row);
        }
    }

    private async Task LoadDiffAsync(GitChangeRow row)
    {
        await RunOperationAsync("Loading diff…", async () =>
        {
            string diff = await _git.GetDiffAsync(_state!.RepositoryRoot, row.Change.Path, row.Staged);
            this.FindControl<TextBlock>("DiffTitle")!.Text = row.Change.Path;
            this.FindControl<TextBlock>("DiffKind")!.Text = row.Staged ? "STAGED" : "WORKING TREE";
            DiffLineRow[] lines = string.IsNullOrEmpty(diff)
                ? [new DiffLineRow("No textual diff is available for this file.", Brushes.Gray)]
                : diff.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Split('\n')
                    .Select(ToDiffLine)
                    .ToArray();
            this.FindControl<ItemsControl>("DiffLines")!.ItemsSource = lines;
            this.FindControl<ScrollViewer>("DiffScroll")!.IsVisible = true;
            this.FindControl<TextBlock>("DiffEmpty")!.IsVisible = false;
            ShowStatus("Diff loaded.");
        });
    }

    private async void Stage_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: GitChangeRow row } && _state is not null)
        {
            await MutateAsync("Staging file…", () => _git.StageAsync(_state.RepositoryRoot, row.Change.Path));
        }
    }

    private async void Unstage_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: GitChangeRow row } && _state is not null)
        {
            await MutateAsync("Unstaging file…", () => _git.UnstageAsync(_state.RepositoryRoot, row.Change.Path));
        }
    }

    private async void StageAll_Click(object? sender, RoutedEventArgs args)
    {
        if (_state is not null)
        {
            await MutateAsync("Staging all changes…", () => _git.StageAllAsync(_state.RepositoryRoot));
        }
    }

    private async void UnstageAll_Click(object? sender, RoutedEventArgs args)
    {
        if (_state is not null)
        {
            await MutateAsync("Unstaging all changes…", () => _git.UnstageAllAsync(_state.RepositoryRoot));
        }
    }

    private async void Commit_Click(object? sender, RoutedEventArgs args) => await CommitAsync(push: false);

    private async void CommitPush_Click(object? sender, RoutedEventArgs args) => await CommitAsync(push: true);

    private async Task CommitAsync(bool push)
    {
        string message = this.FindControl<TextBox>("CommitMessageBox")!.Text?.Trim() ?? string.Empty;
        if (_state is null || message.Length == 0)
        {
            ShowStatus("Enter a commit message before committing.", error: true);
            return;
        }
        await RunOperationAsync(push ? "Committing changes…" : "Committing changes…", async () =>
        {
            string root = _state.RepositoryRoot;
            await _git.CommitAsync(root, message);
            this.FindControl<TextBox>("CommitMessageBox")!.Text = string.Empty;
            await LoadStateCoreAsync(root);
            if (!push)
            {
                ShowStatus("Commit completed.");
                return;
            }

            ShowStatus("Commit succeeded. Pushing…");
            try
            {
                await _git.PushAsync(root);
                await LoadStateCoreAsync(root);
                ShowStatus("Commit and push completed.");
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Commit completed, but push failed: {exception.Message}",
                    exception);
            }
        });
    }

    private async void Fetch_Click(object? sender, RoutedEventArgs args)
    {
        if (_state is not null)
        {
            await MutateAsync("Fetching…", () => _git.FetchAsync(_state.RepositoryRoot));
        }
    }

    private async void Pull_Click(object? sender, RoutedEventArgs args)
    {
        if (_state is not null)
        {
            await MutateAsync("Pulling with fast-forward only…", () => _git.PullAsync(_state.RepositoryRoot));
        }
    }

    private async void Push_Click(object? sender, RoutedEventArgs args)
    {
        if (_state is not null)
        {
            await MutateAsync("Pushing…", () => _git.PushAsync(_state.RepositoryRoot));
        }
    }

    private async Task MutateAsync(string status, Func<Task<ProcessResult>> operation)
    {
        await RunOperationAsync(status, async () =>
        {
            await operation();
            ShowStatus(status.TrimEnd('…') + " completed.");
            await LoadStateCoreAsync(_state!.RepositoryRoot);
        });
    }

    private async Task RunOperationAsync(string status, Func<Task> operation)
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        SetActionButtons(false);
        ShowStatus(status);
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            StudioLog.Error("Source control operation failed.", exception);
            ShowStatus(exception.Message, error: true);
        }
        finally
        {
            _busy = false;
            if (_state is not null)
            {
                RenderState(_state);
            }
            else
            {
                SetActionButtons(false);
            }
        }
    }

    private void SetActionButtons(bool enabled)
    {
        foreach (string name in new[] { "FetchButton", "PullButton", "PushButton", "RefreshButton", "StageAllButton", "UnstageAllButton", "CommitButton", "CommitPushButton" })
        {
            this.FindControl<Button>(name)!.IsEnabled = enabled;
        }
    }

    private void ShowStatus(string message, bool error = false)
    {
        TextBlock status = this.FindControl<TextBlock>("OperationStatus")!;
        status.Text = message;
        status.Foreground = new SolidColorBrush(Color.Parse(error ? "#F07178" : "#BAC2BB"));
        status.IsVisible = true;
    }

    private void ResetDiff()
    {
        this.FindControl<TextBlock>("DiffTitle")!.Text = "DIFF";
        this.FindControl<TextBlock>("DiffKind")!.Text = string.Empty;
        this.FindControl<ItemsControl>("DiffLines")!.ItemsSource = null;
        this.FindControl<ScrollViewer>("DiffScroll")!.IsVisible = false;
        TextBlock empty = this.FindControl<TextBlock>("DiffEmpty")!;
        empty.Text = _state?.IsRepository == true
            ? "Select a changed file to inspect its unified diff."
            : "This project is not a Git repository.";
        empty.IsVisible = true;
    }

    private static DiffLineRow ToDiffLine(string line)
    {
        IBrush foreground = line.StartsWith("+", StringComparison.Ordinal) && !line.StartsWith("+++", StringComparison.Ordinal)
            ? new SolidColorBrush(Color.Parse("#67D991"))
            : line.StartsWith("-", StringComparison.Ordinal) && !line.StartsWith("---", StringComparison.Ordinal)
                ? new SolidColorBrush(Color.Parse("#F07178"))
                : line.StartsWith("@@", StringComparison.Ordinal)
                    ? new SolidColorBrush(Color.Parse("#77AADD"))
                    : new SolidColorBrush(Color.Parse("#BAC2BB"));
        return new DiffLineRow(line, foreground);
    }
}

public sealed record GitChangeRow(GitFileChange Change, bool Staged)
{
    private string NormalizedPath => Change.Path.TrimEnd(
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar);

    public string Status => Change.DisplayStatus(Staged).ToString();

    public string FileName => Path.GetFileName(NormalizedPath);

    public string Directory => Path.GetDirectoryName(NormalizedPath) ?? string.Empty;
}

public sealed record DiffLineRow(string Text, IBrush Foreground);
