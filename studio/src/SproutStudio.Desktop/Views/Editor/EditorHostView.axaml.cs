using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using System.ComponentModel;
using System.Reflection;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using SproutStudio.Core;
using SproutStudio.Editor;
using SproutStudio.Models;
using SproutStudio.ViewModels;

namespace SproutStudio.Views.Editor;

public sealed partial class EditorHostView : UserControl
{
    private static readonly Lazy<IHighlightingDefinition?> SproutHighlighting = new(
        LoadSproutHighlighting,
        LazyThreadSafetyMode.ExecutionAndPublication);
    private TextEditor? _editor;
    private MainWindowViewModel? _viewModel;
    private FoldingManager? _foldingManager;
    private CompletionWindow? _completionWindow;
    private readonly DiagnosticBackgroundRenderer _diagnosticRenderer = new();
    private CancellationTokenSource? _hoverCancellation;
    private bool _synchronizingText;
    private bool _completionRequested;
    private bool _parameterHintRequested;

    public EditorHostView()
    {
        InitializeComponent();
        AttachedToVisualTree += EditorHostView_AttachedToVisualTree;
        DetachedFromVisualTree += EditorHostView_DetachedFromVisualTree;
        DataContextChanged += EditorHostView_DataContextChanged;
    }

    private void EditorHostView_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _editor = this.FindControl<TextEditor>("CodeEditor");
        if (_editor is not null)
        {
            _editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
            _editor.TextChanged += Editor_TextChanged;
            _editor.TextArea.TextEntered += TextArea_TextEntered;
            _editor.TextArea.TextView.PointerHover += TextView_PointerHover;
            _editor.TextArea.TextView.PointerHoverStopped += TextView_PointerHoverStopped;
            _editor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticRenderer);
            _foldingManager = FoldingManager.Install(_editor.TextArea);
            _editor.TextArea.Options.ConvertTabsToSpaces = true;
            _editor.TextArea.Options.IndentationSize = 4;
            _editor.TextArea.Options.EnableRectangularSelection = true;
            _editor.TextArea.Options.ShowTabs = false;
            _editor.TextArea.Options.ShowSpaces = false;
            _editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(
                Color.FromArgb(38, 255, 255, 255));
        }
        AttachViewModel(DataContext as MainWindowViewModel);
        ApplyEditorSettings();
    }

    private void EditorHostView_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_editor is not null)
        {
            _editor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
            _editor.TextChanged -= Editor_TextChanged;
            _editor.TextArea.TextEntered -= TextArea_TextEntered;
            _editor.TextArea.TextView.PointerHover -= TextView_PointerHover;
            _editor.TextArea.TextView.PointerHoverStopped -= TextView_PointerHoverStopped;
            _editor.TextArea.TextView.BackgroundRenderers.Remove(_diagnosticRenderer);
            if (_foldingManager is not null)
            {
                FoldingManager.Uninstall(_foldingManager);
                _foldingManager = null;
            }
        }
        _completionWindow?.Hide();
        _completionWindow = null;
        _hoverCancellation?.Cancel();
        _hoverCancellation?.Dispose();
        _hoverCancellation = null;
        AttachViewModel(null);
    }

    private void EditorHostView_DataContextChanged(object? sender, EventArgs args) =>
        AttachViewModel(DataContext as MainWindowViewModel);

    private void AttachViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }
        if (_viewModel is not null)
        {
            _viewModel.SourceNavigationRequested -= ViewModel_SourceNavigationRequested;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.DiagnosticsChanged -= ViewModel_DiagnosticsChanged;
            _viewModel.LanguageAnalysisUpdated -= ViewModel_LanguageAnalysisUpdated;
        }
        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.SourceNavigationRequested += ViewModel_SourceNavigationRequested;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.DiagnosticsChanged += ViewModel_DiagnosticsChanged;
            _viewModel.LanguageAnalysisUpdated += ViewModel_LanguageAnalysisUpdated;
        }
        SynchronizeSelectedDocument();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainWindowViewModel.SelectedDocument))
        {
            SynchronizeSelectedDocument();
        }
        else if (args.PropertyName is nameof(MainWindowViewModel.EditorWordWrap)
            or nameof(MainWindowViewModel.ShowEditorWhitespace))
        {
            ApplyEditorSettings();
        }
    }

    private void ApplyEditorSettings()
    {
        if (_editor is null || _viewModel is null)
        {
            return;
        }
        _editor.WordWrap = _viewModel.EditorWordWrap;
        _editor.TextArea.Options.ShowSpaces = _viewModel.ShowEditorWhitespace;
        _editor.TextArea.Options.ShowTabs = _viewModel.ShowEditorWhitespace;
    }

    private void SynchronizeSelectedDocument()
    {
        if (_editor is null)
        {
            return;
        }
        string source = _viewModel?.SelectedDocument?.Source ?? string.Empty;
        ApplyHighlighting(_viewModel?.SelectedDocument);
        UpdateFoldings(source);
        UpdateDiagnosticDecorations();
        if (string.Equals(_editor.Text, source, StringComparison.Ordinal))
        {
            return;
        }
        _synchronizingText = true;
        try
        {
            _editor.Text = source;
            int offset = Math.Clamp(
                _viewModel?.SelectedDocument?.CaretOffset ?? 0,
                0,
                _editor.Document.TextLength);
            _editor.TextArea.Caret.Offset = offset;
        }
        finally
        {
            _synchronizingText = false;
        }
    }

    private void ApplyHighlighting(DocumentTabViewModel? document)
    {
        if (_editor is null)
        {
            return;
        }
        if (document?.Model.Language.Id == global::SproutStudio.Editor.DocumentLanguage.Sprout)
        {
            _editor.SyntaxHighlighting = SproutHighlighting.Value;
            return;
        }
        string extension = Path.GetExtension(document?.FilePath ?? document?.DisplayName ?? string.Empty);
        _editor.SyntaxHighlighting = string.IsNullOrEmpty(extension)
            ? null
            : HighlightingManager.Instance.GetDefinitionByExtension(extension);
    }

    private static IHighlightingDefinition? LoadSproutHighlighting()
    {
        Assembly assembly = typeof(EditorHostView).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream("SproutStudio.Content.Sprout.xshd");
        if (stream is null)
        {
            return null;
        }
        using XmlReader reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private void Editor_TextChanged(object? sender, EventArgs args)
    {
        if (!_synchronizingText && _editor is not null && _viewModel?.SelectedDocument is not null)
        {
            _viewModel.SelectedDocument.Source = _editor.Text;
            UpdateFoldings(_editor.Text);
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs args)
    {
        if (_editor is not null && _viewModel is not null)
        {
            _viewModel.UpdateCaret(
                _editor.TextArea.Caret.Line,
                _editor.TextArea.Caret.Column,
                _editor.TextArea.Caret.Offset);
            _editor.TextArea.TextView.HighlightedLine = _editor.TextArea.Caret.Line;
        }
    }

    private void ViewModel_DiagnosticsChanged(object? sender, EventArgs args) =>
        UpdateDiagnosticDecorations();

    private void UpdateDiagnosticDecorations()
    {
        if (_editor is null || _viewModel?.SelectedDocument is null)
        {
            _diagnosticRenderer.Update(string.Empty, []);
        }
        else
        {
            _diagnosticRenderer.Update(
                _viewModel.SelectedDocument.Source,
                _viewModel.CurrentDiagnostics);
        }
        _editor?.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    private void ViewModel_LanguageAnalysisUpdated(
        object? sender,
        LanguageAnalysisSnapshot snapshot)
    {
        if (_editor is null || _viewModel?.SelectedDocument?.Id != snapshot.DocumentId)
        {
            return;
        }
        UpdateFoldings(_viewModel.SelectedDocument.Source);
        if (_completionRequested)
        {
            ShowCompletion(snapshot.Response.Completions);
            _completionRequested = false;
        }
        if (_parameterHintRequested && snapshot.Response.Hover is not null)
        {
            ShowHoverAtCaret(snapshot.Response.Hover);
            _parameterHintRequested = false;
        }
    }

    private void UpdateFoldings(string source)
    {
        if (_foldingManager is null || source.Length > 1_000_000)
        {
            return;
        }
        _foldingManager.UpdateFoldings(BraceFoldingStrategy.CreateFoldings(source), -1);
    }

    private void TextArea_TextEntered(object? sender, TextInputEventArgs args)
    {
        if (_viewModel?.SelectedDocument?.Model.Language.Id
            != global::SproutStudio.Editor.DocumentLanguage.Sprout
            || string.IsNullOrEmpty(args.Text))
        {
            return;
        }
        char character = args.Text[^1];
        _completionRequested = char.IsLetterOrDigit(character) || character is '_' or '.';
        _parameterHintRequested = character is '(' or ',';
        if (_completionRequested || _parameterHintRequested)
        {
            _viewModel.RequestLanguageAnalysis();
        }
    }

    private void ViewModel_SourceNavigationRequested(object? sender, SourceSpan span) =>
        SelectSpan(span);

    public void SelectSpan(SourceSpan span)
    {
        if (_editor is null || _viewModel?.SelectedDocument is null)
        {
            return;
        }
        int start = SourceTextCoordinates.Utf8OffsetToUtf16Index(
            _viewModel.SelectedDocument.Source,
            span.Start);
        int end = SourceTextCoordinates.Utf8OffsetToUtf16Index(
            _viewModel.SelectedDocument.Source,
            span.End);
        start = Math.Clamp(start, 0, _editor.Document.TextLength);
        end = Math.Clamp(end, start, _editor.Document.TextLength);
        _editor.Select(start, end - start);
        _editor.TextArea.Caret.Offset = start;
        _editor.ScrollToLine(Math.Max(1, span.StartLine));
        _editor.Focus();
    }

    private async void CodeEditor_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.KeyModifiers.HasFlag(KeyModifiers.Control) && args.Key == Key.F)
        {
            ShowFind(showReplace: false);
            args.Handled = true;
        }
        else if (args.KeyModifiers.HasFlag(KeyModifiers.Control) && args.Key == Key.H)
        {
            ShowFind(showReplace: true);
            args.Handled = true;
        }
        else if (args.KeyModifiers.HasFlag(KeyModifiers.Control) && args.Key == Key.Space)
        {
            _completionRequested = true;
            _viewModel?.RequestLanguageAnalysis();
            args.Handled = true;
        }
        else if (args.Key == Key.F12)
        {
            await GoToDefinitionAsync();
            args.Handled = true;
        }
        else if (args.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && args.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && args.Key == Key.F)
        {
            if (_viewModel?.FormatDocumentCommand.CanExecute(null) == true)
            {
                _viewModel.FormatDocumentCommand.Execute(null);
            }
            args.Handled = true;
        }
        else if (args.Key == Key.F3)
        {
            FindNext(args.KeyModifiers.HasFlag(KeyModifiers.Shift));
            args.Handled = true;
        }
        else if (args.Key == Key.Escape && this.FindControl<Border>("FindPanel")?.IsVisible == true)
        {
            HideFind();
            args.Handled = true;
        }
    }

    private void FindTextBox_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            FindNext(args.KeyModifiers.HasFlag(KeyModifiers.Shift));
            args.Handled = true;
        }
        else if (args.Key == Key.Escape)
        {
            HideFind();
            args.Handled = true;
        }
    }

    private void ReplaceTextBox_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            ReplaceCurrent();
            args.Handled = true;
        }
        else if (args.Key == Key.Escape)
        {
            HideFind();
            args.Handled = true;
        }
    }

    private void FindPrevious_Click(object? sender, RoutedEventArgs args) => FindNext(backward: true);

    private void FindNext_Click(object? sender, RoutedEventArgs args) => FindNext(backward: false);

    private void CloseFind_Click(object? sender, RoutedEventArgs args) => HideFind();

    private void Replace_Click(object? sender, RoutedEventArgs args) => ReplaceCurrent();

    private void ReplaceAll_Click(object? sender, RoutedEventArgs args)
    {
        if (_editor is null)
        {
            return;
        }
        string needle = this.FindControl<TextBox>("FindTextBox")?.Text ?? string.Empty;
        if (needle.Length == 0)
        {
            return;
        }
        string replacement = this.FindControl<TextBox>("ReplaceTextBox")?.Text ?? string.Empty;
        string source = _editor.Text;
        int position = 0;
        int replacements = 0;
        using (_editor.Document.RunUpdate())
        {
            while ((position = source.IndexOf(needle, position, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                _editor.Document.Replace(position, needle.Length, replacement);
                source = _editor.Text;
                position += replacement.Length;
                replacements++;
            }
        }
        if (replacements > 0)
        {
            _editor.TextArea.Caret.Offset = Math.Min(position, _editor.Document.TextLength);
        }
    }

    private void ShowFind(bool showReplace)
    {
        Border? panel = this.FindControl<Border>("FindPanel");
        TextBox? box = this.FindControl<TextBox>("FindTextBox");
        if (panel is null || box is null)
        {
            return;
        }
        panel.IsVisible = true;
        this.FindControl<StackPanel>("ReplaceRow")!.IsVisible = showReplace;
        if (_editor is { SelectedText.Length: > 0 })
        {
            box.Text = _editor.SelectedText;
        }
        box.Focus();
        box.SelectAll();
    }

    private void ReplaceCurrent()
    {
        if (_editor is null)
        {
            return;
        }
        string needle = this.FindControl<TextBox>("FindTextBox")?.Text ?? string.Empty;
        if (needle.Length == 0)
        {
            return;
        }
        if (!string.Equals(_editor.SelectedText, needle, StringComparison.OrdinalIgnoreCase))
        {
            FindNext(backward: false);
        }
        if (string.Equals(_editor.SelectedText, needle, StringComparison.OrdinalIgnoreCase))
        {
            string replacement = this.FindControl<TextBox>("ReplaceTextBox")?.Text ?? string.Empty;
            int start = _editor.SelectionStart;
            _editor.Document.Replace(start, _editor.SelectionLength, replacement);
            _editor.Select(start, replacement.Length);
            _editor.TextArea.Caret.Offset = start + replacement.Length;
        }
    }

    private void HideFind()
    {
        Border? panel = this.FindControl<Border>("FindPanel");
        if (panel is not null)
        {
            panel.IsVisible = false;
        }
        _editor?.Focus();
    }

    private void FindNext(bool backward)
    {
        if (_editor is null)
        {
            return;
        }
        string needle = this.FindControl<TextBox>("FindTextBox")?.Text ?? string.Empty;
        if (string.IsNullOrEmpty(needle))
        {
            return;
        }
        string source = _editor.Text;
        int start = backward
            ? Math.Max(0, _editor.SelectionStart - 1)
            : Math.Min(source.Length, _editor.SelectionStart + _editor.SelectionLength);
        int match = backward
            ? source.LastIndexOf(needle, start, StringComparison.OrdinalIgnoreCase)
            : source.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
        if (match < 0)
        {
            match = backward
                ? source.LastIndexOf(needle, StringComparison.OrdinalIgnoreCase)
                : source.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        }
        if (match >= 0)
        {
            _editor.Select(match, needle.Length);
            _editor.TextArea.Caret.Offset = match + needle.Length;
            _editor.TextArea.Caret.BringCaretToView();
        }
    }

    private void ShowCompletion(IReadOnlyList<CompletionSuggestion> suggestions)
    {
        if (_editor is null || suggestions.Count == 0 || !_editor.IsKeyboardFocusWithin)
        {
            _completionWindow?.Hide();
            _completionWindow = null;
            return;
        }
        _completionWindow?.Hide();
        var window = new CompletionWindow(_editor.TextArea)
        {
            Width = 380,
            MaxHeight = 280,
            CloseAutomatically = true,
        };
        foreach (CompletionSuggestion suggestion in suggestions)
        {
            window.CompletionList.CompletionData.Add(new SproutCompletionData(suggestion));
        }
        window.CompletionList.CompletionAcceptKeys = [Key.Enter, Key.Tab];
        window.CompletionList.SelectedItem = window.CompletionList.CompletionData[0];
        window.Closed += (_, _) => _completionWindow = null;
        _completionWindow = window;
        window.Show();
    }

    private async Task GoToDefinitionAsync()
    {
        if (_editor is null || _viewModel?.SelectedDocument is not { } document)
        {
            return;
        }
        LanguageServiceResponse? response = await _viewModel.QueryLanguageAtOffsetAsync(
            document,
            _editor.TextArea.Caret.Offset);
        if (response?.Definition is not null)
        {
            SelectSpan(response.Definition.Span);
        }
    }

    private async void TextView_PointerHover(object? sender, PointerEventArgs args)
    {
        if (_editor is null
            || _viewModel?.SelectedDocument is not { } document
            || document.Model.Language.Id != global::SproutStudio.Editor.DocumentLanguage.Sprout)
        {
            return;
        }
        TextView textView = _editor.TextArea.TextView;
        Point point = args.GetPosition(textView);
        TextViewPosition? position = textView.GetPosition(point);
        if (position is null)
        {
            return;
        }
        int offset = _editor.Document.GetOffset(position.Value.Line, position.Value.Column);
        _hoverCancellation?.Cancel();
        _hoverCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _hoverCancellation = cancellation;
        try
        {
            LanguageServiceResponse? response = await _viewModel.QueryLanguageAtOffsetAsync(
                document,
                offset,
                cancellation.Token);
            if (!cancellation.IsCancellationRequested && response?.Hover is not null)
            {
                ShowHover(response.Hover, point.X + 48, point.Y + 34);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void TextView_PointerHoverStopped(object? sender, PointerEventArgs args)
    {
        _hoverCancellation?.Cancel();
        HideHover();
    }

    private void ShowHoverAtCaret(HoverInformation hover)
    {
        if (_editor is null)
        {
            return;
        }
        Point position = _editor.TextArea.TextView.GetVisualPosition(
            new TextViewPosition(_editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column),
            VisualYPosition.LineBottom);
        ShowHover(hover, position.X + 48, position.Y + 34);
    }

    private void ShowHover(HoverInformation hover, double left, double top)
    {
        Border? panel = this.FindControl<Border>("HoverPanel");
        if (panel is null)
        {
            return;
        }
        this.FindControl<TextBlock>("HoverTitle")!.Text = hover.Title;
        this.FindControl<TextBlock>("HoverContents")!.Text = hover.Contents;
        double maximumLeft = Math.Max(12, Bounds.Width - 440);
        double maximumTop = Math.Max(12, Bounds.Height - 150);
        panel.Margin = new Thickness(
            Math.Clamp(left, 12, maximumLeft),
            Math.Clamp(top, 12, maximumTop),
            0,
            0);
        panel.IsVisible = true;
    }

    private void HideHover()
    {
        Border? panel = this.FindControl<Border>("HoverPanel");
        if (panel is not null)
        {
            panel.IsVisible = false;
        }
    }

    private sealed class SproutCompletionData : ICompletionData
    {
        private readonly CompletionSuggestion _suggestion;

        public SproutCompletionData(CompletionSuggestion suggestion)
        {
            _suggestion = suggestion;
        }

        public IImage? Image => null;

        public string Text => _suggestion.Label;

        public object Content => $"{_suggestion.Label}    {_suggestion.Kind}";

        public object Description => _suggestion.Detail;

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            string source = textArea.Document.Text;
            int start = SourceTextCoordinates.Utf8OffsetToUtf16Index(
                source,
                _suggestion.ReplacementSpan.Start);
            int end = SourceTextCoordinates.Utf8OffsetToUtf16Index(
                source,
                _suggestion.ReplacementSpan.End);
            start = Math.Clamp(start, 0, textArea.Document.TextLength);
            end = Math.Clamp(end, start, textArea.Document.TextLength);
            textArea.Document.Replace(start, end - start, _suggestion.InsertText);
            textArea.Caret.Offset = start + _suggestion.InsertText.Length;
        }
    }
}
