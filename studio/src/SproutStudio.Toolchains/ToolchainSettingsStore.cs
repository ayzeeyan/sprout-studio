using System.Text.Json;

namespace SproutStudio.Toolchains;

public sealed class ToolchainSettingsStore
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ToolchainSettingsStore(string? settingsPath = null)
    {
        _settingsPath = Path.GetFullPath(settingsPath ?? DefaultSettingsPath());
    }

    public string SettingsPath => _settingsPath;

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ToolchainSettingsDocument document = await LoadDocumentAsync(cancellationToken)
                .ConfigureAwait(false);
            return new Dictionary<string, string>(
                document.CustomPaths,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetCustomPathAsync(
        string toolchainId,
        string? path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolchainId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ToolchainSettingsDocument document = await LoadDocumentAsync(cancellationToken)
                .ConfigureAwait(false);
            if (path is null)
            {
                document.CustomPaths.Remove(toolchainId);
            }
            else
            {
                document.CustomPaths[toolchainId] = Path.GetFullPath(path);
            }
            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ToolchainSettingsDocument> LoadDocumentAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return new ToolchainSettingsDocument();
        }
        string json = await File.ReadAllTextAsync(_settingsPath, cancellationToken)
            .ConfigureAwait(false);
        ToolchainSettingsDocument document;
        try
        {
            document = JsonSerializer.Deserialize<ToolchainSettingsDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("Toolchain settings were empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Toolchain settings contain invalid JSON.", exception);
        }
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported toolchain settings schema {document.SchemaVersion}.");
        }
        document.CustomPaths = new Dictionary<string, string>(
            document.CustomPaths,
            StringComparer.OrdinalIgnoreCase);
        return document;
    }

    private async Task SaveDocumentAsync(
        ToolchainSettingsDocument document,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (directory is null)
        {
            throw new InvalidOperationException("Toolchain settings path has no parent directory.");
        }
        Directory.CreateDirectory(directory);
        string temporaryPath = _settingsPath + $".{Environment.ProcessId}.tmp";
        try
        {
            string json = JsonSerializer.Serialize(document, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string DefaultSettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SproutStudio",
        "toolchains.json");

    private sealed class ToolchainSettingsDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public Dictionary<string, string> CustomPaths { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
