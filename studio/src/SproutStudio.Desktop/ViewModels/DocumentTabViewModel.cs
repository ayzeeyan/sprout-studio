using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SproutStudio.Editor;

namespace SproutStudio.ViewModels;

public sealed class DocumentTabViewModel : ObservableObject
{
    private int _caretLine = 1;
    private int _caretColumn = 1;
    private int _caretOffset;
    private long _analysisVersion;

    public DocumentTabViewModel(EditorDocument document)
    {
        Model = document ?? throw new ArgumentNullException(nameof(document));
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? SourceChanged;

    public event EventHandler? CloseRequested;

    public IRelayCommand CloseCommand { get; }

    public EditorDocument Model { get; }

    public Guid Id => Model.Id;

    public string DisplayName => Model.DisplayName;

    public string TabTitle => Model.TabTitle;

    public string Breadcrumb => EditorBreadcrumbs.Format(
        Model.DisplayName,
        Model.FilePath,
        Model.ProjectRoot);

    public string LanguageName => Model.Language.DisplayName;

    public string LanguageKey => Model.Language.Key;

    public string? FilePath => Model.FilePath;

    public bool IsDirty => Model.IsDirty;

    public string Source
    {
        get => Model.Source;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.Equals(Model.Source, value, StringComparison.Ordinal))
            {
                return;
            }

            Model.UpdateSource(value);
            _analysisVersion++;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(TabTitle));
            SourceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int CaretLine
    {
        get => _caretLine;
        private set => SetProperty(ref _caretLine, value);
    }

    public int CaretColumn
    {
        get => _caretColumn;
        private set => SetProperty(ref _caretColumn, value);
    }

    public int CaretOffset
    {
        get => _caretOffset;
        private set => SetProperty(ref _caretOffset, value);
    }

    public long AnalysisVersion => _analysisVersion;

    public void SetCaret(int line, int column, int offset)
    {
        CaretLine = Math.Max(1, line);
        CaretColumn = Math.Max(1, column);
        CaretOffset = Math.Max(0, offset);
    }

    public void RefreshMetadata()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(LanguageName));
        OnPropertyChanged(nameof(LanguageKey));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(IsDirty));
    }

    public void ReplaceSource(string source)
    {
        Source = source;
    }
}
