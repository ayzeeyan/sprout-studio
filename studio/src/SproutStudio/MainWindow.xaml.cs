using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SproutStudio.Core;
using SproutStudio.Interop;
using SproutStudio.Toolchains;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SproutStudio;

public sealed partial class MainWindow : Window
{
    private readonly StudioSession _session;
    private readonly ProjectWorkspace _projects;
    private readonly ToolchainManager _toolchains;
    private readonly bool _openToolchainsOnActivation;
    private readonly ObservableCollection<ProjectFileItem> _workspace = [];
    private readonly ObservableCollection<RecentProject> _recentProjects = [];
    private readonly ObservableCollection<string> _console = [];
    private readonly ObservableCollection<ProblemItem> _problems = [];
    private bool _updatingEditor;

    public MainWindow(bool openToolchainsOnActivation = false)
    {
        var nativeClient = new NativeSproutEngineClient();
        _session = new StudioSession(nativeClient);
        _projects = new ProjectWorkspace(nativeClient);
        _toolchains = ToolchainManager.CreateDefault();
        _openToolchainsOnActivation = openToolchainsOnActivation;
        InitializeComponent();
        WorkspaceList.ItemsSource = _workspace;
        RecentProjectsList.ItemsSource = _recentProjects;
        ConsoleList.ItemsSource = _console;
        ProblemsList.ItemsSource = _problems;
        ProfileCombo.SelectedIndex = 0;
        RefreshDocument();
        Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        try
        {
            await _projects.LoadRecentAsync();
            RefreshRecentProjects();
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not load recent projects", exception);
        }
        if (_openToolchainsOnActivation)
        {
            try
            {
                await ShowToolchainSettingsAsync();
            }
            catch (Exception exception)
            {
                ShowHostFailure("Could not open toolchain settings", exception);
            }
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs args)
    {
        _projects.Close();
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
            _projects.Close();
            RefreshDocument();
            StatusText.Text = $"Opened {file.Name}";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open file", exception);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            _session.UpdateSource(Editor.Text);
            if (_session.Document.FilePath is null)
            {
                var picker = new FileSavePicker
                {
                    SuggestedFileName = Path.GetFileNameWithoutExtension(_session.Document.DisplayName),
                };
                picker.FileTypeChoices.Add("Sprout source", [".spr"]);
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
                if (file is null)
                {
                    return;
                }
                await _session.SaveDocumentAsAsync(file.Path);
                RefreshDocument();
            }
            else
            {
                await _session.SaveDocumentAsync();
            }
            StatusText.Text = $"Saved {_session.Document.DisplayName}";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not save file", exception);
        }
    }

    private async void NewProjectButton_Click(object sender, RoutedEventArgs args)
    {
        var nameBox = new TextBox
        {
            Header = "Project name",
            PlaceholderText = "HelloSprout",
            Text = "HelloSprout",
        };
        var templateBox = new ComboBox
        {
            Header = "Template",
            ItemsSource = new[] { "Guided", "Structured", "Advanced" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var fields = new StackPanel { Spacing = 12 };
        fields.Children.Add(nameBox);
        fields.Children.Add(templateBox);
        var dialog = new ContentDialog
        {
            Title = "Create Sprout project",
            Content = fields,
            PrimaryButtonText = "Choose location",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = ((FrameworkElement)Content).XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        string name = nameBox.Text.Trim();
        ProjectTemplate template = (ProjectTemplate)Math.Max(0, templateBox.SelectedIndex);
        string destination = Path.Combine(folder.Path, name);
        await CreateProjectAsync(destination, name, template);
    }

    private async void OpenProjectButton_Click(object sender, RoutedEventArgs args)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".toml");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await OpenProjectAsync(file.Path);
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

    private async void ToolchainsButton_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            await ShowToolchainSettingsAsync();
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open toolchain settings", exception);
        }
    }

    private async Task ShowToolchainSettingsAsync()
    {
        string previousTitle = Title;
        Title = "Sprout Studio — Toolchain Settings";
        try
        {
            var dialog = new ToolchainSettingsDialog(
                _toolchains,
                WindowNative.GetWindowHandle(this));
            await dialog.ShowAsync(((FrameworkElement)Content).XamlRoot);
            StatusText.Text = "Toolchain settings updated";
        }
        finally
        {
            Title = previousTitle;
        }
    }

    private async Task ExecuteAsync(bool run)
    {
        _session.UpdateSource(Editor.Text);
        SetBusy(true, run ? "Running…" : "Checking…");
        try
        {
            if (_session.Document.FilePath is not null)
            {
                await _session.SaveDocumentAsync();
            }
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

        int start = SourceTextCoordinates.Utf8OffsetToUtf16Index(
            Editor.Text,
            problem.Diagnostic.Span.Start);
        int end = SourceTextCoordinates.Utf8OffsetToUtf16Index(
            Editor.Text,
            problem.Diagnostic.Span.End);
        Editor.Focus(FocusState.Programmatic);
        Editor.Select(start, Math.Max(1, end - start));
    }

    private async void WorkspaceList_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not ProjectFileItem item
            || string.Equals(item.FullPath, _session.Document.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await _session.LoadDocumentAsync(item.FullPath);
            if (_projects.CurrentProject is ProjectInfo project)
            {
                _session.UpdateProfile(project.Profile);
            }
            RefreshDocument();
            StatusText.Text = $"Opened {item.DisplayName}";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open project file", exception);
        }
    }

    private async void RecentProjectsList_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is RecentProject project)
        {
            await OpenProjectAsync(project.RootPath);
        }
    }

    private void RefreshDocument()
    {
        _updatingEditor = true;
        Editor.Text = _session.Document.Source;
        _updatingEditor = false;
        EditorTabTitle.Text = _session.Document.DisplayName;
        Title = $"{_session.Document.DisplayName} — Sprout Studio";
        ProfileCombo.SelectedIndex = (int)_session.Document.Profile;
        SaveButton.IsEnabled = true;
        ProfileCombo.IsEnabled = _projects.CurrentProject is null;
        RefreshWorkspace();
        RefreshResults();
    }

    private void RefreshWorkspace()
    {
        _workspace.Clear();
        if (_projects.CurrentProject is not ProjectInfo project)
        {
            ProjectNameText.Text = "Single file";
            _workspace.Add(new ProjectFileItem(
                _session.Document.DisplayName,
                _session.Document.FilePath ?? string.Empty));
            return;
        }

        ProjectNameText.Text = $"{project.Name}  {project.Version}";
        foreach (string path in project.SourceFiles)
        {
            _workspace.Add(new ProjectFileItem(Path.GetRelativePath(project.RootPath, path), path));
        }
    }

    private void RefreshRecentProjects()
    {
        _recentProjects.Clear();
        foreach (RecentProject project in _projects.RecentProjects)
        {
            _recentProjects.Add(project);
        }
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
        SaveButton.IsEnabled = !busy;
        NewProjectButton.IsEnabled = !busy;
        OpenProjectButton.IsEnabled = !busy;
        ToolchainsButton.IsEnabled = !busy;
        ProfileCombo.IsEnabled = !busy && _projects.CurrentProject is null;
        StatusText.Text = status;
    }

    private async Task CreateProjectAsync(
        string destination,
        string name,
        ProjectTemplate template)
    {
        SetBusy(true, "Creating project…");
        try
        {
            ProjectInfo project = await _projects.CreateAsync(destination, name, template);
            await LoadProjectEntryAsync(project);
            StatusText.Text = $"Created project {project.Name}";
        }
        catch (ProjectValidationException exception)
        {
            ShowProjectFailure(exception);
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not create project", exception);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private async Task OpenProjectAsync(string path)
    {
        SetBusy(true, "Opening project…");
        try
        {
            ProjectInfo project = await _projects.OpenAsync(path);
            await LoadProjectEntryAsync(project);
            StatusText.Text = $"Opened project {project.Name}";
        }
        catch (ProjectValidationException exception)
        {
            ShowProjectFailure(exception);
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open project", exception);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private async Task LoadProjectEntryAsync(ProjectInfo project)
    {
        await _session.LoadDocumentAsync(project.EntryPath);
        _session.SetProjectContext(project.RootPath);
        _session.UpdateProfile(project.Profile);
        RefreshDocument();
        RefreshRecentProjects();
    }

    private void ShowProjectFailure(ProjectValidationException exception)
    {
        _console.Clear();
        _problems.Clear();
        foreach (ProjectDiagnostic item in exception.Diagnostics)
        {
            var diagnostic = new StudioDiagnostic(
                item.Code,
                "error",
                item.Title,
                item.Message,
                new SourceSpan(item.Path ?? "sprout.toml", 0, 0, 1, 1, 1, 1),
                [],
                [],
                []);
            _problems.Add(new ProblemItem(diagnostic));
        }
        StatusText.Text = "Project validation failed";
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
            [],
            [],
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

    public string Message
    {
        get
        {
            IEnumerable<string> details = Diagnostic.Suggestions.Concat(
                Diagnostic.RelatedInformation.Select(item => item.Message));
            string suffix = string.Join(" ", details);
            return suffix.Length == 0 ? Diagnostic.Message : $"{Diagnostic.Message} {suffix}";
        }
    }

    public string Location => $"{Diagnostic.Span.StartLine}:{Diagnostic.Span.StartColumn}";
}

public sealed record ProjectFileItem(string DisplayName, string FullPath);
