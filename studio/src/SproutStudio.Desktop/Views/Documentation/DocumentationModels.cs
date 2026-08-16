namespace SproutStudio.Views.Documentation;

public sealed record DocumentationTopic(
    string Id,
    string Title,
    string Path,
    string Content);

public sealed record DocumentationHeading(
    string Id,
    string Title,
    int Level);

public sealed record DocumentationSearchResult(
    DocumentationTopic Topic,
    string Section,
    string Excerpt)
{
    public string Title => Topic.Title;
}
