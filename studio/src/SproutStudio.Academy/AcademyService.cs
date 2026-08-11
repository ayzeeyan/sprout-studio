using SproutStudio.Toolchains;

namespace SproutStudio.Academy;

public sealed class AcademyService
{
    private readonly AcademyStateStore _store;
    private readonly ToolchainManager _toolchains;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AcademyService(
        AcademyCatalog catalog,
        AcademyStateStore store,
        ToolchainManager toolchains)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _toolchains = toolchains ?? throw new ArgumentNullException(nameof(toolchains));
        Recommendations = new RecommendationEngine(Catalog);
    }

    public AcademyCatalog Catalog { get; }

    public RecommendationEngine Recommendations { get; }

    public static AcademyService CreateDefault(ToolchainManager toolchains, string? statePath = null) =>
        new(AcademyCatalog.LoadDefault(), new AcademyStateStore(statePath), toolchains);

    public Task<AcademyState> LoadStateAsync(CancellationToken cancellationToken = default) =>
        _store.LoadAsync(cancellationToken);

    public async Task<AcademyState> SaveProfileAsync(
        AcademyProfile profile,
        CancellationToken cancellationToken = default)
    {
        AcademyProfile normalized = AcademyProfileValidator.ValidateAndNormalize(profile);
        return await ModifyAsync(
            state => state with { Profile = normalized },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<EnrollmentResult> EnrollAsync(
        string trackId,
        CancellationToken cancellationToken = default)
    {
        TrackDefinition track = Catalog.GetTrack(trackId);
        ToolchainDetectionResult? detection = null;
        if (track.RequiredToolchainId is not null)
        {
            detection = await _toolchains.DetectAsync(
                track.RequiredToolchainId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!detection.IsUsable)
            {
                return new EnrollmentResult(
                    EnrollmentStatus.ToolchainRequired,
                    track,
                    detection);
            }
        }

        bool alreadyEnrolled = false;
        await ModifyAsync(
            state =>
            {
                alreadyEnrolled = state.EnrolledTrackIds.Contains(
                    track.Id,
                    StringComparer.OrdinalIgnoreCase);
                if (alreadyEnrolled)
                {
                    return state;
                }
                AcademyState enrolled = state with
                {
                    EnrolledTrackIds = state.EnrolledTrackIds
                        .Append(track.Id)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                };
                return AwardAchievements(enrolled).State;
            },
            cancellationToken).ConfigureAwait(false);
        return new EnrollmentResult(
            alreadyEnrolled ? EnrollmentStatus.AlreadyEnrolled : EnrollmentStatus.Enrolled,
            track,
            detection);
    }

    public async Task<LessonCompletionResult> CompleteLessonAsync(
        string trackId,
        string lessonId,
        CancellationToken cancellationToken = default)
    {
        TrackDefinition track = Catalog.GetTrack(trackId);
        LessonDefinition lesson = track.Lessons.SingleOrDefault(item =>
                string.Equals(item.Id, lessonId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Lesson `{lessonId}` does not belong to track `{trackId}`.",
                nameof(lessonId));
        bool alreadyCompleted = false;
        AchievementDefinition[] newlyEarned = [];
        await ModifyAsync(
            state =>
            {
                if (!state.EnrolledTrackIds.Contains(track.Id, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Enroll in {track.Title} before completing its lessons.");
                }
                alreadyCompleted = state.CompletedLessonIds.Contains(
                    lesson.Id,
                    StringComparer.OrdinalIgnoreCase);
                if (alreadyCompleted)
                {
                    return state;
                }
                AcademyState completed = state with
                {
                    CompletedLessonIds = state.CompletedLessonIds
                        .Append(lesson.Id)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                };
                (AcademyState awarded, AchievementDefinition[] earned) = AwardAchievements(completed);
                newlyEarned = earned;
                return awarded;
            },
            cancellationToken).ConfigureAwait(false);
        return new LessonCompletionResult(
            lesson,
            alreadyCompleted,
            alreadyCompleted ? 0 : lesson.Xp,
            newlyEarned);
    }

    public AcademyDashboard CreateDashboard(AcademyState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var completed = state.CompletedLessonIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        TrackProgress[] tracks = Catalog.Tracks
            .Select(track =>
            {
                int count = track.Lessons.Count(lesson => completed.Contains(lesson.Id));
                return new TrackProgress(
                    track,
                    state.EnrolledTrackIds.Contains(track.Id, StringComparer.OrdinalIgnoreCase),
                    count,
                    track.Lessons.Length,
                    count * 100 / track.Lessons.Length);
            })
            .ToArray();
        int xp = Catalog.Tracks
            .SelectMany(track => track.Lessons)
            .Where(lesson => completed.Contains(lesson.Id))
            .Sum(lesson => lesson.Xp);
        AchievementDefinition[] achievements = state.AchievementAwards
            .Select(award => Catalog.GetAchievement(award.Id))
            .ToArray();
        return new AcademyDashboard(state, tracks, xp, 1 + (xp / 100), xp % 100, achievements);
    }

    private async Task<AcademyState> ModifyAsync(
        Func<AcademyState, AcademyState> change,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AcademyState current = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            AcademyState updated = change(current);
            if (!ReferenceEquals(current, updated))
            {
                await _store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            }
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private (AcademyState State, AchievementDefinition[] NewAchievements) AwardAchievements(
        AcademyState state)
    {
        var earnedIds = state.AchievementAwards
            .Select(award => award.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eligible = new List<string>();
        if (state.EnrolledTrackIds.Length > 0)
        {
            eligible.Add("seedling-planted");
        }
        if (state.CompletedLessonIds.Length > 0)
        {
            eligible.Add("first-execution");
        }
        if (TrackCompleted("sprout-fundamentals"))
        {
            eligible.Add("syntax-adapter");
        }
        if (CompletedAnyTrackLesson("python-automation"))
        {
            eligible.Add("python-pioneer");
        }
        if (Catalog.Tracks.Count(TrackHasCompletedLesson) >= 2)
        {
            eligible.Add("polyglot-explorer");
        }
        if (Catalog.Tracks.Any(track => TrackCompleted(track.Id)))
        {
            eligible.Add("deep-roots");
        }

        AchievementDefinition[] newlyEarned = eligible
            .Where(earnedIds.Add)
            .Select(Catalog.GetAchievement)
            .ToArray();
        if (newlyEarned.Length == 0)
        {
            return (state, []);
        }
        DateTimeOffset earnedAt = DateTimeOffset.UtcNow;
        AcademyState updated = state with
        {
            AchievementAwards = state.AchievementAwards
                .Concat(newlyEarned.Select(item => new AchievementAward(item.Id, earnedAt)))
                .ToArray(),
        };
        return (updated, newlyEarned);

        bool TrackHasCompletedLesson(TrackDefinition track) =>
            CompletedAnyTrackLesson(track.Id);

        bool CompletedAnyTrackLesson(string id)
        {
            TrackDefinition track = Catalog.GetTrack(id);
            return track.Lessons.Any(lesson => state.CompletedLessonIds.Contains(
                lesson.Id,
                StringComparer.OrdinalIgnoreCase));
        }

        bool TrackCompleted(string id)
        {
            TrackDefinition track = Catalog.GetTrack(id);
            return track.Lessons.All(lesson => state.CompletedLessonIds.Contains(
                lesson.Id,
                StringComparer.OrdinalIgnoreCase));
        }
    }
}
