using System.Text.RegularExpressions;
using static SproutStudio.Toolchains.ToolchainAdapterHelpers;

namespace SproutStudio.Toolchains;

public sealed partial class RustToolchainAdapter : IToolchainAdapter
{
    private static readonly Version MinimumVersion = new(1, 95, 0);
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);
    private readonly IProcessRunner _processRunner;

    public RustToolchainAdapter(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public string Id => "rust";

    public string DisplayName => "Rust";

    public ToolchainCapability Capabilities =>
        ToolchainCapability.Run |
        ToolchainCapability.Build |
        ToolchainCapability.Format |
        ToolchainCapability.Test;

    public async Task<ToolchainDetectionResult> DetectAsync(
        string? customPath,
        ToolchainLocationSource? customSource,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ToolchainCandidate> candidates = ToolchainCandidateResolver.Resolve(
            ExecutableName("rustc"),
            customPath,
            customSource,
            RustCommonDirectories());
        ToolchainDetectionResult? bestFailure = null;
        foreach (ToolchainCandidate candidate in candidates)
        {
            ToolchainDetectionResult result = await ValidateCandidateAsync(candidate, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsUsable)
            {
                return result;
            }
            if (bestFailure is null || FailurePriority(result.Status) > FailurePriority(bestFailure.Status))
            {
                bestFailure = result;
            }
        }
        return bestFailure ?? NotInstalled(
            Id,
            DisplayName,
            "Rust was not found on PATH, in common installation locations, or in settings.");
    }

    public ProcessRequest CreateActionRequest(
        ToolchainAction action,
        ToolchainDetectionResult detection,
        string workingDirectory,
        IReadOnlyList<string> additionalArguments)
    {
        EnsureUsable(detection);
        string cargo = RequiredComponentPath(detection, "cargo");
        var arguments = new List<string>
        {
            action switch
            {
                ToolchainAction.Run => "run",
                ToolchainAction.Build => "build",
                ToolchainAction.Format => "fmt",
                ToolchainAction.Test => "test",
                _ => throw new ArgumentOutOfRangeException(nameof(action)),
            },
        };
        arguments.AddRange(additionalArguments);
        return new ProcessRequest(cargo, arguments, Path.GetFullPath(workingDirectory));
    }

    private async Task<ToolchainDetectionResult> ValidateCandidateAsync(
        ToolchainCandidate candidate,
        CancellationToken cancellationToken)
    {
        string rustcPath = Path.Combine(candidate.Directory, ExecutableName("rustc"));
        string cargoPath = Path.Combine(candidate.Directory, ExecutableName("cargo"));
        string rustfmtPath = Path.Combine(candidate.Directory, ExecutableName("rustfmt"));
        ToolchainComponent rustc = await ValidateVersionComponentAsync(
            "rustc",
            rustcPath,
            ["--version"],
            RustcVersion,
            cancellationToken).ConfigureAwait(false);
        ToolchainComponent cargo = await ValidateVersionComponentAsync(
            "cargo",
            cargoPath,
            ["--version"],
            CargoVersion,
            cancellationToken).ConfigureAwait(false);
        ToolchainComponent rustfmt = await ValidateVersionComponentAsync(
            "rustfmt",
            rustfmtPath,
            ["--version"],
            RustfmtVersion,
            cancellationToken).ConfigureAwait(false);
        ToolchainComponent[] components = [rustc, cargo, rustfmt];

        ToolchainStatus status;
        string message;
        if (!components.Any(component => component.Available))
        {
            status = ToolchainStatus.FoundButBroken;
            message = $"No Rust executables were found under {candidate.Directory}.";
        }
        else if (components.Any(component => !component.Available))
        {
            status = ToolchainStatus.PartialInstallation;
            message = MissingMessage("Rust", components);
        }
        else if (components.Any(component => !component.Valid))
        {
            status = ToolchainStatus.FoundButBroken;
            message = BrokenMessage("Rust", components);
        }
        else if (!TrySemanticVersion(rustc.Version, out Version? version) || version < MinimumVersion)
        {
            status = ToolchainStatus.UnsupportedVersion;
            message = $"Sprout Studio requires Rust {MinimumVersion} or newer; found {rustc.Version ?? "an unknown version"}.";
        }
        else
        {
            status = IsCustom(candidate.Source)
                ? ToolchainStatus.CustomLocation
                : ToolchainStatus.Ready;
            message = candidate.Source switch
            {
                ToolchainLocationSource.Configured => "Rust is ready from the saved custom location.",
                ToolchainLocationSource.Explicit => "Rust is ready from the selected custom location.",
                _ => "Rust is installed and validated.",
            };
        }

        return new ToolchainDetectionResult(
            Id,
            DisplayName,
            status,
            candidate.Source,
            rustcPath,
            rustc.Version,
            status is ToolchainStatus.Ready or ToolchainStatus.CustomLocation
                ? Capabilities
                : ToolchainCapability.None,
            components,
            message);
    }

    private Task<ToolchainComponent> ValidateVersionComponentAsync(
        string name,
        string executablePath,
        IReadOnlyList<string> arguments,
        Func<string, string?> versionParser,
        CancellationToken cancellationToken) =>
        ToolchainValidation.ValidateVersionComponentAsync(
            _processRunner,
            name,
            executablePath,
            required: true,
            arguments,
            versionParser,
            ValidationTimeout,
            cancellationToken);

    private static string? RustcVersion(string output) =>
        VersionFromMatch(RustcVersionPattern().Match(output));

    private static string? CargoVersion(string output) =>
        VersionFromMatch(CargoVersionPattern().Match(output));

    private static string? RustfmtVersion(string output) =>
        VersionFromMatch(RustfmtVersionPattern().Match(output));

    private static string? VersionFromMatch(Match match) =>
        match.Success ? match.Groups[1].Value : null;

    [GeneratedRegex(@"\brustc\s+(\d+\.\d+\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex RustcVersionPattern();

    [GeneratedRegex(@"\bcargo\s+(\d+\.\d+\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex CargoVersionPattern();

    [GeneratedRegex(@"\brustfmt\s+(\d+\.\d+\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex RustfmtVersionPattern();

    private static IEnumerable<string> RustCommonDirectories()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            yield return Path.Combine(profile, ".cargo", "bin");
        }
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Rust", "bin");
            foreach (string directory in Directory.EnumerateDirectories(programFiles, "Rust*"))
            {
                yield return Path.Combine(directory, "bin");
            }
        }
    }
}

public sealed partial class GoToolchainAdapter : IToolchainAdapter
{
    private static readonly Version MinimumVersion = new(1, 22, 0);
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);
    private readonly IProcessRunner _processRunner;

    public GoToolchainAdapter(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public string Id => "go";

    public string DisplayName => "Go";

    public ToolchainCapability Capabilities =>
        ToolchainCapability.Run |
        ToolchainCapability.Build |
        ToolchainCapability.Format |
        ToolchainCapability.Test;

    public async Task<ToolchainDetectionResult> DetectAsync(
        string? customPath,
        ToolchainLocationSource? customSource,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ToolchainCandidate> candidates = ToolchainCandidateResolver.Resolve(
            ExecutableName("go"),
            customPath,
            customSource,
            GoCommonDirectories());
        ToolchainDetectionResult? bestFailure = null;
        foreach (ToolchainCandidate candidate in candidates)
        {
            ToolchainDetectionResult result = await ValidateCandidateAsync(candidate, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsUsable)
            {
                return result;
            }
            if (bestFailure is null || FailurePriority(result.Status) > FailurePriority(bestFailure.Status))
            {
                bestFailure = result;
            }
        }
        return bestFailure ?? NotInstalled(
            Id,
            DisplayName,
            "Go was not found on PATH, in common installation locations, or in settings.");
    }

    public ProcessRequest CreateActionRequest(
        ToolchainAction action,
        ToolchainDetectionResult detection,
        string workingDirectory,
        IReadOnlyList<string> additionalArguments)
    {
        EnsureUsable(detection);
        string go = RequiredComponentPath(detection, "go");
        var arguments = action switch
        {
            ToolchainAction.Run => new List<string> { "run", "." },
            ToolchainAction.Build => ["build", "./..."],
            ToolchainAction.Format => ["fmt", "./..."],
            ToolchainAction.Test => ["test", "./..."],
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        arguments.AddRange(additionalArguments);
        return new ProcessRequest(go, arguments, Path.GetFullPath(workingDirectory));
    }

    private async Task<ToolchainDetectionResult> ValidateCandidateAsync(
        ToolchainCandidate candidate,
        CancellationToken cancellationToken)
    {
        string goPath = Path.Combine(candidate.Directory, ExecutableName("go"));
        string gofmtPath = Path.Combine(candidate.Directory, ExecutableName("gofmt"));
        ToolchainComponent go = await ToolchainValidation.ValidateVersionComponentAsync(
            _processRunner,
            "go",
            goPath,
            required: true,
            ["version"],
            GoVersion,
            ValidationTimeout,
            cancellationToken).ConfigureAwait(false);
        ToolchainComponent gofmt = await ToolchainValidation.ValidateCommandComponentAsync(
            _processRunner,
            "gofmt",
            gofmtPath,
            required: true,
            ["-h"],
            result => result.Started
                && !result.TimedOut
                && (result.ExitCode is 0 or 2)
                && $"{result.StandardOutput}\n{result.StandardError}".Contains(
                    "usage",
                    StringComparison.OrdinalIgnoreCase),
            ValidationTimeout,
            cancellationToken).ConfigureAwait(false);
        ToolchainComponent[] components = [go, gofmt];

        ToolchainStatus status;
        string message;
        if (!components.Any(component => component.Available))
        {
            status = ToolchainStatus.FoundButBroken;
            message = $"No Go executables were found under {candidate.Directory}.";
        }
        else if (components.Any(component => !component.Available))
        {
            status = ToolchainStatus.PartialInstallation;
            message = MissingMessage("Go", components);
        }
        else if (components.Any(component => !component.Valid))
        {
            status = ToolchainStatus.FoundButBroken;
            message = BrokenMessage("Go", components);
        }
        else if (!TrySemanticVersion(go.Version, out Version? version) || version < MinimumVersion)
        {
            status = ToolchainStatus.UnsupportedVersion;
            message = $"Sprout Studio requires Go {MinimumVersion} or newer; found {go.Version ?? "an unknown version"}.";
        }
        else
        {
            status = IsCustom(candidate.Source)
                ? ToolchainStatus.CustomLocation
                : ToolchainStatus.Ready;
            message = candidate.Source switch
            {
                ToolchainLocationSource.Configured => "Go is ready from the saved custom location.",
                ToolchainLocationSource.Explicit => "Go is ready from the selected custom location.",
                _ => "Go is installed and validated.",
            };
        }

        return new ToolchainDetectionResult(
            Id,
            DisplayName,
            status,
            candidate.Source,
            goPath,
            go.Version,
            status is ToolchainStatus.Ready or ToolchainStatus.CustomLocation
                ? Capabilities
                : ToolchainCapability.None,
            components,
            message);
    }

    private static string? GoVersion(string output) =>
        VersionFromMatch(GoVersionPattern().Match(output));

    private static string? VersionFromMatch(Match match) =>
        match.Success ? match.Groups[1].Value : null;

    [GeneratedRegex(@"\bgo version go(\d+\.\d+(?:\.\d+)?)\b", RegexOptions.CultureInvariant)]
    private static partial Regex GoVersionPattern();

    private static IEnumerable<string> GoCommonDirectories()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Go", "bin");
        }
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            yield return Path.Combine(local, "Programs", "Go", "bin");
        }
        if (!OperatingSystem.IsWindows())
        {
            yield return "/usr/local/go/bin";
        }
    }
}

internal sealed record ToolchainCandidate(string Directory, ToolchainLocationSource Source);

internal static class ToolchainCandidateResolver
{
    public static IReadOnlyList<ToolchainCandidate> Resolve(
        string mainExecutable,
        string? customPath,
        ToolchainLocationSource? customSource,
        IEnumerable<string> commonDirectories)
    {
        var results = new List<ToolchainCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            string fullPath = Path.GetFullPath(customPath);
            string directory = Directory.Exists(fullPath)
                ? fullPath
                : File.Exists(fullPath) || Path.HasExtension(fullPath)
                    ? Path.GetDirectoryName(fullPath) ?? fullPath
                    : fullPath;
            Add(directory, customSource ?? ToolchainLocationSource.Configured, requireExecutable: false);
            Add(Path.Combine(directory, "bin"), customSource ?? ToolchainLocationSource.Configured, requireExecutable: false);
            return results;
        }

        string? pathEnvironment = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnvironment))
        {
            foreach (string item in pathEnvironment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                Add(item.Trim().Trim('"'), ToolchainLocationSource.PathEnvironment, requireExecutable: true);
            }
        }
        foreach (string directory in commonDirectories)
        {
            Add(directory, ToolchainLocationSource.CommonLocation, requireExecutable: true);
        }
        return results;

        void Add(string directory, ToolchainLocationSource source, bool requireExecutable)
        {
            string fullDirectory;
            try
            {
                fullDirectory = Path.GetFullPath(directory);
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return;
            }
            if (requireExecutable && !File.Exists(Path.Combine(fullDirectory, mainExecutable)))
            {
                return;
            }
            if (seen.Add(fullDirectory))
            {
                results.Add(new ToolchainCandidate(fullDirectory, source));
            }
        }
    }
}

internal static class ToolchainValidation
{
    public static async Task<ToolchainComponent> ValidateVersionComponentAsync(
        IProcessRunner processRunner,
        string name,
        string executablePath,
        bool required,
        IReadOnlyList<string> arguments,
        Func<string, string?> versionParser,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(executablePath))
        {
            return Missing(name, executablePath, required);
        }
        ProcessResult result = await processRunner.RunAsync(
            new ProcessRequest(executablePath, arguments, Timeout: timeout),
            cancellationToken).ConfigureAwait(false);
        string output = CombinedOutput(result);
        string? version = result.Success ? versionParser(output) : null;
        bool valid = result.Success && version is not null;
        return new ToolchainComponent(
            name,
            executablePath,
            required,
            Available: true,
            Valid: valid,
            Version: version,
            Failure: valid ? null : Failure(result, "Version output was not recognized."));
    }

    public static async Task<ToolchainComponent> ValidateCommandComponentAsync(
        IProcessRunner processRunner,
        string name,
        string executablePath,
        bool required,
        IReadOnlyList<string> arguments,
        Func<ProcessResult, bool> validator,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(executablePath))
        {
            return Missing(name, executablePath, required);
        }
        ProcessResult result = await processRunner.RunAsync(
            new ProcessRequest(executablePath, arguments, Timeout: timeout),
            cancellationToken).ConfigureAwait(false);
        bool valid = validator(result);
        return new ToolchainComponent(
            name,
            executablePath,
            required,
            Available: true,
            Valid: valid,
            Version: null,
            Failure: valid ? null : Failure(result, "Validation output was not recognized."));
    }

    private static ToolchainComponent Missing(string name, string path, bool required) =>
        new(name, path, required, false, false, null, "Executable was not found.");

    private static string CombinedOutput(ProcessResult result) =>
        $"{result.StandardOutput}\n{result.StandardError}".Trim();

    private static string Failure(ProcessResult result, string fallback)
    {
        if (!result.Started)
        {
            return result.StartError ?? "The process could not start.";
        }
        if (result.TimedOut)
        {
            return "Validation timed out.";
        }
        string output = CombinedOutput(result);
        return output.Length == 0 ? fallback : output;
    }
}

internal static class ToolchainAdapterHelpers
{
    public static string ExecutableName(string name) =>
        OperatingSystem.IsWindows() ? $"{name}.exe" : name;

    public static bool IsCustom(ToolchainLocationSource source) =>
        source is ToolchainLocationSource.Configured or ToolchainLocationSource.Explicit;

    public static int FailurePriority(ToolchainStatus status) => status switch
    {
        ToolchainStatus.UnsupportedVersion => 4,
        ToolchainStatus.PartialInstallation => 3,
        ToolchainStatus.FoundButBroken => 2,
        ToolchainStatus.NotInstalled => 1,
        _ => 0,
    };

    public static ToolchainDetectionResult NotInstalled(
        string id,
        string displayName,
        string message) =>
        new(
            id,
            displayName,
            ToolchainStatus.NotInstalled,
            null,
            null,
            null,
            ToolchainCapability.None,
            [],
            message);

    public static string MissingMessage(
        string displayName,
        IEnumerable<ToolchainComponent> components)
    {
        string missing = string.Join(
            ", ",
            components.Where(component => !component.Available).Select(component => component.Name));
        return $"{displayName} is partially installed; missing: {missing}.";
    }

    public static string BrokenMessage(
        string displayName,
        IEnumerable<ToolchainComponent> components)
    {
        string broken = string.Join(
            "; ",
            components
                .Where(component => component.Available && !component.Valid)
                .Select(component => $"{component.Name}: {component.Failure}"));
        return $"{displayName} was found but validation failed. {broken}";
    }

    public static bool TrySemanticVersion(string? value, out Version? version)
    {
        if (value is null)
        {
            version = null;
            return false;
        }
        string normalized = value.Count(character => character == '.') == 1
            ? $"{value}.0"
            : value;
        return Version.TryParse(normalized, out version);
    }

    public static void EnsureUsable(ToolchainDetectionResult detection)
    {
        ArgumentNullException.ThrowIfNull(detection);
        if (!detection.IsUsable)
        {
            throw new InvalidOperationException(
                $"{detection.DisplayName} is not ready: {detection.Message}");
        }
    }

    public static string RequiredComponentPath(
        ToolchainDetectionResult detection,
        string componentName) =>
        detection.Components
            .Single(component =>
                component.Valid && string.Equals(
                    component.Name,
                    componentName,
                    StringComparison.OrdinalIgnoreCase))
            .ExecutablePath;
}
