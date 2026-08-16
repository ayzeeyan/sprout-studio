namespace SproutStudio.Editor;

public static class EditorTextCoordinates
{
    public static string NormalizeLineEndings(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    public static string ToEditorLineEndings(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return NormalizeLineEndings(value).Replace("\n", "\r", StringComparison.Ordinal);
    }

    public static int EditorIndexToNormalizedIndex(string editorText, int editorIndex)
    {
        ArgumentNullException.ThrowIfNull(editorText);
        int bounded = Math.Clamp(editorIndex, 0, editorText.Length);
        return NormalizeLineEndings(editorText[..bounded]).Length;
    }

    public static int NormalizedIndexToEditorIndex(string editorText, int normalizedIndex)
    {
        ArgumentNullException.ThrowIfNull(editorText);
        int editorIndex = 0;
        int currentNormalized = 0;
        int target = Math.Max(0, normalizedIndex);
        while (editorIndex < editorText.Length && currentNormalized < target)
        {
            if (editorText[editorIndex] == '\r'
                && editorIndex + 1 < editorText.Length
                && editorText[editorIndex + 1] == '\n')
            {
                editorIndex += 2;
            }
            else
            {
                editorIndex++;
            }
            currentNormalized++;
        }
        return editorIndex;
    }
}
