namespace SproutStudio.Editor;

/// <summary>Pure line-level editing operations shared by the code editors.</summary>
public static class EditorLineEditing
{
    /// <summary>Returns the single-line comment prefix for a language, or null when it has none.</summary>
    public static string? LineCommentPrefix(DocumentLanguage language) => language switch
    {
        DocumentLanguage.Sprout or DocumentLanguage.Python or DocumentLanguage.Toml => "#",
        DocumentLanguage.Rust or DocumentLanguage.Go or DocumentLanguage.Java or DocumentLanguage.C
            or DocumentLanguage.Cpp or DocumentLanguage.CSharp or DocumentLanguage.JavaScript
            or DocumentLanguage.TypeScript => "//",
        _ => null,
    };

    /// <summary>
    /// Toggles line comments over a block of lines. When every non-blank line is already
    /// commented the prefix (and one following space) is removed; otherwise every non-blank
    /// line is commented at the block's smallest indentation so the markers stay aligned.
    /// </summary>
    public static IReadOnlyList<string> ToggleLineComment(IReadOnlyList<string> lines, string prefix)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        List<string> contentLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        if (contentLines.Count == 0)
        {
            return lines.ToArray();
        }

        bool allCommented = contentLines.All(line => line.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
        if (allCommented)
        {
            return lines.Select(line => Uncomment(line, prefix)).ToArray();
        }

        int indentation = contentLines.Min(LeadingWhitespace);
        return lines
            .Select(line => string.IsNullOrWhiteSpace(line)
                ? line
                : string.Concat(line.AsSpan(0, indentation), prefix, " ", line.AsSpan(indentation)))
            .ToArray();
    }

    private static string Uncomment(string line, string prefix)
    {
        int start = LeadingWhitespace(line);
        if (string.IsNullOrWhiteSpace(line) || !line.AsSpan(start).StartsWith(prefix, StringComparison.Ordinal))
        {
            return line;
        }
        int end = start + prefix.Length;
        if (end < line.Length && line[end] == ' ')
        {
            end++;
        }
        return string.Concat(line.AsSpan(0, start), line.AsSpan(end));
    }

    private static int LeadingWhitespace(string line)
    {
        int index = 0;
        while (index < line.Length && (line[index] == ' ' || line[index] == '\t'))
        {
            index++;
        }
        return index;
    }
}
