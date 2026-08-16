namespace SproutStudio.Editor;

public static class EditorBreadcrumbs
{
    public static string Format(string displayName, string? filePath, string? projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            string relativePath = Path.GetRelativePath(projectRoot, filePath);
            string parentPrefix = $"..{Path.DirectorySeparatorChar}";
            if (!Path.IsPathRooted(relativePath)
                && !relativePath.Equals("..", StringComparison.Ordinal)
                && !relativePath.StartsWith(parentPrefix, StringComparison.Ordinal))
            {
                return JoinSegments(relativePath);
            }
        }

        return Path.GetFileName(filePath);
    }

    private static string JoinSegments(string path) => string.Join(
        "  \u203a  ",
        path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries));
}
