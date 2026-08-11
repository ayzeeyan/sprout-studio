namespace SproutStudio.Core;

public enum EngineOperation
{
    Check = 0,
    Run = 1,
}

public enum SyntaxProfile
{
    Guided = 0,
    Structured = 1,
    Advanced = 2,
}

public sealed record EngineRequest(
    EngineOperation Operation,
    SyntaxProfile Profile,
    string SourceName,
    string Source);

public sealed record SourceSpan(
    string File,
    long Start,
    long End,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);

public sealed record StudioDiagnostic(
    string Code,
    string Severity,
    string Title,
    string Message,
    SourceSpan Span,
    IReadOnlyList<string> Suggestions);

public sealed record EngineResponse(
    int ApiVersion,
    bool Success,
    IReadOnlyList<string> Output,
    IReadOnlyList<StudioDiagnostic> Diagnostics);

public interface IEngineClient
{
    Task<EngineResponse> ExecuteAsync(
        EngineRequest request,
        CancellationToken cancellationToken = default);
}

