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
    C,
    Cpp,
    CSharp,
    JavaScript,
    TypeScript,
    Json,
    Toml,
    Markdown,
    PlainText,
}

public sealed record LanguageDescriptor(
    DocumentLanguage Id,
    string Key,
    string DisplayName,
    string IconGlyph,
    string Accent,
    double IconScale,
    string[] Extensions,
    EditorCapability Capabilities,
    string? ToolchainId,
    string? DocumentationId,
    string[] AcademyTrackIds);

public static class LanguageRegistry
{
    private static readonly LanguageDescriptor[] Languages =
    [
        new(DocumentLanguage.Sprout, "sprout", "Sprout", "S", "#49C979", 1.0, [".spr"], EditorCapability.Inspect
            | EditorCapability.Format | EditorCapability.Check | EditorCapability.Run
            | EditorCapability.Debug | EditorCapability.Build, null, "syntax-profiles",
            ["sprout-fundamentals", "sprout-game-development", "computer-science-foundations"]),
        new(DocumentLanguage.Rust, "rust", "Rust", "Rs", "#D46B42", 0.86, [".rs"], EditorCapability.Format
            | EditorCapability.Run | EditorCapability.Build | EditorCapability.Test, "rust", "toolchains", ["rust-systems"]),
        new(DocumentLanguage.Go, "go", "Go", "Go", "#2AA9C4", 0.88, [".go"], EditorCapability.Format
            | EditorCapability.Run | EditorCapability.Build | EditorCapability.Test, "go", "toolchains", ["go-backend"]),
        new(DocumentLanguage.Python, "python", "Python", "Py", "#3776AB", 0.84, [".py"], EditorCapability.Run
            | EditorCapability.Build | EditorCapability.Test, "python", "python-interop", ["python-automation"]),
        new(DocumentLanguage.Java, "java", "Java", "J", "#E76F00", 0.96, [".java"], EditorCapability.Run
            | EditorCapability.Build, "java", "toolchains", []),
        new(DocumentLanguage.C, "c", "C", "C", "#5C6BC0", 1.0, [".c", ".h"], EditorCapability.None, null, null, []),
        new(DocumentLanguage.Cpp, "cpp", "C++", "C++", "#4D77B8", 0.72, [".cc", ".cpp", ".cxx", ".hpp", ".hxx"], EditorCapability.None, null, null, []),
        new(DocumentLanguage.CSharp, "csharp", "C#", "C#", "#8A5DC7", 0.82, [".cs"], EditorCapability.None, null, null, []),
        new(DocumentLanguage.JavaScript, "javascript", "JavaScript", "JS", "#C9AD2B", 0.82, [".js", ".mjs", ".cjs"], EditorCapability.None, null, null, []),
        new(DocumentLanguage.TypeScript, "typescript", "TypeScript", "TS", "#3178C6", 0.82, [".ts", ".tsx"], EditorCapability.None, null, null, []),
        new(DocumentLanguage.Json, "json", "JSON", "{}", "#7E8980", 0.72, [".json"], EditorCapability.None, null, null, []),
        new(DocumentLanguage.Toml, "toml", "TOML", "T", "#8B6F57", 0.94, [".toml"], EditorCapability.None, null, "projects", []),
        new(DocumentLanguage.Markdown, "markdown", "Markdown", "M", "#6F7782", 0.94, [".md"], EditorCapability.None, null, null, []),
        new(DocumentLanguage.PlainText, "text", "Plain Text", "·", "#687169", 1.0, [".txt"], EditorCapability.None, null, null, []),
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

    public static LanguageDescriptor Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return TryGet(key, out LanguageDescriptor? language)
            ? language!
            : throw new KeyNotFoundException($"Unknown language `{key}`.");
    }

    public static bool TryGet(string? key, out LanguageDescriptor? language)
    {
        language = string.IsNullOrWhiteSpace(key)
            ? null
            : Languages.FirstOrDefault(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.DisplayName, key, StringComparison.OrdinalIgnoreCase));
        return language is not null;
    }

    public static LanguageDescriptor ForAcademyTrack(string trackId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        return Languages.FirstOrDefault(language => language.AcademyTrackIds.Contains(
                trackId,
                StringComparer.OrdinalIgnoreCase))
            ?? Get(DocumentLanguage.Sprout);
    }

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
