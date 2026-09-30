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

public enum DocumentKind
{
    Source,
    AcademyLesson,
    AcademyExercise,
    AcademyQuiz,
    Documentation,
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
    string[] AcademyTrackIds,
    string? IconPathData = null);

public static class LanguageRegistry
{
    private static readonly LanguageDescriptor[] Languages =
    [
        new(DocumentLanguage.Sprout, "sprout", "Sprout", "S", "#49C979", 1.0, [".spr"], EditorCapability.Inspect
            | EditorCapability.Format | EditorCapability.Check | EditorCapability.Run
            | EditorCapability.Debug | EditorCapability.Build, null, "syntax-profiles",
            ["sprout-fundamentals", "sprout-game-development", "computer-science-foundations"],
            "M18,2 C9,3 4,8 4,15 C4,18 5,20 7,22 C7,16 10,10 17,6 C12,11 9,17 8,23 C15,22 20,16 20,9 C20,6 19,4 18,2 Z"),
        new(DocumentLanguage.Java, "java", "Java", "J", "#E76F00", 1.0, [".java"], EditorCapability.Run
            | EditorCapability.Build, "java", "toolchains", ["java-oop-zero-to-hero"],
            "M4,19h14a1,1 0 0,0 1,-1v-1H4v1a1,1 0 0,0 1,1zm15,-10h-2V6a1,1 0 0,0 -1,-1H4a1,1 0 0,0 -1,1v8a4,4 0 0,0 4,4h6a4,4 0 0,0 4,-4v-1h2a3,3 0 0,0 3,-3v-2a3,3 0 0,0 -3,-3zm1,5a1,1 0 0,1 -1,1h-2V7h2a1,1 0 0,1 1,1v4zM7,1c-.5,1 .5,2 0,3s-1,2 -.5,3M11,1c-.5,1 .5,2 0,3s-1,2 -.5,3"),
        new(DocumentLanguage.Rust, "rust", "Rust", "Rs", "#DEA584", 0.95, [".rs"], EditorCapability.Format
            | EditorCapability.Run | EditorCapability.Build | EditorCapability.Test, "rust", "toolchains", ["rust-systems"],
            "M12,2a10,10 0 1,0 10,10A10,10 0 0,0 12,2zm0,2a8,8 0 0,1 7.4,5h-1.6a6,6 0 0,0 -1.8,-1.8v1.6a8,8 0 0,1 0,6.4v-1.6a6,6 0 0,0 1.8,-1.8h1.6A8,8 0 0,1 12,20a8,8 0 0,1 -7.4,-5h1.6a6,6 0 0,0 1.8,1.8v-1.6a8,8 0 0,1 0,-6.4v1.6a6,6 0 0,0 -1.8,1.8H4.6A8,8 0 0,1 12,4zm-2,4h4a3,3 0 0,1 3,3c0,1.3 -.8,2.4 -2,2.8l2,3.2h-2.5l-1.7,-2.8H12v2.8h-2V8zm2,2v2h2a1,1 0 0,0 1,-1 1,1 0 0,0 -1,-1h-2z"),
        new(DocumentLanguage.Python, "python", "Python", "Py", "#3776AB", 0.95, [".py"], EditorCapability.Run
            | EditorCapability.Build | EditorCapability.Test, "python", "python-interop", ["python-automation"],
            "M11.9,2C6.8,2,7.2,4.2,7.2,4.2l0,2.3h4.8v0.7H5.2c0,0-3.2,0.4-3.2,5.1s2.8,4.9,2.8,4.9h1.7v-2.4c0-2.7,2.3-2.6,2.3-2.6h4.7c2.3,0,2.2-2.2,2.2-2.2V4.4C15.8,4.4,16.2,2,11.9,2z M9.1,3.6c0.5,0,0.9,0.4,0.9,0.9c0,0.5-0.4,0.9-0.9,0.9c-0.5,0-0.9-0.4-0.9-0.9C8.2,4,8.6,3.6,9.1,3.6z M12.1,22c5.1,0,4.7-2.2,4.7-2.2l0-2.3H12v-0.7h6.8c0,0,3.2-0.4,3.2-5.1s-2.8-4.9-2.8-4.9h-1.7v2.4c0,2.7-2.3,2.6-2.3,2.6H10.5c-2.3,0-2.2,2.2-2.2,2.2v4.6C8.2,19.6,7.8,22,12.1,22z M14.9,20.4c-0.5,0-0.9-0.4-0.9-0.9c0-0.5,0.4-0.9,0.9-0.9c0.5,0,0.9,0.4,0.9,0.9C15.8,20,15.4,20.4,14.9,20.4z"),
        new(DocumentLanguage.Go, "go", "Go", "Go", "#00ACD7", 0.82, [".go"], EditorCapability.Format
            | EditorCapability.Run | EditorCapability.Build | EditorCapability.Test, "go", "toolchains", ["go-backend"],
            "M1.2,12 a5.2,5.2 0 1,0 10.4,0 a5.2,5.2 0 1,0 -10.4,0 M3.5,12 a2.9,2.9 0 1,0 5.8,0 a2.9,2.9 0 1,0 -5.8,0 M6.4,10.6 h5.2 v2.8 h-5.2 Z M12.4,12 a5.2,5.2 0 1,0 10.4,0 a5.2,5.2 0 1,0 -10.4,0 M14.7,12 a2.9,2.9 0 1,0 5.8,0 a2.9,2.9 0 1,0 -5.8,0"),
        new(DocumentLanguage.C, "c", "C", "C", "#5C6BC0", 1.0, [".c", ".h"], EditorCapability.None, null, null, [],
            "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 21.2,15.5H16.8A6,6 0 1,1 16.8,8.5H21.2A10,10 0 0,0 12,2Z"),
        new(DocumentLanguage.Cpp, "cpp", "C++", "C++", "#00599C", 0.95, [".cc", ".cpp", ".cxx", ".hpp", ".hxx"], EditorCapability.None, null, null, [],
            "M9,4A8,8 0 0,0 1,12A8,8 0 0,0 9,20A8,8 0 0,0 16.5,14.8H13A5,5 0 1,1 13,9.2H16.5A8,8 0 0,0 9,4ZM16,10V11H15V13H16V14H18V13H19V11H18V10ZM20,10V11H19V13H20V14H22V13H23V11H22V10Z"),
        new(DocumentLanguage.CSharp, "csharp", "C#", "C#", "#8A5DC7", 0.95, [".cs"], EditorCapability.None, null, null, [],
            "M9,4A8,8 0 0,0 1,12A8,8 0 0,0 9,20A8,8 0 0,0 16.5,14.8H13A5,5 0 1,1 13,9.2H16.5A8,8 0 0,0 9,4ZM17,9H16V11H14V12H16V14H14V15H16V17H17V15H19V17H20V15H22V14H20V12H22V11H20V9H19V11H17ZM17,12H19V14H17Z"),
        new(DocumentLanguage.JavaScript, "javascript", "JavaScript", "JS", "#F7DF1E", 0.95, [".js", ".mjs", ".cjs"], EditorCapability.None, null, null, [],
            "M3,3H21V21H3V3M12,17.5A2,2 0 0,0 14,15.5V11H12.5V15.5A0.5,0.5 0 0,1 12,16A0.5,0.5 0 0,1 11.5,15.5V14H10V15.5A2,2 0 0,0 12,17.5M16.5,17.5C18,17.5 19,16.5 19,15.5C19,14 18,13.5 16.5,13C15.5,12.5 15,12.3 15,11.8C15,11.3 15.5,11 16.5,11C17.3,11 17.8,11.3 18,12H19.5C19.3,10.5 18,9.5 16.5,9.5C15,9.5 13.5,10.5 13.5,11.8C13.5,13.3 14.5,13.8 16,14.3C17,14.8 17.5,15 17.5,15.5C17.5,16 17,16.2 16.5,16.2C15.5,16.2 15,15.7 14.8,15H13.2C13.5,16.5 14.8,17.5 16.5,17.5Z"),
        new(DocumentLanguage.TypeScript, "typescript", "TypeScript", "TS", "#3178C6", 0.95, [".ts", ".tsx"], EditorCapability.None, null, null, [],
            "M3,3H21V21H3V3M10.5,10.5H5.5V9H12.5V10.5H10.5V17H8.5V10.5M15.5,17.5C17,17.5 18,16.5 18,15.5C18,14 17,13.5 15.5,13C14.5,12.5 14,12.3 14,11.8C14,11.3 14.5,11 15.5,11C16.3,11 16.8,11.3 17,12H18.5C18.3,10.5 17,9.5 15.5,9.5C14,9.5 12.5,10.5 12.5,11.8C12.5,13.3 13.5,13.8 15,14.3C16,14.8 16.5,15 16.5,15.5C16.5,16 16,16.2 15.5,16.2C14.5,16.2 14,15.7 13.8,15H12.2C12.5,16.5 13.8,17.5 15.5,17.5Z"),
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
