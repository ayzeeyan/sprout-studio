namespace SproutStudio.Toolchains;

public sealed class ToolchainRegistry
{
    private readonly Dictionary<string, IToolchainAdapter> _adapters;

    public ToolchainRegistry(IEnumerable<IToolchainAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = new Dictionary<string, IToolchainAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (IToolchainAdapter adapter in adapters)
        {
            if (!_adapters.TryAdd(adapter.Id, adapter))
            {
                throw new ArgumentException(
                    $"A toolchain adapter with id `{adapter.Id}` is already registered.",
                    nameof(adapters));
            }
        }
        if (_adapters.Count == 0)
        {
            throw new ArgumentException("At least one toolchain adapter is required.", nameof(adapters));
        }
    }

    public IReadOnlyCollection<IToolchainAdapter> Adapters => _adapters.Values;

    public IToolchainAdapter Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _adapters.TryGetValue(id, out IToolchainAdapter? adapter)
            ? adapter
            : throw new KeyNotFoundException($"Unknown toolchain `{id}`.");
    }
}

public sealed class ToolchainManager
{
    private readonly ToolchainRegistry _registry;
    private readonly ToolchainSettingsStore _settings;
    private readonly IProcessRunner _processRunner;

    public ToolchainManager(
        ToolchainRegistry registry,
        ToolchainSettingsStore settings,
        IProcessRunner processRunner)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public static ToolchainManager CreateDefault(string? settingsPath = null)
    {
        var processRunner = new ProcessRunner();
        var registry = new ToolchainRegistry(
        [
            new RustToolchainAdapter(processRunner),
            new GoToolchainAdapter(processRunner),
            new PythonToolchainAdapter(processRunner),
            new JavaToolchainAdapter(processRunner),
        ]);
        return new ToolchainManager(
            registry,
            new ToolchainSettingsStore(settingsPath),
            processRunner);
    }

    public IReadOnlyCollection<IToolchainAdapter> RegisteredToolchains => _registry.Adapters;

    public async Task<IReadOnlyList<ToolchainDetectionResult>> DetectAllAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, string> settings = await _settings.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        Task<ToolchainDetectionResult>[] detections = _registry.Adapters
            .OrderBy(adapter => adapter.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(adapter => adapter.DetectAsync(
                settings.GetValueOrDefault(adapter.Id),
                settings.ContainsKey(adapter.Id) ? ToolchainLocationSource.Configured : null,
                cancellationToken))
            .ToArray();
        return await Task.WhenAll(detections).ConfigureAwait(false);
    }

    public async Task<ToolchainDetectionResult> DetectAsync(
        string toolchainId,
        string? explicitPath = null,
        CancellationToken cancellationToken = default)
    {
        IToolchainAdapter adapter = _registry.Get(toolchainId);
        if (explicitPath is not null)
        {
            return await adapter.DetectAsync(
                Path.GetFullPath(explicitPath),
                ToolchainLocationSource.Explicit,
                cancellationToken).ConfigureAwait(false);
        }
        IReadOnlyDictionary<string, string> settings = await _settings.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        string? configured = settings.GetValueOrDefault(adapter.Id);
        return await adapter.DetectAsync(
            configured,
            configured is null ? null : ToolchainLocationSource.Configured,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ToolchainDetectionResult> ConfigureCustomPathAsync(
        string toolchainId,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected toolchain location does not exist.", fullPath);
        }
        IToolchainAdapter adapter = _registry.Get(toolchainId);
        await _settings.SetCustomPathAsync(adapter.Id, fullPath, cancellationToken)
            .ConfigureAwait(false);
        return await adapter.DetectAsync(
            fullPath,
            ToolchainLocationSource.Configured,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ToolchainDetectionResult> ClearCustomPathAsync(
        string toolchainId,
        CancellationToken cancellationToken = default)
    {
        IToolchainAdapter adapter = _registry.Get(toolchainId);
        await _settings.SetCustomPathAsync(adapter.Id, null, cancellationToken)
            .ConfigureAwait(false);
        return await adapter.DetectAsync(null, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessResult> ExecuteAsync(
        string toolchainId,
        ToolchainAction action,
        string workingDirectory,
        IReadOnlyList<string>? additionalArguments = null,
        CancellationToken cancellationToken = default)
    {
        IToolchainAdapter adapter = _registry.Get(toolchainId);
        ToolchainCapability requiredCapability = action switch
        {
            ToolchainAction.Run => ToolchainCapability.Run,
            ToolchainAction.Build => ToolchainCapability.Build,
            ToolchainAction.Format => ToolchainCapability.Format,
            ToolchainAction.Test => ToolchainCapability.Test,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        ToolchainDetectionResult detection = await DetectAsync(
            adapter.Id,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!detection.IsUsable || !detection.Capabilities.HasFlag(requiredCapability))
        {
            throw new InvalidOperationException(
                $"{adapter.DisplayName} cannot {action.ToString().ToLowerInvariant()}: {detection.Message}");
        }
        ProcessRequest request = adapter.CreateActionRequest(
            action,
            detection,
            Path.GetFullPath(workingDirectory),
            additionalArguments ?? []);
        return await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
