using Avalonia.Controls;
using SproutStudio.Services;

namespace SproutStudio.Views.Documentation;

public sealed partial class DocumentationWindow : Window
{
    private IReadOnlyList<DocumentationTopic> _topics = [];

    public DocumentationWindow()
    {
        InitializeComponent();
        Opened += async (_, _) => await LoadTopicsAsync();
    }

    private async Task LoadTopicsAsync()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Content", "Docs");
        try
        {
            string[] files = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
                : [];
            var topics = new List<DocumentationTopic>();
            foreach (string file in files.Order(StringComparer.OrdinalIgnoreCase))
            {
                string content = await File.ReadAllTextAsync(file);
                string title = content.Split('\n')
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?[2..]
                    ?? Humanize(Path.GetFileNameWithoutExtension(file));
                topics.Add(new DocumentationTopic(title, file, content));
            }
            _topics = topics;
            ApplyFilter(string.Empty);
        }
        catch (Exception exception)
        {
            StudioLog.Error("Offline documentation failed to load.", exception);
            this.FindControl<TextBox>("DocumentText")!.Text = exception.Message;
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs args) =>
        ApplyFilter(this.FindControl<TextBox>("SearchBox")?.Text ?? string.Empty);

    private void ApplyFilter(string query)
    {
        DocumentationTopic[] filtered = _topics
            .Where(topic => string.IsNullOrWhiteSpace(query)
                || topic.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || topic.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        ListBox list = this.FindControl<ListBox>("TopicList")!;
        list.ItemsSource = filtered;
        list.SelectedIndex = filtered.Length == 0 ? -1 : 0;
    }

    private void TopicList_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (this.FindControl<ListBox>("TopicList")?.SelectedItem is not DocumentationTopic topic)
        {
            return;
        }
        this.FindControl<TextBlock>("TopicTitle")!.Text = topic.Title;
        this.FindControl<TextBox>("DocumentText")!.Text = topic.Content;
    }

    private static string Humanize(string value) => string.Join(
        " ",
        value.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));

    private sealed record DocumentationTopic(string Title, string Path, string Content);
}
