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

    public IReadOnlyList<ModuleDefinition> GetModules(string trackId)
    {
        TrackDefinition track = GetTrack(trackId);
        return track.Modules ?? [];
    }

    public CourseModel ToCourseModel(string trackId)
    {
        TrackDefinition track = GetTrack(trackId);
        var modules = (track.Modules ?? []).Select(m => new ModuleModel(m.Id, m.Title, m.Summary, m.LessonIds, m.Checkpoint)).ToArray();
        var allLessons = track.Lessons.Select(l => new LessonModel(
            l.Id,
            l.Title,
            l.Summary,
            l.Content,
            l.Xp,
            l.ConceptIds,
            l.Exercise is not null ? new ExerciseModel(
                l.Exercise.StarterCode,
                l.Exercise.SolutionCode,
                l.Exercise.ExpectedOutput,
                l.Exercise.Hints,
                l.Exercise.Assertions,
                l.Exercise.Title,
                l.Exercise.Objective,
                l.Exercise.Questions,
                l.Exercise.VerifyExpectedOutput,
                l.Exercise.EntryPoint,
                l.Exercise.TestHarness) : null,
            l.ModuleId,
            l.TaskType,
            l.EstimatedMinutes)).ToArray();

        return new CourseModel(
            track.Id,
            track.Title,
            track.Summary,
            track.Level ?? "Beginner to Advanced",
            track.Category ?? "Programming",
            track.RequiredToolchainId,
            modules,
            allLessons);
    }

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
                ValidateExercise(track, lesson);
            }

            if (track.Modules is { Length: > 0 } modules)
            {
                Dictionary<string, ModuleDefinition> moduleIds = UniqueById(modules, module => module.Id, "module");
                var assignedLessons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var checkpointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ModuleDefinition module in modules)
                {
                    if (string.IsNullOrWhiteSpace(module.Title)
                        || string.IsNullOrWhiteSpace(module.Summary)
                        || module.LessonIds.Length == 0
                        || module.LessonIds.Any(id => !track.Lessons.Any(lesson => string.Equals(lesson.Id, id, StringComparison.OrdinalIgnoreCase)))
                        || module.LessonIds.Any(id => !assignedLessons.Add(id)))
                    {
                        throw new InvalidDataException($"Academy module `{module.Id}` in `{track.Id}` is incomplete or has duplicate lessons.");
                    }
                    if (module.Checkpoint is { } checkpoint)
                    {
                        if (!checkpointIds.Add(checkpoint.Id)
                            || !string.Equals(checkpoint.ModuleId, module.Id, StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrWhiteSpace(checkpoint.Title)
                            || checkpoint.Questions.Length == 0)
                        {
                            throw new InvalidDataException($"Academy checkpoint `{checkpoint.Id}` is incomplete.");
                        }
                        ValidateQuestions(checkpoint.Id, checkpoint.Questions);
                    }
                }
                if (track.Lessons.Any(lesson => !assignedLessons.Contains(lesson.Id)
                    || !moduleIds.ContainsKey(lesson.ModuleId ?? string.Empty)))
                {
                    throw new InvalidDataException($"Every lesson in modular track `{track.Id}` must belong to exactly one valid module.");
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

    private static void ValidateExercise(TrackDefinition track, LessonDefinition lesson)
    {
        if (lesson.Exercise is not { } exercise)
        {
            return;
        }
        if (exercise.Assertions.Any(string.IsNullOrWhiteSpace)
            || exercise.Hints.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException($"Academy exercise `{lesson.Id}` contains blank requirements or hints.");
        }
        if (exercise.Questions is { Length: > 0 } questions)
        {
            ValidateQuestions(lesson.Id, questions);
        }
        if (string.Equals(track.Id, "java-oop-zero-to-hero", StringComparison.OrdinalIgnoreCase)
            && (exercise.Hints.Length != 4
                || string.Equals(exercise.StarterCode, exercise.SolutionCode, StringComparison.Ordinal)
                || exercise.Assertions.Length == 0
                || string.IsNullOrWhiteSpace(exercise.TestHarness)))
        {
            throw new InvalidDataException(
                $"Java Zero-to-Hero exercise `{lesson.Id}` must have incomplete starter code, source requirements, behavioral tests, and four progressive hints.");
        }
    }

    private static void ValidateQuestions(string ownerId, QuizQuestion[] questions)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (QuizQuestion question in questions)
        {
            if (!ids.Add(question.Id)
                || string.IsNullOrWhiteSpace(question.Question)
                || string.IsNullOrWhiteSpace(question.Explanation)
                || question.CorrectAnswers.Length == 0
                || question.CorrectAnswers.Any(string.IsNullOrWhiteSpace)
                || (question.Options.Length > 0
                    && question.CorrectAnswers.Any(answer => !question.Options.Contains(answer, StringComparer.OrdinalIgnoreCase))))
            {
                throw new InvalidDataException($"Academy quiz question `{question.Id}` in `{ownerId}` is incomplete.");
            }
        }
    }

    private sealed record AcademyCatalogDocument(
        TrackDefinition[] Tracks,
        ConceptDefinition[] Concepts,
        AchievementDefinition[] Achievements);
}
