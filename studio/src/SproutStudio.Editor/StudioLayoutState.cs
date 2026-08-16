using System.Text.Json;

namespace SproutStudio.Editor;

public sealed record StudioLayoutState
{
    public int SchemaVersion { get; init; } = 5;
    public int? WindowX { get; init; }
    public int? WindowY { get; init; }
    public int? WindowWidth { get; init; }
    public int? WindowHeight { get; init; }
    public double ExplorerWidth { get; init; } = 260;
    public double InspectorWidth { get; init; } = 340;
    public double BottomPanelHeight { get; init; } = 220;
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
        "workspace",
        "search",
        "source-control",
        "academy",
        "documentation",
        "packages",
        "toolchains",
        "settings",
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
            if (state?.SchemaVersion != 5)
            {
                return new StudioLayoutState();
            }
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
        bool hasWindowBounds = state.WindowX.HasValue
            && state.WindowY.HasValue
            && state.WindowWidth.HasValue
            && state.WindowHeight.HasValue;
        return state with
        {
            SchemaVersion = 5,
            WindowX = hasWindowBounds ? Math.Clamp(state.WindowX!.Value, -100_000, 100_000) : null,
            WindowY = hasWindowBounds ? Math.Clamp(state.WindowY!.Value, -100_000, 100_000) : null,
            WindowWidth = hasWindowBounds ? Math.Clamp(state.WindowWidth!.Value, 900, 7_680) : null,
            WindowHeight = hasWindowBounds ? Math.Clamp(state.WindowHeight!.Value, 600, 4_320) : null,
            ExplorerWidth = Math.Clamp(state.ExplorerWidth, 220, 360),
            InspectorWidth = Math.Clamp(state.InspectorWidth, 280, 440),
            BottomPanelHeight = Math.Clamp(state.BottomPanelHeight, 120, 500),
            SelectedBottomTab = Math.Clamp(state.SelectedBottomTab, 0, 3),
            SelectedInspectorTab = Math.Clamp(state.SelectedInspectorTab, 0, 4),
            SelectedActivity = ActivityKeys.Contains(state.SelectedActivity)
                ? state.SelectedActivity
                : "explorer",
        };
    }
}
