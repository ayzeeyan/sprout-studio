using System.Text.Json;
using System.Text.Json.Serialization;

namespace SproutStudio.Academy;

public sealed class AcademyStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AcademyStateStore(string? path = null)
    {
        _path = System.IO.Path.GetFullPath(path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SproutStudio",
            "academy.json"));
    }

    public string Path => _path;

    public async Task<AcademyState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return AcademyState.Empty;
            }
            try
            {
                string json = await File.ReadAllTextAsync(_path, cancellationToken)
                    .ConfigureAwait(false);
                AcademyState state = JsonSerializer.Deserialize<AcademyState>(json, SerializerOptions)
                    ?? throw new InvalidDataException("The local Academy state is empty.");
                if (state.SchemaVersion != AcademyState.CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"Academy state schema {state.SchemaVersion} is not supported.");
                }
                return state;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The local Academy state is malformed.", exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        AcademyState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != AcademyState.CurrentSchemaVersion)
        {
            throw new ArgumentException("Academy state schema is invalid.", nameof(state));
        }
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;
        try
        {
            string directory = System.IO.Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("Academy state path has no parent directory.");
            Directory.CreateDirectory(directory);
            temporaryPath = $"{_path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            string json = JsonSerializer.Serialize(state, SerializerOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, _path, overwrite: true);
            temporaryPath = null;
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            _gate.Release();
        }
    }
}
