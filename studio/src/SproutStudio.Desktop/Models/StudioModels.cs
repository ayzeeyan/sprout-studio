using SproutStudio.Core;
using SproutStudio.Editor;

namespace SproutStudio.Models;

public sealed record ProjectCreationRequest(
    string Destination,
    string Name,
    ProjectTemplate Template);

public sealed record CommandPaletteItem(
    string Id,
    string Label,
    string Category,
    string? Shortcut = null);

public sealed record LanguageAnalysisSnapshot(
    Guid DocumentId,
    long DocumentVersion,
    LanguageServiceResponse Response);

public sealed record DocumentationNavigationRequest(
    string TopicId,
    string? Anchor = null);

public sealed record AboutSystemInfo(
    string StudioVersion,
    string StudioBuildConfiguration,
    string EngineVersion,
    int? EngineAbiVersion,
    string EngineArchitecture,
    string EngineBuildConfiguration,
    string DotNetRuntime,
    string ProcessArchitecture)
{
    public string AbiDisplay => EngineAbiVersion?.ToString() ?? "Unavailable";

    public string ClipboardText => string.Join(Environment.NewLine,
    [
        $"Sprout Studio {StudioVersion} ({StudioBuildConfiguration})",
        $"Sprout Engine {EngineVersion}",
        $"Engine ABI: {AbiDisplay}",
        $"Engine architecture: {EngineArchitecture}",
        $"Engine build: {EngineBuildConfiguration}",
        $"Runtime: {DotNetRuntime}",
        $"Process architecture: {ProcessArchitecture}",
        $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
    ]);
}

public enum ConsoleLineKind
{
    Normal,
    Muted,
    Success,
    Warning,
    Error,
}

public enum ActivityDestination
{
    Explorer,
    Search,
    SourceControl,
    Academy,
    Documentation,
    Packages,
    Toolchains,
    Settings,
}
