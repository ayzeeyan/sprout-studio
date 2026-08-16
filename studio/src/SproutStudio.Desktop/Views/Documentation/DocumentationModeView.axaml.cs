using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SproutStudio.Models;
using SproutStudio.Services;
using SproutStudio.ViewModels;
using SproutStudio.Views.Modes;

namespace SproutStudio.Views.Documentation;

public sealed partial class DocumentationModeView : UserControl, IStudioModeView
{
    private readonly List<DocumentationLocation> _history = [];
    private readonly Dictionary<string, Control> _headingTargets = new(StringComparer.OrdinalIgnoreCase);
    private DocumentationCatalog? _catalog;
    private DocumentationTopic? _currentTopic;
    private MainWindowViewModel? _workspace;
    private int _historyIndex = -1;
    private bool _synchronizingSelection;

    public DocumentationModeView()
    {
        InitializeComponent();
    }

    public async Task ActivateAsync(MainWindowViewModel workspace, CancellationToken cancellationToken = default)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        DataContext = workspace;
        if (_catalog is null)
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "Content", "Docs");
            try
            {
                _catalog = await DocumentationCatalog.LoadAsync(directory, cancellationToken);
                ListBox list = this.FindControl<ListBox>("TopicList")!;
                list.ItemsSource = _catalog.Topics;
                if (_catalog.Topics.Count > 0)
                {
                    await NavigateAsync(_catalog.Find("studio") ?? _catalog.Topics[0], null, addHistory: true);
                }
            }
            catch (Exception exception)
            {
                StudioLog.Error("Offline documentation failed to load.", exception);
                this.FindControl<StackPanel>("DocumentEmpty")!.Children.Add(new TextBlock
                {
                    Text = exception.Message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                });
            }
        }
        UpdateHistoryButtons();
    }

    public async Task OpenTopicAsync(string topicId, string? anchor = null)
    {
        if (_catalog?.Find(topicId) is { } topic)
        {
            await NavigateAsync(topic, anchor, addHistory: true);
        }
    }

    private async Task NavigateAsync(
        DocumentationTopic topic,
        string? anchor,
        bool addHistory)
    {
        _currentTopic = topic;
        var renderer = new AvaloniaMarkdownRenderer(HandleLinkAsync);
        MarkdownRenderResult result = renderer.Render(topic.Content);
        _headingTargets.Clear();
        foreach (RenderedDocumentationHeading heading in result.Headings)
        {
            _headingTargets[heading.Heading.Id] = heading.Target;
        }
        this.FindControl<ContentControl>("DocumentHost")!.Content = result.Content;
        this.FindControl<ItemsControl>("TableOfContents")!.ItemsSource = result.Headings
            .Where(item => item.Heading.Level <= 3)
            .Select(item => item.Heading)
            .ToArray();
        this.FindControl<TextBlock>("TopicSubtitle")!.Text = topic.Title;
        this.FindControl<StackPanel>("DocumentEmpty")!.IsVisible = false;
        this.FindControl<ScrollViewer>("DocumentScroll")!.IsVisible = true;
        this.FindControl<ScrollViewer>("DocumentScroll")!.Offset = default;
        _synchronizingSelection = true;
        this.FindControl<ListBox>("TopicList")!.SelectedItem = topic;
        _synchronizingSelection = false;

        if (addHistory)
        {
            if (_historyIndex + 1 < _history.Count)
            {
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            }
            _history.Add(new DocumentationLocation(topic.Id, anchor));
            if (_history.Count > 40)
            {
                _history.RemoveAt(0);
            }
            _historyIndex = _history.Count - 1;
        }
        UpdateHistoryButtons();
        if (!string.IsNullOrWhiteSpace(anchor))
        {
            await ScrollToAnchorAsync(anchor);
        }
    }

    private async Task HandleLinkAsync(string link)
    {
        if (_catalog is null || string.IsNullOrWhiteSpace(link))
        {
            return;
        }
        string decoded = Uri.UnescapeDataString(link);
        if (decoded.StartsWith('#'))
        {
            await ScrollToAnchorAsync(decoded[1..]);
            return;
        }
        string[] parts = decoded.Split('#', 2);
        if (parts[0].EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && _catalog.Find(parts[0]) is { } localTopic)
        {
            await NavigateAsync(localTopic, parts.ElementAtOrDefault(1), addHistory: true);
            return;
        }
        if (Uri.TryCreate(decoded, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "https" or "http")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private async Task ScrollToAnchorAsync(string anchor)
    {
        string normalized = anchor.Trim().TrimStart('#').ToLowerInvariant();
        if (_headingTargets.TryGetValue(normalized, out Control? target))
        {
            await Task.Yield();
            target.BringIntoView();
        }
    }

    private async void TopicList_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_synchronizingSelection
            && this.FindControl<ListBox>("TopicList")?.SelectedItem is DocumentationTopic topic)
        {
            await NavigateAsync(topic, null, addHistory: true);
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs args)
    {
        string query = this.FindControl<TextBox>("SearchBox")!.Text?.Trim() ?? string.Empty;
        ListBox topics = this.FindControl<ListBox>("TopicList")!;
        ListBox resultsList = this.FindControl<ListBox>("SearchResults")!;
        TextBlock empty = this.FindControl<TextBlock>("SearchEmpty")!;
        bool searching = query.Length > 0;
        IReadOnlyList<DocumentationSearchResult> results = searching && _catalog is not null
            ? _catalog.Search(query)
            : [];
        topics.IsVisible = !searching;
        resultsList.ItemsSource = results;
        resultsList.IsVisible = searching && results.Count > 0;
        empty.IsVisible = searching && results.Count == 0;
        this.FindControl<TextBlock>("NavigationHeader")!.Text = searching ? "SEARCH RESULTS" : "CONTENTS";
        this.FindControl<TextBlock>("SearchCount")!.Text = searching ? results.Count.ToString() : string.Empty;
    }

    private async void SearchResults_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (this.FindControl<ListBox>("SearchResults")?.SelectedItem is DocumentationSearchResult result)
        {
            await NavigateAsync(result.Topic, Slug(result.Section), addHistory: true);
        }
    }

    private async void Toc_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string anchor })
        {
            await ScrollToAnchorAsync(anchor);
        }
    }

    private async void Back_Click(object? sender, RoutedEventArgs args)
    {
        if (_catalog is null || _historyIndex <= 0)
        {
            return;
        }
        DocumentationLocation location = _history[--_historyIndex];
        if (_catalog.Find(location.TopicId) is { } topic)
        {
            await NavigateAsync(topic, location.Anchor, addHistory: false);
        }
    }

    private async void Forward_Click(object? sender, RoutedEventArgs args)
    {
        if (_catalog is null || _historyIndex + 1 >= _history.Count)
        {
            return;
        }
        DocumentationLocation location = _history[++_historyIndex];
        if (_catalog.Find(location.TopicId) is { } topic)
        {
            await NavigateAsync(topic, location.Anchor, addHistory: false);
        }
    }

    private void UpdateHistoryButtons()
    {
        this.FindControl<Button>("BackButton")!.IsEnabled = _historyIndex > 0;
        this.FindControl<Button>("ForwardButton")!.IsEnabled = _historyIndex + 1 < _history.Count;
        this.FindControl<Button>("ViewSourceButton")!.IsEnabled = _currentTopic is not null;
    }

    private async void ViewSource_Click(object? sender, RoutedEventArgs args)
    {
        if (_currentTopic is not null && _workspace is not null)
        {
            await _workspace.OpenDocumentationSourceAsync(_currentTopic.Path);
        }
    }

    private void OpenAcademy_Click(object? sender, RoutedEventArgs args) =>
        _workspace?.ActivateMode(ActivityDestination.Academy);

    private static string Slug(string value)
    {
        string slug = new(value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }
        return slug.Trim('-');
    }

    private sealed record DocumentationLocation(string TopicId, string? Anchor);
}
