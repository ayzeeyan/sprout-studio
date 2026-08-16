using System.Text.RegularExpressions;

namespace SproutStudio.Views.Documentation;

public sealed partial class DocumentationCatalog
{
    private readonly DocumentationTopic[] _topics;

    private DocumentationCatalog(IEnumerable<DocumentationTopic> topics)
    {
        _topics = topics.OrderBy(TopicOrder).ThenBy(topic => topic.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<DocumentationTopic> Topics => _topics;

    public static async Task<DocumentationCatalog> LoadAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string fullDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException($"Bundled documentation directory `{fullDirectory}` is missing.");
        }
        var topics = new List<DocumentationTopic>();
        foreach (string file in Directory.GetFiles(fullDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            string id = Path.GetFileNameWithoutExtension(file);
            string title = FirstHeading().Match(content) is { Success: true } match
                ? match.Groups[1].Value.Trim()
                : Humanize(id);
            topics.Add(new DocumentationTopic(id, title, file, content));
        }
        return new DocumentationCatalog(topics);
    }

    public DocumentationTopic? Find(string idOrFile) => _topics.FirstOrDefault(topic =>
        string.Equals(topic.Id, idOrFile, StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetFileName(topic.Path), Path.GetFileName(idOrFile), StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<DocumentationSearchResult> Search(string? query, int maximumResults = 80)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }
        string needle = query.Trim();
        var results = new List<DocumentationSearchResult>();
        foreach (DocumentationTopic topic in _topics)
        {
            string section = topic.Title;
            foreach (string sourceLine in topic.Content.Split('\n'))
            {
                string line = sourceLine.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    section = line.TrimStart('#').Trim();
                }
                if (line.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new DocumentationSearchResult(
                        topic,
                        section,
                        CleanExcerpt(line, needle)));
                    if (results.Count >= Math.Clamp(maximumResults, 1, 250))
                    {
                        return results;
                    }
                }
            }
        }
        return results;
    }

    private static int TopicOrder(DocumentationTopic topic) => topic.Id switch
    {
        "studio" => 0,
        "syntax-profiles" => 10,
        "grammar" => 11,
        "projects" => 20,
        "python-interop" => 30,
        "toolchains" => 40,
        "building" => 50,
        "academy" => 60,
        "advanced-ide" => 70,
        "architecture" => 80,
        "releasing" => 90,
        _ => 100,
    };

    private static string CleanExcerpt(string line, string needle)
    {
        string clean = MarkdownPunctuation().Replace(line, string.Empty).Trim();
        int index = clean.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        int start = Math.Max(0, index - 55);
        int length = Math.Min(clean.Length - start, 150);
        string excerpt = clean.Substring(start, length);
        return (start > 0 ? "…" : string.Empty) + excerpt + (start + length < clean.Length ? "…" : string.Empty);
    }

    private static string Humanize(string value) => string.Join(
        " ",
        value.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex FirstHeading();

    [GeneratedRegex(@"[`*_>#|\[\]()]", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownPunctuation();
}
