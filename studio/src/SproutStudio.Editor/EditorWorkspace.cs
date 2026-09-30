using SproutStudio.Core;

namespace SproutStudio.Editor;

public sealed class EditorDocument
{
    internal EditorDocument(
        string displayName,
        string? filePath,
        string source,
        LanguageDescriptor language,
        SyntaxProfile profile,
        string? projectRoot)
    {
        Id = Guid.NewGuid();
        DisplayName = displayName;
        FilePath = filePath;
        Source = source;
        SavedSource = source;
        Language = language;
        Profile = profile;
        ProjectRoot = projectRoot;
    }

    public Guid Id { get; }

    public string DisplayName { get; internal set; }

    public string? FilePath { get; internal set; }

    public string Source { get; private set; }

    public string SavedSource { get; private set; }

    public LanguageDescriptor Language { get; internal set; }

    public SyntaxProfile Profile { get; set; }

    public string? ProjectRoot { get; internal set; }

    public DocumentKind Kind { get; internal set; } = DocumentKind.Source;

    public string? AcademyTrackId { get; internal set; }

    public string? AcademyLessonId { get; internal set; }

    public string? LinkedCompanionPath { get; internal set; }

    public bool IsSource => Kind == DocumentKind.Source;

    public bool IsAcademy => Kind is DocumentKind.AcademyLesson or DocumentKind.AcademyExercise or DocumentKind.AcademyQuiz;

    public bool IsDocumentation => Kind == DocumentKind.Documentation;

    public bool IsDirty => !string.Equals(Source, SavedSource, StringComparison.Ordinal);

    public string TabTitle => IsDirty ? $"{DisplayName} ●" : DisplayName;

    public void UpdateSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
    }

    internal void MarkSaved()
    {
        SavedSource = Source;
    }
}

public sealed class EditorWorkspace
{
    private readonly List<EditorDocument> _documents = [];

    public IReadOnlyList<EditorDocument> Documents => _documents;

    public EditorDocument CreateSproutDocument(SyntaxProfile profile = SyntaxProfile.Guided)
    {
        var document = new EditorDocument(
            "untitled.spr",
            null,
            "has name = \"Sprout\"\nprint(\"Hello \" + name)\n",
            LanguageRegistry.Get(DocumentLanguage.Sprout),
            profile,
            null);
        _documents.Add(document);
        return document;
    }

    public EditorDocument CreateDocument(string fileName, string source)
    {
        var lang = LanguageRegistry.ForPath(fileName);
        var document = new EditorDocument(
            fileName,
            null,
            source,
            lang,
            SyntaxProfile.Guided,
            null);
        _documents.Add(document);
        return document;
    }

    public EditorDocument OpenOrCreateAcademyDocument(
        string trackId,
        string lessonId,
        string title,
        LanguageDescriptor language,
        bool isExercise,
        bool isQuiz = false,
        string? linkedCompanionPath = null)
    {
        string academyUri = $"academy://tracks/{trackId}/lessons/{lessonId}";
        EditorDocument? existing = _documents.FirstOrDefault(document => string.Equals(
            document.FilePath,
            academyUri,
            StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        DocumentKind kind = isQuiz
            ? DocumentKind.AcademyQuiz
            : isExercise
                ? DocumentKind.AcademyExercise
                : DocumentKind.AcademyLesson;
        string displayName = isQuiz
            ? $"{title} · Quiz"
            : isExercise
                ? $"{title} · Exercise"
                : $"{title} · Lesson";
        var doc = new EditorDocument(
            displayName,
            academyUri,
            string.Empty,
            language,
            SyntaxProfile.Guided,
            null)
        {
            Kind = kind,
            AcademyTrackId = trackId,
            AcademyLessonId = lessonId,
            LinkedCompanionPath = linkedCompanionPath,
        };
        _documents.Add(doc);
        return doc;
    }

    public async Task<EditorDocument> OpenAsync(
        string filePath,
        SyntaxProfile sproutProfile = SyntaxProfile.Guided,
        string? projectRoot = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);
        EditorDocument? existing = _documents.FirstOrDefault(document => string.Equals(
            document.FilePath,
            fullPath,
            StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The editor file does not exist.", fullPath);
        }
        if (!LanguageRegistry.IsSupportedPath(fullPath))
        {
            throw new NotSupportedException(
                $"Sprout Studio does not support `{Path.GetExtension(fullPath)}` files yet.");
        }
        string source = await File.ReadAllTextAsync(fullPath, cancellationToken)
            .ConfigureAwait(false);
        var document = new EditorDocument(
            Path.GetFileName(fullPath),
            fullPath,
            source,
            LanguageRegistry.ForPath(fullPath),
            sproutProfile,
            projectRoot is null ? null : Path.GetFullPath(projectRoot));
        _documents.Add(document);
        return document;
    }

    public async Task SaveAsync(
        EditorDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FilePath is null)
        {
            throw new InvalidOperationException("Save As is required for an untitled document.");
        }
        await SaveToAsync(document, document.FilePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsAsync(
        EditorDocument document,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);
        if (!LanguageRegistry.IsSupportedPath(fullPath))
        {
            throw new NotSupportedException(
                $"Sprout Studio cannot save `{Path.GetExtension(fullPath)}` files yet.");
        }
        await SaveToAsync(document, fullPath, cancellationToken).ConfigureAwait(false);
        document.FilePath = fullPath;
        document.DisplayName = Path.GetFileName(fullPath);
        document.Language = LanguageRegistry.ForPath(fullPath);
    }

    public bool Close(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _documents.Remove(document);
    }

    public void CloseAll() => _documents.Clear();

    private static async Task SaveToAsync(
        EditorDocument document,
        string filePath,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string temporaryPath = $"{filePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, document.Source, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, filePath, overwrite: true);
            document.MarkSaved();
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

public sealed record WorkspaceFile(string DisplayName, string RelativePath, string FullPath);

public static class WorkspaceScanner
{
    private const int MaximumFiles = 5_000;
    private static readonly HashSet<string> IgnoredDirectories = new(
        [".git", ".sprout", "build", "target", "bin", "obj", "node_modules"],
        StringComparer.OrdinalIgnoreCase);

    public static Task<IReadOnlyList<WorkspaceFile>> ScanAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string root = Path.GetFullPath(rootPath);
        return Task.Run<IReadOnlyList<WorkspaceFile>>(
            () => Scan(root, cancellationToken),
            cancellationToken);
    }

    private static IReadOnlyList<WorkspaceFile> Scan(
        string root,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Workspace root `{root}` does not exist.");
        }
        var result = new List<WorkspaceFile>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string path in Directory.EnumerateFileSystemEntries(directory)
                .Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileAttributes attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (!IgnoredDirectories.Contains(Path.GetFileName(path)))
                    {
                        pending.Push(path);
                    }
                    continue;
                }
                if (!LanguageRegistry.IsSupportedPath(path))
                {
                    continue;
                }
                string relative = Path.GetRelativePath(root, path);
                result.Add(new WorkspaceFile(Path.GetFileName(path), relative, path));
                if (result.Count > MaximumFiles)
                {
                    throw new InvalidOperationException(
                        $"Workspace contains more than {MaximumFiles} supported files; narrow the workspace before opening it.");
                }
            }
        }
        return result.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
