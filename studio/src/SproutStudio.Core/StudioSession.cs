namespace SproutStudio.Core;

public sealed class StudioSession
{
    private readonly IEngineClient _engine;
    private readonly List<string> _consoleLines = [];
    private readonly List<StudioDiagnostic> _problems = [];

    public StudioSession(IEngineClient engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        Document = StudioDocument.Untitled();
    }

    public StudioDocument Document { get; private set; }

    public IReadOnlyList<string> ConsoleLines => _consoleLines;

    public IReadOnlyList<StudioDiagnostic> Problems => _problems;

    public bool IsRunning { get; private set; }

    public void NewDocument(SyntaxProfile profile = SyntaxProfile.Guided)
    {
        EnsureNotRunning();
        Document = StudioDocument.Untitled(profile);
        ClearResults();
    }

    public void UpdateSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Document = Document.WithSource(source);
    }

    public void UpdateProfile(SyntaxProfile profile)
    {
        Document = Document.WithProfile(profile);
    }

    public async Task LoadDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        EnsureNotRunning();
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);
        if (!string.Equals(Path.GetExtension(fullPath), ".spr", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Sprout Studio can only open .spr source files.", nameof(filePath));
        }

        string source = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        Document = new StudioDocument(Path.GetFileName(fullPath), fullPath, source, Document.Profile);
        ClearResults();
    }

    public async Task SaveDocumentAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotRunning();
        if (Document.FilePath is null)
        {
            throw new InvalidOperationException("Save requires a document with a file path.");
        }

        string temporaryPath = Document.FilePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, Document.Source, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, Document.FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task<EngineResponse> CheckAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(EngineOperation.Check, cancellationToken);

    public Task<EngineResponse> RunAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(EngineOperation.Run, cancellationToken);

    private async Task<EngineResponse> ExecuteAsync(
        EngineOperation operation,
        CancellationToken cancellationToken)
    {
        EnsureNotRunning();
        IsRunning = true;
        ClearResults();
        try
        {
            StudioDocument snapshot = Document;
            var request = new EngineRequest(
                operation,
                snapshot.Profile,
                snapshot.FilePath ?? snapshot.DisplayName,
                snapshot.Source);
            EngineResponse response = await _engine.ExecuteAsync(request, cancellationToken)
                .ConfigureAwait(false);
            _consoleLines.AddRange(response.Output);
            _problems.AddRange(response.Diagnostics);
            return response;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ClearResults()
    {
        _consoleLines.Clear();
        _problems.Clear();
    }

    private void EnsureNotRunning()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("An engine operation is already running.");
        }
    }
}
