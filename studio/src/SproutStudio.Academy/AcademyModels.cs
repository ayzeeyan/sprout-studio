using System.Text.Json.Serialization;

namespace SproutStudio.Academy;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExperienceLevel
{
    AbsoluteBeginner,
    Intermediate,
    Advanced,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProgrammingGoal
{
    GeneralProgramming,
    GameDevelopment,
    WebBackend,
    AiAutomation,
    ComputerScience,
    SystemsProgramming,
}

public sealed record AcademyProfile(
    string DisplayName,
    string PreferredUsername,
    ExperienceLevel Experience,
    ProgrammingGoal[] Goals,
    bool ShowGamification);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubmissionType
{
    MultipleChoice,
    MultipleSelect,
    ShortAnswer,
    FillInBlank,
    CodeExercise,
    ProjectExercise,
    Quiz,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskType
{
    Lesson,
    Reading,
    Quiz,
    Exercise,
    CodingExercise,
    Checkpoint,
    CapstoneStep,
    Revision,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskStatus
{
    NotStarted,
    InProgress,
    ReadyToSubmit,
    Submitted,
    Passed,
    NeedsRevision,
    Completed,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HintAssistanceMode
{
    Standard,   // 5 hints / day default
    Reduced,    // 10 hints / day
    Unlimited,  // Unlimited hints for accessibility
}

public sealed record HintItem(
    string Id,
    int Level,
    string LevelName,
    string Content,
    bool IsRevealed);

public sealed record HintUsageState(
    string LastUsageDate,
    int UsedCountToday,
    int DailyAllowance,
    HintAssistanceMode Mode,
    string[] RevealedHintIds)
{
    public const int DefaultDailyAllowance = 5;

    public static HintUsageState Default { get; } = new(
        DateTime.Now.ToString("yyyy-MM-dd"),
        0,
        DefaultDailyAllowance,
        HintAssistanceMode.Standard,
        []);

    public int RemainingHintsToday => Mode == HintAssistanceMode.Unlimited
        ? int.MaxValue
        : Math.Max(0, DailyAllowance - UsedCountToday);

    public bool CanRevealNewHint => Mode == HintAssistanceMode.Unlimited || RemainingHintsToday > 0;
}

public sealed record SubmissionRecord(
    string Id,
    string TrackId,
    string LessonId,
    string? ModuleId,
    string ExerciseTitle,
    int AttemptNumber,
    DateTimeOffset SubmittedAt,
    SubmissionType Type,
    string Answer,
    bool Passed,
    int Score,
    string Feedback,
    string[] PassedRequirements,
    string[] FailedRequirements,
    string[] CompilerDiagnostics);

public sealed record AcademyTask(
    string Id,
    string Title,
    string TrackId,
    string TrackTitle,
    string? ModuleId,
    string? ModuleTitle,
    string LessonId,
    TaskType Type,
    TaskStatus Status,
    int EstimatedMinutes,
    int Priority,
    string CompletionRequirement,
    int ProgressPercent);

public sealed record QuizQuestion(
    string Id,
    string Question,
    SubmissionType Type,
    string[] Options,
    string[] CorrectAnswers,
    string Explanation,
    string? ConceptId = null);

public sealed record QuizDefinition(
    string Title,
    string Summary,
    QuizQuestion[] Questions,
    int PassingScorePercent = 70);

public sealed record CheckpointDefinition(
    string Id,
    string ModuleId,
    string Title,
    string Summary,
    QuizQuestion[] Questions,
    ExerciseDefinition? CodingExercise = null);

public sealed record ExerciseDefinition(
    string StarterCode,
    string SolutionCode,
    string ExpectedOutput,
    string[] Hints,
    string[] Assertions,
    string? Title = null,
    string? Objective = null,
    QuizQuestion[]? Questions = null,
    bool VerifyExpectedOutput = false,
    string? EntryPoint = null,
    string? TestHarness = null);

public sealed record ModuleDefinition(
    string Id,
    string Title,
    string Summary,
    string[] LessonIds,
    CheckpointDefinition? Checkpoint = null);

public sealed record LessonDefinition(
    string Id,
    string Title,
    string Summary,
    string Content,
    int Xp,
    string[] ConceptIds,
    string? ModuleId = null,
    ExerciseDefinition? Exercise = null,
    TaskType TaskType = TaskType.Lesson,
    int EstimatedMinutes = 10);

public sealed record TrackDefinition(
    string Id,
    string Title,
    string Summary,
    string? RequiredToolchainId,
    LessonDefinition[] Lessons,
    string? Level = null,
    string? Category = null,
    ModuleDefinition[]? Modules = null);

public sealed record ExerciseModel(
    string StarterCode,
    string SolutionCode,
    string ExpectedOutput,
    string[] Hints,
    string[] Assertions,
    string? Title = null,
    string? Objective = null,
    QuizQuestion[]? Questions = null,
    bool VerifyExpectedOutput = false,
    string? EntryPoint = null,
    string? TestHarness = null);

public sealed record LessonModel(
    string Id,
    string Title,
    string Summary,
    string Content,
    int Xp,
    string[] ConceptIds,
    ExerciseModel? Exercise = null,
    string? ModuleId = null,
    TaskType TaskType = TaskType.Lesson,
    int EstimatedMinutes = 10);

public sealed record ModuleModel(
    string Id,
    string Title,
    string Summary,
    string[] LessonIds,
    CheckpointDefinition? Checkpoint = null);

public sealed record CourseModel(
    string Id,
    string Title,
    string Summary,
    string Level,
    string Category,
    string? RequiredToolchainId,
    ModuleModel[] Modules,
    LessonModel[] Lessons);

public sealed record ExerciseValidationResult(
    bool Success,
    string Output,
    string[] PassedAssertions,
    string[] FailedAssertions,
    string? Feedback,
    RequirementEvaluation[] RequirementResults);

public sealed record RequirementEvaluation(
    string Id,
    string Title,
    bool Passed,
    string Expected,
    string Observed,
    string Message,
    string? SourceSearchTerm = null,
    int? SourceLine = null,
    int? SourceColumn = null);

public sealed record ExerciseRuntimeResult(
    bool Success,
    string Output,
    string[] Diagnostics,
    TimeSpan Duration,
    string Summary,
    bool RanProgram,
    RuntimeTestResult[]? TestResults = null,
    int? ErrorLine = null,
    int? ErrorColumn = null);

public sealed record RuntimeTestResult(
    string Id,
    string Title,
    bool Passed,
    string Expected,
    string Observed,
    string Message,
    string? SourceSearchTerm = null);

public sealed record ExerciseExecutionResult(
    bool Success,
    string? ExpectedOutput,
    string? ActualOutput,
    string[] CompilerDiagnostics,
    string[] PassedAssertions,
    string[] FailedAssertions,
    string Feedback,
    bool ExecutedWithRealToolchain,
    RequirementEvaluation[] RequirementResults,
    TimeSpan Duration,
    string ExecutionSummary);

public sealed record LanguageExample(string Language, string Code);

public sealed record ConceptDefinition(
    string Id,
    string Title,
    string Summary,
    string[] RelatedConceptIds,
    LanguageExample[] Examples);

public sealed record AchievementDefinition(
    string Id,
    string Title,
    string Description);

public sealed record AchievementAward(string Id, DateTimeOffset EarnedAt);

/// <summary>A beginner-friendly Handbook guide: an easy version of the documentation.</summary>
public sealed record GuideDefinition(
    string Id,
    string Title,
    string Summary,
    int Minutes,
    string Level,
    string[] ConceptIds,
    string? ReferenceDocId = null);

/// <summary>When the learner finished reading a Handbook guide.</summary>
public sealed record GuideReadRecord(string GuideId, DateTimeOffset ReadAt);

public sealed record AcademyState(
    int SchemaVersion,
    AcademyProfile? Profile,
    string[] EnrolledTrackIds,
    string[] CompletedLessonIds,
    AchievementAward[] AchievementAwards,
    SubmissionRecord[] Submissions,
    HintUsageState HintUsage,
    string? ActiveTrackId = null,
    string? ActiveLessonId = null,
    GuideReadRecord[]? GuideReads = null)
{
    /// <summary>Guides the learner has read (never null for consumers).</summary>
    public GuideReadRecord[] ReadGuides => GuideReads ?? [];

    public bool HasReadGuide(string guideId) =>
        ReadGuides.Any(read => string.Equals(read.GuideId, guideId, StringComparison.OrdinalIgnoreCase));

    public const int CurrentSchemaVersion = 2;

    public static AcademyState Empty { get; } = new(
        CurrentSchemaVersion,
        null,
        [],
        [],
        [],
        [],
        HintUsageState.Default,
        null,
        null);
}

public sealed record ConceptLearningProgress(
    string ConceptId,
    bool Practiced,
    TrackDefinition? SuggestedTrack,
    LessonDefinition? SuggestedLesson);

public sealed record TrackRecommendation(
    TrackDefinition Track,
    string Explanation,
    string Rule);

public sealed record TrackProgress(
    TrackDefinition Track,
    bool Enrolled,
    int CompletedLessons,
    int TotalLessons,
    int CompletionPercent);

public sealed record AcademyDashboard(
    AcademyState State,
    TrackProgress[] Tracks,
    AcademyTask? ContinueTask,
    AcademyTask[] PrioritizedTasks,
    HintUsageState HintUsage,
    SubmissionRecord[] RecentSubmissions,
    int TotalXp,
    int Level,
    int XpTowardNextLevel,
    AchievementDefinition[] EarnedAchievements,
    int CompletedLessonsToday = 0,
    int XpEarnedToday = 0,
    LearningHabitSummary? Habits = null)
{
    /// <summary>Streaks, daily goal, and activity calendar (never null for consumers).</summary>
    public LearningHabitSummary HabitSummary => Habits ?? LearningHabitSummary.Empty;

    /// <summary>XP needed per level; levels are 1 + TotalXp / XpPerLevel.</summary>
    public const int XpPerLevel = 100;
}

public enum EnrollmentStatus
{
    Enrolled,
    AlreadyEnrolled,
    ToolchainRequired,
}

public sealed record EnrollmentResult(
    EnrollmentStatus Status,
    TrackDefinition Track,
    Toolchains.ToolchainDetectionResult? Toolchain);

public sealed record LessonCompletionResult(
    LessonDefinition Lesson,
    bool AlreadyCompleted,
    int AwardedXp,
    AchievementDefinition[] NewAchievements);

public static class AcademyProfileValidator
{
    public static AcademyProfile ValidateAndNormalize(AcademyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string displayName = profile.DisplayName.Trim();
        string username = profile.PreferredUsername.Trim();
        if (displayName.Length is < 1 or > 60)
        {
            throw new ArgumentException(
                "Display name must contain between 1 and 60 characters.",
                nameof(profile));
        }
        if (username.Length is < 2 or > 32
            || username.Any(character => !char.IsLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "Preferred username must contain 2 to 32 letters, numbers, dots, hyphens, or underscores.",
                nameof(profile));
        }
        if (!Enum.IsDefined(profile.Experience))
        {
            throw new ArgumentException("Experience level is invalid.", nameof(profile));
        }
        ProgrammingGoal[] goals = profile.Goals
            .Distinct()
            .Order()
            .ToArray();
        if (goals.Length == 0 || goals.Any(goal => !Enum.IsDefined(goal)))
        {
            throw new ArgumentException("Select at least one valid programming goal.", nameof(profile));
        }
        return profile with
        {
            DisplayName = displayName,
            PreferredUsername = username,
            Goals = goals,
        };
    }
}
