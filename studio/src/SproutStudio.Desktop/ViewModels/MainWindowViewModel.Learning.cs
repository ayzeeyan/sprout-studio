using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using SproutStudio.Academy;
using SproutStudio.Editor;
using SproutStudio.Services;

namespace SproutStudio.ViewModels;

/// <summary>
/// The "Academy IDE" layer: learning habits (streaks and daily goal) surfaced across the shell,
/// the Learn inspector that explains the concepts used in the open file, and celebration toasts
/// raised when the dashboard reports progress.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private const int MaxToasts = 3;
    private CancellationTokenSource? _learnConceptsCancellation;

    /// <summary>Raised to ask the Academy view to focus a concept in the Concept Browser.</summary>
    public string? SelectedAcademyConceptId { get; internal set; }

    /// <summary>Asks the Academy view to open a Handbook guide.</summary>
    public string? SelectedAcademyGuideId { get; internal set; }

    public IRelayCommand<string> OpenGuideCommand { get; private set; } = null!;

    public IRelayCommand ShowHandbookCommand { get; private set; } = null!;

    public bool IsAcademyHandbookSection => string.Equals(SelectedAcademySection, "handbook", StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<StudioToastViewModel> Toasts { get; } = [];

    public ObservableCollection<LearnConceptViewModel> LearnConcepts { get; } = [];

    public IRelayCommand ShowLearningProgressCommand { get; private set; } = null!;

    public IRelayCommand<string> OpenConceptCommand { get; private set; } = null!;

    public IAsyncRelayCommand<LearnConceptViewModel> PracticeConceptCommand { get; private set; } = null!;

    public IRelayCommand<StudioToastViewModel> DismissToastCommand { get; private set; } = null!;

    public LearningHabitSummary Habits => _academyDashboard?.HabitSummary ?? LearningHabitSummary.Empty;

    /// <summary>The learner can hide levels, XP, streaks, and achievements in their Academy profile.</summary>
    public bool ShowGamification => _academyDashboard?.State.Profile?.ShowGamification ?? true;

    public bool HasStreak => Habits.CurrentStreak > 0;

    public string StreakText => Habits.CurrentStreak == 1 ? "1 day" : $"{Habits.CurrentStreak} days";

    public string StreakTooltip => Habits.CurrentStreak == 0
        ? "Start a learning streak: submit any Academy exercise today."
        : Habits.PracticedToday
            ? $"{StreakText} learning streak — you've practiced today. Longest: {Habits.LongestStreak} days."
            : $"{StreakText} learning streak — practice today to keep it alive. Longest: {Habits.LongestStreak} days.";

    public int DailyGoalPercent => Habits.DailyGoalPercent;

    /// <summary>Sweep angle (degrees) for the daily-goal progress ring.</summary>
    public double DailyGoalSweep => Math.Max(0.01, Habits.DailyGoalPercent * 3.6);

    public bool DailyGoalMet => Habits.DailyGoalMet;

    public string DailyGoalText => $"{Habits.XpEarnedToday} / {Habits.DailyXpGoal} XP today";

    public string LevelBadgeText => $"Lv {_academyDashboard?.Level ?? 1}";

    public IReadOnlyList<LearningActivityDay> ActivityCalendar => Habits.Calendar;

    public bool HasContinueLesson => CurrentAcademyNextTask is not null;

    /// <summary>The first unread Handbook guide behind the next lesson, if any.</summary>
    public GuideDefinition? NextLessonGuide
    {
        get
        {
            if (CurrentAcademyNextTask is not { } task)
            {
                return null;
            }
            LessonDefinition? lesson = _academy.Catalog.Tracks
                .SelectMany(track => track.Lessons)
                .FirstOrDefault(candidate => string.Equals(candidate.Id, task.LessonId, StringComparison.OrdinalIgnoreCase));
            AcademyState state = _academyDashboard?.State ?? AcademyState.Empty;
            return lesson is null
                ? null
                : _academy.Catalog.GuidesForConcepts(lesson.ConceptIds).FirstOrDefault(guide => !state.HasReadGuide(guide.Id));
        }
    }

    public bool HasNextLessonGuide => NextLessonGuide is not null;

    public string NextLessonGuideText => NextLessonGuide is { } guide
        ? $"Read first: {guide.Title} · {guide.Minutes} min"
        : string.Empty;

    public string? NextLessonGuideId => NextLessonGuide?.Id;

    public string ContinueLessonTrack => CurrentAcademyTrack?.Track.Title ?? "Sprout Academy";

    public bool HasLearnConcepts => LearnConcepts.Count > 0;

    public bool NoLearnConcepts => LearnConcepts.Count == 0;

    public string LearnHeaderText => SelectedDocument is { } document
        ? LearnConcepts.Count == 0
            ? ConceptDetector.Supports(document.LanguageKey)
                ? "No concepts detected yet. Start writing and the ideas you use will appear here."
                : $"Concept explanations are available for Sprout, Python, Java, Rust, and Go. {document.LanguageName} files are not analyzed."
            : $"{LearnConcepts.Count} concept{(LearnConcepts.Count == 1 ? string.Empty : "s")} used in {document.DisplayName}"
        : "Open a file to see the programming concepts it uses.";

    private void InitializeLearningCommands()
    {
        ShowLearningProgressCommand = new RelayCommand(() => ShowAcademySection("profile"));
        OpenConceptCommand = new RelayCommand<string>(OpenConcept);
        OpenGuideCommand = new RelayCommand<string>(OpenGuide);
        ShowHandbookCommand = new RelayCommand(() => ShowAcademySection("handbook"));
        PracticeConceptCommand = new AsyncRelayCommand<LearnConceptViewModel>(PracticeConceptAsync);
        DismissToastCommand = new RelayCommand<StudioToastViewModel>(toast =>
        {
            if (toast is not null)
            {
                Toasts.Remove(toast);
            }
        });
    }

    /// <summary>Shows a transient notification in the bottom-right corner of the window.</summary>
    public void ShowToast(string title, string message, string glyph = "\uE73E", StudioToastKind kind = StudioToastKind.Info)
    {
        var toast = new StudioToastViewModel(title, message, glyph, kind);
        Toasts.Add(toast);
        while (Toasts.Count > MaxToasts)
        {
            Toasts.RemoveAt(0);
        }
        _ = DismissLaterAsync(toast, kind == StudioToastKind.Celebration ? 7 : 5);
    }

    private async Task DismissLaterAsync(StudioToastViewModel toast, int seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        Toasts.Remove(toast);
    }

    private void NotifyLearningProperties()
    {
        OnPropertyChanged(nameof(Habits));
        OnPropertyChanged(nameof(ShowGamification));
        OnPropertyChanged(nameof(HasStreak));
        OnPropertyChanged(nameof(StreakText));
        OnPropertyChanged(nameof(StreakTooltip));
        OnPropertyChanged(nameof(DailyGoalPercent));
        OnPropertyChanged(nameof(DailyGoalSweep));
        OnPropertyChanged(nameof(DailyGoalMet));
        OnPropertyChanged(nameof(DailyGoalText));
        OnPropertyChanged(nameof(LevelBadgeText));
        OnPropertyChanged(nameof(ActivityCalendar));
        OnPropertyChanged(nameof(HasContinueLesson));
        OnPropertyChanged(nameof(NextLessonGuide));
        OnPropertyChanged(nameof(HasNextLessonGuide));
        OnPropertyChanged(nameof(NextLessonGuideText));
        OnPropertyChanged(nameof(NextLessonGuideId));
        OnPropertyChanged(nameof(ContinueLessonTrack));
    }

    /// <summary>Compares two dashboards and celebrates what changed (lessons, levels, goals, badges).</summary>
    private void CelebrateProgress(AcademyDashboard? previous, AcademyDashboard current)
    {
        if (previous is null)
        {
            return;
        }
        bool gamified = current.State.Profile?.ShowGamification ?? true;

        int newLessons = current.State.CompletedLessonIds.Length - previous.State.CompletedLessonIds.Length;
        int gainedXp = current.TotalXp - previous.TotalXp;
        if (newLessons > 0 && !gamified)
        {
            ShowToast("Lesson complete", "Nice work. Your next lesson is ready when you are.", "\uE73E", StudioToastKind.Success);
            return;
        }
        if (!gamified)
        {
            return;
        }
        if (newLessons > 0 && gainedXp > 0)
        {
            ShowToast("Lesson complete", $"+{gainedXp} XP · {current.XpTowardNextLevel}/{AcademyDashboard.XpPerLevel} toward level {current.Level + 1}", "\uE73E", StudioToastKind.Success);
        }
        if (current.Level > previous.Level)
        {
            ShowToast($"Level {current.Level}!", "You leveled up. Keep growing.", "\uE734", StudioToastKind.Celebration);
        }

        var known = previous.State.AchievementAwards.Select(award => award.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (AchievementAward award in current.State.AchievementAwards.Where(award => !known.Contains(award.Id)))
        {
            AchievementDefinition definition = _academy.Catalog.GetAchievement(award.Id);
            ShowToast($"Achievement: {definition.Title}", definition.Description, "\uE735", StudioToastKind.Celebration);
        }

        if (current.HabitSummary.DailyGoalMet && !previous.HabitSummary.DailyGoalMet)
        {
            ShowToast("Daily goal reached", $"{current.HabitSummary.XpEarnedToday} XP today · {current.HabitSummary.CurrentStreak}-day streak", "\uE945", StudioToastKind.Celebration);
        }
        else if (current.HabitSummary.CurrentStreak > previous.HabitSummary.CurrentStreak && current.HabitSummary.CurrentStreak > 1)
        {
            ShowToast("Streak extended", $"{current.HabitSummary.CurrentStreak} days in a row. Nice consistency!", "\uE945", StudioToastKind.Success);
        }
    }

    /// <summary>Re-detects concepts shortly after edits settle.</summary>
    private void ScheduleLearnConcepts(bool immediate = false)
    {
        _learnConceptsCancellation?.Cancel();
        _learnConceptsCancellation?.Dispose();
        _learnConceptsCancellation = new CancellationTokenSource();
        _ = RefreshLearnConceptsAsync(immediate ? TimeSpan.Zero : TimeSpan.FromMilliseconds(450), _learnConceptsCancellation.Token);
    }

    private async Task RefreshLearnConceptsAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
            DocumentTabViewModel? document = SelectedDocument;
            IReadOnlyList<ConceptMatch> matches = document is { IsSource: true }
                ? ConceptDetector.Detect(document.Source, document.LanguageKey)
                : [];
            AcademyState state = _academyDashboard?.State ?? AcademyState.Empty;
            LearnConcepts.Clear();
            foreach (ConceptMatch match in matches)
            {
                if (_academy.Catalog.Concepts.FirstOrDefault(concept => concept.Id == match.ConceptId) is not { } concept)
                {
                    continue;
                }
                ConceptLearningProgress progress = _academy.GetConceptProgress(concept.Id, state, document?.LanguageKey);
                GuideDefinition? guide = _academy.Catalog.GuidesForConcepts([concept.Id]).FirstOrDefault();
                LearnConcepts.Add(new LearnConceptViewModel(concept, match, progress, document?.LanguageName ?? "Sprout", guide)
                {
                    LanguageKey = document?.LanguageKey ?? "sprout",
                });
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            StudioLog.Warning("Could not analyze concepts for the Learn panel.", exception);
            LearnConcepts.Clear();
        }
        OnPropertyChanged(nameof(HasLearnConcepts));
        OnPropertyChanged(nameof(NoLearnConcepts));
        OnPropertyChanged(nameof(LearnHeaderText));
    }

    private void OpenConcept(string? conceptId)
    {
        if (string.IsNullOrWhiteSpace(conceptId))
        {
            return;
        }
        SelectedAcademyConceptId = conceptId;
        OnPropertyChanged(nameof(SelectedAcademyConceptId));
        ShowAcademySection("concepts");
    }

    /// <summary>Opens a Handbook guide in the Academy.</summary>
    public void OpenGuide(string? guideId)
    {
        if (string.IsNullOrWhiteSpace(guideId))
        {
            return;
        }
        SelectedAcademyGuideId = guideId;
        ShowAcademySection("handbook");
    }

    /// <summary>Academy entries for the command palette, including one per Handbook guide.</summary>
    private IEnumerable<Models.CommandPaletteItem> AcademyPaletteCommands()
    {
        yield return new("academy.continue", "Academy: Continue Lesson", "Academy");
        yield return new("academy.handbook", "Academy: Open Handbook", "Academy");
        yield return new("academy.progress", "Academy: My Progress", "Academy");
        foreach (GuideDefinition guide in _academy.Catalog.Guides)
        {
            yield return new($"guide.{guide.Id}", $"Read: {guide.Title}", "Handbook", $"{guide.Minutes} min");
        }
    }

    private Task RunGuideAsync(string guideId)
    {
        OpenGuide(guideId);
        return Task.CompletedTask;
    }

    private async Task PracticeConceptAsync(LearnConceptViewModel? concept)
    {
        if (concept?.Progress is { SuggestedTrack: { } track, SuggestedLesson: { } lesson })
        {
            await OpenAcademyPracticeAsync(track, lesson);
        }
        else if (concept is not null)
        {
            OpenConcept(concept.ConceptId);
        }
    }
}

public enum StudioToastKind
{
    Info,
    Success,
    Celebration,
    Warning,
}

public sealed class StudioToastViewModel(string title, string message, string glyph, StudioToastKind kind)
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public string Glyph { get; } = glyph;

    public StudioToastKind Kind { get; } = kind;

    public bool IsCelebration => Kind == StudioToastKind.Celebration;

    public bool IsSuccess => Kind == StudioToastKind.Success;

    public bool IsWarning => Kind == StudioToastKind.Warning;
}

public sealed class LearnConceptViewModel
{
    public LearnConceptViewModel(ConceptDefinition concept, ConceptMatch match, ConceptLearningProgress progress, string languageName, GuideDefinition? guide = null)
    {
        Guide = guide;
        Concept = concept;
        Match = match;
        Progress = progress;
        // Show how the same idea looks elsewhere: up to two examples in other languages.
        Examples = concept.Examples
            .Where(example => !string.Equals(example.Language, languageName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .Select(example => new LearnExampleViewModel(example.Language, example.Code))
            .ToArray();
    }

    public ConceptDefinition Concept { get; }

    /// <summary>The Handbook guide that explains this concept, if any.</summary>
    public GuideDefinition? Guide { get; }

    public bool HasGuide => Guide is not null;

    public string? GuideId => Guide?.Id;

    public string GuideTooltip => Guide is null ? string.Empty : $"Read \"{Guide.Title}\" ({Guide.Minutes} min)";

    public ConceptMatch Match { get; }

    public ConceptLearningProgress Progress { get; }

    public string ConceptId => Concept.Id;

    public string Title => Concept.Title;

    public string Summary => Concept.Summary;

    public string UsageText => Match.Occurrences == 1
        ? $"Line {Match.FirstLine}"
        : $"Line {Match.FirstLine} · used {Match.Occurrences}×";

    public string Evidence => Match.Evidence;

    /// <summary>Registry key of the analyzed file's language (for highlighting the evidence line).</summary>
    public string LanguageKey { get; init; } = "sprout";

    public IReadOnlyList<LearnExampleViewModel> Examples { get; }

    public bool Practiced => Progress.Practiced;

    public string StatusText => Progress.Practiced
        ? "Practiced in Academy"
        : Progress.SuggestedLesson is { } lesson
            ? $"Learn it: {lesson.Title} · +{lesson.Xp} XP"
            : "Explore in the Concept Browser";

    public string PracticeLabel => Progress.SuggestedLesson is null ? "Explore" : Progress.Practiced ? "Practice more" : "Start lesson";
}

public sealed record LearnExampleViewModel(string Language, string Code)
{
    public string LanguageKey => Language.ToLowerInvariant();
}
