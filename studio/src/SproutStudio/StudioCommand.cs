namespace SproutStudio;

internal enum StudioCommandId
{
    NewDocument,
    OpenDocument,
    SaveDocument,
    NewProject,
    OpenProject,
    Check,
    Run,
    Debug,
    Build,
    FormatDocument,
    GoToDefinition,
    ToggleBreakpoint,
    ShowExplorer,
    FocusSearch,
    ShowSourceControl,
    OpenAcademy,
    OpenDocumentation,
    OpenPackages,
    OpenToolchains,
    ToggleExplorer,
    ToggleInspector,
    ToggleBottomPanel,
    ToggleActivityBar,
}

internal sealed record StudioCommand(
    StudioCommandId Id,
    string Category,
    string Title,
    string Shortcut,
    Func<bool> CanExecute,
    Func<Task> ExecuteAsync)
{
    public string Display => $"{Category}: {Title}";
}
