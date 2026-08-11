using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SproutStudio.Core;
using SproutStudio.Interop;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SproutStudio;

public sealed partial class MainWindow : Window
{
    private readonly StudioSession _session = new(new NativeSproutEngineClient());
    private readonly ObservableCollection<string> _workspace = [];
    private readonly ObservableCollection<string> _console = [];
    private readonly ObservableCollection<ProblemItem> _problems = [];
    private bool _updatingEditor;

    public MainWindow()
    {
        InitializeComponent();
        WorkspaceList.ItemsSource = _workspace;
        ConsoleList.ItemsSource = _console;
        ProblemsList.ItemsSource = _problems;
        ProfileCombo.SelectedIndex = 0;
        RefreshDocument();
    }

    private void NewButton_Click(object sender, RoutedEventArgs args)
    {
        _session.NewDocument(SelectedProfile());
        RefreshDocument();
        StatusText.Text = "New document";
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs args)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".spr");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            await _session.LoadDocumentAsync(file.Path);
            RefreshDocument();
            StatusText.Text = $"Opened {file.Name}";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open file", exception);
        }
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs args)
    {
        await ExecuteAsync(run: false);
    }

    private async void RunButton_Click(object sender, RoutedEventArgs args)
    {
        await ExecuteAsync(run: true);
    }

    private async Task ExecuteAsync(bool run)
    {
        _session.UpdateSource(Editor.Text);
        SetBusy(true, run ? "Running…" : "Checking…");
        try
        {
            EngineResponse response = run
                ? await _session.RunAsync()
                : await _session.CheckAsync();
            RefreshResults();
            StatusText.Text = response.Success
                ? run ? "Run completed" : "No problems found"
                : $"{response.Diagnostics.Count} problem(s)";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Engine operation failed", exception);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_updatingEditor)
        {
            _session.UpdateSource(Editor.Text);
        }
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ProfileCombo.SelectedIndex >= 0)
        {
            _session.UpdateProfile(SelectedProfile());
        }
    }

    private void ProblemsList_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not ProblemItem problem)
        {
            return;
        }

        int start = (int)Math.Clamp(problem.Diagnostic.Span.Start, 0, Editor.Text.Length);
        int end = (int)Math.Clamp(problem.Diagnostic.Span.End, start, Editor.Text.Length);
        Editor.Focus(FocusState.Programmatic);
        Editor.Select(start, Math.Max(1, end - start));
    }

    private void RefreshDocument()
    {
        _updatingEditor = true;
        Editor.Text = _session.Document.Source;
        _updatingEditor = false;
        EditorTabTitle.Text = _session.Document.DisplayName;
        Title = $"{_session.Document.DisplayName} — Sprout Studio";
        ProfileCombo.SelectedIndex = (int)_session.Document.Profile;
        _workspace.Clear();
        _workspace.Add(_session.Document.DisplayName);
        RefreshResults();
    }

    private void RefreshResults()
    {
        _console.Clear();
        foreach (string line in _session.ConsoleLines)
        {
            _console.Add(line);
        }

        _problems.Clear();
        foreach (StudioDiagnostic diagnostic in _session.Problems)
        {
            _problems.Add(new ProblemItem(diagnostic));
        }
    }

    private void SetBusy(bool busy, string status)
    {
        BusyIndicator.IsActive = busy;
        RunButton.IsEnabled = !busy;
        CheckButton.IsEnabled = !busy;
        NewButton.IsEnabled = !busy;
        OpenButton.IsEnabled = !busy;
        ProfileCombo.IsEnabled = !busy;
        StatusText.Text = status;
    }

    private void ShowHostFailure(string title, Exception exception)
    {
        _console.Clear();
        _problems.Clear();
        var diagnostic = new StudioDiagnostic(
            "SPT901",
            "error",
            title,
            exception.Message,
            new SourceSpan(_session.Document.DisplayName, 0, 0, 1, 1, 1, 1),
            []);
        _problems.Add(new ProblemItem(diagnostic));
        StatusText.Text = title;
    }

    private SyntaxProfile SelectedProfile() =>
        ProfileCombo.SelectedIndex switch
        {
            1 => SyntaxProfile.Structured,
            2 => SyntaxProfile.Advanced,
            _ => SyntaxProfile.Guided,
        };
}

public sealed class ProblemItem
{
    public ProblemItem(StudioDiagnostic diagnostic)
    {
        Diagnostic = diagnostic;
    }

    public StudioDiagnostic Diagnostic { get; }

    public string Code => Diagnostic.Code;

    public string Title => Diagnostic.Title;

    public string Message => Diagnostic.Suggestions.Count == 0
        ? Diagnostic.Message
        : $"{Diagnostic.Message} {string.Join(" ", Diagnostic.Suggestions)}";

    public string Location => $"{Diagnostic.Span.StartLine}:{Diagnostic.Span.StartColumn}";
}
