using System.IO.Enumeration;

namespace SproutStudio.Editor;

public sealed record ProjectSearchQuery(
    string Text,
    string FileFilter,
    bool MatchCase,
    bool WholeWord,
    int MaximumResults = 500);

public sealed record ProjectSearchResult(
    string FilePath,
    string RelativePath,
    int Line,
    int Column,
    string Preview);

public sealed class ProjectSearchService
{
    private const long MaximumFileSize = 2 * 1024 * 1024;
    private static readonly HashSet<string> IgnoredDirectories = new(
        [".git", ".sprout", "bin", "build", "obj", "target", "node_modules"],
        StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ProjectSearchResult>> SearchAsync(
        string projectRoot,
        ProjectSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(query);
        string root = Path.GetFullPath(projectRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Project directory `{root}` does not exist.");
        }
        string needle = query.Text.Trim();
        if (needle.Length == 0)
        {
            return [];
        }
        int limit = Math.Clamp(query.MaximumResults, 1, 2_000);
        StringComparison comparison = query.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        string[] filters = query.FileFilter.Split(
            [',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<ProjectSearchResult>(Math.Min(limit, 128));

        foreach (string file in EnumerateProjectFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MatchesFilter(file, filters) || new FileInfo(file).Length > MaximumFileSize)
            {
                continue;
            }
            await SearchFileAsync(file).ConfigureAwait(false);
            if (results.Count >= limit)
            {
                break;
            }
        }
        return results;

        async Task SearchFileAsync(string file)
        {
            using var reader = new StreamReader(file, detectEncodingFromByteOrderMarks: true);
            int lineNumber = 0;
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                lineNumber++;
                int searchStart = 0;
                while (searchStart <= line.Length - needle.Length)
                {
                    int index = line.IndexOf(needle, searchStart, comparison);
                    if (index < 0)
                    {
                        break;
                    }
                    if (!query.WholeWord || IsWholeWord(line, index, needle.Length))
                    {
                        results.Add(new ProjectSearchResult(
                            file,
                            Path.GetRelativePath(root, file),
                            lineNumber,
                            index + 1,
                            line.Trim()));
                        if (results.Count >= limit)
                        {
                            return;
                        }
                    }
                    searchStart = index + Math.Max(1, needle.Length);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            IEnumerable<string> directories;
            IEnumerable<string> files;
            try
            {
                directories = Directory.EnumerateDirectories(directory).ToArray();
                files = Directory.EnumerateFiles(directory).ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (string child in directories)
            {
                var info = new DirectoryInfo(child);
                if (!IgnoredDirectories.Contains(info.Name)
                    && !info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    pending.Push(child);
                }
            }
            foreach (string file in files.Order(StringComparer.OrdinalIgnoreCase))
            {
                if (LanguageRegistry.IsSupportedPath(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static bool MatchesFilter(string file, IReadOnlyList<string> filters)
    {
        if (filters.Count == 0)
        {
            return true;
        }
        string name = Path.GetFileName(file);
        return filters.Any(filter => FileSystemName.MatchesSimpleExpression(
            filter,
            name,
            ignoreCase: true));
    }

    private static bool IsWholeWord(string line, int start, int length)
    {
        bool leftBoundary = start == 0 || !IsWordCharacter(line[start - 1]);
        int end = start + length;
        bool rightBoundary = end == line.Length || !IsWordCharacter(line[end]);
        return leftBoundary && rightBoundary;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';
}
