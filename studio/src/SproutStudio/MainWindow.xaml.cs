using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Ellipse = Microsoft.UI.Xaml.Shapes.Ellipse;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;
using SproutStudio.Academy;
using SproutStudio.Core;
using SproutStudio.Editor;
using SproutStudio.Interop;
using SproutStudio.Toolchains;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.Graphics;
using WinRT.Interop;

namespace SproutStudio;

public sealed partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions =
    [
        ".spr", ".rs", ".go", ".py", ".java", ".js", ".mjs", ".cjs", ".ts", ".tsx",
        ".json", ".toml", ".md", ".txt",
    ];

    private readonly NativeSproutEngineClient _native;
    private readonly ProjectWorkspace _projects;
    private readonly EditorWorkspace _editorWorkspace = new();
    private readonly ToolchainManager _toolchains;
    private readonly ExternalLanguageService _externalLanguages;
    private readonly AcademyService _academy;
    private readonly ProcessRunner _processRunner = new();
    private readonly StudioLayoutStateStore _layoutStore = new();
    private readonly ObservableCollection<WorkspaceFile> _workspace = [];
    private readonly List<WorkspaceFile> _allWorkspaceFiles = [];
    private readonly ObservableCollection<string> _console = [];
    private readonly ObservableCollection<string> _buildOutput = [];
    private readonly ObservableCollection<ProblemItem> _problems = [];
    private readonly ObservableCollection<CompletionSuggestion> _completions = [];
    private readonly ObservableCollection<OutlineItem> _outline = [];
    private readonly ObservableCollection<DebugVariable> _variables = [];
    private readonly ObservableCollection<string> _callStack = [];
    private readonly ObservableCollection<string> _breakpointItems = [];
    private readonly ObservableCollection<string> _gitStatus = [];
    private readonly SortedSet<int> _breakpoints = [];
    private readonly HashSet<string> _watches = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<StudioCommand> _commands;
    private readonly bool _openToolchainsOnActivation;
    private readonly bool _openAcademyOnActivation;
    private IReadOnlyList<DebugSnapshot> _debugSnapshots = [];
    private LanguageServiceResponse? _lastInspection;
    private CancellationTokenSource? _inspectionCancellation;
    private CancellationTokenSource? _operationCancellation;
    private EditorDocument? _activeDocument;
    private int _debugIndex = -1;
    private bool _updatingEditor;
    private bool _updatingProfile;
    private bool _commandPaletteOpen;
    private double _explorerWidth = 280;
    private double _inspectorWidth = 350;
    private double _bottomPanelHeight = 190;
    private bool _activityVisible = true;
    private bool _explorerVisible = true;
    private bool _inspectorVisible = true;
    private bool _bottomPanelVisible = true;

    private TextBlock ToolbarProjectName => Toolbar.ProjectName;
    private AppBarButton NewButton => Toolbar.NewDocument;
    private AppBarButton OpenButton => Toolbar.OpenDocument;
    private AppBarButton SaveButton => Toolbar.SaveDocument;
    private AppBarButton NewProjectButton => Toolbar.CreateProject;
    private AppBarButton OpenProjectButton => Toolbar.OpenProject;
    private ComboBox ProfileCombo => Toolbar.SyntaxProfile;
    private AppBarButton CheckButton => Toolbar.Check;
    private AppBarButton RunButton => Toolbar.Run;
    private AppBarButton DebugButton => Toolbar.Debug;
    private AppBarButton BuildButton => Toolbar.Build;
    private Microsoft.UI.Xaml.Controls.Primitives.ToggleButton PackageHubButton => ActivityRail.Packages;
    private Microsoft.UI.Xaml.Controls.Primitives.ToggleButton AcademyButton => ActivityRail.Academy;
    private Microsoft.UI.Xaml.Controls.Primitives.ToggleButton ToolchainsButton => ActivityRail.Toolchains;
    private TextBox WorkspaceSearchBox => ExplorerPane.SearchBox;
    private TextBlock ProjectNameText => ExplorerPane.ProjectName;
    private ListView WorkspaceList => ExplorerPane.Files;
    private ListView ExplorerOutlineList => ExplorerPane.Outline;
    private TextBlock GitBranchText => ExplorerPane.GitBranch;
    private ListView GitStatusList => ExplorerPane.GitStatus;
    private TabView EditorTabs => EditorPane.Tabs;
    private TextBlock BreadcrumbText => EditorPane.Breadcrumb;
    private TextBox Editor => EditorPane.TextEditor;
    private TextBlock LineNumbersText => EditorPane.LineNumbers;
    private TextBlock MiniMapText => EditorPane.Minimap;
    private Border CompletionPanel => EditorPane.CompletionPanel;
    private ListView CompletionList => EditorPane.Completions;
    private TabView OutputTabs => BottomPane.Tabs;
    private ListView ProblemsList => BottomPane.Problems;
    private ListView ConsoleList => BottomPane.Console;
    private ListView BuildOutputList => BottomPane.BuildOutput;
    private TabView InspectorTabs => InspectorPane.Tabs;
    private TextBlock DebugPositionText => InspectorPane.DebugPosition;
    private TextBox WatchNameBox => InspectorPane.WatchName;
    private ListView VariablesList => InspectorPane.Variables;
    private ListView CallStackList => InspectorPane.CallStack;
    private ListView BreakpointsList => InspectorPane.Breakpoints;
    private TextBlock HoverTitleText => InspectorPane.HoverTitle;
    private TextBlock HoverContentsText => InspectorPane.HoverContents;
    private ListView InspectorCompletionList => InspectorPane.Completions;
    private ListView InspectorOutlineList => InspectorPane.Outline;
    private Canvas CanvasSurface => InspectorPane.Canvas;
    private TextBlock CanvasEmptyText => InspectorPane.CanvasEmpty;
    private TextBlock StatusText => StudioStatus.Task;
    private TextBlock CaretStatusText => StudioStatus.Caret;
    private TextBlock LanguageStatusText => StudioStatus.LanguageLabel;
    private ProgressRing BusyIndicator => StudioStatus.Progress;

    public MainWindow(
        bool openToolchainsOnActivation = false,
        bool openAcademyOnActivation = false,
        string? academyStatePath = null)
    {
        _native = new NativeSproutEngineClient();
        _projects = new ProjectWorkspace(_native);
        _toolchains = ToolchainManager.CreateDefault();
        _externalLanguages = new ExternalLanguageService(_toolchains);
        _academy = AcademyService.CreateDefault(_toolchains, academyStatePath);
        _openToolchainsOnActivation = openToolchainsOnActivation;
        _openAcademyOnActivation = openAcademyOnActivation;

        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1500, 900));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(StudioTitle.DragRegion);
        _commands = CreateCommands();
        WireShellEvents();
        WorkspaceList.ItemsSource = _workspace;
        ConsoleList.ItemsSource = _console;
        BuildOutputList.ItemsSource = _buildOutput;
        ProblemsList.ItemsSource = _problems;
        CompletionList.ItemsSource = _completions;
        InspectorCompletionList.ItemsSource = _completions;
        ExplorerOutlineList.ItemsSource = _outline;
        InspectorOutlineList.ItemsSource = _outline;
        VariablesList.ItemsSource = _variables;
        CallStackList.ItemsSource = _callStack;
        BreakpointsList.ItemsSource = _breakpointItems;
        GitStatusList.ItemsSource = _gitStatus;
        ProfileCombo.SelectedIndex = 0;

        _workspace.CollectionChanged += (_, _) => ExplorerPane.SetWorkspaceEmpty(_workspace.Count == 0);
        _problems.CollectionChanged += (_, _) => BottomPane.SetProblemsEmpty(_problems.Count == 0);
        _console.CollectionChanged += (_, _) => BottomPane.SetConsoleEmpty(_console.Count == 0);
        _buildOutput.CollectionChanged += (_, _) => BottomPane.SetBuildEmpty(_buildOutput.Count == 0);
        _outline.CollectionChanged += (_, _) =>
        {
            InspectorPane.SetOutlineEmpty(_outline.Count == 0);
            ExplorerPane.OutlineExpander.IsExpanded = _outline.Count > 0;
        };
        _variables.CollectionChanged += (_, _) => InspectorPane.SetVariablesEmpty(_variables.Count == 0);
        ExplorerPane.SetWorkspaceEmpty(true);
        BottomPane.SetProblemsEmpty(true);
        BottomPane.SetConsoleEmpty(true);
        BottomPane.SetBuildEmpty(true);
        InspectorPane.SetOutlineEmpty(true);
        InspectorPane.SetVariablesEmpty(true);

        EditorDocument document = _editorWorkspace.CreateSproutDocument();
        AddDocumentTab(document, select: true);
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        if (openToolchainsOnActivation || openAcademyOnActivation)
        {
            RootLayout.Loaded += MainContent_Loaded;
        }
    }

    private void WireShellEvents()
    {
        Toolbar.ProjectSelector.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.OpenProject);
        NewButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.NewDocument);
        OpenButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.OpenDocument);
        SaveButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.SaveDocument);
        NewProjectButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.NewProject);
        OpenProjectButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.OpenProject);
        ProfileCombo.SelectionChanged += ProfileCombo_SelectionChanged;
        CheckButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.Check);
        RunButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.Run);
        DebugButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.Debug);
        BuildButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.Build);

        ActivityRail.Explorer.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.ShowExplorer);
        ActivityRail.Search.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.FocusSearch);
        ActivityRail.SourceControl.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.ShowSourceControl);
        ActivityRail.Academy.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.OpenAcademy);
        ActivityRail.Documentation.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.OpenDocumentation);
        ActivityRail.Packages.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.OpenPackages);
        ActivityRail.Toolchains.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.OpenToolchains);

        ExplorerPane.CollapseButton.Click += (_, _) => ToggleExplorer();
        WorkspaceSearchBox.TextChanged += WorkspaceSearchBox_TextChanged;
        WorkspaceList.ItemClick += WorkspaceList_ItemClick;
        ExplorerOutlineList.ItemClick += OutlineList_ItemClick;
        ExplorerPane.RefreshSourceControl.Click += RefreshGitButton_Click;

        EditorTabs.AddTabButtonClick += EditorTabs_AddTabButtonClick;
        EditorTabs.SelectionChanged += EditorTabs_SelectionChanged;
        EditorTabs.TabCloseRequested += EditorTabs_TabCloseRequested;
        EditorPane.FormatButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.FormatDocument);
        EditorPane.DefinitionButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.GoToDefinition);
        EditorPane.BreakpointButton.Click += async (_, _) => await ExecuteCommandAsync(StudioCommandId.ToggleBreakpoint);
        Editor.TextChanged += Editor_TextChanged;
        Editor.SelectionChanged += Editor_SelectionChanged;
        Editor.KeyDown += Editor_KeyDown;
        CompletionList.ItemClick += CompletionList_ItemClick;

        BottomPane.CollapseButton.Click += (_, _) => ToggleBottomPanel();
        ProblemsList.ItemClick += ProblemsList_ItemClick;

        InspectorPane.CollapseButton.Click += (_, _) => ToggleInspector();
        InspectorOutlineList.ItemClick += OutlineList_ItemClick;
        InspectorPane.PreviousSnapshot.Click += DebugPreviousButton_Click;
        InspectorPane.NextSnapshot.Click += DebugNextButton_Click;
        InspectorPane.AddWatch.Click += AddWatchButton_Click;
    }

    private IReadOnlyList<StudioCommand> CreateCommands() =>
    [
        new(StudioCommandId.NewDocument, "File", "New Document", "Ctrl+N", () => NewButton.IsEnabled,
            () => RunSynchronous(CreateNewTab)),
        new(StudioCommandId.OpenDocument, "File", "Open File", "Ctrl+O", () => OpenButton.IsEnabled,
            () => RunEvent(OpenButton_Click)),
        new(StudioCommandId.SaveDocument, "File", "Save", "Ctrl+S", () => SaveButton.IsEnabled,
            () => RunEvent(SaveButton_Click)),
        new(StudioCommandId.NewProject, "Project", "New Project", string.Empty, () => NewProjectButton.IsEnabled,
            () => RunEvent(NewProjectButton_Click)),
        new(StudioCommandId.OpenProject, "Project", "Open Project", string.Empty, () => OpenProjectButton.IsEnabled,
            () => RunEvent(OpenProjectButton_Click)),
        new(StudioCommandId.Check, "Sprout", "Check Project", string.Empty, () => CheckButton.IsEnabled,
            () => ExecuteActiveAsync(EngineOperation.Check)),
        new(StudioCommandId.Run, "Sprout", "Run Project", "F5", () => RunButton.IsEnabled,
            () => ExecuteActiveAsync(EngineOperation.Run)),
        new(StudioCommandId.Debug, "Sprout", "Debug Project", string.Empty, () => DebugButton.IsEnabled,
            () => RunEvent(DebugButton_Click)),
        new(StudioCommandId.Build, "Sprout", "Build Project", string.Empty, () => BuildButton.IsEnabled,
            () => RunEvent(BuildButton_Click)),
        new(StudioCommandId.FormatDocument, "Editor", "Format Document", string.Empty,
            () => _activeDocument?.Language.Id == DocumentLanguage.Sprout,
            () => RunEvent(FormatButton_Click)),
        new(StudioCommandId.GoToDefinition, "Editor", "Go to Definition", "F12",
            () => _activeDocument?.Language.Id == DocumentLanguage.Sprout,
            () => RunEvent(GoToDefinitionButton_Click)),
        new(StudioCommandId.ToggleBreakpoint, "Debug", "Toggle Breakpoint", string.Empty,
            () => DebugButton.IsEnabled,
            () => RunEvent(ToggleBreakpointButton_Click)),
        new(StudioCommandId.ShowExplorer, "View", "Focus Explorer", string.Empty, () => true,
            () => RunEvent(WorkspaceRailButton_Click)),
        new(StudioCommandId.FocusSearch, "View", "Focus Workspace Search", string.Empty, () => true,
            () => RunEvent(SearchRailButton_Click)),
        new(StudioCommandId.ShowSourceControl, "View", "Open Source Control", string.Empty, () => true,
            () => RunEvent(GitRailButton_Click)),
        new(StudioCommandId.OpenAcademy, "Academy", "Open Academy", string.Empty, () => AcademyButton.IsEnabled,
            () => RunEvent(AcademyButton_Click)),
        new(StudioCommandId.OpenDocumentation, "Help", "Open Quick Reference", string.Empty, () => true,
            () => RunEvent(DocsRailButton_Click)),
        new(StudioCommandId.OpenPackages, "Project", "Open Package Hub", string.Empty, () => PackageHubButton.IsEnabled,
            () => RunEvent(PackageHubButton_Click)),
        new(StudioCommandId.OpenToolchains, "Settings", "Open Toolchains", string.Empty, () => ToolchainsButton.IsEnabled,
            () => RunEvent(ToolchainsButton_Click)),
        new(StudioCommandId.ToggleExplorer, "View", "Toggle Explorer", "Ctrl+B", () => true,
            () => RunSynchronous(ToggleExplorer)),
        new(StudioCommandId.ToggleInspector, "View", "Toggle Inspector", string.Empty, () => true,
            () => RunSynchronous(ToggleInspector)),
        new(StudioCommandId.ToggleBottomPanel, "View", "Toggle Bottom Panel", "Ctrl+J", () => true,
            () => RunSynchronous(ToggleBottomPanel)),
        new(StudioCommandId.ToggleActivityBar, "View", "Toggle Activity Bar", string.Empty, () => true,
            () => RunSynchronous(ToggleActivityBar)),
    ];

    private static Task RunSynchronous(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private Task RunEvent(Action<object, RoutedEventArgs> handler)
    {
        handler(this, new RoutedEventArgs());
        return Task.CompletedTask;
    }

    private async Task ExecuteCommandAsync(StudioCommandId id)
    {
        StudioCommand command = _commands.First(item => item.Id == id);
        if (!command.CanExecute())
        {
            StatusText.Text = $"{command.Title} is not available in the current context";
            return;
        }

        try
        {
            await command.ExecuteAsync();
        }
        catch (Exception exception)
        {
            ShowHostFailure($"Could not run {command.Title}", exception);
        }
    }

    private async Task ShowCommandPaletteAsync()
    {
        if (_commandPaletteOpen)
        {
            return;
        }

        _commandPaletteOpen = true;
        try
        {
            var palette = new CommandPaletteDialog(_commands);
            StudioCommand? command = await palette.ShowAsync(RootLayout.XamlRoot);
            if (command is not null)
            {
                await ExecuteCommandAsync(command.Id);
            }
        }
        finally
        {
            _commandPaletteOpen = false;
        }
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        try
        {
            ApplyLayoutState(_layoutStore.Load());
            await _projects.LoadRecentAsync();
            await RefreshGitAsync();
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not initialize the workspace", exception);
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args) =>
        _layoutStore.Save(CaptureLayoutState());

    private async void MainContent_Loaded(object sender, RoutedEventArgs args)
    {
        RootLayout.Loaded -= MainContent_Loaded;
        try
        {
            if (_openAcademyOnActivation)
            {
                await ShowAcademyAsync();
            }
            else if (_openToolchainsOnActivation)
            {
                await ShowToolchainSettingsAsync();
            }
        }
        catch (Exception exception)
        {
            string surface = _openAcademyOnActivation ? "Academy" : "Toolchain Settings";
            Title = $"Sprout Studio — {surface} Failed: {exception.Message}";
            ShowHostFailure($"Could not open {surface}", exception);
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs args) => CreateNewTab();

    private void EditorTabs_AddTabButtonClick(TabView sender, object args) => CreateNewTab();

    private void CreateNewTab()
    {
        EditorDocument document = _editorWorkspace.CreateSproutDocument(SelectedProfile());
        AddDocumentTab(document, select: true);
        StatusText.Text = "New Sprout document";
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs args)
    {
        var picker = new FileOpenPicker();
        foreach (string extension in SupportedExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }
        await OpenDocumentAsync(file.Path, _projects.CurrentProject?.RootPath);
    }

    private async Task OpenDocumentAsync(string path, string? projectRoot)
    {
        try
        {
            SyntaxProfile profile = _projects.CurrentProject?.Profile ?? SelectedProfile();
            EditorDocument document = await _editorWorkspace.OpenAsync(
                path,
                profile,
                projectRoot);
            AddDocumentTab(document, select: true);
            StatusText.Text = $"Opened {document.DisplayName}";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open file", exception);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs args)
    {
        if (_activeDocument is null)
        {
            return;
        }
        try
        {
            if (await SaveDocumentAsync(_activeDocument))
            {
                RefreshTabHeader(_activeDocument);
                StatusText.Text = $"Saved {_activeDocument.DisplayName}";
            }
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not save file", exception);
        }
    }

    private async Task<bool> SaveDocumentAsync(EditorDocument document)
    {
        if (document.FilePath is null)
        {
            var picker = new FileSavePicker
            {
                SuggestedFileName = Path.GetFileNameWithoutExtension(document.DisplayName),
            };
            picker.FileTypeChoices.Add("Sprout source", [".spr"]);
            picker.FileTypeChoices.Add("Rust source", [".rs"]);
            picker.FileTypeChoices.Add("Go source", [".go"]);
            picker.FileTypeChoices.Add("Python source", [".py"]);
            picker.FileTypeChoices.Add("Text file", [".txt"]);
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return false;
            }
            await _editorWorkspace.SaveAsAsync(document, file.Path);
        }
        else
        {
            await _editorWorkspace.SaveAsync(document);
        }
        return true;
    }

    private async void EditorTabs_TabCloseRequested(
        TabView sender,
        TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab is not TabViewItem tab || tab.Tag is not EditorDocument document)
        {
            return;
        }
        if (document.IsDirty)
        {
            var dialog = new ContentDialog
            {
                Title = $"Save changes to {document.DisplayName}?",
                Content = "Closing this tab will discard unsaved changes.",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Discard",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootLayout.XamlRoot,
            };
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None
                || result == ContentDialogResult.Primary && !await SaveDocumentAsync(document))
            {
                return;
            }
        }
        _editorWorkspace.Close(document);
        EditorTabs.TabItems.Remove(tab);
        if (EditorTabs.TabItems.Count == 0)
        {
            CreateNewTab();
        }
    }

    private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (EditorTabs.SelectedItem is TabViewItem tab && tab.Tag is EditorDocument document)
        {
            ActivateDocument(document);
        }
    }

    private void AddDocumentTab(EditorDocument document, bool select)
    {
        TabViewItem? existing = EditorTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(item => ReferenceEquals(item.Tag, document));
        if (existing is null)
        {
            existing = new TabViewItem
            {
                Header = document.TabTitle,
                IconSource = new FontIconSource
                {
                    Glyph = "\uE8A5",
                    Foreground = (SolidColorBrush)Application.Current.Resources["StudioAccentBrush"],
                },
                Tag = document,
            };
            EditorTabs.TabItems.Add(existing);
        }
        if (select)
        {
            EditorTabs.SelectedItem = existing;
            ActivateDocument(document);
        }
    }

    private void ActivateDocument(EditorDocument document)
    {
        if (ReferenceEquals(_activeDocument, document) && Editor.Text == document.Source)
        {
            return;
        }
        _activeDocument = document;
        _updatingEditor = true;
        Editor.Text = document.Source;
        _updatingEditor = false;
        _updatingProfile = true;
        ProfileCombo.SelectedIndex = (int)document.Profile;
        _updatingProfile = false;
        BreadcrumbText.Text = document.FilePath ?? document.DisplayName;
        LanguageStatusText.Text = document.Language.DisplayName;
        Title = $"{document.DisplayName} — Sprout Studio";
        StudioTitle.Context = $"{_projects.CurrentProject?.Name ?? "Single file"} — {document.DisplayName}";
        CompletionPanel.Visibility = Visibility.Collapsed;
        _debugSnapshots = [];
        _debugIndex = -1;
        RefreshEditorDecorations();
        RefreshCapabilities();
        ScheduleInspection(showCompletion: false);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs args)
    {
        if (_updatingEditor || _activeDocument is null)
        {
            return;
        }
        _activeDocument.UpdateSource(NormalizeLineEndings(Editor.Text));
        RefreshTabHeader(_activeDocument);
        RefreshEditorDecorations();
        ScheduleInspection(showCompletion: false);
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs args)
    {
        RefreshCaretStatus();
        ScheduleInspection(showCompletion: false);
    }

    private async void Editor_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        bool control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
                VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (control && args.Key == VirtualKey.Space)
        {
            args.Handled = true;
            ScheduleInspection(showCompletion: true, immediate: true);
            return;
        }
        if (args.Key == VirtualKey.F12)
        {
            args.Handled = true;
            await ExecuteCommandAsync(StudioCommandId.GoToDefinition);
            return;
        }
        if (CompletionPanel.Visibility == Visibility.Visible
            && args.Key is VirtualKey.Enter or VirtualKey.Tab)
        {
            args.Handled = true;
            if (CompletionList.SelectedItem is CompletionSuggestion completion)
            {
                ApplyCompletion(completion);
            }
        }
        else if (args.Key == VirtualKey.Escape)
        {
            CompletionPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void CompletionList_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is CompletionSuggestion completion)
        {
            ApplyCompletion(completion);
        }
    }

    private void ApplyCompletion(CompletionSuggestion completion)
    {
        string source = _activeDocument?.Source ?? NormalizeLineEndings(Editor.Text);
        int normalizedStart = SourceTextCoordinates.Utf8OffsetToUtf16Index(
            source,
            completion.ReplacementSpan.Start);
        int normalizedEnd = SourceTextCoordinates.Utf8OffsetToUtf16Index(
            source,
            completion.ReplacementSpan.End);
        int start = NormalizedIndexToEditorIndex(Editor.Text, normalizedStart);
        int end = NormalizedIndexToEditorIndex(Editor.Text, normalizedEnd);
        Editor.Select(start, Math.Max(0, end - start));
        Editor.SelectedText = completion.InsertText;
        Editor.SelectionStart = start + completion.InsertText.Length;
        CompletionPanel.Visibility = Visibility.Collapsed;
        Editor.Focus(FocusState.Programmatic);
    }

    private void ScheduleInspection(bool showCompletion, bool immediate = false)
    {
        _inspectionCancellation?.Cancel();
        _inspectionCancellation?.Dispose();
        _inspectionCancellation = new CancellationTokenSource();
        _ = InspectAfterDelayAsync(
            showCompletion,
            immediate ? TimeSpan.Zero : TimeSpan.FromMilliseconds(180),
            _inspectionCancellation.Token);
    }

    private async Task InspectAfterDelayAsync(
        bool showCompletion,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
            EditorDocument? document = _activeDocument;
            if (document is null || document.Language.Id != DocumentLanguage.Sprout)
            {
                ClearLanguageInsight();
                return;
            }
            var request = new LanguageServiceRequest(
                LanguageServiceOperation.Inspect,
                document.Profile,
                document.FilePath ?? document.DisplayName,
                document.Source,
                NormalizedCaretIndex());
            LanguageServiceResponse response = await _native.QueryAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(document, _activeDocument))
            {
                return;
            }
            _lastInspection = response;
            _completions.Clear();
            foreach (CompletionSuggestion completion in response.Completions.Take(80))
            {
                _completions.Add(completion);
            }
            CompletionList.SelectedIndex = _completions.Count > 0 ? 0 : -1;
            CompletionPanel.Visibility = showCompletion && _completions.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            HoverTitleText.Text = response.Hover?.Title ?? "No symbol at the caret";
            HoverContentsText.Text = response.Hover?.Contents ?? string.Empty;
            RefreshOutline(response.Symbols);
            RefreshProblems(response.Diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Editor insight unavailable: {exception.Message}";
        }
    }

    private void ClearLanguageInsight()
    {
        _lastInspection = null;
        _completions.Clear();
        _outline.Clear();
        CompletionPanel.Visibility = Visibility.Collapsed;
        HoverTitleText.Text = "Language services are not enabled for this file";
        HoverContentsText.Text = "Editing and saving remain available. Rust and Go use validated external toolchain actions.";
    }

    private void RefreshOutline(IReadOnlyList<DocumentSymbol> symbols)
    {
        _outline.Clear();
        foreach (DocumentSymbol symbol in symbols)
        {
            AddOutline(symbol, 0);
        }
    }

    private void AddOutline(DocumentSymbol symbol, int depth)
    {
        _outline.Add(new OutlineItem(symbol, depth));
        foreach (DocumentSymbol child in symbol.Children)
        {
            AddOutline(child, depth + 1);
        }
    }

    private void OutlineList_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is OutlineItem item)
        {
            SelectSpan(item.Symbol.Span);
        }
    }

    private async void FormatButton_Click(object sender, RoutedEventArgs args)
    {
        EditorDocument? document = _activeDocument;
        if (document is null)
        {
            return;
        }
        SetBusy(true, $"Formatting {document.DisplayName}…");
        try
        {
            if (document.Language.Id == DocumentLanguage.Sprout)
            {
                LanguageServiceResponse response = await _native.QueryAsync(new LanguageServiceRequest(
                    LanguageServiceOperation.Format,
                    document.Profile,
                    document.FilePath ?? document.DisplayName,
                    document.Source));
                RefreshProblems(response.Diagnostics);
                if (!response.Success || response.FormattedSource is null)
                {
                    StatusText.Text = "Formatting stopped; review Problems";
                    return;
                }
                document.UpdateSource(response.FormattedSource);
                _updatingEditor = true;
                Editor.Text = document.Source;
                _updatingEditor = false;
                RefreshTabHeader(document);
                RefreshEditorDecorations();
                StatusText.Text = "Formatted with the Sprout engine";
            }
            else
            {
                if (!await SaveDocumentAsync(document))
                {
                    return;
                }
                ProcessResult result = await _externalLanguages.ExecuteAsync(
                    document,
                    ToolchainAction.Format,
                    _operationCancellation?.Token ?? default);
                WriteProcessResult(result, _buildOutput);
                if (result.Success && document.FilePath is not null)
                {
                    document.UpdateSource(await File.ReadAllTextAsync(document.FilePath));
                    await _editorWorkspace.SaveAsync(document);
                    ActivateDocument(document);
                }
                StatusText.Text = result.Success ? "External formatter completed" : "Formatter failed";
            }
        }
        catch (Exception exception)
        {
            ShowHostFailure("Formatting failed", exception);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private async void GoToDefinitionButton_Click(object sender, RoutedEventArgs args)
    {
        if (_activeDocument?.Language.Id != DocumentLanguage.Sprout)
        {
            StatusText.Text = "Go to definition is currently available for Sprout files";
            return;
        }
        try
        {
            LanguageServiceResponse response = await _native.QueryAsync(new LanguageServiceRequest(
                LanguageServiceOperation.Inspect,
                _activeDocument.Profile,
                _activeDocument.FilePath ?? _activeDocument.DisplayName,
                _activeDocument.Source,
                NormalizedCaretIndex()));
            if (response.Definition is null)
            {
                StatusText.Text = "No definition at the caret";
                return;
            }
            SelectSpan(response.Definition.Span);
            StatusText.Text = $"Definition: {response.Definition.Name}";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Go to definition failed", exception);
        }
    }

    private void CheckButton_Click(object sender, RoutedEventArgs args) =>
        _ = ExecuteActiveAsync(EngineOperation.Check);

    private void RunButton_Click(object sender, RoutedEventArgs args) =>
        _ = ExecuteActiveAsync(EngineOperation.Run);

    private async Task ExecuteActiveAsync(EngineOperation operation)
    {
        EditorDocument? document = _activeDocument;
        if (document is null)
        {
            return;
        }
        BeginOperation();
        SetBusy(true, operation == EngineOperation.Run ? "Running…" : "Checking…");
        try
        {
            if (document.Language.Id != DocumentLanguage.Sprout)
            {
                if (operation != EngineOperation.Run)
                {
                    throw new NotSupportedException(
                        $"Check is not available for {document.Language.DisplayName} files.");
                }
                if (!await SaveDocumentAsync(document))
                {
                    return;
                }
                ProcessResult result = await _externalLanguages.ExecuteAsync(
                    document,
                    ToolchainAction.Run,
                    _operationCancellation!.Token);
                _console.Clear();
                WriteProcessResult(result, _console);
                OutputTabs.SelectedIndex = 1;
                StatusText.Text = result.Success ? "Run completed" : "Run failed";
                return;
            }

            EngineResponse response = await _native.ExecuteAsync(new EngineRequest(
                operation,
                document.Profile,
                document.FilePath ?? document.DisplayName,
                document.Source,
                document.ProjectRoot),
                _operationCancellation!.Token);
            RefreshProblems(response.Diagnostics);
            _console.Clear();
            foreach (string line in response.Output)
            {
                _console.Add(line);
            }
            RenderCanvas(response.CanvasCommands ?? []);
            OutputTabs.SelectedIndex = response.Success && operation == EngineOperation.Run ? 1 : 0;
            StatusText.Text = response.Success
                ? operation == EngineOperation.Run ? "Run completed" : "No problems found"
                : $"{response.Diagnostics.Count} problem(s)";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operation canceled";
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

    private async void DebugButton_Click(object sender, RoutedEventArgs args)
    {
        EditorDocument? document = _activeDocument;
        if (document?.Language.Id != DocumentLanguage.Sprout)
        {
            StatusText.Text = "Recorded debugging is available for Sprout files";
            return;
        }
        BeginOperation();
        SetBusy(true, "Recording a debug trace…");
        try
        {
            EngineResponse response = await _native.ExecuteAsync(new EngineRequest(
                EngineOperation.Debug,
                document.Profile,
                document.FilePath ?? document.DisplayName,
                document.Source,
                document.ProjectRoot),
                _operationCancellation!.Token);
            RefreshProblems(response.Diagnostics);
            _debugSnapshots = response.DebugSnapshots ?? [];
            _console.Clear();
            foreach (string line in response.Output)
            {
                _console.Add(line);
            }
            if (_debugSnapshots.Count == 0)
            {
                _debugIndex = -1;
                RefreshDebugSnapshot();
                StatusText.Text = response.Success
                    ? "Program completed without executable statements"
                    : "Debug trace stopped on diagnostics";
                return;
            }
            _debugIndex = FindInitialDebugSnapshot();
            RefreshDebugSnapshot();
            InspectorPane.SelectDebugger();
            StatusText.Text = _breakpoints.Count == 0
                ? "Recorded debugger ready — use the step controls"
                : $"Recorded debugger selected breakpoint line {_debugSnapshots[_debugIndex].Span.StartLine}";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Debugger failed", exception);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private int FindInitialDebugSnapshot()
    {
        foreach (int breakpoint in _breakpoints)
        {
            int index = _debugSnapshots
                .Select((snapshot, position) => (snapshot, position))
                .FirstOrDefault(item => item.snapshot.Span.StartLine == breakpoint)
                .position;
            if (index > 0 || _debugSnapshots[0].Span.StartLine == breakpoint)
            {
                return index;
            }
        }
        return 0;
    }

    private void DebugPreviousButton_Click(object sender, RoutedEventArgs args)
    {
        if (_debugIndex > 0)
        {
            _debugIndex--;
            RefreshDebugSnapshot();
        }
    }

    private void DebugNextButton_Click(object sender, RoutedEventArgs args)
    {
        if (_debugIndex >= 0 && _debugIndex + 1 < _debugSnapshots.Count)
        {
            _debugIndex++;
            RefreshDebugSnapshot();
        }
    }

    private void RefreshDebugSnapshot()
    {
        _variables.Clear();
        _callStack.Clear();
        if (_debugIndex < 0 || _debugIndex >= _debugSnapshots.Count)
        {
            DebugPositionText.Text = "No debug trace";
            RenderCanvas([]);
            return;
        }
        DebugSnapshot snapshot = _debugSnapshots[_debugIndex];
        DebugPositionText.Text = $"Step {_debugIndex + 1} / {_debugSnapshots.Count} · line {snapshot.Span.StartLine}";
        IEnumerable<DebugVariable> ordered = snapshot.Variables
            .OrderByDescending(variable => _watches.Contains(variable.Name))
            .ThenBy(variable => variable.Name, StringComparer.Ordinal);
        foreach (DebugVariable variable in ordered)
        {
            _variables.Add(variable);
        }
        foreach (string watch in _watches.Where(watch => snapshot.Variables.All(
            variable => variable.Name != watch)))
        {
            _variables.Insert(0, new DebugVariable(watch, "watch", "not in scope"));
        }
        foreach (DebugStackFrame frame in snapshot.CallStack)
        {
            _callStack.Add($"{frame.Name}  {frame.Span.File}:{frame.Span.StartLine}");
        }
        _console.Clear();
        foreach (string line in snapshot.Output)
        {
            _console.Add(line);
        }
        RenderCanvas(snapshot.CanvasCommands);
        SelectSpan(snapshot.Span);
    }

    private void AddWatchButton_Click(object sender, RoutedEventArgs args)
    {
        string name = WatchNameBox.Text.Trim();
        if (name.Length == 0)
        {
            return;
        }
        _watches.Add(name);
        WatchNameBox.Text = string.Empty;
        RefreshDebugSnapshot();
    }

    private void ToggleBreakpointButton_Click(object sender, RoutedEventArgs args)
    {
        int line = CurrentLineAndColumn().line;
        if (!_breakpoints.Add(line))
        {
            _breakpoints.Remove(line);
        }
        _breakpointItems.Clear();
        foreach (int breakpoint in _breakpoints)
        {
            _breakpointItems.Add($"● {_activeDocument?.DisplayName}:{breakpoint}");
        }
        StatusText.Text = _breakpoints.Contains(line)
            ? $"Breakpoint added at line {line}"
            : $"Breakpoint removed from line {line}";
    }

    private async void BuildButton_Click(object sender, RoutedEventArgs args)
    {
        EditorDocument? document = _activeDocument;
        if (document is null)
        {
            return;
        }
        BeginOperation();
        SetBusy(true, "Building…");
        _buildOutput.Clear();
        OutputTabs.SelectedIndex = 2;
        try
        {
            if (document.Language.Id != DocumentLanguage.Sprout)
            {
                if (!await SaveDocumentAsync(document))
                {
                    return;
                }
                ProcessResult result = await _externalLanguages.ExecuteAsync(
                    document,
                    ToolchainAction.Build,
                    _operationCancellation!.Token);
                WriteProcessResult(result, _buildOutput);
                StatusText.Text = result.Success ? "External build completed" : "External build failed";
                return;
            }
            ProjectInfo project = _projects.CurrentProject
                ?? throw new InvalidOperationException("Open a Sprout project before building a bundle.");
            if (document.IsDirty && !await SaveDocumentAsync(document))
            {
                return;
            }
            BuildResponse response = await _native.BuildAsync(
                project.RootPath,
                _operationCancellation!.Token);
            if (!response.Success || response.Artifact is null)
            {
                foreach (ProjectDiagnostic diagnostic in response.Diagnostics)
                {
                    _buildOutput.Add($"{diagnostic.Code}: {diagnostic.Message}");
                }
                StatusText.Text = "Build failed";
                return;
            }
            _buildOutput.Add("Validated interpreted Sprout bundle created.");
            _buildOutput.Add($"Target: {response.Artifact.Target}");
            _buildOutput.Add($"Sources: {response.Artifact.SourceCount}");
            _buildOutput.Add($"Artifact: {response.Artifact.Path}");
            _buildOutput.Add("This artifact requires a compatible Sprout runtime; it is not a native executable.");
            StatusText.Text = "Build completed";
        }
        catch (Exception exception)
        {
            _buildOutput.Add(exception.Message);
            ShowHostFailure("Build failed", exception);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private async void PackageHubButton_Click(object sender, RoutedEventArgs args)
    {
        ActivityRail.Select("packages");
        if (_projects.CurrentProject is not ProjectInfo project)
        {
            StatusText.Text = "Open a Sprout project to manage its packages";
            return;
        }
        try
        {
            var dialog = new PackageHubDialog(_native, project);
            await dialog.ShowAsync(RootLayout.XamlRoot);
            await _projects.OpenAsync(dialog.Project.RootPath);
            await RefreshWorkspaceAsync();
            StatusText.Text = "Package manifest refreshed";
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open Package Hub", exception);
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
            XamlRoot = RootLayout.XamlRoot,
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
        string destination = Path.Combine(folder.Path, name);
        await CreateProjectAsync(
            destination,
            name,
            (ProjectTemplate)Math.Max(0, templateBox.SelectedIndex));
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

    private void ProjectSelectorButton_Click(object sender, RoutedEventArgs args) =>
        OpenProjectButton_Click(sender, args);

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
        EditorDocument document = await _editorWorkspace.OpenAsync(
            project.EntryPath,
            project.Profile,
            project.RootPath);
        document.Profile = project.Profile;
        AddDocumentTab(document, select: true);
        await RefreshWorkspaceAsync();
        await RefreshGitAsync();
    }

    private async Task RefreshWorkspaceAsync()
    {
        _allWorkspaceFiles.Clear();
        if (_projects.CurrentProject is ProjectInfo project)
        {
            ToolbarProjectName.Text = project.Name;
            ProjectNameText.Text = project.Name.ToUpperInvariant();
            StudioTitle.Context = $"{project.Name} — {_activeDocument?.DisplayName ?? Path.GetFileName(project.EntryPath)}";
            IReadOnlyList<WorkspaceFile> files = await WorkspaceScanner.ScanAsync(project.RootPath);
            _allWorkspaceFiles.AddRange(files);
        }
        else
        {
            ToolbarProjectName.Text = "Single file";
            ProjectNameText.Text = "SINGLE FILE";
            StudioTitle.Context = $"Single file — {_activeDocument?.DisplayName ?? "untitled.spr"}";
            foreach (EditorDocument document in _editorWorkspace.Documents)
            {
                string path = document.FilePath ?? document.DisplayName;
                _allWorkspaceFiles.Add(new WorkspaceFile(document.DisplayName, document.DisplayName, path));
            }
        }
        ApplyWorkspaceFilter();
        RefreshCapabilities();
    }

    private void WorkspaceSearchBox_TextChanged(object sender, TextChangedEventArgs args) =>
        ApplyWorkspaceFilter();

    private void ApplyWorkspaceFilter()
    {
        string filter = WorkspaceSearchBox.Text.Trim();
        _workspace.Clear();
        foreach (WorkspaceFile file in _allWorkspaceFiles.Where(file =>
            filter.Length == 0
            || file.RelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            _workspace.Add(file);
        }
    }

    private async void WorkspaceList_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is WorkspaceFile file && File.Exists(file.FullPath))
        {
            await OpenDocumentAsync(file.FullPath, _projects.CurrentProject?.RootPath);
        }
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_updatingProfile && _activeDocument?.Language.Id == DocumentLanguage.Sprout)
        {
            _activeDocument.Profile = SelectedProfile();
            ScheduleInspection(showCompletion: false);
        }
    }

    private SyntaxProfile SelectedProfile() => ProfileCombo.SelectedIndex switch
    {
        1 => SyntaxProfile.Structured,
        2 => SyntaxProfile.Advanced,
        _ => SyntaxProfile.Guided,
    };

    private void ProblemsList_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is ProblemItem problem)
        {
            SelectSpan(problem.Diagnostic.Span);
        }
    }

    private void SelectSpan(SourceSpan span)
    {
        string source = _activeDocument?.Source ?? NormalizeLineEndings(Editor.Text);
        int normalizedStart = SourceTextCoordinates.Utf8OffsetToUtf16Index(source, span.Start);
        int normalizedEnd = SourceTextCoordinates.Utf8OffsetToUtf16Index(source, span.End);
        int start = NormalizedIndexToEditorIndex(Editor.Text, normalizedStart);
        int end = NormalizedIndexToEditorIndex(Editor.Text, normalizedEnd);
        Editor.Focus(FocusState.Programmatic);
        Editor.Select(start, Math.Max(1, end - start));
    }

    private void RefreshProblems(IEnumerable<StudioDiagnostic> diagnostics)
    {
        _problems.Clear();
        foreach (StudioDiagnostic diagnostic in diagnostics)
        {
            _problems.Add(new ProblemItem(diagnostic));
        }
    }

    private void RefreshTabHeader(EditorDocument document)
    {
        TabViewItem? tab = EditorTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(item => ReferenceEquals(item.Tag, document));
        if (tab is not null)
        {
            tab.Header = document.TabTitle;
        }
    }

    private void RefreshEditorDecorations()
    {
        string source = _activeDocument?.Source ?? NormalizeLineEndings(Editor.Text);
        int lines = Math.Max(1, source.Count(character => character == '\n') + 1);
        int shownLines = Math.Min(lines, 10_000);
        LineNumbersText.Text = string.Join('\n', Enumerable.Range(1, shownLines));
        MiniMapText.Text = source.Length <= 8_000 ? source : source[..8_000];
        RefreshCaretStatus();
    }

    private void RefreshCaretStatus()
    {
        (int line, int column) = CurrentLineAndColumn();
        CaretStatusText.Text = $"Ln {line}, Col {column}";
    }

    private (int line, int column) CurrentLineAndColumn()
    {
        int cursor = Math.Clamp(Editor.SelectionStart, 0, Editor.Text.Length);
        string prefix = NormalizeLineEndings(Editor.Text[..cursor]);
        int line = prefix.Count(character => character == '\n') + 1;
        int lastLineStart = prefix.LastIndexOf('\n') + 1;
        return (line, prefix.Length - lastLineStart + 1);
    }

    private int NormalizedCaretIndex()
    {
        return EditorTextCoordinates.EditorIndexToNormalizedIndex(
            Editor.Text,
            Editor.SelectionStart);
    }

    private static string NormalizeLineEndings(string value) =>
        EditorTextCoordinates.NormalizeLineEndings(value);

    private static int NormalizedIndexToEditorIndex(string editorText, int normalizedIndex) =>
        EditorTextCoordinates.NormalizedIndexToEditorIndex(editorText, normalizedIndex);

    private void RefreshCapabilities()
    {
        LanguageDescriptor? language = _activeDocument?.Language;
        bool busy = BusyIndicator.IsActive;
        ProfileCombo.IsEnabled = !busy && language?.Id == DocumentLanguage.Sprout;
        CheckButton.IsEnabled = !busy && language?.Capabilities.HasFlag(EditorCapability.Check) == true;
        RunButton.IsEnabled = !busy && language?.Capabilities.HasFlag(EditorCapability.Run) == true;
        DebugButton.IsEnabled = !busy && language?.Capabilities.HasFlag(EditorCapability.Debug) == true;
        BuildButton.IsEnabled = !busy
            && language?.Capabilities.HasFlag(EditorCapability.Build) == true
            && (language.Id != DocumentLanguage.Sprout || _projects.CurrentProject is not null);
        PackageHubButton.IsEnabled = !busy && _projects.CurrentProject is not null;
        SaveButton.IsEnabled = !busy && _activeDocument is not null;
    }

    private void BeginOperation()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
    }

    private void SetBusy(bool busy, string status)
    {
        BusyIndicator.IsActive = busy;
        NewButton.IsEnabled = !busy;
        OpenButton.IsEnabled = !busy;
        NewProjectButton.IsEnabled = !busy;
        OpenProjectButton.IsEnabled = !busy;
        AcademyButton.IsEnabled = !busy;
        ToolchainsButton.IsEnabled = !busy;
        StatusText.Text = status;
        RefreshCapabilities();
    }

    private static void WriteProcessResult(ProcessResult result, ICollection<string> output)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            foreach (string line in result.StandardOutput.Split('\n'))
            {
                output.Add(line.TrimEnd('\r'));
            }
        }
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            foreach (string line in result.StandardError.Split('\n'))
            {
                output.Add(line.TrimEnd('\r'));
            }
        }
        if (!result.Started && result.StartError is not null)
        {
            output.Add(result.StartError);
        }
        output.Add($"Exit: {(result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "none")} · {result.Duration.TotalMilliseconds:F0} ms");
    }

    private void RenderCanvas(IReadOnlyList<CanvasCommand> commands)
    {
        CanvasSurface.Children.Clear();
        CanvasSurface.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 17, 19, 17));
        foreach (CanvasCommand command in commands)
        {
            SolidColorBrush brush = new(ParseColor(command.Color));
            switch (command.Kind.ToLowerInvariant())
            {
                case "clear":
                    CanvasSurface.Children.Clear();
                    CanvasSurface.Background = brush;
                    break;
                case "rectangle":
                    var rectangle = new Rectangle
                    {
                        Width = Math.Max(0, command.Width ?? 0),
                        Height = Math.Max(0, command.Height ?? 0),
                        Fill = brush,
                    };
                    Canvas.SetLeft(rectangle, command.X ?? 0);
                    Canvas.SetTop(rectangle, command.Y ?? 0);
                    CanvasSurface.Children.Add(rectangle);
                    break;
                case "circle":
                    double radius = Math.Max(0, command.Radius ?? 0);
                    var circle = new Ellipse
                    {
                        Width = radius * 2,
                        Height = radius * 2,
                        Fill = brush,
                    };
                    Canvas.SetLeft(circle, (command.X ?? 0) - radius);
                    Canvas.SetTop(circle, (command.Y ?? 0) - radius);
                    CanvasSurface.Children.Add(circle);
                    break;
            }
        }
        CanvasEmptyText.Visibility = commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Windows.UI.Color ParseColor(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith('#') && normalized.Length is 7 or 9)
        {
            int offset = normalized.Length == 9 ? 3 : 1;
            byte alpha = normalized.Length == 9
                ? byte.Parse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : (byte)255;
            byte red = byte.Parse(normalized.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte green = byte.Parse(normalized.AsSpan(offset + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte blue = byte.Parse(normalized.AsSpan(offset + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ColorHelper.FromArgb(alpha, red, green, blue);
        }
        return normalized.ToLowerInvariant() switch
        {
            "red" => Colors.IndianRed,
            "green" => Colors.MediumSeaGreen,
            "blue" => Colors.CornflowerBlue,
            "yellow" => Colors.Goldenrod,
            "white" => Colors.White,
            "black" => Colors.Black,
            "orange" => Colors.Orange,
            "purple" => Colors.MediumPurple,
            _ => Colors.MediumSeaGreen,
        };
    }

    private async void ToolchainsButton_Click(object sender, RoutedEventArgs args)
    {
        ActivityRail.Select("toolchains");
        try
        {
            await ShowToolchainSettingsAsync();
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open toolchain settings", exception);
        }
    }

    private async void AcademyButton_Click(object sender, RoutedEventArgs args)
    {
        ActivityRail.Select("academy");
        try
        {
            await ShowAcademyAsync();
        }
        catch (Exception exception)
        {
            ShowHostFailure("Could not open Academy", exception);
        }
    }

    private async Task ShowAcademyAsync()
    {
        string previousTitle = Title;
        try
        {
            AcademyState state = await _academy.LoadStateAsync();
            if (state.Profile is null)
            {
                var profileDialog = new AcademyProfileDialog(_academy);
                profileDialog.Opened += (_, _) => Title = "Sprout Studio — Academy Profile";
                AcademyProfile? profile = await profileDialog.ShowAsync(RootLayout.XamlRoot);
                if (profile is null)
                {
                    StatusText.Text = "Academy setup postponed";
                    return;
                }
                state = await _academy.LoadStateAsync();
            }
            var dialog = new AcademyDialog(
                _academy,
                _toolchains,
                state,
                WindowNative.GetWindowHandle(this));
            dialog.Opened += (_, _) => Title = "Sprout Studio — Academy";
            await dialog.ShowAsync(RootLayout.XamlRoot);
            StatusText.Text = "Academy progress saved locally";
        }
        finally
        {
            Title = previousTitle;
        }
    }

    private async Task ShowToolchainSettingsAsync()
    {
        string previousTitle = Title;
        try
        {
            var dialog = new ToolchainSettingsDialog(
                _toolchains,
                WindowNative.GetWindowHandle(this));
            dialog.Opened += (_, _) => Title = "Sprout Studio — Toolchain Settings";
            await dialog.ShowAsync(RootLayout.XamlRoot);
            StatusText.Text = "Toolchain settings updated";
        }
        finally
        {
            Title = previousTitle;
        }
    }

    private void WorkspaceRailButton_Click(object sender, RoutedEventArgs args)
    {
        _explorerVisible = true;
        ActivityRail.Select("explorer");
        ApplyResponsiveLayout(RootLayout.ActualWidth);
        WorkspaceList.Focus(FocusState.Programmatic);
    }

    private void SearchRailButton_Click(object sender, RoutedEventArgs args)
    {
        _explorerVisible = true;
        ActivityRail.Select("search");
        ApplyResponsiveLayout(RootLayout.ActualWidth);
        WorkspaceSearchBox.Focus(FocusState.Programmatic);
    }

    private void RootLayout_SizeChanged(object sender, SizeChangedEventArgs args) =>
        ApplyResponsiveLayout(args.NewSize.Width);

    private async void CommandPaletteAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowCommandPaletteAsync();
    }

    private async void ExplorerAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ExecuteCommandAsync(StudioCommandId.ToggleExplorer);
    }

    private async void BottomPanelAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ExecuteCommandAsync(StudioCommandId.ToggleBottomPanel);
    }

    private async void NewDocumentAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ExecuteCommandAsync(StudioCommandId.NewDocument);
    }

    private async void OpenDocumentAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ExecuteCommandAsync(StudioCommandId.OpenDocument);
    }

    private async void SaveDocumentAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ExecuteCommandAsync(StudioCommandId.SaveDocument);
    }

    private async void RunAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ExecuteCommandAsync(StudioCommandId.Run);
    }

    private static bool IsKeyDown(VirtualKey key) =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void ExplorerSplitter_DragDelta(object? sender, Shell.PaneDragEventArgs args)
    {
        double inspectorSpace = InspectorColumn.Width.Value + InspectorSplitterColumn.Width.Value;
        double maximum = Math.Max(200, Math.Min(420, MainWorkspaceGrid.ActualWidth - inspectorSpace - 380));
        _explorerWidth = Math.Clamp(_explorerWidth + args.Delta, 200, maximum);
        ApplyResponsiveLayout(RootLayout.ActualWidth);
    }

    private void InspectorSplitter_DragDelta(object? sender, Shell.PaneDragEventArgs args)
    {
        double explorerSpace = ExplorerColumn.Width.Value + ExplorerSplitterColumn.Width.Value;
        double maximum = Math.Max(260, Math.Min(480, MainWorkspaceGrid.ActualWidth - explorerSpace - 380));
        _inspectorWidth = Math.Clamp(_inspectorWidth - args.Delta, 260, maximum);
        ApplyResponsiveLayout(RootLayout.ActualWidth);
    }

    private void BottomSplitter_DragDelta(object? sender, Shell.PaneDragEventArgs args)
    {
        double maximum = Math.Max(120, ApplicationBody.ActualHeight * 0.52);
        _bottomPanelHeight = Math.Clamp(_bottomPanelHeight - args.Delta, 120, maximum);
        ApplyResponsiveLayout(RootLayout.ActualWidth);
    }

    private void ToggleExplorer()
    {
        _explorerVisible = !_explorerVisible;
        ApplyResponsiveLayout(RootLayout.ActualWidth);
        StatusText.Text = _explorerVisible ? "Explorer shown" : "Explorer hidden";
    }

    private void ToggleInspector()
    {
        _inspectorVisible = !_inspectorVisible;
        ApplyResponsiveLayout(RootLayout.ActualWidth);
        StatusText.Text = _inspectorVisible ? "Inspector shown" : "Inspector hidden";
    }

    private void ToggleBottomPanel()
    {
        _bottomPanelVisible = !_bottomPanelVisible;
        ApplyResponsiveLayout(RootLayout.ActualWidth);
        StatusText.Text = _bottomPanelVisible ? "Bottom panel shown" : "Bottom panel hidden";
    }

    private void ToggleActivityBar()
    {
        _activityVisible = !_activityVisible;
        ApplyResponsiveLayout(RootLayout.ActualWidth);
        StatusText.Text = _activityVisible ? "Activity bar shown" : "Activity bar hidden";
    }

    private void ApplyResponsiveLayout(double width)
    {
        bool showActivity = _activityVisible;
        bool showExplorer = _explorerVisible && width >= 720;
        bool showInspector = _inspectorVisible && width >= 1_080;

        ActivityRail.Visibility = showActivity ? Visibility.Visible : Visibility.Collapsed;
        ActivityColumn.Width = new GridLength(showActivity ? 48 : 0);

        ExplorerPane.Visibility = showExplorer ? Visibility.Visible : Visibility.Collapsed;
        ExplorerSplitter.Visibility = showExplorer ? Visibility.Visible : Visibility.Collapsed;
        ExplorerColumn.Width = new GridLength(showExplorer ? _explorerWidth : 0);
        ExplorerSplitterColumn.Width = new GridLength(showExplorer ? 4 : 0);

        InspectorPane.Visibility = showInspector ? Visibility.Visible : Visibility.Collapsed;
        InspectorSplitter.Visibility = showInspector ? Visibility.Visible : Visibility.Collapsed;
        InspectorColumn.Width = new GridLength(showInspector ? _inspectorWidth : 0);
        InspectorSplitterColumn.Width = new GridLength(showInspector ? 4 : 0);

        BottomPane.Visibility = _bottomPanelVisible ? Visibility.Visible : Visibility.Collapsed;
        BottomSplitter.Visibility = _bottomPanelVisible ? Visibility.Visible : Visibility.Collapsed;
        BottomPanelRow.Height = new GridLength(_bottomPanelVisible ? _bottomPanelHeight : 0);
        BottomSplitterRow.Height = new GridLength(_bottomPanelVisible ? 4 : 0);
    }

    private void ApplyLayoutState(StudioLayoutState state)
    {
        StudioLayoutState sanitized = StudioLayoutStateStore.Sanitize(state);
        _explorerWidth = sanitized.ExplorerWidth;
        _inspectorWidth = sanitized.InspectorWidth;
        _bottomPanelHeight = sanitized.BottomPanelHeight;
        _activityVisible = sanitized.ActivityVisible;
        _explorerVisible = sanitized.ExplorerVisible;
        _inspectorVisible = sanitized.InspectorVisible;
        _bottomPanelVisible = sanitized.BottomPanelVisible;
        OutputTabs.SelectedIndex = sanitized.SelectedBottomTab;
        InspectorTabs.SelectedIndex = sanitized.SelectedInspectorTab;
        ActivityRail.Select(sanitized.SelectedActivity);
        ApplyResponsiveLayout(RootLayout.ActualWidth);
    }

    private StudioLayoutState CaptureLayoutState() => new()
    {
        ExplorerWidth = _explorerWidth,
        InspectorWidth = _inspectorWidth,
        BottomPanelHeight = _bottomPanelHeight,
        ActivityVisible = _activityVisible,
        ExplorerVisible = _explorerVisible,
        InspectorVisible = _inspectorVisible,
        BottomPanelVisible = _bottomPanelVisible,
        SelectedBottomTab = Math.Max(0, OutputTabs.SelectedIndex),
        SelectedInspectorTab = Math.Max(0, InspectorTabs.SelectedIndex),
        SelectedActivity = ActivityRail.SelectedKey,
    };

    private async void DocsRailButton_Click(object sender, RoutedEventArgs args)
    {
        ActivityRail.Select("documentation");
        var content = new StackPanel { Spacing = 10, MinWidth = 520 };
        content.Children.Add(new TextBlock
        {
            Text = "Editor shortcuts",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        content.Children.Add(new TextBlock
        {
            Text = "Ctrl+Space  Completion\nF12  Go to definition\nCtrl+S  Save\n\nCanvas built-ins\ncanvas_clear(color)\ncanvas_rect(x, y, width, height, color)\ncanvas_circle(x, y, radius, color)",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            TextWrapping = TextWrapping.Wrap,
        });
        await new ContentDialog
        {
            Title = "Sprout Studio quick reference",
            Content = content,
            CloseButtonText = "Close",
            XamlRoot = RootLayout.XamlRoot,
        }.ShowAsync();
    }

    private async void GitRailButton_Click(object sender, RoutedEventArgs args)
    {
        _explorerVisible = true;
        ActivityRail.Select("source-control");
        ExplorerPane.SourceControlExpander.IsExpanded = true;
        ApplyResponsiveLayout(RootLayout.ActualWidth);
        await RefreshGitAsync();
    }

    private async void RefreshGitButton_Click(object sender, RoutedEventArgs args) =>
        await RefreshGitAsync();

    private async Task RefreshGitAsync()
    {
        string root = _projects.CurrentProject?.RootPath
            ?? (_activeDocument?.FilePath is string filePath
                ? Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory
                : Environment.CurrentDirectory);
        try
        {
            ProcessResult result = await _processRunner.RunAsync(new ProcessRequest(
                "git",
                ["-c", "status.relativePaths=false", "status", "--short", "--branch"],
                root,
                TimeSpan.FromSeconds(8)));
            _gitStatus.Clear();
            string[] lines = result.StandardOutput.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries);
            string branchLine = lines.FirstOrDefault()?.TrimStart('#', ' ') ?? "Git repository is clean";
            GitBranchText.Text = branchLine;
            StudioStatus.Branch.Text = branchLine.Split(
                ["...", " "],
                StringSplitOptions.RemoveEmptyEntries)[0];
            foreach (string line in lines.Skip(1).Take(100))
            {
                _gitStatus.Add(line);
            }
            if (!result.Success && !string.IsNullOrWhiteSpace(result.StandardError))
            {
                GitBranchText.Text = result.StandardError.Trim();
            }
        }
        catch (Exception exception)
        {
            GitBranchText.Text = exception.Message;
        }
    }

    private void ShowProjectFailure(ProjectValidationException exception)
    {
        _problems.Clear();
        foreach (ProjectDiagnostic item in exception.Diagnostics)
        {
            _problems.Add(new ProblemItem(new StudioDiagnostic(
                item.Code,
                "error",
                "Invalid Sprout project",
                item.Message,
                new SourceSpan(item.Path ?? "sprout.toml", 0, 0, 1, 1, 1, 1),
                [],
                [],
                [])));
        }
        OutputTabs.SelectedIndex = 0;
        StatusText.Text = "Project validation failed";
    }

    private void ShowHostFailure(string title, Exception exception)
    {
        _problems.Clear();
        _problems.Add(new ProblemItem(new StudioDiagnostic(
            "SPT901",
            "error",
            title,
            exception.Message,
            new SourceSpan(_activeDocument?.DisplayName ?? "<studio>", 0, 0, 1, 1, 1, 1),
            [],
            [],
            [])));
        OutputTabs.SelectedIndex = 0;
        StatusText.Text = title;
    }
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

public sealed class OutlineItem
{
    public OutlineItem(DocumentSymbol symbol, int depth)
    {
        Symbol = symbol;
        Depth = depth;
    }

    public DocumentSymbol Symbol { get; }

    public int Depth { get; }

    public string Display => $"{new string(' ', Depth * 2)}{Symbol.Name}";

    public string Detail => Symbol.Detail;
}
