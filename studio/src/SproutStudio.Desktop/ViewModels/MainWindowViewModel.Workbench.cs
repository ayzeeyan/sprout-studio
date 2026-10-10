using CommunityToolkit.Mvvm.Input;
using SproutStudio.Models;
using SproutStudio.Services;

namespace SproutStudio.ViewModels;

/// <summary>
/// Workbench conveniences layered on the main view model: editor zoom, Zen mode,
/// problem counters, the title-bar command center, and engine/architecture status.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private const double DefaultEditorFontSize = 14;
    private ZenSnapshot? _zenSnapshot;

    /// <summary>Raised when the editor should prompt for a line number.</summary>
    public event EventHandler? GoToLineRequested;

    public IRelayCommand ZoomInCommand { get; private set; } = null!;

    public IRelayCommand ZoomOutCommand { get; private set; } = null!;

    public IRelayCommand ResetZoomCommand { get; private set; } = null!;

    public IRelayCommand ToggleZenModeCommand { get; private set; } = null!;

    public IRelayCommand ShowProblemsCommand { get; private set; } = null!;

    public IRelayCommand GoToLineCommand { get; private set; } = null!;

    public bool IsZenMode => _zenSnapshot is not null;

    public string ZoomText => $"{Math.Round(EditorFontSize / DefaultEditorFontSize * 100)}%";

    public bool EditorHighlightCurrentLine => _settings.HighlightCurrentLine;

    public bool EditorShowIndentGuides => _settings.ShowIndentGuides;

    public int ErrorCount => Problems.Count(problem =>
        problem.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));

    public int WarningCount => Problems.Count(problem =>
        problem.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));

    public string CommandCenterText => CurrentProject is null
        ? "Search commands, files, and settings"
        : $"{CurrentProject.Name}  ·  Search commands";

    /// <summary>Short status-bar label, e.g. "Sprout 0.1.0 · x64".</summary>
    public string EngineArchitecture => _engineInfo is null
        ? "Engine offline"
        : $"Sprout {_engineInfo.EngineVersion} · {FriendlyArchitecture(_engineInfo.Architecture)}";

    public string EngineSummary => _engineInfo is null
        ? "The Sprout engine is not loaded."
        : $"Sprout engine {_engineInfo.EngineVersion} · ABI {_engineInfo.AbiVersion} · "
            + $"{_engineInfo.Architecture} ({FriendlyArchitecture(_engineInfo.Architecture)}) · {_engineInfo.BuildConfiguration}";

    private void InitializeWorkbenchCommands()
    {
        ZoomInCommand = new RelayCommand(() => SetEditorFontSize(EditorFontSize + 1));
        ZoomOutCommand = new RelayCommand(() => SetEditorFontSize(EditorFontSize - 1));
        ResetZoomCommand = new RelayCommand(() => SetEditorFontSize(DefaultEditorFontSize));
        ToggleZenModeCommand = new RelayCommand(ToggleZenMode);
        ShowProblemsCommand = new RelayCommand(ShowProblems);
        GoToLineCommand = new RelayCommand(
            () => GoToLineRequested?.Invoke(this, EventArgs.Empty),
            () => SelectedDocument?.IsSource == true);
    }

    /// <summary>Changes the editor font size (zoom) and persists it as a setting.</summary>
    public void SetEditorFontSize(double size)
    {
        double clamped = Math.Clamp(
            Math.Round(size),
            StudioSettingsState.MinimumFontSize,
            StudioSettingsState.MaximumFontSize);
        if (Math.Abs(clamped - _settings.EditorFontSize) < 0.01)
        {
            return;
        }
        _settings = _settings with { EditorFontSize = clamped };
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StudioLog.Error("Could not persist the editor zoom level.", exception);
        }
        OnPropertyChanged(nameof(EditorFontSize));
        OnPropertyChanged(nameof(ZoomText));
        StatusText = $"Zoom {ZoomText}";
    }

    /// <summary>Applies and persists an accent theme chosen outside the Settings dialog.</summary>
    private Task SetAccentThemeAsync(string themeId)
    {
        StudioAccentTheme theme = StudioThemeService.Find(themeId);
        _settings = _settings with { AccentTheme = theme.Id };
        StudioThemeService.Apply(theme.Id);
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StudioLog.Error("Could not persist the accent theme.", exception);
        }
        NotifyEditorSettings();
        StatusText = $"{theme.DisplayName} accent theme";
        return Task.CompletedTask;
    }

    private void NotifyEditorSettings()
    {
        OnPropertyChanged(nameof(ZoomText));
        OnPropertyChanged(nameof(EditorHighlightCurrentLine));
        OnPropertyChanged(nameof(EditorShowIndentGuides));
    }

    private void NotifyProblemCounts()
    {
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
    }

    private void ShowProblems()
    {
        if (!IsWorkspaceMode)
        {
            SelectActivity(ActivityDestination.Explorer);
        }
        BottomPanelVisible = true;
        SelectedBottomTab = 0;
    }

    private void ToggleZenMode()
    {
        if (_zenSnapshot is { } snapshot)
        {
            _zenSnapshot = null;
            ActivityVisible = snapshot.Activity;
            ExplorerVisible = snapshot.Explorer;
            InspectorVisible = snapshot.Inspector;
            BottomPanelVisible = snapshot.Bottom;
            StatusText = "Zen mode off";
        }
        else
        {
            _zenSnapshot = new ZenSnapshot(ActivityVisible, ExplorerVisible, InspectorVisible, BottomPanelVisible);
            if (!IsWorkspaceMode)
            {
                SelectActivity(ActivityDestination.Explorer);
            }
            ExplorerVisible = false;
            InspectorVisible = false;
            BottomPanelVisible = false;
            ActivityVisible = false;
            StatusText = "Zen mode — press F11 to return";
        }
        OnPropertyChanged(nameof(IsZenMode));
    }

    private static string FriendlyArchitecture(string architecture) => architecture.ToLowerInvariant() switch
    {
        "x86_64" or "amd64" or "x64" => "x64",
        "aarch64" or "arm64" => "ARM64",
        "x86" or "i686" => "x86",
        _ => architecture,
    };

    private sealed record ZenSnapshot(bool Activity, bool Explorer, bool Inspector, bool Bottom);
}
