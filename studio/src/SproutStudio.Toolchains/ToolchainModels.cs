namespace SproutStudio.Toolchains;

[Flags]
public enum ToolchainCapability
{
    None = 0,
    Run = 1,
    Build = 2,
    Format = 4,
    Test = 8,
}

public enum ToolchainAction
{
    Run,
    Build,
    Format,
    Test,
}

public enum ToolchainStatus
{
    Ready,
    NotInstalled,
    FoundButBroken,
    PartialInstallation,
    UnsupportedVersion,
    CustomLocation,
}

public enum ToolchainLocationSource
{
    PathEnvironment,
    CommonLocation,
    Configured,
    Explicit,
}

public sealed record ToolchainComponent(
    string Name,
    string ExecutablePath,
    bool Required,
    bool Available,
    bool Valid,
    string? Version,
    string? Failure);

public sealed record ToolchainDetectionResult(
    string Id,
    string DisplayName,
    ToolchainStatus Status,
    ToolchainLocationSource? LocationSource,
    string? ExecutablePath,
    string? Version,
    ToolchainCapability Capabilities,
    IReadOnlyList<ToolchainComponent> Components,
    string Message)
{
    public bool IsUsable => Status is ToolchainStatus.Ready or ToolchainStatus.CustomLocation;
}

public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    TimeSpan? Timeout = null);

public sealed record ProcessResult(
    bool Started,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut,
    string? StartError)
{
    public bool Success => Started && !TimedOut && ExitCode == 0;
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}

public interface IToolchainAdapter
{
    string Id { get; }

    string DisplayName { get; }

    ToolchainCapability Capabilities { get; }

    Task<ToolchainDetectionResult> DetectAsync(
        string? customPath,
        ToolchainLocationSource? customSource,
        CancellationToken cancellationToken = default);

    ProcessRequest CreateActionRequest(
        ToolchainAction action,
        ToolchainDetectionResult detection,
        string workingDirectory,
        IReadOnlyList<string> additionalArguments);
}
