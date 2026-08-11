using SproutStudio.Core;

namespace SproutStudio.Editor;

[Flags]
public enum EditorCapability
{
    None = 0,
    Inspect = 1,
    Format = 2,
    Check = 4,
    Run = 8,
    Debug = 16,
    Build = 32,
    Test = 64,
}

public enum DocumentLanguage
{
    Sprout,
    Rust,
    Go,
    Python,
    Java,
    JavaScript,
    TypeScript,
    Json,
    Toml,
    Markdown,
    PlainText,
}

public sealed record LanguageDescriptor(
    DocumentLanguage Id,
    string DisplayName,
    string[] Extensions,
    EditorCapability Capabilities,
    string? ToolchainId);

public static class LanguageRegistry
{
    private static readonly LanguageDescriptor[] Languages =
    [
        new(DocumentLanguage.Sprout, "Sprout", [".spr"], EditorCapability.Inspect
            | EditorCapability.Format | EditorCapability.Check | EditorCapability.Run
            | EditorCapability.Debug | EditorCapability.Build, null),
        new(DocumentLanguage.Rust, "Rust", [".rs"], EditorCapability.Format
            | EditorCapability.Run | EditorCapability.Build | EditorCapability.Test, "rust"),
        new(DocumentLanguage.Go, "Go", [".go"], EditorCapability.Format
            | EditorCapability.Run | EditorCapability.Build | EditorCapability.Test, "go"),
        new(DocumentLanguage.Python, "Python", [".py"], EditorCapability.None, null),
        new(DocumentLanguage.Java, "Java", [".java"], EditorCapability.None, null),
        new(DocumentLanguage.JavaScript, "JavaScript", [".js", ".mjs", ".cjs"], EditorCapability.None, null),
        new(DocumentLanguage.TypeScript, "TypeScript", [".ts", ".tsx"], EditorCapability.None, null),
        new(DocumentLanguage.Json, "JSON", [".json"], EditorCapability.None, null),
        new(DocumentLanguage.Toml, "TOML", [".toml"], EditorCapability.None, null),
        new(DocumentLanguage.Markdown, "Markdown", [".md"], EditorCapability.None, null),
        new(DocumentLanguage.PlainText, "Plain Text", [".txt"], EditorCapability.None, null),
    ];

    public static IReadOnlyList<LanguageDescriptor> All => Languages;

    public static LanguageDescriptor ForPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string extension = Path.GetExtension(path);
        return Languages.FirstOrDefault(language => language.Extensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase))
            ?? Languages[^1];
    }

    public static LanguageDescriptor Get(DocumentLanguage id) =>
        Languages.Single(language => language.Id == id);

    public static bool IsSupportedPath(string path)
    {
        string extension = Path.GetExtension(path);
        return Languages.Any(language => language.Extensions.Contains(
            extension,
            StringComparer.OrdinalIgnoreCase));
    }
}

public enum LanguageServiceOperation
{
    Inspect,
    Format,
}

public sealed record LanguageServiceRequest(
    LanguageServiceOperation Operation,
    SyntaxProfile Profile,
    string SourceName,
    string Source,
    int CursorUtf16Index = 0);

public sealed record CompletionSuggestion(
    string Label,
    string Kind,
    string Detail,
    string InsertText,
    SourceSpan ReplacementSpan);

public sealed record HoverInformation(
    string Title,
    string Contents,
    SourceSpan Span);

public sealed record DefinitionLocation(
    string Name,
    string Kind,
    SourceSpan Span);

public sealed record DocumentSymbol(
    string Name,
    string Kind,
    string Detail,
    SourceSpan Span,
    IReadOnlyList<DocumentSymbol> Children);

public sealed record LanguageServiceResponse(
    int ApiVersion,
    bool Success,
    IReadOnlyList<StudioDiagnostic> Diagnostics,
    IReadOnlyList<CompletionSuggestion> Completions,
    HoverInformation? Hover,
    DefinitionLocation? Definition,
    IReadOnlyList<DocumentSymbol> Symbols,
    string? FormattedSource);

public interface ISproutLanguageService
{
    Task<LanguageServiceResponse> QueryAsync(
        LanguageServiceRequest request,
        CancellationToken cancellationToken = default);
}
