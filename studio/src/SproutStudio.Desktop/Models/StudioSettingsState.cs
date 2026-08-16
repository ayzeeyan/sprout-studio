using System.Text.Json;

namespace SproutStudio.Models;

public sealed record StudioSettingsState(
    int SchemaVersion = 1,
    double EditorFontSize = 14,
    bool WordWrap = false,
    bool ShowWhitespace = false)
{
    public const int CurrentSchemaVersion = 1;

    public static StudioSettingsState Sanitize(StudioSettingsState? value)
    {
        if (value is null || value.SchemaVersion != CurrentSchemaVersion)
        {
            return new StudioSettingsState();
        }
        return value with { EditorFontSize = Math.Clamp(value.EditorFontSize, 11, 24) };
    }
}

public sealed class StudioSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public StudioSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SproutStudio",
            "settings.json");
    }

    public StudioSettingsState Load()
    {
        if (!File.Exists(_path))
        {
            return new StudioSettingsState();
        }
        try
        {
            return StudioSettingsState.Sanitize(
                JsonSerializer.Deserialize<StudioSettingsState>(File.ReadAllText(_path), JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new StudioSettingsState();
        }
    }

    public void Save(StudioSettingsState settings)
    {
        StudioSettingsState sanitized = StudioSettingsState.Sanitize(settings);
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string temporary = $"{_path}.{Environment.ProcessId}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(sanitized, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
