using System.Reflection;
using System.Text.Json;

namespace SproutStudio.Academy;

public sealed class AcademyCatalog
{
    private const string ResourceName = "SproutStudio.Academy.Content.catalog.json";
    private readonly Dictionary<string, TrackDefinition> _tracks;
    private readonly Dictionary<string, LessonDefinition> _lessons;
    private readonly Dictionary<string, ConceptDefinition> _concepts;
    private readonly Dictionary<string, AchievementDefinition> _achievements;

    private AcademyCatalog(AcademyCatalogDocument document)
    {
        Tracks = document.Tracks;
        Concepts = document.Concepts;
        Achievements = document.Achievements;
        _tracks = UniqueById(Tracks, item => item.Id, "track");
        _lessons = UniqueById(Tracks.SelectMany(item => item.Lessons), item => item.Id, "lesson");
        _concepts = UniqueById(Concepts, item => item.Id, "concept");
        _achievements = UniqueById(Achievements, item => item.Id, "achievement");
        Validate();
    }

    public TrackDefinition[] Tracks { get; }

    public ConceptDefinition[] Concepts { get; }

    public AchievementDefinition[] Achievements { get; }

    public static AcademyCatalog LoadDefault()
    {
        Assembly assembly = typeof(AcademyCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"Embedded Academy catalog `{ResourceName}` is missing.");
        AcademyCatalogDocument document;
        try
        {
            document = JsonSerializer.Deserialize<AcademyCatalogDocument>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("The embedded Academy catalog is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The embedded Academy catalog is malformed.", exception);
        }
        return new AcademyCatalog(document);
    }

    public TrackDefinition GetTrack(string id) =>
        _tracks.TryGetValue(id, out TrackDefinition? track)
            ? track
            : throw new KeyNotFoundException($"Unknown Academy track `{id}`.");

    public LessonDefinition GetLesson(string id) =>
        _lessons.TryGetValue(id, out LessonDefinition? lesson)
            ? lesson
            : throw new KeyNotFoundException($"Unknown Academy lesson `{id}`.");

    public ConceptDefinition GetConcept(string id) =>
        _concepts.TryGetValue(id, out ConceptDefinition? concept)
            ? concept
            : throw new KeyNotFoundException($"Unknown Academy concept `{id}`.");

    public AchievementDefinition GetAchievement(string id) =>
        _achievements.TryGetValue(id, out AchievementDefinition? achievement)
            ? achievement
            : throw new KeyNotFoundException($"Unknown Academy achievement `{id}`.");

    public IReadOnlyList<ConceptDefinition> SearchConcepts(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Concepts;
        }
        string needle = query.Trim();
        return Concepts
            .Where(concept => concept.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || concept.Summary.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || concept.Examples.Any(example => example.Language.Contains(
                    needle,
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static Dictionary<string, T> UniqueById<T>(
        IEnumerable<T> items,
        Func<T, string> idSelector,
        string kind)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (T item in items)
        {
            string id = idSelector(item);
            if (string.IsNullOrWhiteSpace(id) || !result.TryAdd(id, item))
            {
                throw new InvalidDataException($"Academy catalog contains an invalid or duplicate {kind} id `{id}`.");
            }
        }
        return result;
    }

    private void Validate()
    {
        if (Tracks.Length == 0 || Concepts.Length == 0 || Achievements.Length == 0)
        {
            throw new InvalidDataException("Academy catalog must contain tracks, concepts, and achievements.");
        }
        foreach (TrackDefinition track in Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.Title)
                || string.IsNullOrWhiteSpace(track.Summary)
                || track.Lessons.Length == 0
                || (track.RequiredToolchainId is not null
                    && track.RequiredToolchainId is not ("rust" or "go" or "python" or "java")))
            {
                throw new InvalidDataException($"Academy track `{track.Id}` is incomplete.");
            }
            foreach (LessonDefinition lesson in track.Lessons)
            {
                if (string.IsNullOrWhiteSpace(lesson.Title)
                    || string.IsNullOrWhiteSpace(lesson.Summary)
                    || string.IsNullOrWhiteSpace(lesson.Content)
                    || lesson.Xp <= 0
                    || lesson.ConceptIds.Any(id => !_concepts.ContainsKey(id)))
                {
                    throw new InvalidDataException($"Academy lesson `{lesson.Id}` is incomplete.");
                }
            }
        }
        foreach (ConceptDefinition concept in Concepts)
        {
            if (string.IsNullOrWhiteSpace(concept.Title)
                || string.IsNullOrWhiteSpace(concept.Summary)
                || concept.Examples.Length < 2
                || concept.RelatedConceptIds.Any(id => !_concepts.ContainsKey(id)))
            {
                throw new InvalidDataException($"Academy concept `{concept.Id}` is incomplete.");
            }
        }
    }

    private sealed record AcademyCatalogDocument(
        TrackDefinition[] Tracks,
        ConceptDefinition[] Concepts,
        AchievementDefinition[] Achievements);
}
