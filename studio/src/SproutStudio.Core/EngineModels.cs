using System.Text;

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
    string Source,
    string? ProjectRoot = null);

public sealed record SourceSpan(
    string File,
    long Start,
    long End,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);

public sealed record DiagnosticRelatedInformation(
    string Message,
    SourceSpan Span);

public sealed record DiagnosticFix(
    string Title,
    string Replacement,
    SourceSpan Span);

public sealed record StudioDiagnostic(
    string Code,
    string Severity,
    string Title,
    string Message,
    SourceSpan Span,
    IReadOnlyList<string> Suggestions,
    IReadOnlyList<DiagnosticRelatedInformation> RelatedInformation,
    IReadOnlyList<DiagnosticFix> OptionalFixes);

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

public static class SourceTextCoordinates
{
    public static int Utf8OffsetToUtf16Index(string source, long byteOffset)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (byteOffset <= 0)
        {
            return 0;
        }

        byte[] utf8 = Encoding.UTF8.GetBytes(source);
        int boundedOffset = (int)Math.Min(byteOffset, utf8.LongLength);
        return Encoding.UTF8.GetCharCount(utf8, 0, boundedOffset);
    }
}
