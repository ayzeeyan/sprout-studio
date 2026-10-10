using AvaloniaEdit;
using AvaloniaEdit.Document;
using SproutStudio.Editor;

namespace SproutStudio.Views.Editor;

/// <summary>
/// Line-oriented editing commands (comment, duplicate, move) applied to an AvaloniaEdit
/// editor. Each command is grouped into a single undo step.
/// </summary>
internal static class EditorLineCommands
{
    public static bool ToggleComment(TextEditor editor, DocumentLanguage language)
    {
        if (EditorLineEditing.LineCommentPrefix(language) is not { } prefix)
        {
            return false;
        }
        TextDocument document = editor.Document;
        (DocumentLine first, DocumentLine last) = SelectedLines(editor);
        string[] lines = Enumerable.Range(first.LineNumber, last.LineNumber - first.LineNumber + 1)
            .Select(number => document.GetText(document.GetLineByNumber(number)))
            .ToArray();
        IReadOnlyList<string> toggled = EditorLineEditing.ToggleLineComment(lines, prefix);
        string replacement = string.Join(NewLine(document, first), toggled);

        int caretLine = editor.TextArea.Caret.Line;
        int caretColumn = editor.TextArea.Caret.Column;
        int caretLength = document.GetLineByNumber(caretLine).Length;
        bool hadSelection = !editor.TextArea.Selection.IsEmpty;
        int start = first.Offset;
        using (document.RunUpdate())
        {
            document.Replace(start, last.EndOffset - start, replacement);
        }

        int delta = document.GetLineByNumber(caretLine).Length - caretLength;
        editor.TextArea.Caret.Line = caretLine;
        editor.TextArea.Caret.Column = Math.Max(1, caretColumn + delta);
        if (hadSelection)
        {
            editor.Select(start, replacement.Length);
        }
        return true;
    }

    public static void DuplicateLines(TextEditor editor)
    {
        TextDocument document = editor.Document;
        if (!editor.TextArea.Selection.IsEmpty && editor.TextArea.Selection.IsMultiline == false)
        {
            // Duplicate an inline selection in place, like most code editors.
            int selectionStart = editor.SelectionStart;
            string selected = editor.SelectedText;
            document.Insert(selectionStart + selected.Length, selected);
            editor.Select(selectionStart + selected.Length, selected.Length);
            return;
        }

        (DocumentLine first, DocumentLine last) = SelectedLines(editor);
        string block = document.GetText(first.Offset, last.EndOffset - first.Offset);
        string newline = NewLine(document, first);
        int caretColumn = editor.TextArea.Caret.Column;
        int lineCount = last.LineNumber - first.LineNumber + 1;
        int caretLine = editor.TextArea.Caret.Line;
        document.Insert(last.EndOffset, newline + block);
        editor.TextArea.ClearSelection();
        editor.TextArea.Caret.Line = caretLine + lineCount;
        editor.TextArea.Caret.Column = caretColumn;
    }

    public static void MoveLines(TextEditor editor, int direction)
    {
        TextDocument document = editor.Document;
        (DocumentLine first, DocumentLine last) = SelectedLines(editor);
        DocumentLine? neighbour = direction < 0 ? first.PreviousLine : last.NextLine;
        if (neighbour is null)
        {
            return;
        }

        string newline = NewLine(document, first);
        string block = document.GetText(first.Offset, last.EndOffset - first.Offset);
        string neighbourText = document.GetText(neighbour);
        int caretLine = editor.TextArea.Caret.Line;
        int caretColumn = editor.TextArea.Caret.Column;
        bool hadSelection = !editor.TextArea.Selection.IsEmpty;

        int regionStart = direction < 0 ? neighbour.Offset : first.Offset;
        int regionEnd = direction < 0 ? last.EndOffset : neighbour.EndOffset;
        string replacement = direction < 0
            ? block + newline + neighbourText
            : neighbourText + newline + block;
        using (document.RunUpdate())
        {
            document.Replace(regionStart, regionEnd - regionStart, replacement);
        }

        int movedStart = direction < 0 ? regionStart : regionStart + neighbourText.Length + newline.Length;
        editor.TextArea.Caret.Line = caretLine + direction;
        editor.TextArea.Caret.Column = caretColumn;
        if (hadSelection)
        {
            editor.Select(movedStart, block.Length);
        }
        editor.TextArea.Caret.BringCaretToView();
    }

    public static bool GoToLine(TextEditor editor, int line, int column = 1)
    {
        if (editor.Document.LineCount == 0)
        {
            return false;
        }
        int target = Math.Clamp(line, 1, editor.Document.LineCount);
        DocumentLine documentLine = editor.Document.GetLineByNumber(target);
        editor.TextArea.ClearSelection();
        editor.TextArea.Caret.Line = target;
        editor.TextArea.Caret.Column = Math.Clamp(column, 1, documentLine.Length + 1);
        editor.ScrollTo(target, editor.TextArea.Caret.Column);
        editor.TextArea.Caret.BringCaretToView();
        editor.Focus();
        return true;
    }

    private static (DocumentLine First, DocumentLine Last) SelectedLines(TextEditor editor)
    {
        TextDocument document = editor.Document;
        if (editor.TextArea.Selection.IsEmpty)
        {
            DocumentLine caretLine = document.GetLineByNumber(editor.TextArea.Caret.Line);
            return (caretLine, caretLine);
        }
        int start = editor.SelectionStart;
        int end = editor.SelectionStart + editor.SelectionLength;
        DocumentLine first = document.GetLineByOffset(start);
        DocumentLine last = document.GetLineByOffset(end);
        // A selection ending at column 1 of a line does not include that line.
        if (last.LineNumber > first.LineNumber && end == last.Offset)
        {
            last = last.PreviousLine;
        }
        return (first, last);
    }

    private static string NewLine(TextDocument document, DocumentLine line) =>
        line.DelimiterLength > 0
            ? document.GetText(line.EndOffset, line.DelimiterLength)
            : line.PreviousLine is { DelimiterLength: > 0 } previous
                ? document.GetText(previous.EndOffset, previous.DelimiterLength)
                : "\n";
}
