using Avalonia.Controls;
using Avalonia.Interactivity;
using SproutStudio.Academy;
using SproutStudio.Editor;
using SproutStudio.Services;
using SproutStudio.Views.Documentation;

namespace SproutStudio.Views.Academy;

/// <summary>
/// The Academy Handbook: beginner-friendly guides with a one-click switch to the full
/// documentation page, reading progress, and links into the lessons that practice each guide.
/// </summary>
public sealed partial class AcademyModeView
{
    private AvaloniaMarkdownRenderer? _guideRenderer;
    private DocumentationCatalog? _documentation;
    private bool _showFullReference;
    private bool _renderingHandbook;

    /// <summary>Opens the Handbook focused on one guide (used by lessons and the Learn inspector).</summary>
    public void SelectGuide(string guideId)
    {
        SelectSectionPanel("handbook");
        RenderHandbook(guideId);
    }

    private void RenderHandbook(string? selectGuideId = null)
    {
        ListBox list = this.FindControl<ListBox>("GuideList")!;
        string? selectedId = selectGuideId ?? (list.SelectedItem as HandbookGuideRow)?.Guide.Id;
        HandbookGuideRow[] rows = _academy.Catalog.Guides
            .Select(guide => new HandbookGuideRow(guide, _state.ReadGuides.FirstOrDefault(read =>
                string.Equals(read.GuideId, guide.Id, StringComparison.OrdinalIgnoreCase))?.ReadAt))
            .ToArray();
        _renderingHandbook = true;
        list.ItemsSource = rows;
        list.SelectedItem = rows.FirstOrDefault(row => string.Equals(row.Guide.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        _renderingHandbook = false;

        int read = rows.Count(row => row.IsRead);
        this.FindControl<ProgressBar>("HandbookProgress")!.Value = rows.Length == 0 ? 0 : read * 100 / rows.Length;
        this.FindControl<TextBlock>("HandbookProgressText")!.Text = $"{read} of {rows.Length} read";
        if (list.SelectedItem is HandbookGuideRow selected)
        {
            _ = ShowGuideAsync(selected);
        }
    }

    private void EnsureGuideSelected()
    {
        ListBox list = this.FindControl<ListBox>("GuideList")!;
        if (list.ItemsSource is null)
        {
            RenderHandbook();
        }
        if (list.SelectedItem is null && list.ItemsSource is IEnumerable<HandbookGuideRow> rows)
        {
            HandbookGuideRow[] all = rows.ToArray();
            // Start with the first guide the learner hasn't read yet.
            list.SelectedItem = all.FirstOrDefault(row => !row.IsRead) ?? all.FirstOrDefault();
        }
    }

    private void GuideList_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_renderingHandbook || this.FindControl<ListBox>("GuideList")?.SelectedItem is not HandbookGuideRow row)
        {
            return;
        }
        _showFullReference = false;
        _ = ShowGuideAsync(row);
        this.FindControl<ScrollViewer>("GuideScroll")?.ScrollToHome();
    }

    private async Task ShowGuideAsync(HandbookGuideRow row)
    {
        GuideDefinition guide = row.Guide;
        this.FindControl<TextBlock>("GuideEyebrow")!.Text = _showFullReference && guide.ReferenceDocId is { } referenceId
            ? $"FULL REFERENCE · {referenceId.ToUpperInvariant()}.MD"
            : $"{guide.Level.ToUpperInvariant()} · {guide.Minutes} MIN READ" + (row.IsRead ? " · READ ✓" : string.Empty);
        this.FindControl<TextBlock>("GuideTitle")!.Text = guide.Title;
        this.FindControl<TextBlock>("GuideSummary")!.Text = guide.Summary;
        this.FindControl<Button>("GuideEasyButton")!.Classes.Set("selected", !_showFullReference);
        this.FindControl<Button>("GuideFullButton")!.Classes.Set("selected", _showFullReference);
        this.FindControl<Button>("GuideFullButton")!.IsEnabled = guide.ReferenceDocId is not null;

        _guideRenderer ??= new AvaloniaMarkdownRenderer(OpenGuideLinkAsync);
        string markdown = _academy.Catalog.GetGuideContent(guide.Id);
        if (_showFullReference && guide.ReferenceDocId is { } docId)
        {
            DocumentationTopic? topic = (await LoadDocumentationAsync())?.Find(docId);
            markdown = topic?.Content
                ?? $"The full reference page `{docId}` is not available in this installation.";
        }
        this.FindControl<ContentControl>("GuideContentHost")!.Content = _guideRenderer.Render(WithoutTitle(markdown)).Content;
        RenderGuideFooter(row);
    }

    private void RenderGuideFooter(HandbookGuideRow row)
    {
        bool gamified = _state.Profile?.ShowGamification ?? true;
        bool hasPractice = _academy.Catalog.Tracks.SelectMany(track => track.Lessons)
            .Any(lesson => lesson.ConceptIds.Intersect(row.Guide.ConceptIds, StringComparer.OrdinalIgnoreCase).Any());
        this.FindControl<TextBlock>("GuideFooterTitle")!.Text = row.IsRead ? "You've read this guide" : "Finished reading?";
        this.FindControl<TextBlock>("GuideFooterText")!.Text = row.IsRead
            ? $"Read on {row.ReadAt!.Value.LocalDateTime:MMMM d}." + (hasPractice ? " Put it into practice with a lesson below." : string.Empty)
            : gamified
                ? "Mark it as read to track your Handbook progress. Reading counts toward today's streak."
                : "Mark it as read to track your Handbook progress.";
        this.FindControl<Button>("GuideMarkReadButton")!.IsVisible = !row.IsRead;

        var completed = _state.CompletedLessonIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        GuidePracticeRow[] practice = _academy.Catalog.Tracks
            .SelectMany(track => track.Lessons.Select(lesson => (Track: track, Lesson: lesson)))
            .Where(item => item.Lesson.ConceptIds.Intersect(row.Guide.ConceptIds, StringComparer.OrdinalIgnoreCase).Any())
            .OrderBy(item => completed.Contains(item.Lesson.Id))
            .ThenByDescending(item => _state.EnrolledTrackIds.Contains(item.Track.Id, StringComparer.OrdinalIgnoreCase))
            .ThenBy(item => item.Track.RequiredToolchainId is null ? 0 : 1)
            .Take(3)
            .Select(item => new GuidePracticeRow(
                item.Track,
                item.Lesson,
                LanguageRegistry.ForAcademyTrack(item.Track.Id).Key,
                completed.Contains(item.Lesson.Id)))
            .ToArray();
        this.FindControl<ItemsControl>("GuidePracticeList")!.ItemsSource = practice;

        Button next = this.FindControl<Button>("GuideNextButton")!;
        int index = Array.FindIndex(_academy.Catalog.Guides, guide => guide.Id == row.Guide.Id);
        GuideDefinition? following = index >= 0 && index + 1 < _academy.Catalog.Guides.Length
            ? _academy.Catalog.Guides[index + 1]
            : null;
        next.IsVisible = following is not null;
        next.Content = following is null ? null : $"Next guide: {following.Title}  →";
        next.Tag = following?.Id;
    }

    private async void GuideMarkRead_Click(object? sender, RoutedEventArgs args)
    {
        if (this.FindControl<ListBox>("GuideList")?.SelectedItem is not HandbookGuideRow row)
        {
            return;
        }
        try
        {
            var result = await _academy.MarkGuideReadAsync(row.Guide.Id);
            _state = result.State;
            // Re-render the whole Academy so progress, streak, and badges reflect the read.
            await RefreshAsync(_selectedTrack?.Id);
            RenderHandbook(row.Guide.Id);
            if (_workspace is not null)
            {
                // Refreshing the shared dashboard updates the status bar and raises celebrations.
                await _workspace.RefreshAcademyDashboardAsync();
                if (result.FirstRead)
                {
                    _workspace.StatusText = $"Read \"{row.Guide.Title}\"";
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or KeyNotFoundException)
        {
            StudioLog.Error("Could not record Handbook reading progress.", exception);
            this.FindControl<TextBlock>("GuideFooterText")!.Text = $"Reading progress could not be saved: {exception.Message}";
        }
    }

    private async void GuideEasy_Click(object? sender, RoutedEventArgs args) => await SwitchGuideModeAsync(full: false);

    private async void GuideFull_Click(object? sender, RoutedEventArgs args) => await SwitchGuideModeAsync(full: true);

    private async Task SwitchGuideModeAsync(bool full)
    {
        if (this.FindControl<ListBox>("GuideList")?.SelectedItem is HandbookGuideRow row && _showFullReference != full)
        {
            _showFullReference = full;
            await ShowGuideAsync(row);
            this.FindControl<ScrollViewer>("GuideScroll")?.ScrollToHome();
        }
    }

    private async void GuidePractice_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: GuidePracticeRow practice } && _workspace is not null)
        {
            await _workspace.OpenAcademyPracticeAsync(practice.Track, practice.Lesson);
        }
    }

    private void GuideNext_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string guideId })
        {
            RenderHandbook(guideId);
            this.FindControl<ScrollViewer>("GuideScroll")?.ScrollToHome();
        }
    }

    private Task OpenGuideLinkAsync(string target)
    {
        // Links between documentation pages open in the Documentation mode.
        string file = target.Split('#')[0];
        if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && _workspace is not null)
        {
            string? anchor = target.Contains('#') ? target[(target.IndexOf('#') + 1)..] : null;
            _workspace.RequestDocumentation(Path.GetFileNameWithoutExtension(file), anchor);
        }
        return Task.CompletedTask;
    }

    private async Task<DocumentationCatalog?> LoadDocumentationAsync()
    {
        if (_documentation is null)
        {
            try
            {
                _documentation = await DocumentationCatalog.LoadAsync(Path.Combine(AppContext.BaseDirectory, "Content", "Docs"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                StudioLog.Warning("Bundled documentation could not be loaded for the Handbook.", exception);
            }
        }
        return _documentation;
    }

    /// <summary>The reader shows the title in its own header, so drop the document's first H1.</summary>
    private static string WithoutTitle(string markdown)
    {
        string trimmed = markdown.TrimStart();
        if (!trimmed.StartsWith("# ", StringComparison.Ordinal))
        {
            return markdown;
        }
        int newline = trimmed.IndexOf('\n');
        return newline < 0 ? string.Empty : trimmed[(newline + 1)..];
    }
}

public sealed record HandbookGuideRow(GuideDefinition Guide, DateTimeOffset? ReadAt)
{
    public string Title => Guide.Title;

    public bool IsRead => ReadAt is not null;

    public string MetaText => $"{Guide.Minutes} min · {Guide.Level}" + (IsRead ? " · Read" : string.Empty);
}

public sealed record GuidePracticeRow(TrackDefinition Track, LessonDefinition Lesson, string LanguageKey, bool Completed)
{
    public string TrackTitle => Track.Title;

    public string Label => Completed ? $"Review: {Lesson.Title}" : $"Practice: {Lesson.Title}";
}
