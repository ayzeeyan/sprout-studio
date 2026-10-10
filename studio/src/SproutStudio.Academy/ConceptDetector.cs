using System.Text.RegularExpressions;

namespace SproutStudio.Academy;

/// <summary>A catalog concept observed in source code, with where it first appears.</summary>
public sealed record ConceptMatch(string ConceptId, int Occurrences, int FirstLine, string Evidence);

/// <summary>
/// Maps the constructs used in a source file to Academy concepts so the IDE can explain the
/// code a learner is writing. Detection is lexical and intentionally conservative: comments
/// and string literals are ignored, and each concept needs a recognizable construct.
/// </summary>
public static class ConceptDetector
{
    private static readonly IReadOnlyDictionary<string, (string ConceptId, Regex Pattern)[]> Rules =
        new Dictionary<string, (string, Regex)[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["sprout"] =
            [
                ("variables", Word("has")),
                ("functions", Word("action")),
                ("conditions", Word("if")),
                ("loops", Words("repeat", "while")),
                ("collections", new Regex(@"=\s*\[", RegexOptions.Compiled)),
                ("data-modeling", Word("class")),
                ("oop-encapsulation", new Regex(@"\bclass\b[\s\S]*\baction\b", RegexOptions.Compiled)),
            ],
            ["python"] =
            [
                ("variables", new Regex(@"^\s*[A-Za-z_]\w*\s*=[^=]", RegexOptions.Compiled | RegexOptions.Multiline)),
                ("functions", Word("def")),
                ("conditions", Words("if", "elif")),
                ("loops", Words("for", "while")),
                ("collections", new Regex(@"=\s*[\[{]|\b(list|dict|set)\(", RegexOptions.Compiled)),
                ("data-modeling", Word("class")),
                ("oop-inheritance", new Regex(@"\bclass\s+\w+\s*\(\s*\w", RegexOptions.Compiled)),
            ],
            ["java"] =
            [
                ("variables", new Regex(@"\b(int|long|double|boolean|char|String|var)\s+\w+\s*=", RegexOptions.Compiled)),
                ("functions", new Regex(@"\b(public|private|protected|static)\b[^;=(]*\w+\s*\([^;]*\)\s*\{", RegexOptions.Compiled)),
                ("conditions", Words("if", "switch")),
                ("loops", Words("for", "while")),
                ("collections", new Regex(@"\b(List|ArrayList|Map|HashMap|Set)\b|\[\]", RegexOptions.Compiled)),
                ("data-modeling", Words("class", "record")),
                ("oop-encapsulation", Word("private")),
                ("oop-inheritance", Word("extends")),
                ("oop-abstraction", Words("interface", "abstract", "implements")),
            ],
            ["rust"] =
            [
                ("variables", Word("let")),
                ("functions", Word("fn")),
                ("conditions", Words("if", "match")),
                ("loops", Words("for", "while", "loop")),
                ("collections", new Regex(@"\bVec\b|\bvec!|\bHashMap\b", RegexOptions.Compiled)),
                ("data-modeling", Words("struct", "enum")),
                ("oop-abstraction", Word("trait")),
                ("oop-polymorphism", new Regex(@"\bdyn\b|\bimpl\s+\w+\s+for\b", RegexOptions.Compiled)),
            ],
            ["go"] =
            [
                ("variables", new Regex(@":=|\bvar\b", RegexOptions.Compiled)),
                ("functions", Word("func")),
                ("conditions", Words("if", "switch")),
                ("loops", Word("for")),
                ("collections", new Regex(@"\[\]\w|\bmap\[", RegexOptions.Compiled)),
                ("data-modeling", Word("struct")),
                ("oop-abstraction", Word("interface")),
            ],
        };

    public static bool Supports(string? languageKey) =>
        languageKey is not null && Rules.ContainsKey(languageKey);

    public static IReadOnlyList<ConceptMatch> Detect(string source, string? languageKey)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (languageKey is null || !Rules.TryGetValue(languageKey, out (string ConceptId, Regex Pattern)[]? rules))
        {
            return [];
        }

        string code = StripCommentsAndStrings(source, languageKey);
        var matches = new List<ConceptMatch>();
        foreach ((string conceptId, Regex pattern) in rules)
        {
            MatchCollection found = pattern.Matches(code);
            if (found.Count == 0)
            {
                continue;
            }
            Match first = found[0];
            int line = 1 + code.AsSpan(0, first.Index).Count('\n');
            string evidence = source.Split('\n')[line - 1].Trim();
            matches.Add(new ConceptMatch(conceptId, found.Count, line, evidence.Length > 80 ? evidence[..80] + "…" : evidence));
        }
        return matches.OrderBy(match => match.FirstLine).ToArray();
    }

    /// <summary>Blanks comments and string contents while preserving offsets and line breaks.</summary>
    internal static string StripCommentsAndStrings(string source, string languageKey)
    {
        bool hashComments = languageKey is "sprout" or "python";
        char[] output = source.ToCharArray();
        int index = 0;
        while (index < output.Length)
        {
            char current = source[index];
            bool lineComment = hashComments
                ? current == '#'
                : current == '/' && index + 1 < source.Length && source[index + 1] == '/';
            if (lineComment)
            {
                while (index < output.Length && source[index] != '\n')
                {
                    output[index++] = ' ';
                }
                continue;
            }
            if (current is '"' or '\'')
            {
                char quote = current;
                index++;
                while (index < output.Length && source[index] != quote && source[index] != '\n')
                {
                    if (source[index] == '\\' && index + 1 < output.Length && source[index + 1] != '\n')
                    {
                        output[index++] = ' ';
                    }
                    output[index++] = ' ';
                }
                index++;
                continue;
            }
            index++;
        }
        return new string(output);
    }

    private static Regex Word(string word) => new($@"\b{word}\b", RegexOptions.Compiled);

    private static Regex Words(params string[] words) => new($@"\b({string.Join('|', words)})\b", RegexOptions.Compiled);
}
