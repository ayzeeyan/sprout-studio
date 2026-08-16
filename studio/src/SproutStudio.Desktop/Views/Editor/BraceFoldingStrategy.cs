using AvaloniaEdit.Folding;

namespace SproutStudio.Views.Editor;

internal static class BraceFoldingStrategy
{
    public static IReadOnlyList<NewFolding> CreateFoldings(string source)
    {
        var foldings = new List<NewFolding>();
        var starts = new Stack<int>();
        bool inString = false;
        bool escaped = false;
        bool lineComment = false;
        for (int index = 0; index < source.Length; index++)
        {
            char character = source[index];
            if (lineComment)
            {
                if (character == '\n')
                {
                    lineComment = false;
                }
                continue;
            }
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }
                continue;
            }
            if (character == '"')
            {
                inString = true;
            }
            else if (character == '/' && index + 1 < source.Length && source[index + 1] == '/')
            {
                lineComment = true;
                index++;
            }
            else if (character == '{')
            {
                starts.Push(index);
            }
            else if (character == '}' && starts.TryPop(out int start))
            {
                if (source.AsSpan(start, index - start).Contains('\n'))
                {
                    foldings.Add(new NewFolding(start, index + 1) { Name = "{ … }" });
                }
            }
        }
        return foldings.OrderBy(item => item.StartOffset).ToArray();
    }
}
