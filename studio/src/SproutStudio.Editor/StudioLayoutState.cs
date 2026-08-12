using System.Text.Json;

namespace SproutStudio.Editor;

public sealed record StudioLayoutState
{
    public int SchemaVersion { get; init; } = 1;
    public double ExplorerWidth { get; init; } = 280;
    public double InspectorWidth { get; init; } = 350;
    public double BottomPanelHeight { get; init; } = 190;
    public bool ActivityVisible { get; init; } = true;
    public bool ExplorerVisible { get; init; } = true;
    public bool InspectorVisible { get; init; } = true;
    public bool BottomPanelVisible { get; init; } = true;
    public int SelectedBottomTab { get; init; }
    public int SelectedInspectorTab { get; init; }
    public string SelectedActivity { get; init; } = "explorer";
}

public sealed class StudioLayoutStateStore
{
    private static readonly HashSet<string> ActivityKeys = new(StringComparer.Ordinal)
    {
        "explorer",
        "search",
        "source-control",
        "academy",
        "documentation",
        "packages",
        "toolchains",
    };

    private readonly string _path;

    public StudioLayoutStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SproutStudio",
            "layout.json");
    }

    public StudioLayoutState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new StudioLayoutState();
            }

            StudioLayoutState? state = JsonSerializer.Deserialize<StudioLayoutState>(
                File.ReadAllText(_path));
            return Sanitize(state ?? new StudioLayoutState());
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            return new StudioLayoutState();
        }
    }

    public void Save(StudioLayoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(Sanitize(state), new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Layout persistence must never prevent the IDE from closing.
        }
    }

    public static StudioLayoutState Sanitize(StudioLayoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state with
        {
            SchemaVersion = 1,
            ExplorerWidth = Math.Clamp(state.ExplorerWidth, 200, 420),
            InspectorWidth = Math.Clamp(state.InspectorWidth, 260, 480),
            BottomPanelHeight = Math.Clamp(state.BottomPanelHeight, 120, 500),
            SelectedBottomTab = Math.Clamp(state.SelectedBottomTab, 0, 2),
            SelectedInspectorTab = Math.Clamp(state.SelectedInspectorTab, 0, 3),
            SelectedActivity = ActivityKeys.Contains(state.SelectedActivity)
                ? state.SelectedActivity
                : "explorer",
        };
    }
}
