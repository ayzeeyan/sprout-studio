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
using TaskStatus = SproutStudio.Academy.TaskStatus;

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
    private string? _continueActivityId;
    private bool _profileInitialized;

    public AcademyModeView()
        : this(ToolchainManager.CreateDefault(), null)
    {
    }

    internal AcademyModeView(ToolchainManager toolchains, AcademyService? academy = null)
    {
        _toolchains = toolchains ?? throw new ArgumentNullException(nameof(toolchains));
        _academy = academy ?? AcademyService.CreateDefault(
            _toolchains,
            Environment.GetEnvironmentVariable("SPROUT_STUDIO_TEST_ACADEMY_STATE"));
        InitializeComponent();
        this.FindControl<ListBox>("AcademyNavigation")!.ItemsSource = new AcademySectionItem[]
        {
            new("Home", "\uE80F"),
            new("My Learning", "\uE7BE"),
            new("Learning Tracks", "\uE8F1"),
            new("Concept Browser", "\uEA80"),
            new("Challenges", "\uE7C1"),
            new("Submissions", "\uF0E3"),
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

    public void SelectSection(string section)
    {
        int index = section.Trim().ToLowerInvariant() switch
        {
            "home" => 0,
            "my-learning" or "learning" => 1,
            "tracks" => 2,
            "concepts" => 3,
            "challenges" => 4,
            "submissions" => 5,
            "profile" => 6,
            _ => 0,
        };
        this.FindControl<ListBox>("AcademyNavigation")!.SelectedIndex = index;
    }

    public void SelectTrack(string trackId) => ShowTrack(trackId);

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
            RenderChallenges(dashboard);
            RenderSubmissions(dashboard);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
            ? "Sprout: Built in"
            : _toolchainState?.GetValueOrDefault(progress.Track.RequiredToolchainId) is { } toolchain
                ? $"{toolchain.DisplayName}: {StatusLabel(toolchain.Status)}"
                : $"Requires {progress.Track.RequiredToolchainId}";
        LessonDefinition? next = FindNextActivity(progress.Track);
        return new AcademyTrackRow(
            progress,
            language.Key,
            toolchainLabel,
            next is null ? "Track complete" : $"Next: {next.Title}");
    }

    private void RenderHome(AcademyDashboard dashboard, AcademyTrackRow[] tracks)
    {
        // Student Greeting
        int hour = DateTime.Now.Hour;
        string timeGreeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
        string studentName = _state.Profile?.DisplayName is { Length: > 0 } name ? name : "Student";
        this.FindControl<TextBlock>("WelcomeTitle")!.Text = $"{timeGreeting}, {studentName}";

        // 1. Continue Learning Card. Resolve against the ordered module sequence so an
        // unlocked checkpoint is not skipped in favor of a lesson from the next module.
        AcademyTrackRow featured = (dashboard.ContinueTask is { } continueTask
                ? tracks.FirstOrDefault(track => string.Equals(track.Id, continueTask.TrackId, StringComparison.OrdinalIgnoreCase))
                : null)
            ?? tracks.FirstOrDefault(track => track.Enrolled && track.CompletionPercent < 100)
            ?? tracks.FirstOrDefault(track => track.Id == "sprout-fundamentals")
            ?? tracks[0];
        LessonDefinition? nextActivity = FindNextActivity(featured.Definition);
        ModuleDefinition? nextModule = nextActivity?.ModuleId is { } moduleId
            ? featured.Definition.Modules?.FirstOrDefault(module =>
                string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            : null;

        _continueTrackId = featured.Id;
        _continueActivityId = featured.Enrolled ? nextActivity?.Id : null;
        this.FindControl<LanguageIconView>("ContinueLanguageIcon")!.LanguageKey = featured.LanguageKey;
        this.FindControl<TextBlock>("ContinueTrackTitle")!.Text = featured.Title;
        this.FindControl<TextBlock>("ContinueLessonTitle")!.Text = nextActivity is null
            ? "All course activities complete"
            : $"{nextModule?.Title ?? "Core lessons"} · {nextActivity.Title}";
        this.FindControl<ProgressBar>("ContinueProgress")!.Value = featured.CompletionPercent;
        this.FindControl<TextBlock>("ContinueProgressText")!.Text = featured.ProgressLabel;
        this.FindControl<Button>("ContinueButton")!.Content = !featured.Enrolled
            ? "View Track"
            : nextActivity?.TaskType == TaskType.Checkpoint
                ? "Open Checkpoint"
                : nextActivity is null
                    ? "Review Track"
                    : "Continue Lesson";

        // 2. To-Do Tasks
        AcademyTaskRow[] todoRows = dashboard.PrioritizedTasks.Select(t => new AcademyTaskRow(t)).ToArray();
        this.FindControl<ItemsControl>("HomeTodoList")!.ItemsSource = todoRows;
        this.FindControl<TextBlock>("TodoCountText")!.Text = $"{todoRows.Length} task{(todoRows.Length == 1 ? string.Empty : "s")}";
        this.FindControl<TextBlock>("TodoEmptyText")!.IsVisible = todoRows.Length == 0;

        // 3. Your Courses
        AcademyTrackRow[] enrolledCourses = tracks.Where(t => t.Enrolled).ToArray();
        this.FindControl<ItemsControl>("HomeCoursesList")!.ItemsSource = enrolledCourses;

        // 4. Hints & Learning Assistance
        HintUsageState hints = dashboard.HintUsage;
        if (hints.Mode == HintAssistanceMode.Unlimited)
        {
            this.FindControl<TextBlock>("HomeHintsRemainingText")!.Text = "Unlimited hints enabled (Accessibility Mode)";
            this.FindControl<TextBlock>("HomeHintsDots")!.Text = "∞  ∞  ∞  ∞  ∞";
        }
        else
        {
            int remaining = hints.RemainingHintsToday;
            int total = hints.DailyAllowance;
            this.FindControl<TextBlock>("HomeHintsRemainingText")!.Text = $"{remaining} of {total} hints remaining today";

            int filled = Math.Min(total, remaining);
            int empty = Math.Max(0, total - filled);
            string dots = string.Join(" ", Enumerable.Repeat("●", filled).Concat(Enumerable.Repeat("○", empty)));
            this.FindControl<TextBlock>("HomeHintsDots")!.Text = dots;
        }

        // 5. Recent Submissions
        AcademySubmissionRow[] submissionRows = dashboard.RecentSubmissions.Select(s => new AcademySubmissionRow(s)).ToArray();
        this.FindControl<ItemsControl>("HomeRecentSubmissions")!.ItemsSource = submissionRows;
        this.FindControl<TextBlock>("SubmissionsEmptyText")!.IsVisible = submissionRows.Length == 0;

        // 6. Recommendations
        AcademyProfile recommendationProfile = _state.Profile ?? DefaultProfile();
        this.FindControl<ItemsControl>("HomeRecommendations")!.ItemsSource = _academy.Recommendations
            .Recommend(recommendationProfile)
            .Take(3)
            .Select(recommendation => new AcademyRecommendationRow(
                recommendation.Track.Id,
                recommendation.Track.Title,
                recommendation.Explanation,
                LanguageRegistry.ForAcademyTrack(recommendation.Track.Id).Key))
            .ToArray();
    }

    private void RenderChallenges(AcademyDashboard dashboard)
    {
        var tasks = new List<AcademyTask>(dashboard.PrioritizedTasks);
        foreach (TrackDefinition track in _academy.Catalog.Tracks.Where(track =>
                     _state.EnrolledTrackIds.Contains(track.Id, StringComparer.OrdinalIgnoreCase)))
        {
            foreach (LessonDefinition lesson in track.Lessons.Where(lesson =>
                         lesson.Exercise is not null
                         && !_state.CompletedLessonIds.Contains(lesson.Id, StringComparer.OrdinalIgnoreCase)))
            {
                if (tasks.Any(task => string.Equals(
                    task.LessonId,
                    lesson.Id,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                SubmissionRecord? latest = _state.Submissions
                    .Where(submission => string.Equals(
                        submission.LessonId,
                        lesson.Id,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(submission => submission.SubmittedAt)
                    .FirstOrDefault();
                ModuleDefinition? module = track.Modules?.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, lesson.ModuleId, StringComparison.OrdinalIgnoreCase));
                tasks.Add(new AcademyTask(
                    $"challenge-{lesson.Id}",
                    lesson.Title,
                    track.Id,
                    track.Title,
                    module?.Id,
                    module?.Title,
                    lesson.Id,
                    lesson.TaskType is TaskType.CapstoneStep
                        ? TaskType.CapstoneStep
                        : TaskType.CodingExercise,
                    latest is { Passed: false }
                        ? TaskStatus.NeedsRevision
                        : TaskStatus.NotStarted,
                    lesson.EstimatedMinutes,
                    latest is { Passed: false } ? 2 : 5,
                    latest is { Passed: false }
                        ? "Review feedback and submit a corrected solution"
                        : "Pass the coding requirements and knowledge check",
                    0));
            }
        }

        AcademyTaskRow[] rows = tasks
            .GroupBy(task => $"{task.TrackId}:{task.LessonId}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(task => task.Priority).First())
            .OrderBy(task => task.Priority)
            .ThenBy(task => task.TrackTitle, StringComparer.OrdinalIgnoreCase)
            .Select(task => new AcademyTaskRow(task))
            .ToArray();
        this.FindControl<ItemsControl>("ChallengeList")!.ItemsSource = rows;
        this.FindControl<TextBlock>("ChallengesEmptyText")!.IsVisible = rows.Length == 0;
    }

    private void RenderSubmissions(AcademyDashboard dashboard)
    {
        AcademySubmissionRow[] rows = dashboard.RecentSubmissions
            .Select(submission => new AcademySubmissionRow(submission))
            .ToArray();
        this.FindControl<ItemsControl>("SubmissionList")!.ItemsSource = rows;
        this.FindControl<TextBlock>("SubmissionListEmptyText")!.IsVisible = rows.Length == 0;
    }

    private LessonDefinition? FindNextActivity(TrackDefinition track)
    {
        var assignedLessonIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ModuleDefinition module in track.Modules ?? [])
        {
            foreach (string lessonId in module.LessonIds)
            {
                assignedLessonIds.Add(lessonId);
                if (!_state.CompletedLessonIds.Contains(lessonId, StringComparer.OrdinalIgnoreCase))
                {
                    return _academy.GetActivity(track.Id, lessonId);
                }
            }

            if (module.Checkpoint is { } checkpoint
                && !_state.CompletedLessonIds.Contains(checkpoint.Id, StringComparer.OrdinalIgnoreCase))
            {
                return _academy.GetActivity(track.Id, checkpoint.Id);
            }
        }

        return track.Lessons.FirstOrDefault(lesson =>
            !assignedLessonIds.Contains(lesson.Id)
            && !_state.CompletedLessonIds.Contains(lesson.Id, StringComparer.OrdinalIgnoreCase));
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
        this.FindControl<ScrollViewer>("ChallengesPanel")!.IsVisible = selected == 4;
        this.FindControl<ScrollViewer>("SubmissionsPanel")!.IsVisible = selected == 5;
        this.FindControl<ScrollViewer>("ProfilePanel")!.IsVisible = selected == 6;
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
        enroll.IsVisible = !track.Enrolled;
        bool hasPrerequisite = track.Definition.RequiredToolchainId is not null;
        this.FindControl<Border>("PrerequisitePanel")!.IsVisible = hasPrerequisite;
        this.FindControl<TextBlock>("PrerequisiteText")!.Text = hasPrerequisite
            ? track.ToolchainLabel
            : string.Empty;
        this.FindControl<ItemsControl>("ModuleList")!.ItemsSource = CreateModuleRows(track);
    }

    private AcademyModuleRow[] CreateModuleRows(AcademyTrackRow track)
    {
        var lessonLookup = track.Definition.Lessons.ToDictionary(
            lesson => lesson.Id,
            StringComparer.OrdinalIgnoreCase);
        var assignedLessonIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<AcademyModuleRow>();

        foreach (ModuleDefinition module in track.Definition.Modules ?? [])
        {
            AcademyLessonRow[] lessons = module.LessonIds
                .Select(lessonId =>
                {
                    assignedLessonIds.Add(lessonId);
                    return lessonLookup.GetValueOrDefault(lessonId);
                })
                .Where(lesson => lesson is not null)
                .Select(lesson => CreateLessonRow(track, lesson!))
                .ToArray();
            bool checkpointCompleted = module.Checkpoint is { } checkpoint
                && _state.CompletedLessonIds.Contains(checkpoint.Id, StringComparer.OrdinalIgnoreCase);
            bool checkpointUnlocked = module.LessonIds.All(lessonId =>
                _state.CompletedLessonIds.Contains(lessonId, StringComparer.OrdinalIgnoreCase));
            AcademyCheckpointRow? checkpointRow = module.Checkpoint is null
                ? null
                : new AcademyCheckpointRow(
                    track.Id,
                    module.Checkpoint,
                    track.Enrolled,
                    checkpointCompleted,
                    checkpointUnlocked);
            int completedActivities = lessons.Count(lesson => lesson.Completed) + (checkpointCompleted ? 1 : 0);
            int totalActivities = lessons.Length + (checkpointRow is null ? 0 : 1);
            rows.Add(new AcademyModuleRow(
                rows.Count + 1,
                module.Title,
                module.Summary,
                lessons,
                checkpointRow,
                completedActivities,
                totalActivities));
        }

        AcademyLessonRow[] ungroupedLessons = track.Definition.Lessons
            .Where(lesson => !assignedLessonIds.Contains(lesson.Id))
            .Select(lesson => CreateLessonRow(track, lesson))
            .ToArray();
        if (ungroupedLessons.Length > 0)
        {
            rows.Add(new AcademyModuleRow(
                rows.Count + 1,
                rows.Count == 0 ? "Core Lessons" : "Additional Lessons",
                rows.Count == 0
                    ? track.Definition.Summary
                    : "Continue with the remaining activities in this track.",
                ungroupedLessons,
                null,
                ungroupedLessons.Count(lesson => lesson.Completed),
                ungroupedLessons.Length));
        }

        return rows.ToArray();
    }

    private AcademyLessonRow CreateLessonRow(AcademyTrackRow track, LessonDefinition lesson) =>
        new(
            track.Id,
            track.LanguageKey,
            lesson,
            track.Enrolled,
            _state.CompletedLessonIds.Contains(lesson.Id, StringComparer.OrdinalIgnoreCase),
            "Concepts: " + string.Join(", ", lesson.ConceptIds.Select(id => _academy.Catalog.GetConcept(id).Title)));

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
            if (_workspace is not null)
            {
                await _workspace.RefreshAcademyDashboardAsync();
            }
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
            if (_workspace is not null)
            {
                await _workspace.RefreshAcademyDashboardAsync();
            }
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

    private async void OpenCheckpoint_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: AcademyCheckpointRow checkpoint }
            || !checkpoint.CanOpen
            || _workspace is null)
        {
            return;
        }

        try
        {
            TrackDefinition track = _academy.Catalog.GetTrack(checkpoint.TrackId);
            await _workspace.OpenAcademyPracticeAsync(
                track,
                _academy.GetActivity(track.Id, checkpoint.Id));
        }
        catch (ArgumentException exception)
        {
            StudioLog.Warning($"Could not open Academy checkpoint {checkpoint.Id}: {exception.Message}");
            ShowTrack(checkpoint.TrackId);
        }
    }

    private async void Continue_Click(object? sender, RoutedEventArgs args)
    {
        if (_continueTrackId is not null
            && _continueActivityId is not null
            && _workspace is not null)
        {
            try
            {
                TrackDefinition track = _academy.Catalog.GetTrack(_continueTrackId);
                await _workspace.OpenAcademyPracticeAsync(
                    track,
                    _academy.GetActivity(track.Id, _continueActivityId));
            }
            catch (ArgumentException exception)
            {
                StudioLog.Warning($"Could not open Academy activity {_continueActivityId}: {exception.Message}");
                ShowTrack(_continueTrackId);
            }
        }
        else if (_continueTrackId is not null)
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

    private async void OpenTask_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: AcademyTaskRow row } && _workspace is not null)
        {
            TrackDefinition track = _academy.Catalog.GetTrack(row.Task.TrackId);
            try
            {
                await _workspace.OpenAcademyPracticeAsync(
                    track,
                    _academy.GetActivity(track.Id, row.Task.LessonId));
            }
            catch (ArgumentException exception)
            {
                StudioLog.Warning($"Could not open Academy activity {row.Task.LessonId}: {exception.Message}");
                ShowTrack(track.Id);
            }
        }
    }

    private void ReviewSubmission_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: AcademySubmissionRow row } && _workspace is not null)
        {
            _workspace.StatusText = $"{row.ExerciseTitle}: {row.StatusLabel} — {row.FeedbackSummary}";
        }
    }

    private async void HintSettings_Click(object? sender, RoutedEventArgs args)
    {
        HintUsageState current = _state.HintUsage ?? HintUsageState.Default;
        HintAssistanceMode nextMode = current.Mode switch
        {
            HintAssistanceMode.Standard => HintAssistanceMode.Reduced,
            HintAssistanceMode.Reduced => HintAssistanceMode.Unlimited,
            _ => HintAssistanceMode.Standard,
        };
        int allowance = nextMode switch
        {
            HintAssistanceMode.Reduced => 10,
            HintAssistanceMode.Unlimited => 999,
            _ => 5,
        };

        _state = await _academy.ConfigureHintSettingsAsync(nextMode, allowance);
        if (_workspace is not null)
        {
            _workspace.StatusText = $"Hint assistance mode set to: {nextMode} ({allowance} hints/day)";
        }
        await RefreshAsync(_selectedTrack?.Id);
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

public sealed record AcademyTaskRow(AcademyTask Task)
{
    public string Title => Task.Title;
    public string Subtitle => $"{Task.TrackTitle} · {Task.ModuleTitle}";
    // "CodingExercise" -> "Coding Exercise"
    public string TypeLabel => System.Text.RegularExpressions.Regex.Replace(Task.Type.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
    public string RequirementText => Task.CompletionRequirement;
    public string StatusGlyph => Task.Status switch
    {
        TaskStatus.Completed => "\uE73E",
        TaskStatus.NeedsRevision => "\uE711",
        TaskStatus.ReadyToSubmit => "\uE768",
        _ => "\uE739",
    };
    public string StatusBrush => Task.Status switch
    {
        TaskStatus.Completed => "#22c55e",
        TaskStatus.NeedsRevision => "#f59e0b",
        TaskStatus.ReadyToSubmit => "#3b82f6",
        _ => "#888888",
    };
    public string ActionLabel => Task.Status switch
    {
        TaskStatus.NeedsRevision => "Revise",
        TaskStatus.ReadyToSubmit => "Submit",
        TaskStatus.Completed => "Review",
        _ => "Start",
    };
}

public sealed record AcademySubmissionRow(SubmissionRecord Record)
{
    public string ExerciseTitle => Record.ExerciseTitle;
    public string Subtitle => $"Attempt {Record.AttemptNumber} · {Record.Type}";
    public string StatusGlyph => Record.Passed ? "\uE73E" : "\uE711";
    public string ScoreLabel => Record.Type is SubmissionType.Quiz or SubmissionType.MultipleChoice or SubmissionType.MultipleSelect
        ? $"{Record.Score}%"
        : $"{Record.Score}/10";
    public string StatusLabel => Record.Passed ? $"Passed ({ScoreLabel})" : $"Needs Revision ({ScoreLabel})";
    public string DateLabel => Record.SubmittedAt.LocalDateTime.ToString("MMM d, h:mm tt");
    public string FeedbackSummary => Record.Feedback;
    public string StatusBrush => Record.Passed ? "#22c55e" : "#f59e0b";
}

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

public sealed record AcademyModuleRow(
    int Number,
    string Title,
    string Summary,
    AcademyLessonRow[] Lessons,
    AcademyCheckpointRow? Checkpoint,
    int CompletedActivities,
    int TotalActivities)
{
    public string ModuleLabel => $"MODULE {Number}";
    public bool HasCheckpoint => Checkpoint is not null;
    public int CompletionPercent => TotalActivities == 0 ? 0 : CompletedActivities * 100 / TotalActivities;
    public string ProgressLabel => $"{CompletedActivities}/{TotalActivities} activities";
}

public sealed record AcademyCheckpointRow(
    string TrackId,
    CheckpointDefinition Definition,
    bool TrackEnrolled,
    bool Completed,
    bool Unlocked)
{
    public string Id => Definition.Id;
    public string Title => Definition.Title;
    public string Summary => Definition.Summary;
    public string StateGlyph => Completed ? "\uE73E" : Unlocked ? "\uE785" : "\uE72E";
    public string QuestionLabel =>
        $"{Definition.Questions.Length} review question{(Definition.Questions.Length == 1 ? string.Empty : "s")}";
    public string StatusLabel => Completed
        ? "Completed"
        : !TrackEnrolled
            ? "Enroll to start"
            : Unlocked
                ? "Ready"
                : "Complete the module lessons to unlock";
    public string ActionLabel => Completed ? "Review Checkpoint" : Unlocked ? "Open Checkpoint" : "Locked";
    public bool CanOpen => TrackEnrolled && (Completed || Unlocked);
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
    public string StateGlyph => Completed ? "\uE73E" : "\uE739";
    public bool RequiresSubmission => Definition.Exercise is { } exercise
        && (exercise.Assertions.Length > 0 || (exercise.Questions?.Length ?? 0) > 0);
    public string CompletionLabel => Completed ? "Completed" : "Complete Lesson";
    public bool CanComplete => TrackEnrolled && !Completed && !RequiresSubmission;
    public bool ShowCompleteAction => Completed || !RequiresSubmission;
    public bool CanPractice => TrackEnrolled;
    public string WorkspaceActionLabel => RequiresSubmission ? "Open Activity" : "Practice in Workspace";
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
