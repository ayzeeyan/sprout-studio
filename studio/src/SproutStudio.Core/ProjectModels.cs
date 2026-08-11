using System.Text.Json;

namespace SproutStudio.Core;

public enum ProjectTemplate
{
    Guided = 0,
    Structured = 1,
    Advanced = 2,
}

public sealed record ProjectInfo(
    string RootPath,
    string ManifestPath,
    string Name,
    string Version,
    string EntryPath,
    SyntaxProfile Profile,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyDictionary<string, string>? Dependencies = null,
    IReadOnlyDictionary<string, string>? PythonDependencies = null,
    string BuildTarget = "windows-x86_64");

public sealed record ProjectDiagnostic(
    string Code,
    string Title,
    string Message,
    string? Path);

public sealed record ProjectResponse(
    int ApiVersion,
    bool Success,
    ProjectInfo? Project,
    IReadOnlyList<ProjectDiagnostic> Diagnostics);

public interface IProjectClient
{
    Task<ProjectResponse> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse> CreateAsync(
        string destination,
        string name,
        ProjectTemplate template,
        CancellationToken cancellationToken = default);
}

public enum ProjectDependencyKind
{
    Sprout = 0,
    Python = 1,
}

public sealed record BuildArtifact(
    string Path,
    string Target,
    int SourceCount);

public sealed record BuildResponse(
    int ApiVersion,
    bool Success,
    BuildArtifact? Artifact,
    IReadOnlyList<ProjectDiagnostic> Diagnostics);

public interface IProjectAdvancedClient
{
    Task<ProjectResponse> UpdateDependencyAsync(
        string projectPath,
        ProjectDependencyKind kind,
        string name,
        string? requirement,
        CancellationToken cancellationToken = default);

    Task<BuildResponse> BuildAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}

public sealed record RecentProject(string Name, string RootPath, DateTimeOffset LastOpenedUtc);

public sealed class RecentProjectStore
{
    private const int MaximumEntries = 10;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public RecentProjectStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SproutStudio",
            "recent-projects.json");
    }

    public async Task<IReadOnlyList<RecentProject>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            IReadOnlyList<RecentProject>? projects = await JsonSerializer.DeserializeAsync<
                List<RecentProject>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return projects ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<RecentProject>> AddAsync(
        ProjectInfo project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        IReadOnlyList<RecentProject> existing = await LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        List<RecentProject> updated = [
            new(project.Name, project.RootPath, DateTimeOffset.UtcNow),
            .. existing.Where(item => !string.Equals(
                item.RootPath,
                project.RootPath,
                StringComparison.OrdinalIgnoreCase)),
        ];
        if (updated.Count > MaximumEntries)
        {
            updated.RemoveRange(MaximumEntries, updated.Count - MaximumEntries);
        }

        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string temporaryPath = _filePath + ".tmp";
        try
        {
            await using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, updated, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        return updated;
    }
}

public sealed class ProjectValidationException : Exception
{
    public ProjectValidationException(IReadOnlyList<ProjectDiagnostic> diagnostics)
        : base(diagnostics.Count == 0
            ? "The project operation failed."
            : string.Join(Environment.NewLine, diagnostics.Select(item => item.Message)))
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<ProjectDiagnostic> Diagnostics { get; }
}

public sealed class ProjectWorkspace
{
    private readonly IProjectClient _client;
    private readonly RecentProjectStore _recentStore;
    private readonly List<RecentProject> _recentProjects = [];

    public ProjectWorkspace(IProjectClient client, RecentProjectStore? recentStore = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _recentStore = recentStore ?? new RecentProjectStore();
    }

    public ProjectInfo? CurrentProject { get; private set; }

    public IReadOnlyList<RecentProject> RecentProjects => _recentProjects;

    public void Close()
    {
        CurrentProject = null;
    }

    public async Task LoadRecentAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RecentProject> projects = await _recentStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        _recentProjects.Clear();
        _recentProjects.AddRange(projects);
    }

    public async Task<ProjectInfo> OpenAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ProjectResponse response = await _client.LoadAsync(projectPath, cancellationToken)
            .ConfigureAwait(false);
        return await AcceptAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectInfo> CreateAsync(
        string destination,
        string name,
        ProjectTemplate template,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ProjectResponse response = await _client
            .CreateAsync(destination, name, template, cancellationToken)
            .ConfigureAwait(false);
        return await AcceptAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProjectInfo> AcceptAsync(
        ProjectResponse response,
        CancellationToken cancellationToken)
    {
        if (!response.Success || response.Project is null)
        {
            throw new ProjectValidationException(response.Diagnostics);
        }

        CurrentProject = response.Project;
        IReadOnlyList<RecentProject> recent = await _recentStore
            .AddAsync(response.Project, cancellationToken)
            .ConfigureAwait(false);
        _recentProjects.Clear();
        _recentProjects.AddRange(recent);
        return response.Project;
    }
}
