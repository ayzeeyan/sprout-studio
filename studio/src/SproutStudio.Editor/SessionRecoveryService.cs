using System.Text.Json;

namespace SproutStudio.Editor;

public sealed record DocumentRecoverySnapshot(
    string? FilePath,
    string Title,
    string Content,
    int CaretOffset,
    bool IsDirty,
    DateTimeOffset SavedAt);

public sealed record SessionRecoveryState(
    int SchemaVersion,
    DateTimeOffset LastHeartbeat,
    bool CleanShutdown,
    string? ActiveProjectPath,
    int SelectedDocumentIndex,
    IReadOnlyList<DocumentRecoverySnapshot> Documents)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed class SessionRecoveryService
{
    private readonly string _recoveryFilePath;
    private readonly object _syncRoot = new();

    public SessionRecoveryService(string? customPath = null)
    {
        _recoveryFilePath = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SproutStudio",
            "recovery_session.json");
    }

    public string RecoveryFilePath => _recoveryFilePath;

    public async Task SaveHeartbeatAsync(
        string? activeProjectPath,
        int selectedIndex,
        IReadOnlyList<DocumentRecoverySnapshot> documents,
        CancellationToken cancellationToken = default)
    {
        var state = new SessionRecoveryState(
            SessionRecoveryState.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            CleanShutdown: false,
            activeProjectPath,
            selectedIndex,
            documents);

        string dir = Path.GetDirectoryName(_recoveryFilePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempFile = $"{_recoveryFilePath}.tmp";
        string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(tempFile, json, cancellationToken).ConfigureAwait(false);
        lock (_syncRoot)
        {
            if (File.Exists(_recoveryFilePath))
            {
                File.Delete(_recoveryFilePath);
            }
            File.Move(tempFile, _recoveryFilePath);
        }
    }

    public async Task MarkCleanShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_recoveryFilePath))
        {
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_recoveryFilePath, cancellationToken).ConfigureAwait(false);
            SessionRecoveryState? state = JsonSerializer.Deserialize<SessionRecoveryState>(json);
            if (state is not null)
            {
                var clean = state with { CleanShutdown = true, LastHeartbeat = DateTimeOffset.UtcNow };
                string updated = JsonSerializer.Serialize(clean, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_recoveryFilePath, updated, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore clean shutdown recording errors during application close
        }
    }

    public async Task<SessionRecoveryState?> CheckForRecoverableSessionAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_recoveryFilePath))
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_recoveryFilePath, cancellationToken).ConfigureAwait(false);
            SessionRecoveryState? state = JsonSerializer.Deserialize<SessionRecoveryState>(json);

            // Recoverable if previous session did not close cleanly and contains dirty or open documents
            if (state is not null && !state.CleanShutdown && state.Documents.Count > 0)
            {
                return state;
            }
        }
        catch
        {
            // Corrupt recovery files are ignored safely
        }
        return null;
    }

    public void ClearRecovery()
    {
        lock (_syncRoot)
        {
            if (File.Exists(_recoveryFilePath))
            {
                try
                {
                    File.Delete(_recoveryFilePath);
                }
                catch
                {
                }
            }
        }
    }
}
