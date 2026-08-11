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

public sealed record LessonDefinition(
    string Id,
    string Title,
    string Summary,
    string Content,
    int Xp,
    string[] ConceptIds);

public sealed record TrackDefinition(
    string Id,
    string Title,
    string Summary,
    string? RequiredToolchainId,
    LessonDefinition[] Lessons);

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

public sealed record AcademyState(
    int SchemaVersion,
    AcademyProfile? Profile,
    string[] EnrolledTrackIds,
    string[] CompletedLessonIds,
    AchievementAward[] AchievementAwards)
{
    public const int CurrentSchemaVersion = 1;

    public static AcademyState Empty { get; } = new(
        CurrentSchemaVersion,
        null,
        [],
        [],
        []);
}

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
    int TotalXp,
    int Level,
    int XpTowardNextLevel,
    AchievementDefinition[] EarnedAchievements);

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
