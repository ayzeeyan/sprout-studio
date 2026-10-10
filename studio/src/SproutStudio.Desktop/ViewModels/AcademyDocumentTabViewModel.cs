using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SproutStudio.Academy;
using SproutStudio.Editor;
using SproutStudio.Models;
using SproutStudio.Services;

namespace SproutStudio.ViewModels;

public enum AcademyTabSubmissionState
{
    NotSubmitted,
    Validating,
    TestsPassed,
    Passed,
    NeedsRevision,
}

public enum RequirementStatus
{
    NotChecked,
    Passed,
    Failed,
}

public sealed class RequirementItemViewModel : ObservableObject
{
    private RequirementStatus _status = RequirementStatus.NotChecked;
    private string? _feedback;
    private string? _expected;
    private string? _observed;

    public RequirementItemViewModel(
        string id,
        string description,
        RequirementStatus status = RequirementStatus.NotChecked,
        string? feedback = null,
        string? expected = null,
        string? observed = null,
        Action? openRelevantCode = null)
    {
        Id = id;
        Description = description;
        _status = status;
        _feedback = feedback;
        _expected = expected;
        _observed = observed;
        OpenRelevantCodeCommand = new RelayCommand(() => openRelevantCode?.Invoke());
    }

    public string Id { get; }
    public string Description { get; }
    public IRelayCommand OpenRelevantCodeCommand { get; }

    public RequirementStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(Passed));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(IsNotChecked));
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public bool Passed => Status == RequirementStatus.Passed;
    public bool IsFailed => Status == RequirementStatus.Failed;
    public bool IsNotChecked => Status == RequirementStatus.NotChecked;

    public string StatusIcon => Status switch
    {
        RequirementStatus.Passed => "\uE73E",
        RequirementStatus.Failed => "\uE711",
        _ => "\uE739",
    };

    public string StatusColor => Status switch
    {
        RequirementStatus.Passed => "#49C979",
        RequirementStatus.Failed => "#E5534B",
        _ => "#8A94A8",
    };

    public string? Feedback
    {
        get => _feedback;
        set
        {
            if (SetProperty(ref _feedback, value))
            {
                OnPropertyChanged(nameof(HasFeedback));
            }
        }
    }

    public string? Expected
    {
        get => _expected;
        set
        {
            if (SetProperty(ref _expected, value))
            {
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public string? Observed
    {
        get => _observed;
        set
        {
            if (SetProperty(ref _observed, value))
            {
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public bool HasFeedback => !string.IsNullOrWhiteSpace(_feedback);
    public bool HasDetails => !string.IsNullOrWhiteSpace(_expected) || !string.IsNullOrWhiteSpace(_observed);
}

public sealed class QuizOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public QuizOptionViewModel(
        string text,
        int index,
        bool allowMultiple,
        Action<QuizOptionViewModel> onSelected)
    {
        Text = text;
        Index = index;
        IsMultiple = allowMultiple;
        SelectCommand = new RelayCommand(() =>
        {
            if (!allowMultiple)
            {
                IsSelected = true;
            }
            onSelected(this);
        });
    }

    public string Text { get; }
    public int Index { get; }
    public bool IsMultiple { get; }
    public bool IsSingle => !IsMultiple;
    public IRelayCommand SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class QuizQuestionTabViewModel : ObservableObject
{
    private string? _textAnswer;
    private bool? _isCorrect;
    private string? _feedback;

    public QuizQuestionTabViewModel(QuizQuestion question)
    {
        Question = question;
        Options = [];
        if (question.Options is not null)
        {
            for (int i = 0; i < question.Options.Length; i++)
            {
                int index = i;
                string text = question.Options[i];
                Options.Add(new QuizOptionViewModel(
                    text,
                    index,
                    question.Type == SubmissionType.MultipleSelect,
                    optVm =>
                {
                    if (question.Type != SubmissionType.MultipleSelect)
                    {
                        foreach (var other in Options)
                        {
                            if (other != optVm) other.IsSelected = false;
                        }
                    }
                }));
            }
        }
    }

    public QuizQuestion Question { get; }
    public ObservableCollection<QuizOptionViewModel> Options { get; }

    public string? TextAnswer
    {
        get => _textAnswer;
        set => SetProperty(ref _textAnswer, value);
    }

    public bool? IsCorrect
    {
        get => _isCorrect;
        set
        {
            if (SetProperty(ref _isCorrect, value))
            {
                OnPropertyChanged(nameof(HasFeedback));
            }
        }
    }

    public string? Feedback
    {
        get => _feedback;
        set
        {
            if (SetProperty(ref _feedback, value))
            {
                OnPropertyChanged(nameof(HasFeedback));
            }
        }
    }

    public bool HasFeedback => !string.IsNullOrWhiteSpace(_feedback);
    public string SelectedAnswer => Options.FirstOrDefault(o => o.IsSelected)?.Text ?? TextAnswer ?? string.Empty;
    public string[] SelectedAnswers => Options.Count > 0
        ? Options.Where(option => option.IsSelected).Select(option => option.Text).ToArray()
        : string.IsNullOrWhiteSpace(TextAnswer) ? [] : [TextAnswer.Trim()];
    public bool HasOptions => Options.Count > 0;
    public bool HasNoOptions => Options.Count == 0;
}

public sealed class AttemptHistoryItemViewModel : ObservableObject
{
    public AttemptHistoryItemViewModel(int attemptNumber, bool passed, int score, DateTimeOffset submittedAt, string? feedback)
    {
        AttemptNumber = attemptNumber;
        Passed = passed;
        Score = score;
        SubmittedAt = submittedAt;
        Feedback = feedback;
    }

    public int AttemptNumber { get; }
    public bool Passed { get; }
    public int Score { get; }
    public DateTimeOffset SubmittedAt { get; }
    public string? Feedback { get; }

    public string Title => $"Attempt {AttemptNumber} · {(Passed ? "Passed" : "Needs Revision")}";
    public string StatusIcon => Passed ? "\uE73E" : "\uE711";
    public string StatusColor => Passed ? "#49C979" : "#E5534B";
    public string RelativeTime => FormatRelativeTime(SubmittedAt);

    private static string FormatRelativeTime(DateTimeOffset time)
    {
        TimeSpan delta = DateTimeOffset.UtcNow - time.ToUniversalTime();
        if (delta.TotalMinutes < 2) return "Just now";
        if (delta.TotalHours < 1) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalDays < 1) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays < 2) return "Yesterday";
        return $"{(int)delta.TotalDays}d ago";
    }
}

public sealed class AcademyDocumentTabViewModel : ObservableObject
{
    private readonly MainWindowViewModel _workspace;
    private readonly AcademyService _academy;
    private AcademyTabSubmissionState _submissionState = AcademyTabSubmissionState.NotSubmitted;
    private int _score;
    private int _attemptCount;
    private string? _feedbackMessage;
    private int _xpEarned;
    private int _remainingHintsToday = 5;
    private int _dailyHintAllowance = HintUsageState.DefaultDailyAllowance;
    private bool _unlimitedHints;
    private bool _isSubmitting;
    private bool _lastResultWasSubmission;

    private bool _isRequirementsExpanded = true;
    private bool _isHintsExpanded = true;
    private bool _isResultsExpanded = true;
    private bool _isAttemptHistoryExpanded = false;

    public AcademyDocumentTabViewModel(
        MainWindowViewModel workspace,
        AcademyService academy,
        TrackDefinition track,
        LessonDefinition lesson,
        string? linkedCompanionPath = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _academy = academy ?? throw new ArgumentNullException(nameof(academy));
        Track = track ?? throw new ArgumentNullException(nameof(track));
        Lesson = lesson ?? throw new ArgumentNullException(nameof(lesson));
        LinkedCompanionPath = linkedCompanionPath;

        ModuleDefinition? module = track.Modules?.FirstOrDefault(m => string.Equals(m.Id, lesson.ModuleId, StringComparison.OrdinalIgnoreCase));
        ModuleTitle = module?.Title ?? "Core lessons";

        int lessonIndex = Array.FindIndex(track.Lessons, l => string.Equals(l.Id, lesson.Id, StringComparison.OrdinalIgnoreCase));
        ProgressLabel = $"{Math.Max(1, lessonIndex + 1)} / {track.Lessons.Length}";

        Requirements = [];
        if (lesson.Exercise is not null)
        {
            foreach (string assertion in lesson.Exercise.Assertions)
            {
                // Phrase the grader's assertions as plain-language goals.
                string desc = assertion.StartsWith("contains:", StringComparison.OrdinalIgnoreCase)
                    ? $"Uses  {assertion["contains:".Length..].Trim()}"
                    : assertion.StartsWith("not-contains:", StringComparison.OrdinalIgnoreCase)
                        ? $"Leaves out  {assertion["not-contains:".Length..].Trim()}"
                        : assertion;
                Requirements.Add(new RequirementItemViewModel(
                    assertion,
                    desc,
                    RequirementStatus.NotChecked,
                    openRelevantCode: () => _ = OpenRelevantCodeAsync(assertion)));
            }
        }

        QuizQuestions = [];
        if (lesson.Exercise?.Questions is not null)
        {
            foreach (QuizQuestion q in lesson.Exercise.Questions)
            {
                QuizQuestions.Add(new QuizQuestionTabViewModel(q));
            }
        }

        OpenLinkedCodeCommand = new AsyncRelayCommand(OpenLinkedCodeAsync);
        CheckRequirementsCommand = new AsyncRelayCommand(CheckRequirementsAsync, () => !IsSubmitting);
        SubmitExerciseCommand = new AsyncRelayCommand(SubmitExerciseAsync, () => !IsSubmitting);
        RequestHintCommand = new AsyncRelayCommand(RequestHintAsync);
        ContinueToNextLessonCommand = new AsyncRelayCommand(ContinueToNextLessonAsync);

        ToggleRequirementsCommand = new RelayCommand(() => IsRequirementsExpanded = !IsRequirementsExpanded);
        ToggleHintsCommand = new RelayCommand(() => IsHintsExpanded = !IsHintsExpanded);
        ToggleResultsCommand = new RelayCommand(() => IsResultsExpanded = !IsResultsExpanded);
        ToggleAttemptHistoryCommand = new RelayCommand(() => IsAttemptHistoryExpanded = !IsAttemptHistoryExpanded);
        OpenGuideCommand = new RelayCommand<string>(guideId => _workspace.OpenGuide(guideId));
        RelatedGuides = academy.Catalog.GuidesForConcepts(lesson.ConceptIds).Take(2).ToArray();

        _ = RefreshInitialStateAsync();
    }

    public TrackDefinition Track { get; }
    public LessonDefinition Lesson { get; }

    /// <summary>Handbook guides that explain this lesson's concepts ("read first").</summary>
    public IReadOnlyList<GuideDefinition> RelatedGuides { get; }

    public bool HasRelatedGuides => RelatedGuides.Count > 0;

    public IRelayCommand<string> OpenGuideCommand { get; }

    /// <summary>Header chips: activity kind, estimated time, and XP.</summary>
    public string ActivityKindText => Lesson.TaskType switch
    {
        TaskType.Checkpoint => "Checkpoint",
        TaskType.CapstoneStep => "Capstone",
        _ when HasExercise => "Coding exercise",
        _ when HasQuiz => "Knowledge check",
        _ => "Lesson",
    };

    public string EstimatedTimeText => $"~{Math.Max(1, Lesson.EstimatedMinutes)} min";

    public string XpText => $"{Lesson.Xp} XP";
    public string? ModuleTitle { get; }
    public string LanguageKey => string.IsNullOrWhiteSpace(Track.RequiredToolchainId)
        ? "sprout"
        : Track.RequiredToolchainId;
    public string ExerciseObjective => string.IsNullOrWhiteSpace(Lesson.Exercise?.Objective)
        ? Lesson.Summary
        : Lesson.Exercise.Objective;
    public bool HasExerciseObjective => HasExercise && !string.IsNullOrWhiteSpace(ExerciseObjective);
    public string ProgressLabel { get; }
    public string? LinkedCompanionPath { get; }
    public bool HasLinkedCompanion => !string.IsNullOrEmpty(LinkedCompanionPath);
    public string LinkedCompanionName => Path.GetFileName(LinkedCompanionPath ?? string.Empty);

    public ObservableCollection<RequirementItemViewModel> Requirements { get; }
    public ObservableCollection<QuizQuestionTabViewModel> QuizQuestions { get; }
    public ObservableCollection<HintItem> RevealedHints { get; } = [];
    public ObservableCollection<AttemptHistoryItemViewModel> AttemptHistory { get; } = [];

    public bool HasExercise => Lesson.Exercise is not null;
    public bool HasCodingRequirements => Requirements.Count > 0;
    public bool IsReading => Lesson.Exercise is null;
    public bool HasQuiz => QuizQuestions.Count > 0
        && (Lesson.TaskType is TaskType.Quiz or TaskType.Checkpoint || Requirements.Count == 0);
    public bool HasSupplementalQuestions => QuizQuestions.Count > 0 && !HasQuiz;
    public bool HasKnowledgeQuestions => QuizQuestions.Count > 0;
    public string KnowledgeCheckTitle => HasSupplementalQuestions ? "Knowledge Check" : "Quiz Questions";
    public bool HasHints => Lesson.Exercise?.Hints.Length > 0;
    public bool CanSubmit => HasExercise || HasQuiz || Lesson.TaskType is TaskType.Checkpoint or TaskType.CapstoneStep;

    public string SubmitButtonText => IsSubmitting
        ? "Submitting..."
        : HasQuiz
            ? "Submit Quiz"
            : Lesson.TaskType == TaskType.Checkpoint
                ? "Submit Checkpoint"
                : Lesson.TaskType == TaskType.CapstoneStep
                    ? "Submit Capstone"
                    : "Submit Exercise";

    public string RequirementsToggleGlyph => IsRequirementsExpanded ? "\uE70E" : "\uE70D";
    public string HintsToggleGlyph => IsHintsExpanded ? "\uE70E" : "\uE70D";
    public string ResultsToggleGlyph => IsResultsExpanded ? "\uE70E" : "\uE70D";
    public bool HasAttemptHistory => AttemptHistory.Count > 0;
    public string RevisionAttemptText => _lastResultWasSubmission
        ? $"Attempt {AttemptCount} recorded · Fix the failed requirements and submit again."
        : "Sandbox run only · no attempt or XP was recorded.";

    public bool IsRequirementsExpanded
    {
        get => _isRequirementsExpanded;
        set
        {
            if (SetProperty(ref _isRequirementsExpanded, value))
            {
                OnPropertyChanged(nameof(RequirementsToggleGlyph));
            }
        }
    }

    public bool IsHintsExpanded
    {
        get => _isHintsExpanded;
        set
        {
            if (SetProperty(ref _isHintsExpanded, value))
            {
                OnPropertyChanged(nameof(HintsToggleGlyph));
            }
        }
    }

    public bool IsResultsExpanded
    {
        get => _isResultsExpanded;
        set
        {
            if (SetProperty(ref _isResultsExpanded, value))
            {
                OnPropertyChanged(nameof(ResultsToggleGlyph));
            }
        }
    }

    public bool IsAttemptHistoryExpanded
    {
        get => _isAttemptHistoryExpanded;
        set => SetProperty(ref _isAttemptHistoryExpanded, value);
    }

    public IRelayCommand ToggleRequirementsCommand { get; }
    public IRelayCommand ToggleHintsCommand { get; }
    public IRelayCommand ToggleResultsCommand { get; }
    public IRelayCommand ToggleAttemptHistoryCommand { get; }

    public IAsyncRelayCommand OpenLinkedCodeCommand { get; }
    public IAsyncRelayCommand CheckRequirementsCommand { get; }
    public IAsyncRelayCommand SubmitExerciseCommand { get; }
    public IAsyncRelayCommand RequestHintCommand { get; }
    public IAsyncRelayCommand ContinueToNextLessonCommand { get; }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            if (SetProperty(ref _isSubmitting, value))
            {
                OnPropertyChanged(nameof(SubmitButtonText));
                SubmitExerciseCommand.NotifyCanExecuteChanged();
                CheckRequirementsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public AcademyTabSubmissionState SubmissionState
    {
        get => _submissionState;
        set
        {
            if (SetProperty(ref _submissionState, value))
            {
                OnPropertyChanged(nameof(IsNotSubmitted));
                OnPropertyChanged(nameof(IsValidating));
                OnPropertyChanged(nameof(IsTestsPassed));
                OnPropertyChanged(nameof(IsPassed));
                OnPropertyChanged(nameof(IsNeedsRevision));
                OnPropertyChanged(nameof(HasResultBanner));
            }
        }
    }

    public bool IsNotSubmitted => SubmissionState == AcademyTabSubmissionState.NotSubmitted;
    public bool IsValidating => SubmissionState == AcademyTabSubmissionState.Validating;
    public bool IsPassed => SubmissionState == AcademyTabSubmissionState.Passed;
    public bool IsTestsPassed => SubmissionState == AcademyTabSubmissionState.TestsPassed;
    public bool IsNeedsRevision => SubmissionState == AcademyTabSubmissionState.NeedsRevision;
    public bool HasResultBanner => IsPassed || IsTestsPassed || IsNeedsRevision;

    public int Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    public int AttemptCount
    {
        get => _attemptCount;
        set => SetProperty(ref _attemptCount, value);
    }

    public string? FeedbackMessage
    {
        get => _feedbackMessage;
        set => SetProperty(ref _feedbackMessage, value);
    }

    public int XpEarned
    {
        get => _xpEarned;
        set => SetProperty(ref _xpEarned, value);
    }

    public int RemainingHintsToday
    {
        get => _remainingHintsToday;
        set
        {
            if (SetProperty(ref _remainingHintsToday, value))
            {
                OnPropertyChanged(nameof(HintsRemainingText));
                OnPropertyChanged(nameof(CanRequestHint));
                OnPropertyChanged(nameof(IsHintLimitReached));
            }
        }
    }

    public string HintsRemainingText => _unlimitedHints
        ? "Unlimited hints"
        : $"{RemainingHintsToday} of {_dailyHintAllowance} hints remaining today";
    public bool CanRequestHint => (_unlimitedHints || RemainingHintsToday > 0)
        && RevealedHints.Count < (Lesson.Exercise?.Hints.Length ?? 0);
    public bool IsHintLimitReached => RemainingHintsToday <= 0 && RevealedHints.Count < (Lesson.Exercise?.Hints.Length ?? 0);

    public int PassedRequirementsCount => Requirements.Count(r => r.Passed);
    public int TotalRequirementsCount => Requirements.Count;
    public string RequirementsSummaryText => $"{PassedRequirementsCount} / {TotalRequirementsCount} requirements satisfied";

    private async Task RefreshInitialStateAsync()
    {
        try
        {
            AcademyState state = await _academy.LoadStateAsync();
            if (state.HintUsage is not null)
            {
                RemainingHintsToday = state.HintUsage.RemainingHintsToday;
                _dailyHintAllowance = state.HintUsage.DailyAllowance;
                _unlimitedHints = state.HintUsage.Mode == HintAssistanceMode.Unlimited;
                RestoreRevealedHints(state.HintUsage);
                OnPropertyChanged(nameof(HintsRemainingText));
                OnPropertyChanged(nameof(CanRequestHint));
            }

            var previousSubmissions = state.Submissions
                .Where(s => string.Equals(s.LessonId, Lesson.Id, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.SubmittedAt)
                .ToList();

            AttemptHistory.Clear();
            foreach (SubmissionRecord sub in previousSubmissions)
            {
                AttemptHistory.Add(new AttemptHistoryItemViewModel(
                    sub.AttemptNumber,
                    sub.Passed,
                    sub.Score,
                    sub.SubmittedAt,
                    sub.Feedback));
            }
            OnPropertyChanged(nameof(HasAttemptHistory));

            if (previousSubmissions.Count > 0)
            {
                _lastResultWasSubmission = true;
                OnPropertyChanged(nameof(RevisionAttemptText));
                SubmissionRecord latest = previousSubmissions[0];
                AttemptCount = latest.AttemptNumber;
                Score = latest.Score;
                FeedbackMessage = latest.Feedback;
                SubmissionState = latest.Passed ? AcademyTabSubmissionState.Passed : AcademyTabSubmissionState.NeedsRevision;

                foreach (var req in Requirements)
                {
                    if (latest.PassedRequirements.Contains(req.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        req.Status = RequirementStatus.Passed;
                        req.Feedback = null;
                    }
                    else if (latest.FailedRequirements.Contains(req.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        req.Status = RequirementStatus.Failed;
                        req.Feedback = "Requirement not met.";
                    }
                }
                ApplyQuizFeedback(latest.PassedRequirements, latest.FailedRequirements);
                OnPropertyChanged(nameof(PassedRequirementsCount));
                OnPropertyChanged(nameof(RequirementsSummaryText));
            }
        }
        catch (Exception exception)
        {
            StudioLog.Warning($"Could not restore Academy state for {Lesson.Id}: {exception.Message}");
        }
    }

    public async Task OpenLinkedCodeAsync()
    {
        if (string.IsNullOrEmpty(LinkedCompanionPath))
        {
            return;
        }

        DocumentTabViewModel? existing = _workspace.Documents.FirstOrDefault(d =>
            string.Equals(d.FilePath, LinkedCompanionPath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            _workspace.SelectedDocument = existing;
        }
        else
        {
            await _workspace.OpenDocumentPathAsync(LinkedCompanionPath, _workspace.CurrentProject?.RootPath);
        }
    }

    private async Task OpenRelevantCodeAsync(
        string assertion,
        int? sourceLine = null,
        int? sourceColumn = null)
    {
        await OpenLinkedCodeAsync();
        DocumentTabViewModel? document = _workspace.SelectedDocument;
        if (document is null || !document.IsSource)
        {
            return;
        }

        string searchTerm = assertion.StartsWith("contains:", StringComparison.OrdinalIgnoreCase)
            ? assertion["contains:".Length..].Trim()
            : assertion.StartsWith("not-contains:", StringComparison.OrdinalIgnoreCase)
                ? assertion["not-contains:".Length..].Trim()
                : assertion.Trim();
        int offset;
        int line;
        int column;
        if (sourceLine is > 0)
        {
            line = sourceLine.Value;
            column = Math.Max(1, sourceColumn ?? 1);
            offset = SourceOffsetAt(document.Source, line, column);
        }
        else
        {
            offset = document.Source.IndexOf(searchTerm, StringComparison.Ordinal);
            if (offset < 0)
            {
                offset = 0;
            }
            string before = document.Source[..offset].ReplaceLineEndings("\n");
            line = 1 + before.Count(character => character == '\n');
            int lastBreak = before.LastIndexOf('\n');
            column = offset - lastBreak;
        }
        document.SetCaret(line, Math.Max(1, column), offset);
        _workspace.StatusText = sourceLine is > 0
            ? $"Focused {document.DisplayName}:{line}:{column}"
            : offset == 0 && !document.Source.StartsWith(searchTerm, StringComparison.Ordinal)
                ? $"Open code and implement: {searchTerm}"
                : $"Focused `{searchTerm}` in {document.DisplayName}";
    }

    private static int SourceOffsetAt(string source, int line, int column)
    {
        int currentLine = 1;
        int offset = 0;
        while (currentLine < line && offset < source.Length)
        {
            int nextBreak = source.IndexOf('\n', offset);
            if (nextBreak < 0)
            {
                return source.Length;
            }
            offset = nextBreak + 1;
            currentLine++;
        }

        int lineEnd = source.IndexOf('\n', offset);
        if (lineEnd < 0)
        {
            lineEnd = source.Length;
        }
        return Math.Min(lineEnd, offset + Math.Max(0, column - 1));
    }

    private void RestoreRevealedHints(HintUsageState usage)
    {
        RevealedHints.Clear();
        string[] hints = Lesson.Exercise?.Hints ?? [];
        for (int index = 0; index < hints.Length; index++)
        {
            string id = $"hint-{Lesson.Id}-{index}";
            if (!usage.RevealedHintIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            RevealedHints.Add(new HintItem(
                id,
                index + 1,
                HintLevelName(index),
                hints[index],
                IsRevealed: true));
        }
    }

    private static string HintLevelName(int index) => index switch
    {
        0 => "Nudge",
        1 => "Concept",
        2 => "Structure",
        _ => "Detail",
    };

    public async Task CheckRequirementsAsync()
    {
        if (Lesson.Exercise is null || IsSubmitting)
        {
            return;
        }

        string currentCode = GetCurrentSourceCode();
        _lastResultWasSubmission = false;
        OnPropertyChanged(nameof(RevisionAttemptText));
        SubmissionState = AcademyTabSubmissionState.Validating;
        string suiteName = $"{Lesson.Title} — {LinkedCompanionName}";
        _workspace.TestRun.Begin(suiteName);
        _workspace.SelectedBottomTab = 3;
        _workspace.BottomPanelVisible = true;

        try
        {
            var execResult = await _academy.ExecuteExerciseAsync(
                Lesson,
                currentCode,
                (_, code, ct) => _workspace.ExecuteAcademyRuntimeAsync(Track, Lesson, code, ct));

            ApplyExecutionResult(execResult, suiteName);
            FeedbackMessage = execResult.Feedback;
            SubmissionState = execResult.Success
                ? AcademyTabSubmissionState.TestsPassed
                : AcademyTabSubmissionState.NeedsRevision;
            XpEarned = 0;
            _workspace.StatusText = execResult.Success
                ? "Sandbox tests passed. Submit when you are ready to record this attempt."
                : "Sandbox tests found requirements that need revision. No attempt was recorded.";
        }
        catch (Exception ex)
        {
            FeedbackMessage = $"Check failed: {ex.Message}";
            SubmissionState = AcademyTabSubmissionState.NeedsRevision;
            _workspace.TestRun.PopulateFromExecution(
                suiteName,
                new ExerciseExecutionResult(
                    false,
                    Lesson.Exercise.ExpectedOutput,
                    null,
                    [ex.Message],
                    [],
                    ["runtime:exception"],
                    FeedbackMessage,
                    true,
                    [new RequirementEvaluation(
                        "runtime:exception",
                        "Toolchain execution",
                        false,
                        "The configured toolchain runs the submitted source.",
                        ex.Message,
                        "The test run could not start.")],
                    TimeSpan.Zero,
                    "Test run failed to start."));
        }
    }

    public async Task SubmitExerciseAsync()
    {
        if (IsSubmitting)
        {
            return;
        }

        IsSubmitting = true;
        _lastResultWasSubmission = true;
        OnPropertyChanged(nameof(RevisionAttemptText));
        SubmissionState = AcademyTabSubmissionState.Validating;

        try
        {
            if (HasQuiz)
            {
                var answers = QuizQuestions.ToDictionary(q => q.Question.Id, q => q.SelectedAnswers);
                var (submission, passed, score, feedback, completion) = await _academy.SubmitQuizAnswersAsync(
                    Track.Id,
                    Lesson.Id,
                    answers);

                AttemptCount = submission.AttemptNumber;
                Score = score;
                FeedbackMessage = feedback;
                XpEarned = completion?.AwardedXp ?? (passed ? Lesson.Xp : 0);
                SubmissionState = passed ? AcademyTabSubmissionState.Passed : AcademyTabSubmissionState.NeedsRevision;

                AttemptHistory.Insert(0, new AttemptHistoryItemViewModel(
                    submission.AttemptNumber,
                    submission.Passed,
                    submission.Score,
                    submission.SubmittedAt,
                    submission.Feedback));

                foreach (var q in QuizQuestions)
                {
                    string[] selected = q.SelectedAnswers;
                    bool correct = q.Question.CorrectAnswers.Length == selected.Length
                        && q.Question.CorrectAnswers.All(expected => selected.Contains(expected, StringComparer.OrdinalIgnoreCase));
                    q.IsCorrect = correct;
                    q.Feedback = correct ? "✓ Correct" : $"✗ Expected: {string.Join(", ", q.Question.CorrectAnswers)}. {q.Question.Explanation}";
                }
            }
            else if (Lesson.Exercise is not null)
            {
                string currentCode = GetCurrentSourceCode();
                var (submission, execResult, completion) = await _academy.SubmitCodingExerciseAsync(
                    Track.Id,
                    Lesson.Id,
                    currentCode,
                    (_, code, ct) => _workspace.ExecuteAcademyRuntimeAsync(Track, Lesson, code, ct),
                    HasSupplementalQuestions
                        ? QuizQuestions.ToDictionary(question => question.Question.Id, question => question.SelectedAnswers)
                        : null);

                AttemptCount = submission.AttemptNumber;
                Score = submission.Score;
                FeedbackMessage = submission.Feedback;
                XpEarned = completion?.AwardedXp ?? (submission.Passed ? Lesson.Xp : 0);
                SubmissionState = submission.Passed ? AcademyTabSubmissionState.Passed : AcademyTabSubmissionState.NeedsRevision;

                AttemptHistory.Insert(0, new AttemptHistoryItemViewModel(
                    submission.AttemptNumber,
                    submission.Passed,
                    submission.Score,
                    submission.SubmittedAt,
                    submission.Feedback));

                ApplyQuizFeedback(execResult.PassedAssertions, execResult.FailedAssertions);
                ApplyExecutionResult(execResult, $"{Lesson.Title} — {LinkedCompanionName}");
            }
            else
            {
                var completion = await _academy.CompleteLessonAsync(Track.Id, Lesson.Id);
                SubmissionState = AcademyTabSubmissionState.Passed;
                XpEarned = completion.AwardedXp;
                FeedbackMessage = "Lesson completed!";
            }

            _workspace.StatusText = $"Submitted Academy exercise for {Lesson.Title}";
            OnPropertyChanged(nameof(HasAttemptHistory));
            await _workspace.RefreshAcademyDashboardAsync();
        }
        catch (Exception ex)
        {
            FeedbackMessage = $"Submission failed: {ex.Message}";
            SubmissionState = AcademyTabSubmissionState.NeedsRevision;
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    public async Task RequestHintAsync()
    {
        if (Lesson.Exercise is null || Lesson.Exercise.Hints.Length == 0)
        {
            return;
        }

        int nextHintIndex = Enumerable.Range(0, Lesson.Exercise.Hints.Length)
            .FirstOrDefault(index => !RevealedHints.Any(hint => string.Equals(
                hint.Id,
                $"hint-{Lesson.Id}-{index}",
                StringComparison.OrdinalIgnoreCase)), -1);
        if (nextHintIndex < 0)
        {
            return;
        }

        var (success, hint, message) = await _academy.RequestHintAsync(
            Track.Id,
            Lesson.Id,
            nextHintIndex);

        if (success && hint is not null)
        {
            RevealedHints.Add(hint);
            AcademyState state = await _academy.LoadStateAsync();
            if (state.HintUsage is not null)
            {
                RemainingHintsToday = state.HintUsage.RemainingHintsToday;
            }
            OnPropertyChanged(nameof(CanRequestHint));
        }
        else
        {
            FeedbackMessage = message;
        }
    }

    public async Task ContinueToNextLessonAsync()
    {
        int currentIndex = Array.FindIndex(Track.Lessons, l => string.Equals(l.Id, Lesson.Id, StringComparison.OrdinalIgnoreCase));
        if (currentIndex >= 0 && currentIndex + 1 < Track.Lessons.Length)
        {
            LessonDefinition next = Track.Lessons[currentIndex + 1];
            await _workspace.OpenAcademyPracticeAsync(Track, next);
        }
        else
        {
            _workspace.ActivateMode(ActivityDestination.Academy);
        }
    }

    private void ApplyExecutionResult(ExerciseExecutionResult result, string suiteName)
    {
        foreach (RequirementItemViewModel stale in Requirements
                     .Where(requirement => requirement.Id.StartsWith("runtime:", StringComparison.OrdinalIgnoreCase)
                         && !result.RequirementResults.Any(evaluation => string.Equals(
                             evaluation.Id,
                             requirement.Id,
                             StringComparison.OrdinalIgnoreCase)))
                     .ToArray())
        {
            Requirements.Remove(stale);
        }

        foreach (RequirementEvaluation evaluation in result.RequirementResults.Where(evaluation =>
                     evaluation.Id.StartsWith("runtime:", StringComparison.OrdinalIgnoreCase)
                     && !Requirements.Any(requirement => string.Equals(
                         requirement.Id,
                         evaluation.Id,
                         StringComparison.OrdinalIgnoreCase))))
        {
            string searchTerm = evaluation.SourceSearchTerm ?? evaluation.Title;
            Requirements.Add(new RequirementItemViewModel(
                evaluation.Id,
                evaluation.Title,
                openRelevantCode: () => _ = OpenRelevantCodeAsync(
                    searchTerm,
                    evaluation.SourceLine,
                    evaluation.SourceColumn)));
        }

        foreach (RequirementItemViewModel requirement in Requirements)
        {
            RequirementEvaluation? evaluation = result.RequirementResults.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, requirement.Id, StringComparison.OrdinalIgnoreCase));
            if (evaluation is null)
            {
                requirement.Status = RequirementStatus.NotChecked;
                requirement.Feedback = null;
                requirement.Expected = null;
                requirement.Observed = null;
                continue;
            }

            requirement.Status = evaluation.Passed
                ? RequirementStatus.Passed
                : RequirementStatus.Failed;
            requirement.Feedback = evaluation.Message;
            requirement.Expected = evaluation.Expected;
            requirement.Observed = evaluation.Observed;
        }

        _workspace.TestRun.PopulateFromExecution(suiteName, result);
        _workspace.SelectedBottomTab = 3;
        _workspace.BottomPanelVisible = true;
        OnPropertyChanged(nameof(PassedRequirementsCount));
        OnPropertyChanged(nameof(RequirementsSummaryText));
    }

    private void ApplyQuizFeedback(
        IReadOnlyCollection<string> passedRequirementIds,
        IReadOnlyCollection<string> failedRequirementIds)
    {
        foreach (QuizQuestionTabViewModel question in QuizQuestions)
        {
            if (passedRequirementIds.Contains(question.Question.Id, StringComparer.OrdinalIgnoreCase))
            {
                question.IsCorrect = true;
                question.Feedback = "Correct.";
            }
            else if (failedRequirementIds.Contains(question.Question.Id, StringComparer.OrdinalIgnoreCase))
            {
                question.IsCorrect = false;
                question.Feedback = $"Expected: {string.Join(", ", question.Question.CorrectAnswers)}. {question.Question.Explanation}";
            }
        }
    }

    private string GetCurrentSourceCode()
    {
        if (!string.IsNullOrEmpty(LinkedCompanionPath))
        {
            DocumentTabViewModel? doc = _workspace.Documents.FirstOrDefault(d =>
                string.Equals(d.FilePath, LinkedCompanionPath, StringComparison.OrdinalIgnoreCase));
            if (doc is not null)
            {
                return doc.Source;
            }

            if (File.Exists(LinkedCompanionPath))
            {
                return File.ReadAllText(LinkedCompanionPath);
            }
        }

        return Lesson.Exercise?.StarterCode ?? string.Empty;
    }
}
