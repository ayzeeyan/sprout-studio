namespace SproutStudio.SourceControl;

using SproutStudio.Toolchains;

public sealed record GitFileChange(
    string Path,
    string? OriginalPath,
    char IndexStatus,
    char WorkingTreeStatus)
{
    public bool IsStaged => IndexStatus is not '.' and not '?';

    public bool IsUnstaged => WorkingTreeStatus != '.' || IndexStatus == '?';

    public char DisplayStatus(bool staged) => staged ? IndexStatus : WorkingTreeStatus;

    public string DisplayStatusLabel(bool staged) => StatusLabel(DisplayStatus(staged));

    public static string StatusLabel(char status) => status switch
    {
        'M' => "Modified",
        'A' => "Added",
        'D' => "Deleted",
        'R' => "Renamed",
        'C' => "Copied",
        'T' => "Type changed",
        'U' => "Conflict",
        '?' => "Untracked",
        _ => "Changed",
    };
}

public sealed record GitRepositoryState(
    bool IsRepository,
    string RepositoryRoot,
    string Branch,
    string? Upstream,
    int Ahead,
    int Behind,
    bool HasRemote,
    IReadOnlyList<GitFileChange> Changes,
    string Message)
{
    public static GitRepositoryState NotRepository(string path, string message) =>
        new(false, Path.GetFullPath(path), "No repository", null, 0, 0, false, [], message);

    public string BranchSummary => !IsRepository
        ? "No repository"
        : $"{Branch}{(Ahead > 0 ? $" ↑{Ahead}" : string.Empty)}{(Behind > 0 ? $" ↓{Behind}" : string.Empty)}";
}

public sealed class GitOperationException : InvalidOperationException
{
    public GitOperationException(string operation, ProcessResult result)
        : base(Format(operation, result))
    {
        Operation = operation;
        Result = result;
    }

    public string Operation { get; }

    public ProcessResult Result { get; }

    private static string Format(string operation, ProcessResult result)
    {
        string detail = result.StartError
            ?? result.StandardError.Trim()
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = result.TimedOut
                ? "The Git process timed out."
                : $"Git exited with code {result.ExitCode?.ToString() ?? "unknown"}.";
        }
        return $"Git {operation} failed. {detail}";
    }
}

public sealed class GitRepositoryService
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromMinutes(2);
    private readonly IProcessRunner _runner;

    public GitRepositoryService(IProcessRunner? runner = null)
    {
        _runner = runner ?? new ProcessRunner();
    }

    public async Task<GitRepositoryState> GetStatusAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string workingDirectory = Path.GetFullPath(path);
        ProcessResult rootResult = await RunAsync(
            workingDirectory,
            ["rev-parse", "--show-toplevel"],
            QueryTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!rootResult.Success)
        {
            string message = rootResult.StartError
                ?? rootResult.StandardError.Trim();
            return GitRepositoryState.NotRepository(
                workingDirectory,
                string.IsNullOrWhiteSpace(message) ? "This project is not a Git repository." : message);
        }

        string root = rootResult.StandardOutput.Trim();
        ProcessResult statusResult = await RunRequiredAsync(
            "status",
            root,
            ["-c", "core.quotepath=false", "status", "--porcelain=v2", "--branch", "-z", "--untracked-files=normal"],
            QueryTimeout,
            cancellationToken).ConfigureAwait(false);
        ProcessResult remotesResult = await RunRequiredAsync(
            "remote query",
            root,
            ["remote"],
            QueryTimeout,
            cancellationToken).ConfigureAwait(false);
        return GitStatusParser.Parse(
            root,
            statusResult.StandardOutput,
            remotesResult.StandardOutput.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length > 0);
    }

    public Task<ProcessResult> StageAsync(
        string repositoryRoot,
        string path,
        CancellationToken cancellationToken = default) =>
        RunRequiredAsync("stage", repositoryRoot, ["add", "--", path], QueryTimeout, cancellationToken);

    public Task<ProcessResult> StageAllAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default) =>
        RunRequiredAsync("stage all", repositoryRoot, ["add", "--all"], QueryTimeout, cancellationToken);

    public async Task<ProcessResult> UnstageAsync(
        string repositoryRoot,
        string path,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunAsync(
            repositoryRoot,
            ["restore", "--staged", "--", path],
            QueryTimeout,
            cancellationToken).ConfigureAwait(false);
        return result.Success
            ? result
            : await RunRequiredAsync(
                "unstage",
                repositoryRoot,
                ["reset", "--", path],
                QueryTimeout,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessResult> UnstageAllAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunAsync(
            repositoryRoot,
            ["restore", "--staged", ":/"],
            QueryTimeout,
            cancellationToken).ConfigureAwait(false);
        return result.Success
            ? result
            : await RunRequiredAsync(
                "unstage all",
                repositoryRoot,
                ["reset"],
                QueryTimeout,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetDiffAsync(
        string repositoryRoot,
        string path,
        bool staged,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "-c", "core.quotepath=false", "diff", "--no-ext-diff", "--unified=3" };
        if (staged)
        {
            arguments.Add("--cached");
        }
        arguments.Add("--");
        arguments.Add(path);
        ProcessResult result = await RunRequiredAsync(
            "diff",
            repositoryRoot,
            arguments,
            QueryTimeout,
            cancellationToken).ConfigureAwait(false);
        if (result.StandardOutput.Length > 2_000_000)
        {
            return result.StandardOutput[..2_000_000] + Environment.NewLine
                + "… Diff truncated at 2 MB to protect Studio memory.";
        }
        return result.StandardOutput;
    }

    public Task<ProcessResult> CommitAsync(
        string repositoryRoot,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return RunRequiredAsync(
            "commit",
            repositoryRoot,
            ["commit", "-m", message.Trim()],
            NetworkTimeout,
            cancellationToken);
    }

    public Task<ProcessResult> PushAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default) =>
        RunRequiredAsync("push", repositoryRoot, ["push"], NetworkTimeout, cancellationToken);

    public Task<ProcessResult> PullAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default) =>
        RunRequiredAsync("pull", repositoryRoot, ["pull", "--ff-only"], NetworkTimeout, cancellationToken);

    public Task<ProcessResult> FetchAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default) =>
        RunRequiredAsync("fetch", repositoryRoot, ["fetch", "--prune"], NetworkTimeout, cancellationToken);

    private Task<ProcessResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        _runner.RunAsync(new ProcessRequest(
            "git",
            arguments,
            Path.GetFullPath(workingDirectory),
            timeout), cancellationToken);

    private async Task<ProcessResult> RunRequiredAsync(
        string operation,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ProcessResult result = await RunAsync(
            workingDirectory,
            arguments,
            timeout,
            cancellationToken).ConfigureAwait(false);
        return result.Success ? result : throw new GitOperationException(operation, result);
    }
}

public static class GitStatusParser
{
    public static GitRepositoryState Parse(string repositoryRoot, string porcelain, bool hasRemote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(porcelain);
        string branch = "HEAD";
        string? upstream = null;
        int ahead = 0;
        int behind = 0;
        var changes = new List<GitFileChange>();
        string[] records = porcelain.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < records.Length; index++)
        {
            string record = records[index];
            if (record.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branch = record[14..];
            }
            else if (record.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                upstream = record[18..];
            }
            else if (record.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                string[] values = record[12..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                ahead = ParseCount(values.ElementAtOrDefault(0));
                behind = ParseCount(values.ElementAtOrDefault(1));
            }
            else if (record.StartsWith("1 ", StringComparison.Ordinal))
            {
                string[] fields = record.Split(' ', 9);
                if (fields.Length == 9)
                {
                    changes.Add(Change(fields[8], null, fields[1]));
                }
            }
            else if (record.StartsWith("2 ", StringComparison.Ordinal))
            {
                string[] fields = record.Split(' ', 10);
                if (fields.Length == 10)
                {
                    string? original = index + 1 < records.Length ? records[++index] : null;
                    changes.Add(Change(fields[9], original, fields[1]));
                }
            }
            else if (record.StartsWith("u ", StringComparison.Ordinal))
            {
                string[] fields = record.Split(' ', 11);
                if (fields.Length == 11)
                {
                    changes.Add(Change(fields[10], null, fields[1]));
                }
            }
            else if (record.StartsWith("? ", StringComparison.Ordinal))
            {
                changes.Add(new GitFileChange(record[2..], null, '?', '?'));
            }
        }
        return new GitRepositoryState(
            true,
            Path.GetFullPath(repositoryRoot),
            branch == "(detached)" ? "Detached HEAD" : branch,
            upstream,
            ahead,
            behind,
            hasRemote,
            changes.OrderBy(change => change.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            changes.Count == 0 ? "Working tree clean." : $"{changes.Count} changed file{(changes.Count == 1 ? string.Empty : "s")}.");
    }

    private static GitFileChange Change(string path, string? original, string xy)
    {
        char index = xy.Length > 0 ? xy[0] : '.';
        char workingTree = xy.Length > 1 ? xy[1] : '.';
        return new GitFileChange(path, original, index, workingTree);
    }

    private static int ParseCount(string? value) =>
        int.TryParse(value?.TrimStart('+', '-'), out int count) ? count : 0;
}
