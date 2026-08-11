namespace SproutStudio.Academy;

public sealed class RecommendationEngine
{
    private static readonly IReadOnlyDictionary<ProgrammingGoal, string> GoalTracks =
        new Dictionary<ProgrammingGoal, string>
        {
            [ProgrammingGoal.GeneralProgramming] = "sprout-fundamentals",
            [ProgrammingGoal.GameDevelopment] = "sprout-game-development",
            [ProgrammingGoal.WebBackend] = "go-backend",
            [ProgrammingGoal.AiAutomation] = "python-automation",
            [ProgrammingGoal.ComputerScience] = "computer-science-foundations",
            [ProgrammingGoal.SystemsProgramming] = "rust-systems",
        };

    private readonly AcademyCatalog _catalog;

    public RecommendationEngine(AcademyCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<TrackRecommendation> Recommend(AcademyProfile profile)
    {
        AcademyProfile normalized = AcademyProfileValidator.ValidateAndNormalize(profile);
        var recommendations = new List<TrackRecommendation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (normalized.Experience == ExperienceLevel.AbsoluteBeginner)
        {
            Add(
                "sprout-fundamentals",
                "You selected Absolute Beginner, so this track introduces core ideas with Sprout before specialization.",
                "Rule: absolute beginners start with Sprout Programming Fundamentals.");
        }

        foreach (ProgrammingGoal goal in normalized.Goals)
        {
            string trackId = GoalTracks[goal];
            Add(
                trackId,
                $"You selected {DisplayGoal(goal)} as a programming goal.",
                $"Rule: the {DisplayGoal(goal)} goal maps to {_catalog.GetTrack(trackId).Title}.");
        }

        if (recommendations.Count == 0)
        {
            Add(
                "computer-science-foundations",
                "This broad track connects transferable concepts across languages.",
                "Rule: use Computer Science Foundations when no specialization matches.");
        }
        return recommendations;

        void Add(string id, string explanation, string rule)
        {
            if (seen.Add(id))
            {
                recommendations.Add(new TrackRecommendation(
                    _catalog.GetTrack(id),
                    explanation,
                    rule));
            }
        }
    }

    public static string DisplayGoal(ProgrammingGoal goal) => goal switch
    {
        ProgrammingGoal.GeneralProgramming => "General Programming",
        ProgrammingGoal.GameDevelopment => "Game Development",
        ProgrammingGoal.WebBackend => "Web / Backend",
        ProgrammingGoal.AiAutomation => "AI / Automation",
        ProgrammingGoal.ComputerScience => "Computer Science",
        ProgrammingGoal.SystemsProgramming => "Systems Programming",
        _ => throw new ArgumentOutOfRangeException(nameof(goal)),
    };

    public static string DisplayExperience(ExperienceLevel experience) => experience switch
    {
        ExperienceLevel.AbsoluteBeginner => "Absolute Beginner",
        ExperienceLevel.Intermediate => "Intermediate",
        ExperienceLevel.Advanced => "Advanced",
        _ => throw new ArgumentOutOfRangeException(nameof(experience)),
    };
}
