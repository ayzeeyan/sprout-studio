using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SproutStudio.Academy;
using SproutStudio.Toolchains;

namespace SproutStudio;

internal sealed class AcademyDialog
{
    private readonly AcademyService _academy;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ContentDialog _dialog;
    private readonly ListView _trackList = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        DisplayMemberPath = nameof(TrackDefinition.Title),
    };
    private readonly StackPanel _recommendations = new() { Spacing = 7 };
    private readonly StackPanel _trackDetail = new() { Spacing = 10 };
    private readonly TextBlock _learnStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _conceptSearch = new()
    {
        PlaceholderText = "Search concepts or languages",
        Header = "Concept browser",
    };
    private readonly ListView _conceptList = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        DisplayMemberPath = nameof(ConceptDefinition.Title),
    };
    private readonly StackPanel _conceptDetail = new() { Spacing = 10 };
    private readonly StackPanel _progressContent = new() { Spacing = 12 };
    private readonly AcademyProfileForm _profileForm = new();
    private readonly TextBlock _profileStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly AcademyPrerequisitePanel _prerequisite;
    private AcademyState _state;
    private TrackDefinition? _selectedTrack;

    public AcademyDialog(
        AcademyService academy,
        ToolchainManager toolchains,
        AcademyState initialState,
        nint windowHandle)
    {
        _academy = academy ?? throw new ArgumentNullException(nameof(academy));
        _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        if (_state.Profile is null)
        {
            throw new ArgumentException("Academy profile must be created before opening the Academy.", nameof(initialState));
        }
        _prerequisite = new AcademyPrerequisitePanel(
            toolchains,
            windowHandle,
            _lifetime.Token);

        var tabs = new TabView
        {
            IsAddTabButtonVisible = false,
            MinWidth = 900,
            Height = 660,
        };
        tabs.TabItems.Add(new TabViewItem { Header = "Learn", Content = BuildLearnTab() });
        tabs.TabItems.Add(new TabViewItem { Header = "Concepts", Content = BuildConceptsTab() });
        tabs.TabItems.Add(new TabViewItem { Header = "Progress", Content = BuildProgressTab() });
        tabs.TabItems.Add(new TabViewItem { Header = "Profile", Content = BuildProfileTab() });

        _dialog = new ContentDialog
        {
            Title = "Sprout Academy",
            Content = tabs,
            CloseButtonText = "Close",
        };
        _dialog.Resources["ContentDialogMaxWidth"] = 1040d;
        _dialog.Opened += (_, _) => Opened?.Invoke(this, EventArgs.Empty);
        _dialog.Closed += (_, _) => _lifetime.Cancel();

        _trackList.ItemsSource = _academy.Catalog.Tracks;
        _trackList.SelectionChanged += TrackList_SelectionChanged;
        _conceptSearch.TextChanged += ConceptSearch_TextChanged;
        _conceptList.SelectionChanged += ConceptList_SelectionChanged;
        RefreshAll();

        TrackDefinition first = _academy.Recommendations
            .Recommend(_state.Profile)
            .FirstOrDefault()?.Track
            ?? _academy.Catalog.Tracks[0];
        _trackList.SelectedItem = first;
        RefreshConceptSearch();
    }

    public event EventHandler? Opened;

    public async Task ShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        _dialog.XamlRoot = xamlRoot;
        await _dialog.ShowAsync();
    }

    private UIElement BuildLearnTab()
    {
        var root = new Grid { Padding = new Thickness(12), RowSpacing = 10 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var recommendationCard = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(30, 58, 167, 109)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = _recommendations,
        };
        root.Children.Add(recommendationCard);

        var learning = new Grid { ColumnSpacing = 14 };
        learning.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        learning.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(learning, 1);
        learning.Children.Add(_trackList);
        var detailScroll = new ScrollViewer
        {
            Content = _trackDetail,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(4, 0, 6, 0),
        };
        Grid.SetColumn(detailScroll, 1);
        learning.Children.Add(detailScroll);
        root.Children.Add(learning);

        Grid.SetRow(_learnStatus, 2);
        root.Children.Add(_learnStatus);
        return root;
    }

    private UIElement BuildConceptsTab()
    {
        var root = new Grid { Padding = new Thickness(12), ColumnSpacing = 14 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var browser = new Grid { RowSpacing = 8 };
        browser.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        browser.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        browser.Children.Add(_conceptSearch);
        Grid.SetRow(_conceptList, 1);
        browser.Children.Add(_conceptList);
        root.Children.Add(browser);

        var details = new ScrollViewer
        {
            Content = _conceptDetail,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(4, 0, 6, 0),
        };
        Grid.SetColumn(details, 1);
        root.Children.Add(details);
        return root;
    }

    private UIElement BuildProgressTab() => new ScrollViewer
    {
        Content = _progressContent,
        Padding = new Thickness(16),
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };

    private UIElement BuildProfileTab()
    {
        var content = new StackPanel
        {
            Spacing = 12,
            MaxWidth = 620,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        content.Children.Add(new TextBlock
        {
            Text = "Local Academy profile",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        content.Children.Add(_profileForm.Root);
        var save = new Button { Content = "Save profile" };
        save.Click += SaveProfile_Click;
        content.Children.Add(save);
        content.Children.Add(_profileStatus);
        return new ScrollViewer
        {
            Content = content,
            Padding = new Thickness(16),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private void RefreshAll()
    {
        if (_state.Profile is not AcademyProfile profile)
        {
            return;
        }
        _profileForm.Load(profile);
        RenderRecommendations(profile);
        RenderTrackDetails();
        RenderProgress();
    }

    private void RenderRecommendations(AcademyProfile profile)
    {
        _recommendations.Children.Clear();
        _recommendations.Children.Add(new TextBlock
        {
            Text = "Recommended learning path · transparent rules",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        foreach (TrackRecommendation recommendation in _academy.Recommendations.Recommend(profile))
        {
            _recommendations.Children.Add(new TextBlock
            {
                Text = $"{recommendation.Track.Title} — {recommendation.Explanation} {recommendation.Rule}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.78,
            });
        }
    }

    private void TrackList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        _selectedTrack = _trackList.SelectedItem as TrackDefinition;
        _prerequisite.Hide();
        RenderTrackDetails();
    }

    private void RenderTrackDetails()
    {
        _trackDetail.Children.Clear();
        if (_selectedTrack is not TrackDefinition track)
        {
            _trackDetail.Children.Add(new TextBlock { Text = "Select a track to begin." });
            return;
        }
        AcademyDashboard dashboard = _academy.CreateDashboard(_state);
        TrackProgress progress = dashboard.Tracks.Single(item => item.Track.Id == track.Id);
        _trackDetail.Children.Add(new TextBlock
        {
            Text = track.Title,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        _trackDetail.Children.Add(new TextBlock
        {
            Text = track.Summary,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78,
        });
        _trackDetail.Children.Add(new TextBlock
        {
            Text = track.RequiredToolchainId is null
                ? "Prerequisite: included with Sprout Studio"
                : $"Prerequisite: validated {track.RequiredToolchainId} toolchain (checked only when you enroll)",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        });
        _trackDetail.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = progress.CompletionPercent,
        });
        _trackDetail.Children.Add(new TextBlock
        {
            Text = $"{progress.CompletedLessons} of {progress.TotalLessons} lessons complete",
            Opacity = 0.7,
        });
        var enroll = new Button
        {
            Content = progress.Enrolled ? "Enrolled" : "Enroll",
            IsEnabled = !progress.Enrolled,
        };
        enroll.Click += async (_, _) => await EnrollTrackAsync(track);
        _trackDetail.Children.Add(enroll);
        _trackDetail.Children.Add(_prerequisite.Root);

        _trackDetail.Children.Add(new TextBlock
        {
            Text = "Lessons",
            FontSize = 19,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0),
        });
        foreach (LessonDefinition lesson in track.Lessons)
        {
            bool completed = _state.CompletedLessonIds.Contains(
                lesson.Id,
                StringComparer.OrdinalIgnoreCase);
            var lessonContent = new StackPanel { Spacing = 6 };
            lessonContent.Children.Add(new TextBlock
            {
                Text = $"{(completed ? "✓ " : string.Empty)}{lesson.Title} · {lesson.Xp} XP",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            lessonContent.Children.Add(new TextBlock
            {
                Text = lesson.Summary,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.78,
            });
            lessonContent.Children.Add(new TextBlock
            {
                Text = lesson.Content,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            });
            lessonContent.Children.Add(new TextBlock
            {
                Text = "Concepts: " + string.Join(
                    ", ",
                    lesson.ConceptIds.Select(id => _academy.Catalog.GetConcept(id).Title)),
                Opacity = 0.68,
            });
            var complete = new Button
            {
                Content = completed ? "Completed" : "Mark lesson complete",
                IsEnabled = progress.Enrolled && !completed,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            complete.Click += async (_, _) => await CompleteLessonAsync(track, lesson);
            lessonContent.Children.Add(complete);
            _trackDetail.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(90, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12),
                Child = lessonContent,
            });
        }
    }

    private async Task EnrollTrackAsync(TrackDefinition track)
    {
        _learnStatus.Text = $"Checking prerequisites for {track.Title}…";
        try
        {
            EnrollmentResult result = await _academy.EnrollAsync(track.Id, _lifetime.Token);
            if (result.Status == EnrollmentStatus.ToolchainRequired
                && result.Toolchain is ToolchainDetectionResult detection)
            {
                _learnStatus.Text = detection.Message;
                _prerequisite.Show(track, detection, () => EnrollTrackAsync(track));
                return;
            }
            _prerequisite.Hide();
            _state = await _academy.LoadStateAsync(_lifetime.Token);
            _learnStatus.Text = result.Status == EnrollmentStatus.Enrolled
                ? $"Enrolled in {track.Title}."
                : $"You are already enrolled in {track.Title}.";
            RefreshAll();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _learnStatus.Text = exception.Message;
        }
    }

    private async Task CompleteLessonAsync(TrackDefinition track, LessonDefinition lesson)
    {
        _learnStatus.Text = $"Saving progress for {lesson.Title}…";
        try
        {
            LessonCompletionResult result = await _academy.CompleteLessonAsync(
                track.Id,
                lesson.Id,
                _lifetime.Token);
            _state = await _academy.LoadStateAsync(_lifetime.Token);
            string achievements = result.NewAchievements.Length == 0
                ? string.Empty
                : " Earned: " + string.Join(", ", result.NewAchievements.Select(item => item.Title)) + ".";
            _learnStatus.Text = result.AlreadyCompleted
                ? $"{lesson.Title} was already complete; no additional XP was awarded."
                : $"Completed {lesson.Title} and earned {result.AwardedXp} XP.{achievements}";
            RefreshAll();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _learnStatus.Text = exception.Message;
        }
    }

    private void ConceptSearch_TextChanged(object sender, TextChangedEventArgs args) =>
        RefreshConceptSearch();

    private void RefreshConceptSearch()
    {
        IReadOnlyList<ConceptDefinition> concepts = _academy.Catalog.SearchConcepts(_conceptSearch.Text);
        _conceptList.ItemsSource = concepts;
        _conceptList.SelectedIndex = concepts.Count == 0 ? -1 : 0;
        if (concepts.Count == 0)
        {
            _conceptDetail.Children.Clear();
            _conceptDetail.Children.Add(new TextBlock
            {
                Text = "No concepts match this search.",
                Opacity = 0.72,
            });
        }
    }

    private void ConceptList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        _conceptDetail.Children.Clear();
        if (_conceptList.SelectedItem is not ConceptDefinition concept)
        {
            return;
        }
        _conceptDetail.Children.Add(new TextBlock
        {
            Text = concept.Title,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        _conceptDetail.Children.Add(new TextBlock
        {
            Text = concept.Summary,
            TextWrapping = TextWrapping.Wrap,
        });
        _conceptDetail.Children.Add(new TextBlock
        {
            Text = "Related: " + string.Join(
                ", ",
                concept.RelatedConceptIds.Select(id => _academy.Catalog.GetConcept(id).Title)),
            Opacity = 0.7,
        });
        foreach (LanguageExample example in concept.Examples)
        {
            var exampleContent = new StackPanel { Spacing = 5 };
            exampleContent.Children.Add(new TextBlock
            {
                Text = example.Language,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            exampleContent.Children.Add(new TextBlock
            {
                Text = example.Code,
                FontFamily = new FontFamily("Consolas"),
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.Wrap,
            });
            _conceptDetail.Children.Add(new Border
            {
                Background = new SolidColorBrush(ColorHelper.FromArgb(35, 128, 128, 128)),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12),
                Child = exampleContent,
            });
        }
    }

    private void RenderProgress()
    {
        _progressContent.Children.Clear();
        AcademyDashboard dashboard = _academy.CreateDashboard(_state);
        _progressContent.Children.Add(new TextBlock
        {
            Text = "Course progress",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        foreach (TrackProgress progress in dashboard.Tracks)
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock
            {
                Text = $"{progress.Track.Title} · {progress.CompletedLessons}/{progress.TotalLessons}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            row.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = progress.CompletionPercent,
            });
            _progressContent.Children.Add(row);
        }

        if (_state.Profile?.ShowGamification != true)
        {
            _progressContent.Children.Add(new TextBlock
            {
                Text = "XP, levels, and achievements are hidden by your profile preference. Course completion remains visible above.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
                Margin = new Thickness(0, 12, 0, 0),
            });
            return;
        }
        _progressContent.Children.Add(new TextBlock
        {
            Text = $"Level {dashboard.Level} · {dashboard.TotalXp} meaningful-learning XP",
            FontSize = 19,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 0),
        });
        _progressContent.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = dashboard.XpTowardNextLevel,
        });
        _progressContent.Children.Add(new TextBlock
        {
            Text = "Achievements",
            FontSize = 19,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 0),
        });
        var earned = dashboard.EarnedAchievements
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (AchievementDefinition achievement in _academy.Catalog.Achievements)
        {
            _progressContent.Children.Add(new TextBlock
            {
                Text = $"{(earned.Contains(achievement.Id) ? "✓" : "○")} {achievement.Title} — {achievement.Description}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = earned.Contains(achievement.Id) ? 1 : 0.58,
            });
        }
    }

    private async void SaveProfile_Click(object sender, RoutedEventArgs args)
    {
        _profileStatus.Text = "Saving local profile…";
        try
        {
            _state = await _academy.SaveProfileAsync(
                _profileForm.ReadProfile(),
                _lifetime.Token);
            _profileStatus.Text = "Profile saved locally.";
            RefreshAll();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _profileStatus.Text = exception.Message;
        }
    }
}
