using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SproutStudio.Academy;
using SproutStudio.Core;
using SproutStudio.Editor;
using SproutStudio.Interop;
using SproutStudio.Models;
using SproutStudio.Services;
using SproutStudio.SourceControl;
using SproutStudio.Toolchains;

namespace SproutStudio.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private static readonly IReadOnlyList<CommandPaletteItem> PaletteCommands =
    [
        new("file.new", "New File", "File", "Ctrl+N"),
        new("file.open", "Open File…", "File", "Ctrl+O"),
        new("file.save", "Save", "File", "Ctrl+S"),
        new("file.close", "Close Editor", "File", "Ctrl+W"),
        new("project.new", "New Project…", "Project"),
        new("project.open", "Open Project…", "Project"),
        new("sprout.check", "Sprout: Check", "Sprout", "Ctrl+F5"),
        new("sprout.run", "Sprout: Run", "Sprout", "F5"),
        new("sprout.debug", "Sprout: Debug", "Sprout", "F6"),
        new("project.build", "Project: Build", "Project", "Ctrl+Shift+B"),
        new("editor.format", "Editor: Format Document", "Editor", "Shift+Alt+F"),
        new("sprout.repl", "Sprout: Open REPL", "Sprout"),
        new("view.explorer", "View: Toggle Explorer", "View", "Ctrl+B"),
        new("view.sourceControl", "View: Source Control", "View"),
        new("view.inspector", "View: Toggle Inspector", "View"),
        new("view.bottom", "View: Toggle Bottom Panel", "View", "Ctrl+J"),
        new("view.academy", "Academy: Open", "View"),
        new("view.toolchains", "Toolchains: Open", "View"),
        new("view.docs", "Documentation: Open", "View"),
        new("project.packages", "Project: Open Package Hub", "Project"),
        new("view.settings", "Preferences: Open Settings", "Preferences"),
        new("help.about", "Help: About Sprout Studio", "Help"),
    ];

    private readonly NativeSproutEngineClient _engine = new();
    private readonly ProjectWorkspace _projects;
    private readonly EditorWorkspace _editor = new();
    private readonly ToolchainManager _toolchains = ToolchainManager.CreateDefault();
    private readonly GitRepositoryService _gitRepository = new();
    private readonly StudioLayoutStateStore _layoutStore = new();
    private readonly StudioSettingsStore _settingsStore = new();
    private readonly List<WorkspaceFile> _workspaceFiles = [];
    private CancellationTokenSource? _analysisCancellation;
    private IStudioDialogService? _dialogs;
    private DocumentTabViewModel? _selectedDocument;
    private ProjectInfo? _currentProject;
    private SyntaxProfile _selectedProfile = SyntaxProfile.Guided;
    private string _workspaceFilter = string.Empty;
    private string _statusText = "Ready";
    private string _taskText = "No active task";
    private bool _isBusy;
    private bool _activityVisible = true;
    private bool _explorerVisible = true;
    private bool _inspectorVisible = true;
    private bool _bottomPanelVisible = true;
    private bool _compactToolbar;
    private int _selectedBottomTab;
    private int _selectedInspectorTab;
    private ActivityDestination _selectedActivity = ActivityDestination.Explorer;
    private IReadOnlyList<DebugSnapshot> _debugSnapshots = [];
    private int _debugSnapshotIndex = -1;
    private bool _disposed;
    private bool _engineAvailable;
    private EngineInfo? _engineInfo;
    private IReadOnlyList<StudioDiagnostic> _currentDiagnostics = [];
    private StudioSettingsState _settings;
    private string _replInput = string.Empty;
    private string _replSource = string.Empty;
    private int _replOutputCount;
    private string _gitBranch = "Not a Git repository";
    private bool _hasSourceControl;
    private IReadOnlyList<CanvasCommand> _canvasCommands = [];

    public MainWindowViewModel()
    {
        _projects = new ProjectWorkspace(_engine);
        _settings = _settingsStore.Load();

        NewDocumentCommand = new RelayCommand(CreateNewDocument, () => !IsBusy);
        OpenDocumentCommand = new AsyncRelayCommand(OpenDocumentFromPickerAsync, () => !IsBusy);
        SaveDocumentCommand = new AsyncRelayCommand(SaveSelectedDocumentAsync, CanSaveDocument);
        NewProjectCommand = new AsyncRelayCommand(CreateProjectFromDialogAsync, CanUseProjectService);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectFromPickerAsync, CanUseProjectService);
        CloseDocumentCommand = new AsyncRelayCommand<DocumentTabViewModel>(CloseDocumentAsync);
        OpenWorkspaceItemCommand = new AsyncRelayCommand<WorkspaceNodeViewModel>(OpenWorkspaceItemAsync);
        OpenRecentProjectCommand = new AsyncRelayCommand<RecentProjectViewModel>(OpenRecentProjectAsync, item => item is not null && !IsBusy);
        RemoveRecentProjectCommand = new AsyncRelayCommand<RecentProjectViewModel>(RemoveRecentProjectAsync, item => item is not null && !IsBusy);
        CheckCommand = new AsyncRelayCommand(() => ExecuteActiveAsync(EngineOperation.Check), CanExecuteSprout);
        RunCommand = new AsyncRelayCommand(ExecuteRunAsync, CanRunActive);
        DebugCommand = new AsyncRelayCommand(() => ExecuteActiveAsync(EngineOperation.Debug), CanExecuteSprout);
        BuildCommand = new AsyncRelayCommand(ExecuteBuildAsync, CanBuildActive);
        FormatDocumentCommand = new AsyncRelayCommand(FormatDocumentAsync, CanFormatActive);
        OpenReplCommand = new RelayCommand(OpenRepl, () => EngineAvailable && !IsBusy);
        RunReplCommand = new AsyncRelayCommand(RunReplAsync, CanRunRepl);
        ClearReplCommand = new RelayCommand(ClearRepl);
        CommandPaletteCommand = new AsyncRelayCommand(ShowCommandPaletteAsync, () => !IsBusy);
        ToggleExplorerCommand = new RelayCommand(() => ExplorerVisible = !ExplorerVisible);
        ToggleInspectorCommand = new RelayCommand(() => InspectorVisible = !InspectorVisible);
        ToggleBottomPanelCommand = new RelayCommand(() => BottomPanelVisible = !BottomPanelVisible);
        ToggleActivityBarCommand = new RelayCommand(() => ActivityVisible = !ActivityVisible);
        ShowExplorerCommand = new RelayCommand(() => SelectActivity(ActivityDestination.Explorer));
        ShowSearchCommand = new RelayCommand(() => SelectActivity(ActivityDestination.Search));
        ShowSourceControlCommand = new AsyncRelayCommand(ShowSourceControlAsync, () => !IsBusy);
        RefreshSourceControlCommand = new AsyncRelayCommand(RefreshSourceControlAsync, () => HasProject && !IsBusy);
        ShowAcademyCommand = new AsyncRelayCommand(ShowAcademyAsync, () => !IsBusy);
        ShowDocumentationCommand = new AsyncRelayCommand(ShowDocumentationAsync, () => !IsBusy);
        ShowPackagesCommand = new AsyncRelayCommand(ShowPackagesAsync, () => !IsBusy);
        ShowToolchainsCommand = new AsyncRelayCommand(ShowToolchainsAsync, () => !IsBusy);
        ShowSettingsCommand = new AsyncRelayCommand(ShowSettingsAsync, () => !IsBusy);
        ShowAboutCommand = new AsyncRelayCommand(ShowAboutAsync, () => !IsBusy);
        PreviousDebugSnapshotCommand = new RelayCommand(PreviousDebugSnapshot, CanMoveDebugPrevious);
        NextDebugSnapshotCommand = new RelayCommand(NextDebugSnapshot, CanMoveDebugNext);
        ToggleBreakpointCommand = new RelayCommand(ToggleBreakpoint, () => SelectedDocument is not null);
        NavigateProblemCommand = new RelayCommand<ProblemItemViewModel>(NavigateToProblem);

        StudioLayoutState layout = _layoutStore.Load();
        _activityVisible = layout.ActivityVisible;
        _explorerVisible = layout.ExplorerVisible;
        _inspectorVisible = layout.InspectorVisible;
        _bottomPanelVisible = layout.BottomPanelVisible;
        _selectedBottomTab = layout.SelectedBottomTab;
        _selectedInspectorTab = layout.SelectedInspectorTab;
        _selectedActivity = ParseActivity(layout.SelectedActivity);
    }

    public ObservableCollection<DocumentTabViewModel> Documents { get; } = [];

    public ObservableCollection<WorkspaceNodeViewModel> WorkspaceRoots { get; } = [];

    public ObservableCollection<RecentProjectViewModel> RecentProjects { get; } = [];

    public ObservableCollection<ProblemItemViewModel> Problems { get; } = [];

    public ObservableCollection<ConsoleLineViewModel> ConsoleLines { get; } = [];

    public ObservableCollection<string> BuildOutput { get; } = [];

    public ObservableCollection<string> SourceControlChanges { get; } = [];

    public ObservableCollection<ConsoleLineViewModel> ReplLines { get; } = [];

    public ObservableCollection<OutlineItemViewModel> OutlineItems { get; } = [];

    public ObservableCollection<DebugVariableViewModel> DebugVariables { get; } = [];

    public ObservableCollection<DebugFrameViewModel> DebugFrames { get; } = [];

    public ObservableCollection<BreakpointViewModel> Breakpoints { get; } = [];

    public IReadOnlyList<SyntaxProfile> SyntaxProfiles { get; } = Enum.GetValues<SyntaxProfile>();

    public IRelayCommand NewDocumentCommand { get; }

    public IAsyncRelayCommand OpenDocumentCommand { get; }

    public IAsyncRelayCommand SaveDocumentCommand { get; }

    public IAsyncRelayCommand NewProjectCommand { get; }

    public IAsyncRelayCommand OpenProjectCommand { get; }

    public IAsyncRelayCommand<DocumentTabViewModel> CloseDocumentCommand { get; }

    public IAsyncRelayCommand<WorkspaceNodeViewModel> OpenWorkspaceItemCommand { get; }

    public IAsyncRelayCommand<RecentProjectViewModel> OpenRecentProjectCommand { get; }

    public IAsyncRelayCommand<RecentProjectViewModel> RemoveRecentProjectCommand { get; }

    public IAsyncRelayCommand CheckCommand { get; }

    public IAsyncRelayCommand RunCommand { get; }

    public IAsyncRelayCommand DebugCommand { get; }

    public IAsyncRelayCommand BuildCommand { get; }

    public IAsyncRelayCommand FormatDocumentCommand { get; }

    public IRelayCommand OpenReplCommand { get; }

    public IAsyncRelayCommand RunReplCommand { get; }

    public IRelayCommand ClearReplCommand { get; }

    public IAsyncRelayCommand CommandPaletteCommand { get; }

    public IRelayCommand ToggleExplorerCommand { get; }

    public IRelayCommand ToggleInspectorCommand { get; }

    public IRelayCommand ToggleBottomPanelCommand { get; }

    public IRelayCommand ToggleActivityBarCommand { get; }

    public IRelayCommand ShowExplorerCommand { get; }

    public IRelayCommand ShowSearchCommand { get; }

    public IAsyncRelayCommand ShowSourceControlCommand { get; }

    public IAsyncRelayCommand RefreshSourceControlCommand { get; }

    public IAsyncRelayCommand ShowAcademyCommand { get; }

    public IAsyncRelayCommand ShowDocumentationCommand { get; }

    public IAsyncRelayCommand ShowPackagesCommand { get; }

    public IAsyncRelayCommand ShowToolchainsCommand { get; }

    public IAsyncRelayCommand ShowSettingsCommand { get; }

    public IAsyncRelayCommand ShowAboutCommand { get; }

    public IRelayCommand PreviousDebugSnapshotCommand { get; }

    public IRelayCommand NextDebugSnapshotCommand { get; }

    public IRelayCommand ToggleBreakpointCommand { get; }

    public IRelayCommand<ProblemItemViewModel> NavigateProblemCommand { get; }

    public event EventHandler<SourceSpan>? SourceNavigationRequested;

    public event EventHandler? DiagnosticsChanged;

    public event EventHandler<LanguageAnalysisSnapshot>? LanguageAnalysisUpdated;

    public event EventHandler<DocumentationNavigationRequest>? DocumentationRequested;

    public IReadOnlyList<StudioDiagnostic> CurrentDiagnostics => _currentDiagnostics;

    public DocumentTabViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (!SetProperty(ref _selectedDocument, value))
            {
                return;
            }

            if (value is not null)
            {
                SelectedProfile = value.Model.Profile;
                ScheduleAnalysis(value, immediate: true);
            }
            else
            {
                ClearLanguageState();
            }

            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(ShowWelcome));
            OnPropertyChanged(nameof(WindowContext));
            OnPropertyChanged(nameof(CaretStatus));
            OnPropertyChanged(nameof(CurrentLanguage));
            NotifyCommandState();
        }
    }

    public ProjectInfo? CurrentProject
    {
        get => _currentProject;
        private set
        {
            if (!SetProperty(ref _currentProject, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ProjectSelectorText));
            OnPropertyChanged(nameof(ProjectHeader));
            OnPropertyChanged(nameof(WindowContext));
            OnPropertyChanged(nameof(HasProject));
            OnPropertyChanged(nameof(ShowProjectEmptyState));
            NotifyCommandState();
        }
    }

    public SyntaxProfile SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
            {
                return;
            }

            if (SelectedDocument is not null)
            {
                SelectedDocument.Model.Profile = value;
                ScheduleAnalysis(SelectedDocument, immediate: true);
            }
            OnPropertyChanged(nameof(CurrentProfile));
        }
    }

    public string WorkspaceFilter
    {
        get => _workspaceFilter;
        set
        {
            if (SetProperty(ref _workspaceFilter, value ?? string.Empty))
            {
                RebuildWorkspaceTree();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TaskText
    {
        get => _taskText;
        private set => SetProperty(ref _taskText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsReady));
                NotifyCommandState();
            }
        }
    }

    public bool ActivityVisible
    {
        get => _activityVisible;
        set => SetProperty(ref _activityVisible, value);
    }

    public bool ExplorerVisible
    {
        get => _explorerVisible;
        set => SetProperty(ref _explorerVisible, value);
    }

    public bool InspectorVisible
    {
        get => _inspectorVisible;
        set => SetProperty(ref _inspectorVisible, value);
    }

    public bool BottomPanelVisible
    {
        get => _bottomPanelVisible;
        set => SetProperty(ref _bottomPanelVisible, value);
    }

    public int SelectedBottomTab
    {
        get => _selectedBottomTab;
        set => SetProperty(ref _selectedBottomTab, Math.Clamp(value, 0, 3));
    }

    public int SelectedInspectorTab
    {
        get => _selectedInspectorTab;
        set => SetProperty(ref _selectedInspectorTab, Math.Clamp(value, 0, 4));
    }

    public ActivityDestination SelectedActivity
    {
        get => _selectedActivity;
        private set
        {
            if (SetProperty(ref _selectedActivity, value))
            {
                OnPropertyChanged(nameof(IsExplorerSelected));
                OnPropertyChanged(nameof(IsSearchSelected));
                OnPropertyChanged(nameof(IsSourceControlSelected));
                OnPropertyChanged(nameof(IsAcademySelected));
                OnPropertyChanged(nameof(IsDocumentationSelected));
                OnPropertyChanged(nameof(IsToolchainsSelected));
                OnPropertyChanged(nameof(IsPackagesSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
                OnPropertyChanged(nameof(IsWorkspaceMode));
                OnPropertyChanged(nameof(IsIntegratedMode));
            }
        }
    }

    public bool CompactToolbar
    {
        get => _compactToolbar;
        set
        {
            if (SetProperty(ref _compactToolbar, value))
            {
                OnPropertyChanged(nameof(ShowProjectToolbarGroup));
            }
        }
    }

    public bool ShowProjectToolbarGroup => !CompactToolbar;

    public bool IsReady => !IsBusy;

    public bool EngineAvailable => _engineAvailable;

    public string EngineVersion => _engineInfo?.EngineVersion ?? "Unavailable";

    public bool HasDocuments => Documents.Count > 0;

    public bool ShowWelcome => !HasDocuments;

    public bool HasRecentProjects => RecentProjects.Count > 0;

    public bool HasProject => CurrentProject is not null;

    public bool HasProblems => Problems.Count > 0;

    public bool NoProblems => !HasProblems;

    public bool HasConsoleOutput => ConsoleLines.Count > 0;

    public bool NoConsoleOutput => !HasConsoleOutput;

    public bool HasBuildOutput => BuildOutput.Count > 0;

    public bool NoBuildOutput => !HasBuildOutput;

    public bool HasDebugVariables => DebugVariables.Count > 0;

    public bool NoDebugVariables => !HasDebugVariables;

    public bool HasDebugFrames => DebugFrames.Count > 0;

    public bool NoDebugFrames => !HasDebugFrames;

    public bool HasBreakpoints => Breakpoints.Count > 0;

    public bool NoBreakpoints => !HasBreakpoints;

    public bool ShowProjectEmptyState => !HasProject;

    public string ProjectSelectorText => CurrentProject?.Name ?? "No Project";

    public string ProjectHeader => (CurrentProject?.Name ?? "NO PROJECT").ToUpperInvariant();

    public string WindowContext => CurrentProject is null
        ? SelectedDocument?.DisplayName ?? "No project open"
        : SelectedDocument is null
            ? CurrentProject.Name
            : $"{CurrentProject.Name} — {SelectedDocument.DisplayName}";

    public string CaretStatus => SelectedDocument is null
        ? "Ln 1, Col 1"
        : $"Ln {SelectedDocument.CaretLine}, Col {SelectedDocument.CaretColumn}";

    public string CurrentLanguage => SelectedDocument?.LanguageName ?? "Sprout";

    public string CurrentProfile => SelectedProfile.ToString();

    public int ProblemCount => Problems.Count;

    public int DebugSnapshotNumber => _debugSnapshotIndex + 1;

    public int DebugSnapshotCount => _debugSnapshots.Count;

    public bool HasDebugSnapshots => _debugSnapshots.Count > 0;

    public bool IsExplorerSelected => SelectedActivity == ActivityDestination.Explorer;

    public bool IsSearchSelected => SelectedActivity == ActivityDestination.Search;

    public bool IsSourceControlSelected => SelectedActivity == ActivityDestination.SourceControl;

    public bool IsAcademySelected => SelectedActivity == ActivityDestination.Academy;

    public bool IsDocumentationSelected => SelectedActivity == ActivityDestination.Documentation;

    public bool IsToolchainsSelected => SelectedActivity == ActivityDestination.Toolchains;

    public bool IsPackagesSelected => SelectedActivity == ActivityDestination.Packages;

    public bool IsSettingsSelected => SelectedActivity == ActivityDestination.Settings;

    public bool IsWorkspaceMode => SelectedActivity == ActivityDestination.Explorer;

    public bool IsIntegratedMode => SelectedActivity is ActivityDestination.Search
        or ActivityDestination.SourceControl
        or ActivityDestination.Academy
        or ActivityDestination.Documentation
        or ActivityDestination.Packages
        or ActivityDestination.Toolchains;

    public double EditorFontSize => _settings.EditorFontSize;

    public bool EditorWordWrap => _settings.WordWrap;

    public bool ShowEditorWhitespace => _settings.ShowWhitespace;

    public string ReplInput
    {
        get => _replInput;
        set
        {
            if (SetProperty(ref _replInput, value ?? string.Empty))
            {
                RunReplCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasReplOutput => ReplLines.Count > 0;

    public bool NoReplOutput => ReplLines.Count == 0;

    public string GitBranch
    {
        get => _gitBranch;
        private set => SetProperty(ref _gitBranch, value);
    }

    public bool HasSourceControl
    {
        get => _hasSourceControl;
        private set
        {
            if (SetProperty(ref _hasSourceControl, value))
            {
                OnPropertyChanged(nameof(NoSourceControlChanges));
            }
        }
    }

    public bool NoSourceControlChanges => HasSourceControl && SourceControlChanges.Count == 0;

    public IReadOnlyList<CanvasCommand> CanvasCommands
    {
        get => _canvasCommands;
        private set
        {
            if (SetProperty(ref _canvasCommands, value))
            {
                OnPropertyChanged(nameof(NoCanvasCommands));
            }
        }
    }

    public bool NoCanvasCommands => CanvasCommands.Count == 0;

    public StudioLayoutState InitialLayout => _layoutStore.Load();

    internal GitRepositoryService GitRepository => _gitRepository;

    internal ToolchainManager Toolchains => _toolchains;

    public void ActivateMode(ActivityDestination destination) => SelectActivity(destination);

    public void RequestDocumentation(string topicId, string? anchor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
        SelectActivity(ActivityDestination.Documentation);
        DocumentationRequested?.Invoke(this, new DocumentationNavigationRequest(topicId, anchor));
    }

    public async Task OpenDocumentationSourceAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        SelectActivity(ActivityDestination.Explorer);
        await OpenDocumentPathAsync(path, CurrentProject?.RootPath);
    }

    public async Task OpenFileLocationAsync(string path, int line, int column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        SelectActivity(ActivityDestination.Explorer);
        await OpenDocumentPathAsync(path, CurrentProject?.RootPath);
        if (SelectedDocument is not { } document
            || !string.Equals(document.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        int offset = Utf16OffsetAt(document.Source, line, column);
        long byteOffset = Encoding.UTF8.GetByteCount(document.Source.AsSpan(0, offset));
        document.SetCaret(line, column, offset);
        OnPropertyChanged(nameof(CaretStatus));
        SourceNavigationRequested?.Invoke(this, new SourceSpan(
            path,
            byteOffset,
            byteOffset,
            Math.Max(1, line),
            Math.Max(1, column),
            Math.Max(1, line),
            Math.Max(1, column)));
    }

    public Task OpenAcademyPracticeAsync(TrackDefinition track, LessonDefinition lesson)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(lesson);
        SelectActivity(ActivityDestination.Explorer);
        EditorDocument model = _editor.CreateSproutDocument(SelectedProfile);
        model.UpdateSource(string.Join(Environment.NewLine,
        [
            $"# Academy practice · {track.Title}",
            $"# Lesson: {lesson.Title}",
            $"# Objective: {lesson.Summary}",
            "",
            $"print(\"Practice workspace ready: {EscapeSproutString(lesson.Title)}\")",
            "",
        ]));
        SelectedDocument = AddDocument(model);
        StatusText = $"Opened Academy practice for {lesson.Title}";
        return Task.CompletedTask;
    }

    public void ApplyProjectUpdate(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (CurrentProject is not null
            && string.Equals(CurrentProject.RootPath, project.RootPath, StringComparison.OrdinalIgnoreCase))
        {
            CurrentProject = project;
        }
    }

    public Task RefreshSourceControlStateAsync(CancellationToken cancellationToken = default) =>
        RefreshSourceControlCoreAsync(cancellationToken);

    public void AttachDialogs(IStudioDialogService dialogs) =>
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task InitializeAsync(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        StudioLog.Information($"Sprout Studio starting ({Environment.ProcessPath}).");
        try
        {
            _engineInfo = await _engine.GetInfoAsync();
            _engineAvailable = true;
            OnPropertyChanged(nameof(EngineAvailable));
            OnPropertyChanged(nameof(EngineVersion));
            StudioLog.Information(
                $"Loaded Sprout engine {_engineInfo.EngineVersion} (ABI {_engineInfo.AbiVersion}, {_engineInfo.Architecture}, {_engineInfo.BuildConfiguration}).");
            NotifyCommandState();
        }
        catch (Exception exception)
        {
            _engineAvailable = false;
            OnPropertyChanged(nameof(EngineAvailable));
            await ReportFailureAsync("Sprout engine could not be loaded", exception);
            return;
        }
        await _projects.LoadRecentAsync();
        RefreshRecentProjects();

        string? projectPath = ArgumentValue(arguments, "--project");
        string? filePath = arguments.FirstOrDefault(argument =>
            !argument.StartsWith("--", StringComparison.Ordinal)
            && string.Equals(Path.GetExtension(argument), ".spr", StringComparison.OrdinalIgnoreCase));

        try
        {
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
            await OpenProjectPathAsync(projectPath);
            }
            else if (!string.IsNullOrWhiteSpace(filePath))
            {
                await OpenDocumentPathAsync(filePath, null);
            }

            if (arguments.Contains("--open-academy", StringComparer.OrdinalIgnoreCase))
            {
                await ShowAcademyAsync();
            }
            else if (arguments.Contains("--open-docs", StringComparer.OrdinalIgnoreCase))
            {
                await ShowDocumentationAsync();
            }
            else if (arguments.Contains("--open-toolchains", StringComparer.OrdinalIgnoreCase))
            {
                await ShowToolchainsAsync();
            }
        }
        catch (Exception exception)
        {
            await ReportFailureAsync("Startup failed", exception);
        }
    }

    public void UpdateCaret(int line, int column, int offset)
    {
        SelectedDocument?.SetCaret(line, column, offset);
        OnPropertyChanged(nameof(CaretStatus));
    }

    public void RequestLanguageAnalysis()
    {
        if (SelectedDocument is not null)
        {
            ScheduleAnalysis(SelectedDocument, immediate: true);
        }
    }

    public async Task<LanguageServiceResponse?> QueryLanguageAtOffsetAsync(
        DocumentTabViewModel document,
        int cursorUtf16Index,
        CancellationToken cancellationToken = default)
    {
        if (!ReferenceEquals(document, SelectedDocument)
            || document.Model.Language.Id != DocumentLanguage.Sprout
            || !EngineAvailable)
        {
            return null;
        }
        long version = document.AnalysisVersion;
        LanguageServiceResponse response = await _engine.QueryAsync(new LanguageServiceRequest(
            LanguageServiceOperation.Inspect,
            document.Model.Profile,
            document.FilePath ?? document.DisplayName,
            document.Source,
            Math.Clamp(cursorUtf16Index, 0, document.Source.Length)), cancellationToken);
        return version == document.AnalysisVersion && ReferenceEquals(document, SelectedDocument)
            ? response
            : null;
    }

    public async Task<bool> ConfirmCanCloseAsync()
    {
        DocumentTabViewModel[] dirtyDocuments = Documents.Where(item => item.IsDirty).ToArray();
        if (dirtyDocuments.Length == 0)
        {
            return true;
        }

        string names = string.Join(", ", dirtyDocuments.Select(item => item.DisplayName));
        return _dialogs is not null && await _dialogs.ConfirmAsync(
            "Discard unsaved changes?",
            $"The following documents contain unsaved changes: {names}",
            "Discard");
    }

    public void SaveLayout(
        double explorerWidth,
        double inspectorWidth,
        double bottomHeight,
        int? windowX,
        int? windowY,
        int? windowWidth,
        int? windowHeight)
    {
        var state = new StudioLayoutState
        {
            WindowX = windowX,
            WindowY = windowY,
            WindowWidth = windowWidth,
            WindowHeight = windowHeight,
            ExplorerWidth = explorerWidth,
            InspectorWidth = inspectorWidth,
            BottomPanelHeight = bottomHeight,
            ActivityVisible = ActivityVisible,
            ExplorerVisible = ExplorerVisible,
            InspectorVisible = InspectorVisible,
            BottomPanelVisible = BottomPanelVisible,
            SelectedBottomTab = SelectedBottomTab,
            SelectedInspectorTab = SelectedInspectorTab,
            SelectedActivity = ActivityKey(SelectedActivity),
        };
        _layoutStore.Save(state);
    }

    public async Task OpenDocumentPathAsync(string path, string? projectRoot)
    {
        BeginOperation("Opening file…");
        try
        {
            EditorDocument model = await _editor.OpenAsync(
                path,
                SelectedProfile,
                projectRoot ?? CurrentProject?.RootPath);
            DocumentTabViewModel document = Documents.FirstOrDefault(item => item.Id == model.Id)
                ?? AddDocument(model);
            SelectedDocument = document;
            StatusText = "Ready";
            StudioLog.Information($"Opened document {Path.GetFileName(path)}.");
        }
        catch (Exception exception)
        {
            await ReportFailureAsync("Could not open the document", exception);
        }
        finally
        {
            EndOperation();
        }
    }

    public async Task OpenProjectPathAsync(string path)
    {
        if (!await ConfirmWorkspaceReplacementAsync())
        {
            return;
        }

        BeginOperation("Opening project…");
        try
        {
            ProjectInfo project = await _projects.OpenAsync(path);
            RefreshRecentProjects();
            CurrentProject = project;
            SelectedProfile = project.Profile;
            _editor.CloseAll();
            Documents.Clear();
            SelectedDocument = null;
            await RefreshWorkspaceAsync(project.RootPath);
            await RefreshSourceControlAsync();
            string entryPath = Path.IsPathRooted(project.EntryPath)
                ? project.EntryPath
                : Path.Combine(project.RootPath, project.EntryPath);
            await OpenDocumentPathCoreAsync(entryPath, project.RootPath);
            StatusText = "Ready";
            TaskText = "No active task";
            StudioLog.Information($"Opened project {project.Name}.");
        }
        catch (Exception exception)
        {
            await ReportFailureAsync("Could not open the project", exception);
        }
        finally
        {
            EndOperation();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _analysisCancellation?.Cancel();
        _analysisCancellation?.Dispose();
    }

    private void CreateNewDocument()
    {
        EditorDocument model = _editor.CreateSproutDocument(SelectedProfile);
        SelectedDocument = AddDocument(model);
        StatusText = "Ready";
    }

    private DocumentTabViewModel AddDocument(EditorDocument model)
    {
        var document = new DocumentTabViewModel(model);
        document.SourceChanged += Document_SourceChanged;
        document.CloseRequested += Document_CloseRequested;
        Documents.Add(document);
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(ShowWelcome));
        return document;
    }

    private void Document_CloseRequested(object? sender, EventArgs args)
    {
        if (sender is DocumentTabViewModel document
            && CloseDocumentCommand.CanExecute(document))
        {
            CloseDocumentCommand.Execute(document);
        }
    }

    private void Document_SourceChanged(object? sender, EventArgs args)
    {
        if (sender is DocumentTabViewModel document)
        {
            ScheduleAnalysis(document);
            SaveDocumentCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task OpenDocumentFromPickerAsync()
    {
        if (_dialogs is null)
        {
            return;
        }
        string? path = await _dialogs.PickDocumentToOpenAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await OpenDocumentPathAsync(path, CurrentProject?.RootPath);
        }
    }

    private async Task SaveSelectedDocumentAsync()
    {
        if (SelectedDocument is null || _dialogs is null)
        {
            return;
        }

        BeginOperation("Saving…");
        try
        {
            if (SelectedDocument.FilePath is null)
            {
                string? path = await _dialogs.PickDocumentSavePathAsync(
                    SelectedDocument.DisplayName);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }
                await _editor.SaveAsAsync(SelectedDocument.Model, path);
            }
            else
            {
                await _editor.SaveAsync(SelectedDocument.Model);
            }
            SelectedDocument.RefreshMetadata();
            OnPropertyChanged(nameof(WindowContext));
            StatusText = "Saved";
            StudioLog.Information($"Saved document {SelectedDocument.DisplayName}.");
        }
        catch (Exception exception)
        {
            await ReportFailureAsync("Could not save the document", exception);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task CloseDocumentAsync(DocumentTabViewModel? document)
    {
        if (document is null)
        {
            return;
        }
        if (document.IsDirty)
        {
            bool discard = _dialogs is not null && await _dialogs.ConfirmAsync(
                "Close without saving?",
                $"{document.DisplayName} has unsaved changes.",
                "Close without saving");
            if (!discard)
            {
                return;
            }
        }

        int index = Documents.IndexOf(document);
        bool wasSelected = ReferenceEquals(SelectedDocument, document);
        document.SourceChanged -= Document_SourceChanged;
        document.CloseRequested -= Document_CloseRequested;
        _editor.Close(document.Model);
        Documents.Remove(document);
        if (wasSelected)
        {
            SelectedDocument = Documents.Count == 0
                ? null
                : Documents[Math.Clamp(index, 0, Documents.Count - 1)];
        }
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(ShowWelcome));
    }

    private async Task OpenProjectFromPickerAsync()
    {
        if (_dialogs is null)
        {
            return;
        }
        string? path = await _dialogs.PickProjectToOpenAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await OpenProjectPathAsync(path);
        }
    }

    private async Task CreateProjectFromDialogAsync()
    {
        if (_dialogs is null || !await ConfirmWorkspaceReplacementAsync())
        {
            return;
        }
        ProjectCreationRequest? request = await _dialogs.PromptCreateProjectAsync();
        if (request is null)
        {
            return;
        }

        BeginOperation("Creating project…");
        try
        {
            ProjectInfo project = await _projects.CreateAsync(
                request.Destination,
                request.Name,
                request.Template);
            RefreshRecentProjects();
            CurrentProject = project;
            SelectedProfile = project.Profile;
            _editor.CloseAll();
            Documents.Clear();
            await RefreshWorkspaceAsync(project.RootPath);
            await RefreshSourceControlAsync();
            string entryPath = Path.IsPathRooted(project.EntryPath)
                ? project.EntryPath
                : Path.Combine(project.RootPath, project.EntryPath);
            await OpenDocumentPathCoreAsync(entryPath, project.RootPath);
            StatusText = "Ready";
            StudioLog.Information($"Created project {project.Name}.");
        }
        catch (Exception exception)
        {
            await ReportFailureAsync("Could not create the project", exception);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task<bool> ConfirmWorkspaceReplacementAsync()
    {
        DocumentTabViewModel[] dirtyDocuments = Documents.Where(item => item.IsDirty).ToArray();
        if (dirtyDocuments.Length == 0)
        {
            return true;
        }
        if (_dialogs is null)
        {
            return false;
        }
        return await _dialogs.ConfirmAsync(
            "Open another project?",
            "Unsaved changes in the current workspace will be discarded.",
            "Discard and continue");
    }

    private async Task OpenDocumentPathCoreAsync(string path, string projectRoot)
    {
        EditorDocument model = await _editor.OpenAsync(path, SelectedProfile, projectRoot);
        SelectedDocument = AddDocument(model);
    }

    private async Task RefreshWorkspaceAsync(string rootPath)
    {
        IReadOnlyList<WorkspaceFile> files = await WorkspaceScanner.ScanAsync(rootPath);
        _workspaceFiles.Clear();
        _workspaceFiles.AddRange(files);
        RebuildWorkspaceTree();
    }

    private void RebuildWorkspaceTree()
    {
        WorkspaceRoots.Clear();
        if (CurrentProject is null)
        {
            return;
        }

        string filter = WorkspaceFilter.Trim();
        IEnumerable<WorkspaceFile> files = string.IsNullOrWhiteSpace(filter)
            ? _workspaceFiles
            : _workspaceFiles.Where(file => file.RelativePath.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase));
        foreach (WorkspaceFile file in files)
        {
            AddWorkspaceFile(file);
        }
        SortWorkspaceNodes(WorkspaceRoots);
    }

    private void AddWorkspaceFile(WorkspaceFile file)
    {
        string[] segments = file.RelativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        ObservableCollection<WorkspaceNodeViewModel> level = WorkspaceRoots;
        string relative = string.Empty;
        for (int index = 0; index < segments.Length; index++)
        {
            relative = string.IsNullOrEmpty(relative)
                ? segments[index]
                : Path.Combine(relative, segments[index]);
            bool isDirectory = index < segments.Length - 1;
            WorkspaceNodeViewModel? node = level.FirstOrDefault(item =>
                string.Equals(item.Name, segments[index], StringComparison.OrdinalIgnoreCase)
                && item.IsDirectory == isDirectory);
            if (node is null)
            {
                node = new WorkspaceNodeViewModel(
                    segments[index],
                    relative,
                    isDirectory ? null : file.FullPath,
                    isDirectory);
                level.Add(node);
            }
            level = node.Children;
        }
    }

    private static void SortWorkspaceNodes(ObservableCollection<WorkspaceNodeViewModel> nodes)
    {
        WorkspaceNodeViewModel[] ordered = nodes
            .OrderByDescending(item => item.IsDirectory)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        nodes.Clear();
        foreach (WorkspaceNodeViewModel node in ordered)
        {
            SortWorkspaceNodes(node.Children);
            nodes.Add(node);
        }
    }

    private async Task OpenWorkspaceItemAsync(WorkspaceNodeViewModel? item)
    {
        if (item is { IsDirectory: false, FullPath: not null })
        {
            await OpenDocumentPathAsync(item.FullPath, CurrentProject?.RootPath);
        }
    }

    private async Task OpenRecentProjectAsync(RecentProjectViewModel? recent)
    {
        if (recent is null)
        {
            return;
        }
        bool isAvailable = Directory.Exists(recent.RootPath)
            && File.Exists(Path.Combine(recent.RootPath, "sprout.toml"));
        if (!isAvailable)
        {
            StatusText = "Recent project unavailable";
            if (_dialogs is not null)
            {
                await _dialogs.ShowMessageAsync(
                    "Project not found",
                    $"Sprout Studio could not find this recent project:\n\n{recent.RootPath}\n\nYou can remove it from the list or open its new location.");
            }
            return;
        }
        await OpenProjectPathAsync(recent.RootPath);
    }

    private async Task RemoveRecentProjectAsync(RecentProjectViewModel? recent)
    {
        if (recent is null)
        {
            return;
        }
        await _projects.RemoveRecentAsync(recent.RootPath);
        RefreshRecentProjects();
        StatusText = "Recent project removed";
    }

    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (RecentProject project in _projects.RecentProjects.Take(10))
        {
            RecentProjects.Add(RecentProjectViewModel.FromModel(project));
        }
        OnPropertyChanged(nameof(HasRecentProjects));
    }

    private async Task ExecuteActiveAsync(EngineOperation operation)
    {
        if (SelectedDocument is null)
        {
            return;
        }

        CancelAnalysis();
        BeginOperation(operation switch
        {
            EngineOperation.Check => "Checking…",
            EngineOperation.Run => "Running…",
            EngineOperation.Debug => "Starting debugger…",
            _ => "Working…",
        });
        try
        {
            DocumentTabViewModel document = SelectedDocument;
            var request = new EngineRequest(
                operation,
                document.Model.Profile,
                document.FilePath ?? document.DisplayName,
                document.Source,
                document.Model.ProjectRoot ?? CurrentProject?.RootPath);
            EngineResponse response = await _engine.ExecuteAsync(request);
            ApplyDiagnostics(response.Diagnostics);
            if (operation is EngineOperation.Run or EngineOperation.Debug)
            {
                CanvasCommands = response.CanvasCommands ?? [];
                RenderConsole(response, operation);
                SelectedBottomTab = 1;
                BottomPanelVisible = true;
            }
            else if (Problems.Count > 0)
            {
                SelectedBottomTab = 0;
                BottomPanelVisible = true;
            }

            if (operation == EngineOperation.Debug)
            {
                ApplyDebugSnapshots(response.DebugSnapshots ?? []);
            }
            StatusText = response.Success ? "Ready" : "Problems detected";
            StudioLog.Information($"{operation} completed for {document.DisplayName}; success={response.Success}.");
        }
        catch (Exception exception)
        {
            await ReportFailureAsync($"{operation} failed", exception);
        }
        finally
        {
            EndOperation();
            if (SelectedDocument is not null)
            {
                ScheduleAnalysis(SelectedDocument);
            }
        }
    }

    private Task ExecuteRunAsync()
    {
        if (SelectedDocument?.Model.Language.Id == DocumentLanguage.Sprout)
        {
            return ExecuteActiveAsync(EngineOperation.Run);
        }
        return ExecuteExternalAsync(ToolchainAction.Run);
    }

    private Task ExecuteBuildAsync()
    {
        if (SelectedDocument is { Model.Language.Id: not DocumentLanguage.Sprout })
        {
            return ExecuteExternalAsync(ToolchainAction.Build);
        }
        return BuildProjectAsync();
    }

    private async Task ExecuteExternalAsync(ToolchainAction action)
    {
        DocumentTabViewModel? document = SelectedDocument;
        string? toolchainId = document?.Model.Language.ToolchainId;
        if (document is null || toolchainId is null)
        {
            return;
        }
        if (document.IsDirty || document.FilePath is null)
        {
            await SaveSelectedDocumentAsync();
        }
        if (document.IsDirty || document.FilePath is null)
        {
            StatusText = "Save required";
            return;
        }

        string workingDirectory = document.Model.ProjectRoot
            ?? Path.GetDirectoryName(document.FilePath)
            ?? Environment.CurrentDirectory;
        IReadOnlyList<string> arguments = ExternalArguments(document, action);
        BeginOperation(action == ToolchainAction.Run ? "Running external toolchain…" : "Building external project…");
        try
        {
            ProcessResult result = await _toolchains.ExecuteAsync(
                toolchainId,
                action,
                workingDirectory,
                arguments);
            if (action == ToolchainAction.Run)
            {
                RenderExternalConsole(document, result);
                SelectedBottomTab = 1;
            }
            else
            {
                RenderExternalBuild(document, result, action);
                SelectedBottomTab = 2;
            }
            BottomPanelVisible = true;
            StatusText = result.Success ? "Ready" : $"{document.LanguageName} {action.ToString().ToLowerInvariant()} failed";
            StudioLog.Information(
                $"External {action} completed for {document.DisplayName}; toolchain={toolchainId}; exit={result.ExitCode}.");
        }
        catch (Exception exception)
        {
            await ReportFailureAsync($"{document.LanguageName} {action.ToString().ToLowerInvariant()} failed", exception);
        }
        finally
        {
            EndOperation();
        }
    }

    private static IReadOnlyList<string> ExternalArguments(
        DocumentTabViewModel document,
        ToolchainAction action)
    {
        if (document.FilePath is null)
        {
            return [];
        }
        return (document.Model.Language.Id, action) switch
        {
            (DocumentLanguage.Python, ToolchainAction.Run) => [document.FilePath],
            (DocumentLanguage.Java, ToolchainAction.Run or ToolchainAction.Build) => [document.FilePath],
            _ => [],
        };
    }

    private void RenderExternalConsole(DocumentTabViewModel document, ProcessResult result)
    {
        ConsoleLines.Clear();
        ConsoleLines.Add(new ConsoleLineViewModel($"{document.LanguageName} Toolchain", ConsoleLineKind.Muted));
        ConsoleLines.Add(new ConsoleLineViewModel(string.Empty, ConsoleLineKind.Normal));
        ConsoleLines.Add(new ConsoleLineViewModel($"> Run {document.DisplayName}", ConsoleLineKind.Muted));
        ConsoleLines.Add(new ConsoleLineViewModel(string.Empty, ConsoleLineKind.Normal));
        foreach (string line in OutputLines(result.StandardOutput))
        {
            ConsoleLines.Add(new ConsoleLineViewModel(line, ConsoleLineKind.Normal));
        }
        foreach (string line in OutputLines(result.StandardError))
        {
            ConsoleLines.Add(new ConsoleLineViewModel(line, ConsoleLineKind.Error));
        }
        ConsoleLines.Add(new ConsoleLineViewModel(string.Empty, ConsoleLineKind.Normal));
        ConsoleLines.Add(new ConsoleLineViewModel(
            result.Success
                ? $"✓ Program finished successfully in {result.Duration.TotalMilliseconds:0} ms."
                : $"Program exited with code {result.ExitCode?.ToString() ?? "unknown"}.",
            result.Success ? ConsoleLineKind.Success : ConsoleLineKind.Error));
        OnPropertyChanged(nameof(HasConsoleOutput));
        OnPropertyChanged(nameof(NoConsoleOutput));
    }

    private void RenderExternalBuild(
        DocumentTabViewModel document,
        ProcessResult result,
        ToolchainAction action)
    {
        BuildOutput.Clear();
        BuildOutput.Add($"> {action} {document.DisplayName} with {document.LanguageName}");
        foreach (string line in OutputLines(result.StandardOutput))
        {
            BuildOutput.Add(line);
        }
        foreach (string line in OutputLines(result.StandardError))
        {
            BuildOutput.Add($"ERROR: {line}");
        }
        BuildOutput.Add(result.Success
            ? $"✓ Completed in {result.Duration.TotalMilliseconds:0} ms."
            : $"Failed with exit code {result.ExitCode?.ToString() ?? "unknown"}.");
        OnPropertyChanged(nameof(HasBuildOutput));
        OnPropertyChanged(nameof(NoBuildOutput));
    }

    private static IEnumerable<string> OutputLines(string output) =>
        output.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private async Task FormatDocumentAsync()
    {
        DocumentTabViewModel? document = SelectedDocument;
        if (document is null)
        {
            return;
        }
        if (document.Model.Language.Id == DocumentLanguage.Sprout)
        {
            BeginOperation("Formatting document…");
            try
            {
                LanguageServiceResponse response = await _engine.QueryAsync(new LanguageServiceRequest(
                    LanguageServiceOperation.Format,
                    document.Model.Profile,
                    document.FilePath ?? document.DisplayName,
                    document.Source,
                    document.CaretOffset));
                ApplyDiagnostics(response.Diagnostics);
                RefreshOutline(response.Symbols);
                if (response.Success && response.FormattedSource is not null)
                {
                    document.ReplaceSource(response.FormattedSource);
                    StatusText = "Document formatted";
                }
                else
                {
                    StatusText = "Formatting found problems";
                }
            }
            catch (Exception exception)
            {
                await ReportFailureAsync("Format failed", exception);
            }
            finally
            {
                EndOperation();
                ScheduleAnalysis(document);
            }
            return;
        }

        if (document.IsDirty || document.FilePath is null)
        {
            await SaveSelectedDocumentAsync();
        }
        if (document.IsDirty || document.FilePath is null || document.Model.Language.ToolchainId is null)
        {
            return;
        }
        string workingDirectory = document.Model.ProjectRoot
            ?? Path.GetDirectoryName(document.FilePath)
            ?? Environment.CurrentDirectory;
        BeginOperation("Formatting document…");
        try
        {
            ProcessResult result = await _toolchains.ExecuteAsync(
                document.Model.Language.ToolchainId,
                ToolchainAction.Format,
                workingDirectory);
            RenderExternalBuild(document, result, ToolchainAction.Format);
            SelectedBottomTab = 2;
            BottomPanelVisible = true;
            if (result.Success)
            {
                document.ReplaceSource(await File.ReadAllTextAsync(document.FilePath));
                await _editor.SaveAsync(document.Model);
                document.RefreshMetadata();
            }
            StatusText = result.Success ? "Document formatted" : "Format failed";
        }
        catch (Exception exception)
        {
            await ReportFailureAsync("Format failed", exception);
        }
        finally
        {
            EndOperation();
        }
    }

    private void OpenRepl()
    {
        SelectedBottomTab = 3;
        BottomPanelVisible = true;
    }

    private async Task RunReplAsync()
    {
        string input = ReplInput.Trim();
        if (input.Length == 0)
        {
            return;
        }
        OpenRepl();
        if (string.Equals(input, ":clear", StringComparison.OrdinalIgnoreCase))
        {
            ClearRepl();
            return;
        }

        BeginOperation("Evaluating REPL input…");
        ReplLines.Add(new ConsoleLineViewModel($"> {input}", ConsoleLineKind.Muted));
        try
        {
            string statementCandidate = _replSource + input + "\n";
            string candidate = statementCandidate;
            EngineResponse response;
            if (IsExplicitPrintCall(input))
            {
                response = await ExecuteReplSourceAsync(candidate);
            }
            else
            {
                candidate = _replSource + CreateReplEchoStatement(input, SelectedProfile);
                response = await ExecuteReplSourceAsync(candidate);
                if (!response.Success && response.Diagnostics.Count > 0
                    && response.Diagnostics.All(IsSyntaxDiagnostic))
                {
                    candidate = statementCandidate;
                    response = await ExecuteReplSourceAsync(candidate);
                }
            }
            if (response.Success)
            {
                string[] newOutput = response.Output.Skip(_replOutputCount).ToArray();
                foreach (string line in newOutput)
                {
                    ReplLines.Add(new ConsoleLineViewModel(line, ConsoleLineKind.Normal));
                }
                _replSource = candidate;
                _replOutputCount = response.Output.Count;
                if (newOutput.Length == 0)
                {
                    ReplLines.Add(new ConsoleLineViewModel("✓ Statement accepted", ConsoleLineKind.Success));
                }
                StatusText = "Ready";
            }
            else
            {
                foreach (StudioDiagnostic diagnostic in response.Diagnostics)
                {
                    ReplLines.Add(new ConsoleLineViewModel(
                        $"{diagnostic.Code}: {diagnostic.Message}",
                        diagnostic.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
                            ? ConsoleLineKind.Warning
                            : ConsoleLineKind.Error));
                }
                StatusText = "REPL input rejected";
            }
            ReplInput = string.Empty;
            OnPropertyChanged(nameof(HasReplOutput));
            OnPropertyChanged(nameof(NoReplOutput));
        }
        catch (Exception exception)
        {
            ReplLines.Add(new ConsoleLineViewModel(exception.Message, ConsoleLineKind.Error));
            StudioLog.Error("REPL evaluation failed.", exception);
        }
        finally
        {
            EndOperation();
        }
    }

    private Task<EngineResponse> ExecuteReplSourceAsync(string source) =>
        _engine.ExecuteAsync(new EngineRequest(
            EngineOperation.Run,
            SelectedProfile,
            "repl.spr",
            source,
            CurrentProject?.RootPath));

    private static string CreateReplEchoStatement(string input, SyntaxProfile profile)
    {
        string expression = profile == SyntaxProfile.Structured
            ? input.TrimEnd().TrimEnd(';').TrimEnd()
            : input;
        return profile == SyntaxProfile.Structured
            ? $"print({expression});\n"
            : $"print({expression})\n";
    }

    private static bool IsExplicitPrintCall(string input)
    {
        ReadOnlySpan<char> remaining = input.AsSpan().TrimStart();
        const string PrintAction = "print";
        if (!remaining.StartsWith(PrintAction, StringComparison.Ordinal))
        {
            return false;
        }
        remaining = remaining[PrintAction.Length..].TrimStart();
        return remaining.StartsWith("(", StringComparison.Ordinal);
    }

    private static bool IsSyntaxDiagnostic(StudioDiagnostic diagnostic)
    {
        ReadOnlySpan<char> code = diagnostic.Code.AsSpan();
        return code.StartsWith("SPT", StringComparison.Ordinal)
            && int.TryParse(code[3..], out int number)
            && number < 200;
    }

    private void ClearRepl()
    {
        ReplLines.Clear();
        ReplInput = string.Empty;
        _replSource = string.Empty;
        _replOutputCount = 0;
        OnPropertyChanged(nameof(HasReplOutput));
        OnPropertyChanged(nameof(NoReplOutput));
    }

    private void RenderConsole(EngineResponse response, EngineOperation operation)
    {
        ConsoleLines.Clear();
        ConsoleLines.Add(new ConsoleLineViewModel("Sprout Runtime", ConsoleLineKind.Muted));
        ConsoleLines.Add(new ConsoleLineViewModel(string.Empty, ConsoleLineKind.Normal));
        ConsoleLines.Add(new ConsoleLineViewModel(
            $"> {operation}: {SelectedDocument?.DisplayName}",
            ConsoleLineKind.Muted));
        ConsoleLines.Add(new ConsoleLineViewModel(string.Empty, ConsoleLineKind.Normal));
        foreach (string line in response.Output)
        {
            ConsoleLines.Add(new ConsoleLineViewModel(line, ConsoleLineKind.Normal));
        }
        ConsoleLines.Add(new ConsoleLineViewModel(string.Empty, ConsoleLineKind.Normal));
        ConsoleLines.Add(new ConsoleLineViewModel(
            response.Success ? "✓ Program finished successfully." : "Program finished with errors.",
            response.Success ? ConsoleLineKind.Success : ConsoleLineKind.Error));
        OnPropertyChanged(nameof(HasConsoleOutput));
        OnPropertyChanged(nameof(NoConsoleOutput));
    }

    private void ApplyDiagnostics(IEnumerable<StudioDiagnostic> diagnostics)
    {
        _currentDiagnostics = diagnostics.ToArray();
        Problems.Clear();
        foreach (StudioDiagnostic diagnostic in _currentDiagnostics)
        {
            Problems.Add(ProblemItemViewModel.FromDiagnostic(diagnostic));
        }
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(NoProblems));
        OnPropertyChanged(nameof(ProblemCount));
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleAnalysis(DocumentTabViewModel document, bool immediate = false)
    {
        if (document.Model.Language.Id != DocumentLanguage.Sprout || IsBusy)
        {
            return;
        }

        CancelAnalysis();
        var cancellation = new CancellationTokenSource();
        _analysisCancellation = cancellation;
        long version = document.AnalysisVersion;
        _ = AnalyzeDocumentAsync(document, version, immediate, cancellation.Token);
    }

    private async Task AnalyzeDocumentAsync(
        DocumentTabViewModel document,
        long version,
        bool immediate,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!immediate)
            {
                await Task.Delay(320, cancellationToken);
            }
            var request = new LanguageServiceRequest(
                LanguageServiceOperation.Inspect,
                document.Model.Profile,
                document.FilePath ?? document.DisplayName,
                document.Source,
                document.CaretOffset);
            LanguageServiceResponse response = await _engine.QueryAsync(request, cancellationToken);
            if (cancellationToken.IsCancellationRequested
                || version != document.AnalysisVersion
                || !ReferenceEquals(document, SelectedDocument))
            {
                return;
            }
            ApplyDiagnostics(response.Diagnostics);
            RefreshOutline(response.Symbols);
            LanguageAnalysisUpdated?.Invoke(
                this,
                new LanguageAnalysisSnapshot(document.Id, version, response));
        }
        catch (OperationCanceledException)
        {
            // A newer document version superseded this request.
        }
        catch (Exception exception)
        {
            StudioLog.Warning("Background language analysis failed.", exception);
            StatusText = "Analysis unavailable";
        }
    }

    private void RefreshOutline(IEnumerable<DocumentSymbol> symbols)
    {
        OutlineItems.Clear();
        foreach (DocumentSymbol symbol in symbols)
        {
            AddOutlineItem(symbol, 0);
        }
    }

    private void AddOutlineItem(DocumentSymbol symbol, int depth)
    {
        OutlineItems.Add(new OutlineItemViewModel(
            symbol.Name,
            symbol.Kind,
            symbol.Detail,
            symbol.Span,
            depth));
        foreach (DocumentSymbol child in symbol.Children)
        {
            AddOutlineItem(child, depth + 1);
        }
    }

    private void ClearLanguageState()
    {
        CancelAnalysis();
        Problems.Clear();
        _currentDiagnostics = [];
        OutlineItems.Clear();
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(NoProblems));
        OnPropertyChanged(nameof(ProblemCount));
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CancelAnalysis()
    {
        CancellationTokenSource? cancellation = _analysisCancellation;
        _analysisCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private async Task BuildProjectAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }
        BeginOperation("Building project…");
        BuildOutput.Clear();
        BuildOutput.Add($"> Build {CurrentProject.Name}");
        try
        {
            BuildResponse response = await _engine.BuildAsync(CurrentProject.RootPath);
            foreach (ProjectDiagnostic diagnostic in response.Diagnostics)
            {
                BuildOutput.Add($"{diagnostic.Code}: {diagnostic.Message}");
            }
            if (response.Success && response.Artifact is not null)
            {
                BuildOutput.Add(string.Empty);
                BuildOutput.Add($"✓ Built {response.Artifact.SourceCount} source file(s).");
                BuildOutput.Add(response.Artifact.Path);
                StatusText = "Build succeeded";
            }
            else
            {
                BuildOutput.Add("Build failed.");
                StatusText = "Build failed";
            }
            SelectedBottomTab = 2;
            BottomPanelVisible = true;
            OnPropertyChanged(nameof(HasBuildOutput));
            OnPropertyChanged(nameof(NoBuildOutput));
            StudioLog.Information($"Build completed for {CurrentProject.Name}; success={response.Success}.");
        }
        catch (Exception exception)
        {
            BuildOutput.Add(exception.Message);
            OnPropertyChanged(nameof(HasBuildOutput));
            OnPropertyChanged(nameof(NoBuildOutput));
            await ReportFailureAsync("Build failed", exception);
        }
        finally
        {
            EndOperation();
        }
    }

    private void ApplyDebugSnapshots(IReadOnlyList<DebugSnapshot> snapshots)
    {
        _debugSnapshots = snapshots;
        _debugSnapshotIndex = snapshots.Count == 0 ? -1 : 0;
        InspectorVisible = snapshots.Count > 0;
        SelectedInspectorTab = 0;
        RefreshDebugSnapshot();
    }

    private void RefreshDebugSnapshot()
    {
        DebugVariables.Clear();
        DebugFrames.Clear();
        if (_debugSnapshotIndex >= 0 && _debugSnapshotIndex < _debugSnapshots.Count)
        {
            DebugSnapshot snapshot = _debugSnapshots[_debugSnapshotIndex];
            CanvasCommands = snapshot.CanvasCommands;
            foreach (DebugVariable variable in snapshot.Variables)
            {
                DebugVariables.Add(new DebugVariableViewModel(
                    variable.Name,
                    variable.TypeName,
                    variable.Value));
            }
            foreach (DebugStackFrame frame in snapshot.CallStack)
            {
                DebugFrames.Add(new DebugFrameViewModel(
                    frame.Name,
                    $"{Path.GetFileName(frame.Span.File)}:{frame.Span.StartLine}",
                    frame.Span));
            }
        }
        OnPropertyChanged(nameof(HasDebugSnapshots));
        OnPropertyChanged(nameof(DebugSnapshotNumber));
        OnPropertyChanged(nameof(DebugSnapshotCount));
        OnPropertyChanged(nameof(HasDebugVariables));
        OnPropertyChanged(nameof(NoDebugVariables));
        OnPropertyChanged(nameof(HasDebugFrames));
        OnPropertyChanged(nameof(NoDebugFrames));
        PreviousDebugSnapshotCommand.NotifyCanExecuteChanged();
        NextDebugSnapshotCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveDebugPrevious() => _debugSnapshotIndex > 0;

    private bool CanMoveDebugNext() => _debugSnapshotIndex >= 0
        && _debugSnapshotIndex + 1 < _debugSnapshots.Count;

    private void PreviousDebugSnapshot()
    {
        if (CanMoveDebugPrevious())
        {
            _debugSnapshotIndex--;
            RefreshDebugSnapshot();
        }
    }

    private void NextDebugSnapshot()
    {
        if (CanMoveDebugNext())
        {
            _debugSnapshotIndex++;
            RefreshDebugSnapshot();
        }
    }

    private void ToggleBreakpoint()
    {
        if (SelectedDocument is null)
        {
            return;
        }
        int line = SelectedDocument.CaretLine;
        BreakpointViewModel? existing = Breakpoints.FirstOrDefault(item =>
            item.Line == line
            && string.Equals(item.FileName, SelectedDocument.DisplayName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            Breakpoints.Add(new BreakpointViewModel(line, SelectedDocument.DisplayName));
        }
        else
        {
            Breakpoints.Remove(existing);
        }
        OnPropertyChanged(nameof(HasBreakpoints));
        OnPropertyChanged(nameof(NoBreakpoints));
    }

    private void NavigateToProblem(ProblemItemViewModel? problem)
    {
        if (problem is not null)
        {
            SourceNavigationRequested?.Invoke(this, problem.Diagnostic.Span);
        }
    }

    private async Task ShowCommandPaletteAsync()
    {
        if (_dialogs is null)
        {
            return;
        }
        string? id = await _dialogs.ShowCommandPaletteAsync(PaletteCommands);
        if (!string.IsNullOrWhiteSpace(id))
        {
            await ExecutePaletteCommandAsync(id);
        }
    }

    private Task ExecutePaletteCommandAsync(string id) => id switch
    {
        "file.new" => RunCommandAsync(NewDocumentCommand),
        "file.open" => OpenDocumentCommand.ExecuteAsync(null),
        "file.save" => SaveDocumentCommand.ExecuteAsync(null),
        "file.close" => CloseDocumentCommand.ExecuteAsync(SelectedDocument),
        "project.new" => NewProjectCommand.ExecuteAsync(null),
        "project.open" => OpenProjectCommand.ExecuteAsync(null),
        "sprout.check" => CheckCommand.ExecuteAsync(null),
        "sprout.run" => RunCommand.ExecuteAsync(null),
        "sprout.debug" => DebugCommand.ExecuteAsync(null),
        "project.build" => BuildCommand.ExecuteAsync(null),
        "editor.format" => FormatDocumentCommand.ExecuteAsync(null),
        "sprout.repl" => RunCommandAsync(OpenReplCommand),
        "view.explorer" => RunCommandAsync(ToggleExplorerCommand),
        "view.sourceControl" => ShowSourceControlCommand.ExecuteAsync(null),
        "view.inspector" => RunCommandAsync(ToggleInspectorCommand),
        "view.bottom" => RunCommandAsync(ToggleBottomPanelCommand),
        "view.academy" => ShowAcademyCommand.ExecuteAsync(null),
        "view.toolchains" => ShowToolchainsCommand.ExecuteAsync(null),
        "view.docs" => ShowDocumentationCommand.ExecuteAsync(null),
        "project.packages" => ShowPackagesCommand.ExecuteAsync(null),
        "view.settings" => ShowSettingsCommand.ExecuteAsync(null),
        "help.about" => ShowAboutCommand.ExecuteAsync(null),
        _ => Task.CompletedTask,
    };

    private static Task RunCommandAsync(System.Windows.Input.ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
        return Task.CompletedTask;
    }

    private void SelectActivity(ActivityDestination destination)
    {
        SelectedActivity = destination;
        if (destination == ActivityDestination.Explorer)
        {
            ExplorerVisible = true;
        }
    }

    private async Task ShowSourceControlAsync()
    {
        SelectActivity(ActivityDestination.SourceControl);
        if (HasProject)
        {
            await RefreshSourceControlAsync();
        }
    }

    private Task RefreshSourceControlAsync() => RefreshSourceControlCoreAsync(CancellationToken.None);

    private async Task RefreshSourceControlCoreAsync(CancellationToken cancellationToken)
    {
        ProjectInfo? project = CurrentProject;
        SourceControlChanges.Clear();
        if (project is null)
        {
            HasSourceControl = false;
            GitBranch = "Not a Git repository";
            return;
        }

        GitRepositoryState state = await _gitRepository.GetStatusAsync(
            project.RootPath,
            cancellationToken);
        if (!state.IsRepository)
        {
            HasSourceControl = false;
            GitBranch = "Not a Git repository";
            OnPropertyChanged(nameof(NoSourceControlChanges));
            return;
        }

        HasSourceControl = true;
        GitBranch = state.BranchSummary;
        foreach (GitFileChange change in state.Changes)
        {
            char status = change.IsUnstaged
                ? change.DisplayStatus(staged: false)
                : change.DisplayStatus(staged: true);
            SourceControlChanges.Add($"{status}  {change.Path}");
        }
        OnPropertyChanged(nameof(NoSourceControlChanges));
        StudioLog.Information($"Refreshed source control for {project.Name}; changes={SourceControlChanges.Count}.");
    }

    private async Task ShowAcademyAsync()
    {
        SelectActivity(ActivityDestination.Academy);
        await Task.CompletedTask;
    }

    private async Task ShowDocumentationAsync()
    {
        SelectActivity(ActivityDestination.Documentation);
        await Task.CompletedTask;
    }

    private async Task ShowToolchainsAsync()
    {
        SelectActivity(ActivityDestination.Toolchains);
        await Task.CompletedTask;
    }

    private async Task ShowSettingsAsync()
    {
        SelectActivity(ActivityDestination.Settings);
        try
        {
            if (_dialogs is null)
            {
                return;
            }
            StudioSettingsState? settings = await _dialogs.ShowSettingsAsync(_settings);
            if (settings is not null)
            {
                _settings = StudioSettingsState.Sanitize(settings);
                _settingsStore.Save(_settings);
                OnPropertyChanged(nameof(EditorFontSize));
                OnPropertyChanged(nameof(EditorWordWrap));
                OnPropertyChanged(nameof(ShowEditorWhitespace));
                StatusText = "Settings saved";
            }
        }
        finally
        {
            SelectedActivity = ActivityDestination.Explorer;
        }
    }

    private async Task ShowAboutAsync()
    {
        if (_dialogs is null)
        {
            return;
        }
        Assembly assembly = typeof(MainWindowViewModel).Assembly;
        string studioVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
            ?? assembly.GetName().Version?.ToString(3)
            ?? "Unavailable";
        string studioBuild = assembly.GetCustomAttributes<AssemblyConfigurationAttribute>()
            .FirstOrDefault()?.Configuration
            ?? "Unavailable";
        var info = new AboutSystemInfo(
            studioVersion,
            studioBuild,
            _engineInfo?.EngineVersion ?? "Unavailable",
            _engineInfo?.AbiVersion,
            _engineInfo?.Architecture ?? "Unavailable",
            _engineInfo?.BuildConfiguration ?? "Unavailable",
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString());
        await _dialogs.ShowAboutAsync(info);
    }

    private void BeginOperation(string task)
    {
        IsBusy = true;
        StatusText = task.TrimEnd('…');
        TaskText = task;
    }

    private void EndOperation()
    {
        IsBusy = false;
        TaskText = "No active task";
    }

    private async Task ReportFailureAsync(string title, Exception exception)
    {
        StudioLog.Error(title, exception);
        StatusText = "Action failed";
        ConsoleLines.Add(new ConsoleLineViewModel($"{title}: {exception.Message}", ConsoleLineKind.Error));
        OnPropertyChanged(nameof(HasConsoleOutput));
        OnPropertyChanged(nameof(NoConsoleOutput));
        if (_dialogs is not null)
        {
            await _dialogs.ShowMessageAsync(title, exception.Message);
        }
    }

    private async Task ShowPackagesAsync()
    {
        SelectActivity(ActivityDestination.Packages);
        await Task.CompletedTask;
    }

    private bool CanSaveDocument() => SelectedDocument is not null && !IsBusy;

    private bool CanUseProjectService() => EngineAvailable && !IsBusy;

    private bool CanExecuteSprout() => EngineAvailable && SelectedDocument is not null
        && SelectedDocument.Model.Language.Id == DocumentLanguage.Sprout
        && !IsBusy;

    private bool CanRunActive() => !IsBusy && SelectedDocument is { } document
        && (document.Model.Language.Id == DocumentLanguage.Sprout
            ? EngineAvailable
            : document.FilePath is not null
                && document.Model.Language.ToolchainId is not null
                && document.Model.Language.Capabilities.HasFlag(EditorCapability.Run));

    private bool CanBuildActive() => !IsBusy
        && (SelectedDocument is { Model.Language.Id: not DocumentLanguage.Sprout } document
            ? document.FilePath is not null
                && document.Model.Language.ToolchainId is not null
                && document.Model.Language.Capabilities.HasFlag(EditorCapability.Build)
            : EngineAvailable && CurrentProject is not null);

    private bool CanFormatActive() => !IsBusy && SelectedDocument is { } document
        && (document.Model.Language.Id == DocumentLanguage.Sprout
            ? EngineAvailable
            : document.FilePath is not null
                && document.Model.Language.ToolchainId is not null
                && document.Model.Language.Capabilities.HasFlag(EditorCapability.Format));

    private bool CanRunRepl() => EngineAvailable && !IsBusy && !string.IsNullOrWhiteSpace(ReplInput);

    private void NotifyCommandState()
    {
        NewDocumentCommand.NotifyCanExecuteChanged();
        OpenDocumentCommand.NotifyCanExecuteChanged();
        SaveDocumentCommand.NotifyCanExecuteChanged();
        NewProjectCommand.NotifyCanExecuteChanged();
        OpenProjectCommand.NotifyCanExecuteChanged();
        CheckCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        DebugCommand.NotifyCanExecuteChanged();
        BuildCommand.NotifyCanExecuteChanged();
        FormatDocumentCommand.NotifyCanExecuteChanged();
        OpenReplCommand.NotifyCanExecuteChanged();
        RunReplCommand.NotifyCanExecuteChanged();
        CommandPaletteCommand.NotifyCanExecuteChanged();
        ShowAcademyCommand.NotifyCanExecuteChanged();
        ShowDocumentationCommand.NotifyCanExecuteChanged();
        ShowSourceControlCommand.NotifyCanExecuteChanged();
        RefreshSourceControlCommand.NotifyCanExecuteChanged();
        ShowPackagesCommand.NotifyCanExecuteChanged();
        ShowToolchainsCommand.NotifyCanExecuteChanged();
        ShowSettingsCommand.NotifyCanExecuteChanged();
        ShowAboutCommand.NotifyCanExecuteChanged();
        OpenRecentProjectCommand.NotifyCanExecuteChanged();
        RemoveRecentProjectCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
    }

    private static int Utf16OffsetAt(string source, int line, int column)
    {
        int targetLine = Math.Max(1, line);
        int targetColumn = Math.Max(1, column);
        int offset = 0;
        for (int currentLine = 1; currentLine < targetLine && offset < source.Length; currentLine++)
        {
            int newline = source.IndexOf('\n', offset);
            offset = newline < 0 ? source.Length : newline + 1;
        }
        int lineEnd = source.IndexOf('\n', offset);
        if (lineEnd < 0)
        {
            lineEnd = source.Length;
        }
        return Math.Clamp(offset + targetColumn - 1, offset, lineEnd);
    }

    private static string EscapeSproutString(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string? ArgumentValue(IReadOnlyList<string> arguments, string name)
    {
        for (int index = 0; index + 1 < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }
        return null;
    }

    private static ActivityDestination ParseActivity(string value) => value switch
    {
        "search" => ActivityDestination.Search,
        "source-control" => ActivityDestination.SourceControl,
        "academy" => ActivityDestination.Academy,
        "documentation" => ActivityDestination.Documentation,
        "packages" => ActivityDestination.Packages,
        "toolchains" => ActivityDestination.Toolchains,
        "settings" => ActivityDestination.Settings,
        _ => ActivityDestination.Explorer,
    };

    private static string ActivityKey(ActivityDestination value) => value switch
    {
        ActivityDestination.Search => "search",
        ActivityDestination.SourceControl => "source-control",
        ActivityDestination.Academy => "academy",
        ActivityDestination.Documentation => "documentation",
        ActivityDestination.Packages => "packages",
        ActivityDestination.Toolchains => "toolchains",
        ActivityDestination.Settings => "settings",
        _ => "explorer",
    };
}
