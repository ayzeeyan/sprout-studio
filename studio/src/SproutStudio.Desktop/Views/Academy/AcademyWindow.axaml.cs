using Avalonia.Controls;
using Avalonia.Interactivity;
using SproutStudio.Academy;
using SproutStudio.Services;
using SproutStudio.Toolchains;
using SproutStudio.Views.Dialogs;

namespace SproutStudio.Views.Academy;

public sealed partial class AcademyWindow : Window
{
    private readonly AcademyService _academy;
    private AcademyState _state = AcademyState.Empty;
    private bool _profileInitialized;

    public AcademyWindow()
        : this(AcademyService.CreateDefault(ToolchainManager.CreateDefault()))
    {
    }

    public AcademyWindow(AcademyService academy)
    {
        _academy = academy ?? throw new ArgumentNullException(nameof(academy));
        InitializeComponent();
        this.FindControl<ComboBox>("ExperienceBox")!.ItemsSource = Enum.GetValues<ExperienceLevel>()
            .Select(value => new ExperienceOption(value, RecommendationEngine.DisplayExperience(value)))
            .ToArray();
        Opened += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync(string? selectedTrackId = null)
    {
        try
        {
            _state = await _academy.LoadStateAsync();
            AcademyDashboard dashboard = _academy.CreateDashboard(_state);
            this.FindControl<TextBlock>("LevelText")!.Text = $"Level {dashboard.Level}";
            this.FindControl<TextBlock>("XpText")!.Text = $"{dashboard.TotalXp} XP";
            this.FindControl<StackPanel>("ProgressSummary")!.IsVisible =
                _state.Profile?.ShowGamification ?? true;
            AcademyTrackItem[] tracks = dashboard.Tracks
                .Select(progress => new AcademyTrackItem(progress))
                .ToArray();
            ListBox list = this.FindControl<ListBox>("TrackList")!;
            list.ItemsSource = tracks;
            int selectedIndex = selectedTrackId is null
                ? 0
                : Array.FindIndex(tracks, item => string.Equals(item.Id, selectedTrackId, StringComparison.OrdinalIgnoreCase));
            list.SelectedIndex = Math.Max(0, selectedIndex);
            if (!_profileInitialized)
            {
                RenderProfile(_state.Profile);
                _profileInitialized = true;
            }
            RenderRecommendations(dashboard);
            RefreshConceptSearch();
        }
        catch (Exception exception)
        {
            StudioLog.Error("Academy failed to load.", exception);
            await new MessageDialog("Academy unavailable", exception.Message, "OK", showCancel: false)
                .ShowDialog<bool>(this);
        }
    }

    private void TrackList_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (this.FindControl<ListBox>("TrackList")?.SelectedItem is not AcademyTrackItem track)
        {
            return;
        }
        this.FindControl<TextBlock>("SelectedTrackTitle")!.Text = track.Title;
        this.FindControl<TextBlock>("SelectedTrackSummary")!.Text = track.Summary;
        this.FindControl<ListBox>("LessonList")!.ItemsSource = track.Definition.Lessons
            .Select(lesson => new AcademyLessonItem(
                track.Id,
                lesson,
                track.Enrolled,
                _state.CompletedLessonIds.Contains(lesson.Id, StringComparer.OrdinalIgnoreCase),
                "Concepts: " + string.Join(", ", lesson.ConceptIds
                    .Select(id => _academy.Catalog.GetConcept(id).Title))))
            .ToArray();
    }

    private void ConceptSearch_TextChanged(object? sender, TextChangedEventArgs args) =>
        RefreshConceptSearch();

    private void RefreshConceptSearch()
    {
        TextBox? search = this.FindControl<TextBox>("ConceptSearchBox");
        ListBox? list = this.FindControl<ListBox>("ConceptList");
        if (search is null || list is null)
        {
            return;
        }
        IReadOnlyList<ConceptDefinition> concepts = _academy.Catalog.SearchConcepts(search.Text);
        string? selectedId = (list.SelectedItem as ConceptDefinition)?.Id;
        list.ItemsSource = concepts;
        int index = selectedId is null
            ? 0
            : concepts.ToList().FindIndex(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        list.SelectedIndex = Math.Max(0, index);
    }

    private void ConceptList_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (this.FindControl<ListBox>("ConceptList")?.SelectedItem is not ConceptDefinition concept)
        {
            return;
        }
        this.FindControl<TextBlock>("ConceptTitle")!.Text = concept.Title;
        this.FindControl<TextBlock>("ConceptSummary")!.Text = concept.Summary;
        this.FindControl<TextBlock>("RelatedConceptsText")!.Text = concept.RelatedConceptIds.Length == 0
            ? "No related concepts"
            : "Related: " + string.Join(", ", concept.RelatedConceptIds
                .Select(id => _academy.Catalog.GetConcept(id).Title));
        this.FindControl<ItemsControl>("ConceptExamples")!.ItemsSource = concept.Examples;
    }

    private async void SaveProfile_Click(object? sender, RoutedEventArgs args)
    {
        try
        {
            var goals = new List<ProgrammingGoal>();
            AddGoal("GoalGeneral", ProgrammingGoal.GeneralProgramming);
            AddGoal("GoalGames", ProgrammingGoal.GameDevelopment);
            AddGoal("GoalBackend", ProgrammingGoal.WebBackend);
            AddGoal("GoalAi", ProgrammingGoal.AiAutomation);
            AddGoal("GoalCs", ProgrammingGoal.ComputerScience);
            AddGoal("GoalSystems", ProgrammingGoal.SystemsProgramming);
            ExperienceLevel experience =
                (this.FindControl<ComboBox>("ExperienceBox")!.SelectedItem as ExperienceOption)?.Value
                ?? ExperienceLevel.AbsoluteBeginner;
            var profile = new AcademyProfile(
                this.FindControl<TextBox>("DisplayNameBox")!.Text ?? string.Empty,
                this.FindControl<TextBox>("UsernameBox")!.Text ?? string.Empty,
                experience,
                goals.ToArray(),
                this.FindControl<CheckBox>("GamificationBox")!.IsChecked == true);
            _state = await _academy.SaveProfileAsync(profile);
            AcademyDashboard dashboard = _academy.CreateDashboard(_state);
            RenderProfile(_state.Profile);
            RenderRecommendations(dashboard);
            this.FindControl<StackPanel>("ProgressSummary")!.IsVisible = profile.ShowGamification;
            this.FindControl<TextBlock>("ProfileStatusText")!.Text = "Saved locally.";

            void AddGoal(string controlName, ProgrammingGoal goal)
            {
                if (this.FindControl<CheckBox>(controlName)!.IsChecked == true)
                {
                    goals.Add(goal);
                }
            }
        }
        catch (Exception exception)
        {
            StudioLog.Error("Academy profile save failed.", exception);
            this.FindControl<TextBlock>("ProfileStatusText")!.Text = exception.Message;
        }
    }

    private void RenderProfile(AcademyProfile? profile)
    {
        profile ??= DefaultProfile();
        this.FindControl<TextBox>("DisplayNameBox")!.Text = profile.DisplayName;
        this.FindControl<TextBox>("UsernameBox")!.Text = profile.PreferredUsername;
        ComboBox experience = this.FindControl<ComboBox>("ExperienceBox")!;
        experience.SelectedItem = (experience.ItemsSource as IEnumerable<ExperienceOption>)?
            .FirstOrDefault(item => item.Value == profile.Experience);
        this.FindControl<CheckBox>("GoalGeneral")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.GeneralProgramming);
        this.FindControl<CheckBox>("GoalGames")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.GameDevelopment);
        this.FindControl<CheckBox>("GoalBackend")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.WebBackend);
        this.FindControl<CheckBox>("GoalAi")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.AiAutomation);
        this.FindControl<CheckBox>("GoalCs")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.ComputerScience);
        this.FindControl<CheckBox>("GoalSystems")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.SystemsProgramming);
        this.FindControl<CheckBox>("GamificationBox")!.IsChecked = profile.ShowGamification;
    }

    private void RenderRecommendations(AcademyDashboard dashboard)
    {
        TrackRecommendation[] recommendations = _state.Profile is null
            ? []
            : _academy.Recommendations.Recommend(_state.Profile).ToArray();
        this.FindControl<ItemsControl>("RecommendationList")!.ItemsSource = recommendations
            .Select(item => new RecommendationItem(item.Track.Title, item.Explanation, item.Rule))
            .ToArray();
        this.FindControl<TextBlock>("RecommendationEmptyText")!.IsVisible = recommendations.Length == 0;
        this.FindControl<ItemsControl>("AchievementList")!.ItemsSource = dashboard.EarnedAchievements
            .Select(item => $"✓  {item.Title} — {item.Description}")
            .ToArray();
    }

    private static AcademyProfile DefaultProfile()
    {
        string displayName = Environment.UserName.Trim();
        if (displayName.Length == 0)
        {
            displayName = "Sprout Learner";
        }
        string username = new(displayName
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            .Take(32)
            .ToArray());
        if (username.Length < 2)
        {
            username = "sprout-learner";
        }
        return new AcademyProfile(
            displayName[..Math.Min(displayName.Length, 60)],
            username,
            ExperienceLevel.AbsoluteBeginner,
            [ProgrammingGoal.GeneralProgramming],
            true);
    }

    private async void Enroll_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: AcademyTrackItem track } || !track.CanEnroll)
        {
            return;
        }
        try
        {
            EnrollmentResult result = await _academy.EnrollAsync(track.Id);
            if (result.Status == EnrollmentStatus.ToolchainRequired)
            {
                string message = result.Toolchain?.Message ?? "This track requires an external toolchain.";
                await new MessageDialog("Toolchain required", message, "OK", showCancel: false)
                    .ShowDialog<bool>(this);
            }
            await RefreshAsync(track.Id);
        }
        catch (Exception exception)
        {
            StudioLog.Error("Academy enrollment failed.", exception);
        }
    }

    private async void CompleteLesson_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: AcademyLessonItem lesson } || !lesson.CanComplete)
        {
            return;
        }
        try
        {
            await _academy.CompleteLessonAsync(lesson.TrackId, lesson.Id);
            await RefreshAsync(lesson.TrackId);
        }
        catch (Exception exception)
        {
            StudioLog.Error("Academy lesson completion failed.", exception);
            await new MessageDialog("Could not complete lesson", exception.Message, "OK", showCancel: false)
                .ShowDialog<bool>(this);
        }
    }

    private sealed record AcademyTrackItem(TrackProgress Progress)
    {
        public TrackDefinition Definition => Progress.Track;
        public string Id => Definition.Id;
        public string Title => Definition.Title;
        public string Summary => Definition.Summary;
        public bool Enrolled => Progress.Enrolled;
        public int CompletionPercent => Progress.CompletionPercent;
        public string ProgressLabel => $"{Progress.CompletedLessons}/{Progress.TotalLessons}";
        public string EnrollmentLabel => Enrolled ? "Enrolled" : "Enroll";
        public bool CanEnroll => !Enrolled;
        public bool HasPrerequisite => Definition.RequiredToolchainId is not null;
        public string PrerequisiteLabel => Definition.RequiredToolchainId is null
            ? string.Empty
            : $"Requires validated {Definition.RequiredToolchainId} toolchain";
    }

    private sealed record AcademyLessonItem(
        string TrackId,
        LessonDefinition Definition,
        bool TrackEnrolled,
        bool Completed,
        string ConceptsLabel)
    {
        public string Id => Definition.Id;
        public string Title => Definition.Title;
        public string Summary => Definition.Summary;
        public string Content => Definition.Content;
        public string XpLabel => $"{Definition.Xp} XP";
        public string CompletionLabel => Completed ? "Completed" : "Complete";
        public bool CanComplete => TrackEnrolled && !Completed;
    }

    private sealed record ExperienceOption(ExperienceLevel Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record RecommendationItem(string Title, string Explanation, string Rule);
}
