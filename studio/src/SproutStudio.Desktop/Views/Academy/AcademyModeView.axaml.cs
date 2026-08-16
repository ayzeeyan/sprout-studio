using Avalonia.Controls;
using Avalonia.Interactivity;
using SproutStudio.Academy;
using SproutStudio.Editor;
using SproutStudio.Models;
using SproutStudio.Services;
using SproutStudio.Toolchains;
using SproutStudio.ViewModels;
using SproutStudio.Views.Modes;
using SproutStudio.Views.Shared;

namespace SproutStudio.Views.Academy;

public sealed partial class AcademyModeView : UserControl, IStudioModeView
{
    private readonly AcademyService _academy;
    private readonly ToolchainManager _toolchains;
    private AcademyState _state = AcademyState.Empty;
    private IReadOnlyDictionary<string, ToolchainDetectionResult>? _toolchainState;
    private MainWindowViewModel? _workspace;
    private AcademyTrackRow? _selectedTrack;
    private string? _continueTrackId;
    private bool _profileInitialized;

    public AcademyModeView()
        : this(ToolchainManager.CreateDefault())
    {
    }

    internal AcademyModeView(ToolchainManager toolchains)
    {
        _toolchains = toolchains ?? throw new ArgumentNullException(nameof(toolchains));
        _academy = AcademyService.CreateDefault(
            _toolchains,
            Environment.GetEnvironmentVariable("SPROUT_STUDIO_TEST_ACADEMY_STATE"));
        InitializeComponent();
        this.FindControl<ListBox>("AcademyNavigation")!.ItemsSource = new AcademySectionItem[]
        {
            new("Home", "\uE80F"),
            new("My Learning", "\uE8F1"),
            new("Learning Tracks", "\uE82D"),
            new("Concept Browser", "\uE8A5"),
            new("Profile & Progress", "\uE77B"),
        };
        this.FindControl<ListBox>("AcademyNavigation")!.SelectedIndex = 0;
        this.FindControl<ComboBox>("ExperienceBox")!.ItemsSource = Enum.GetValues<ExperienceLevel>()
            .Select(value => new ExperienceOption(value, RecommendationEngine.DisplayExperience(value)))
            .ToArray();
    }

    private void AcademyModeView_SizeChanged(object? sender, SizeChangedEventArgs args)
    {
        Grid body = this.FindControl<Grid>("AcademyBodyGrid")!;
        Grid tracks = this.FindControl<Grid>("TracksPanel")!;
        Grid concepts = this.FindControl<Grid>("ConceptsPanel")!;
        ScrollViewer progress = this.FindControl<ScrollViewer>("AcademyProgressPanel")!;

        bool compact = args.NewSize.Width < 1240;
        bool narrow = args.NewSize.Width < 1000;

        body.ColumnDefinitions[0].Width = new GridLength(narrow ? 170 : 190);
        body.ColumnDefinitions[3].Width = new GridLength(compact ? 0 : 1);
        body.ColumnDefinitions[4].Width = new GridLength(compact ? 0 : 230);
        progress.IsVisible = !compact;

        tracks.ColumnDefinitions[0].Width = new GridLength(narrow ? 225 : compact ? 260 : 300);
        concepts.ColumnDefinitions[0].Width = new GridLength(narrow ? 225 : compact ? 250 : 270);
    }

    private static void ConstrainedScroll_SizeChanged(object? sender, SizeChangedEventArgs args)
    {
        if (sender is not ScrollViewer { Content: Control content } scroll)
        {
            return;
        }

        const double VerticalScrollBarReserve = 16;
        double availableWidth = args.NewSize.Width
            - scroll.Padding.Left
            - scroll.Padding.Right
            - VerticalScrollBarReserve;
        double maximumWidth = double.IsFinite(content.MaxWidth)
            ? content.MaxWidth
            : double.PositiveInfinity;
        content.Width = Math.Max(0, Math.Min(maximumWidth, availableWidth));
    }

    public async Task ActivateAsync(MainWindowViewModel workspace, CancellationToken cancellationToken = default)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        DataContext = workspace;
        await RefreshAsync(_selectedTrack?.Id, detectToolchains: _toolchainState is null, cancellationToken);
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs args) =>
        await RefreshAsync(_selectedTrack?.Id, detectToolchains: true);

    private async Task RefreshAsync(
        string? selectedTrackId = null,
        bool detectToolchains = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Task<AcademyState> stateTask = _academy.LoadStateAsync(cancellationToken);
            Task<IReadOnlyList<ToolchainDetectionResult>>? toolsTask = detectToolchains
                ? _toolchains.DetectAllAsync(cancellationToken)
                : null;
            _state = await stateTask;
            if (toolsTask is not null)
            {
                _toolchainState = (await toolsTask).ToDictionary(
                    result => result.Id,
                    StringComparer.OrdinalIgnoreCase);
            }
            AcademyDashboard dashboard = _academy.CreateDashboard(_state);
            AcademyTrackRow[] tracks = dashboard.Tracks
                .Select(progress => CreateTrackRow(progress))
                .ToArray();
            ListBox trackList = this.FindControl<ListBox>("TrackList")!;
            trackList.ItemsSource = tracks;
            int selectedIndex = selectedTrackId is null
                ? 0
                : Array.FindIndex(tracks, track => string.Equals(track.Id, selectedTrackId, StringComparison.OrdinalIgnoreCase));
            trackList.SelectedIndex = Math.Max(0, selectedIndex);

            AcademyTrackRow[] enrolled = tracks.Where(track => track.Enrolled).ToArray();
            ListBox learning = this.FindControl<ListBox>("MyLearningList")!;
            learning.ItemsSource = enrolled;
            learning.IsVisible = enrolled.Length > 0;
            this.FindControl<TextBlock>("MyLearningEmpty")!.IsVisible = enrolled.Length == 0;
            RenderHome(dashboard, tracks);
            RenderProgress(dashboard, enrolled.Length);
            RenderToolchains();
            RenderAchievements(dashboard);
            RefreshConceptSearch();
            if (!_profileInitialized)
            {
                RenderProfile(_state.Profile);
                _profileInitialized = true;
            }
        }
        catch (Exception exception)
        {
            StudioLog.Error("Academy failed to load.", exception);
            this.FindControl<TextBlock>("ProfileStatusText")!.Text = exception.Message;
        }
    }

    private AcademyTrackRow CreateTrackRow(TrackProgress progress)
    {
        LanguageDescriptor language = LanguageRegistry.ForAcademyTrack(progress.Track.Id);
        string toolchainLabel = progress.Track.RequiredToolchainId is null
            ? "No external prerequisite"
            : _toolchainState?.GetValueOrDefault(progress.Track.RequiredToolchainId) is { } toolchain
                ? $"{toolchain.DisplayName}: {StatusLabel(toolchain.Status)}"
                : $"Requires {progress.Track.RequiredToolchainId}";
        LessonDefinition? next = progress.Track.Lessons.FirstOrDefault(lesson =>
            !_state.CompletedLessonIds.Contains(lesson.Id, StringComparer.OrdinalIgnoreCase));
        return new AcademyTrackRow(
            progress,
            language.Key,
            toolchainLabel,
            next is null ? "Track complete" : $"Next: {next.Title}");
    }

    private void RenderHome(AcademyDashboard dashboard, AcademyTrackRow[] tracks)
    {
        AcademyTrackRow featured = tracks.FirstOrDefault(track => track.Enrolled && track.CompletionPercent < 100)
            ?? tracks.FirstOrDefault(track => track.Id == "sprout-fundamentals")
            ?? tracks[0];
        _continueTrackId = featured.Id;
        this.FindControl<LanguageIconView>("ContinueLanguageIcon")!.LanguageKey = featured.LanguageKey;
        this.FindControl<TextBlock>("ContinueTrackTitle")!.Text = featured.Title;
        this.FindControl<TextBlock>("ContinueLessonTitle")!.Text = featured.NextLessonLabel;
        this.FindControl<ProgressBar>("ContinueProgress")!.Value = featured.CompletionPercent;
        this.FindControl<TextBlock>("ContinueProgressText")!.Text = featured.ProgressLabel;
        this.FindControl<Button>("ContinueButton")!.Content = featured.Enrolled ? "Continue" : "View Track";
        this.FindControl<TextBlock>("WelcomeTitle")!.Text = _state.Profile?.DisplayName is { Length: > 0 } name
            ? $"Welcome back, {name}"
            : "Continue learning";

        AcademyProfile recommendationProfile = _state.Profile ?? DefaultProfile();
        this.FindControl<ItemsControl>("HomeRecommendations")!.ItemsSource = _academy.Recommendations
            .Recommend(recommendationProfile)
            .Where(recommendation => recommendation.Track.Id != featured.Id)
            .Take(3)
            .Select(recommendation => new AcademyRecommendationRow(
                recommendation.Track.Id,
                recommendation.Track.Title,
                recommendation.Explanation,
                LanguageRegistry.ForAcademyTrack(recommendation.Track.Id).Key))
            .ToArray();
    }

    private void RenderProgress(AcademyDashboard dashboard, int enrolledCount)
    {
        bool show = _state.Profile?.ShowGamification ?? true;
        this.FindControl<TextBlock>("LevelText")!.Text = show ? $"Level {dashboard.Level}" : "Progress";
        this.FindControl<TextBlock>("XpText")!.Text = show ? $"{dashboard.TotalXp} XP" : "Gamification hidden";
        this.FindControl<ProgressBar>("LevelProgress")!.Value = show ? dashboard.XpTowardNextLevel : 0;
        this.FindControl<TextBlock>("LessonCountText")!.Text =
            $"{_state.CompletedLessonIds.Length} lesson{(_state.CompletedLessonIds.Length == 1 ? string.Empty : "s")} complete";
        this.FindControl<TextBlock>("ActiveTrackCountText")!.Text =
            $"{enrolledCount} active track{(enrolledCount == 1 ? string.Empty : "s")}";
    }

    private void RenderToolchains()
    {
        var rows = new List<AcademyToolchainRow>
        {
            new("sprout", "Sprout", "BUNDLED"),
        };
        if (_toolchainState is not null)
        {
            rows.AddRange(_toolchainState.Values
                .OrderBy(result => result.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(result => new AcademyToolchainRow(
                    result.Id,
                    result.DisplayName,
                    StatusLabel(result.Status).ToUpperInvariant())));
        }
        this.FindControl<ItemsControl>("AcademyToolchains")!.ItemsSource = rows;
    }

    private void RenderAchievements(AcademyDashboard dashboard)
    {
        string[] achievements = dashboard.EarnedAchievements
            .Select(achievement => $"✓  {achievement.Title} — {achievement.Description}")
            .ToArray();
        this.FindControl<ItemsControl>("AchievementList")!.ItemsSource = achievements;
        this.FindControl<TextBlock>("AchievementEmpty")!.IsVisible = achievements.Length == 0;
    }

    private void AcademyNavigation_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        int selected = Math.Max(0, this.FindControl<ListBox>("AcademyNavigation")!.SelectedIndex);
        this.FindControl<ScrollViewer>("HomePanel")!.IsVisible = selected == 0;
        this.FindControl<Grid>("MyLearningPanel")!.IsVisible = selected == 1;
        this.FindControl<Grid>("TracksPanel")!.IsVisible = selected == 2;
        this.FindControl<Grid>("ConceptsPanel")!.IsVisible = selected == 3;
        this.FindControl<ScrollViewer>("ProfilePanel")!.IsVisible = selected == 4;
    }

    private void TrackList_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (this.FindControl<ListBox>("TrackList")?.SelectedItem is not AcademyTrackRow track)
        {
            return;
        }
        _selectedTrack = track;
        this.FindControl<LanguageIconView>("TrackLanguageIcon")!.LanguageKey = track.LanguageKey;
        this.FindControl<TextBlock>("SelectedTrackTitle")!.Text = track.Title;
        this.FindControl<TextBlock>("SelectedTrackStatus")!.Text = track.StatusLabel;
        this.FindControl<TextBlock>("SelectedTrackSummary")!.Text = track.Definition.Summary;
        this.FindControl<ProgressBar>("SelectedTrackProgress")!.Value = track.CompletionPercent;
        this.FindControl<TextBlock>("SelectedTrackProgressText")!.Text = track.ProgressLabel;
        Button enroll = this.FindControl<Button>("EnrollButton")!;
        enroll.Content = track.Enrolled ? "Enrolled" : "Enroll";
        enroll.IsEnabled = !track.Enrolled;
        bool hasPrerequisite = track.Definition.RequiredToolchainId is not null;
        this.FindControl<Border>("PrerequisitePanel")!.IsVisible = hasPrerequisite;
        this.FindControl<TextBlock>("PrerequisiteText")!.Text = hasPrerequisite
            ? track.ToolchainLabel
            : string.Empty;
        this.FindControl<ItemsControl>("LessonList")!.ItemsSource = track.Definition.Lessons
            .Select(lesson => new AcademyLessonRow(
                track.Id,
                track.LanguageKey,
                lesson,
                track.Enrolled,
                _state.CompletedLessonIds.Contains(lesson.Id, StringComparer.OrdinalIgnoreCase),
                "Concepts: " + string.Join(", ", lesson.ConceptIds.Select(id => _academy.Catalog.GetConcept(id).Title))))
            .ToArray();
    }

    private async void Enroll_Click(object? sender, RoutedEventArgs args)
    {
        if (_selectedTrack is null || _selectedTrack.Enrolled)
        {
            return;
        }
        try
        {
            EnrollmentResult result = await _academy.EnrollAsync(_selectedTrack.Id);
            if (result.Status == EnrollmentStatus.ToolchainRequired)
            {
                this.FindControl<TextBlock>("PrerequisiteText")!.Text =
                    result.Toolchain?.Message ?? "This track requires a validated external toolchain.";
            }
            await RefreshAsync(_selectedTrack.Id, detectToolchains: result.Status == EnrollmentStatus.ToolchainRequired);
        }
        catch (Exception exception)
        {
            StudioLog.Error("Academy enrollment failed.", exception);
            this.FindControl<TextBlock>("SelectedTrackStatus")!.Text = exception.Message;
        }
    }

    private async void CompleteLesson_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: AcademyLessonRow lesson } || !lesson.CanComplete)
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
            this.FindControl<TextBlock>("SelectedTrackStatus")!.Text = exception.Message;
        }
    }

    private async void Practice_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: AcademyLessonRow lesson } && _workspace is not null)
        {
            await _workspace.OpenAcademyPracticeAsync(
                _academy.Catalog.GetTrack(lesson.TrackId),
                lesson.Definition);
        }
    }

    private void Continue_Click(object? sender, RoutedEventArgs args)
    {
        if (_continueTrackId is not null)
        {
            ShowTrack(_continueTrackId);
        }
    }

    private void ViewTrack_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string trackId })
        {
            ShowTrack(trackId);
        }
    }

    private void ShowTrack(string trackId)
    {
        this.FindControl<ListBox>("AcademyNavigation")!.SelectedIndex = 2;
        ListBox tracks = this.FindControl<ListBox>("TrackList")!;
        tracks.SelectedItem = (tracks.ItemsSource as IEnumerable<AcademyTrackRow>)?
            .FirstOrDefault(track => string.Equals(track.Id, trackId, StringComparison.OrdinalIgnoreCase));
    }

    private void ConceptSearch_TextChanged(object? sender, TextChangedEventArgs args) => RefreshConceptSearch();

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
            : concepts.ToList().FindIndex(concept => string.Equals(concept.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        list.SelectedIndex = concepts.Count == 0 ? -1 : Math.Max(0, index);
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
            : "Related: " + string.Join(", ", concept.RelatedConceptIds.Select(id => _academy.Catalog.GetConcept(id).Title));
        this.FindControl<ItemsControl>("ConceptExamples")!.ItemsSource = concept.Examples
            .Select(example => new AcademyConceptExampleRow(
                example.Language,
                LanguageRegistry.TryGet(example.Language, out LanguageDescriptor? language) ? language!.Key : "text",
                example.Code))
            .ToArray();
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
            RenderProfile(_state.Profile);
            this.FindControl<TextBlock>("ProfileStatusText")!.Text = "Saved locally.";
            await RefreshAsync(_selectedTrack?.Id);

            void AddGoal(string name, ProgrammingGoal goal)
            {
                if (this.FindControl<CheckBox>(name)!.IsChecked == true)
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
            .FirstOrDefault(option => option.Value == profile.Experience);
        this.FindControl<CheckBox>("GoalGeneral")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.GeneralProgramming);
        this.FindControl<CheckBox>("GoalGames")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.GameDevelopment);
        this.FindControl<CheckBox>("GoalBackend")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.WebBackend);
        this.FindControl<CheckBox>("GoalAi")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.AiAutomation);
        this.FindControl<CheckBox>("GoalCs")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.ComputerScience);
        this.FindControl<CheckBox>("GoalSystems")!.IsChecked = profile.Goals.Contains(ProgrammingGoal.SystemsProgramming);
        this.FindControl<CheckBox>("GamificationBox")!.IsChecked = profile.ShowGamification;
    }

    private void OpenDocs_Click(object? sender, RoutedEventArgs args) =>
        _workspace?.RequestDocumentation("academy");

    private void OpenToolchains_Click(object? sender, RoutedEventArgs args) =>
        _workspace?.ActivateMode(ActivityDestination.Toolchains);

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

    private static string StatusLabel(ToolchainStatus status) => status switch
    {
        ToolchainStatus.Ready => "Ready",
        ToolchainStatus.CustomLocation => "Ready",
        ToolchainStatus.NotInstalled => "Missing",
        ToolchainStatus.PartialInstallation => "Partial",
        ToolchainStatus.UnsupportedVersion => "Unsupported",
        ToolchainStatus.FoundButBroken => "Broken",
        _ => status.ToString(),
    };
}

public sealed record AcademySectionItem(string Title, string Glyph);

public sealed record AcademyTrackRow(
    TrackProgress Progress,
    string LanguageKey,
    string ToolchainLabel,
    string NextLessonLabel)
{
    public TrackDefinition Definition => Progress.Track;
    public string Id => Definition.Id;
    public string Title => Definition.Title;
    public bool Enrolled => Progress.Enrolled;
    public int CompletionPercent => Progress.CompletionPercent;
    public string ProgressLabel => $"{Progress.CompletedLessons}/{Progress.TotalLessons} lessons · {CompletionPercent}%";
    public string StatusLabel => CompletionPercent == 100 ? "Track complete" : Enrolled ? "Enrolled" : "Available";
}

public sealed record AcademyLessonRow(
    string TrackId,
    string LanguageKey,
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
    public string StateGlyph => Completed ? "✓" : "○";
    public string CompletionLabel => Completed ? "Completed" : "Complete Lesson";
    public bool CanComplete => TrackEnrolled && !Completed;
    public bool CanPractice => TrackEnrolled && string.Equals(LanguageKey, "sprout", StringComparison.OrdinalIgnoreCase);
}

public sealed record AcademyRecommendationRow(
    string TrackId,
    string Title,
    string Explanation,
    string LanguageKey);

public sealed record AcademyConceptExampleRow(string Language, string LanguageKey, string Code);

public sealed record AcademyToolchainRow(string LanguageKey, string Name, string Status);

public sealed record ExperienceOption(ExperienceLevel Value, string Label)
{
    public override string ToString() => Label;
}
