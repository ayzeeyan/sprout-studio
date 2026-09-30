using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SproutStudio.Academy;

namespace SproutStudio.ViewModels;

public sealed class TestCaseResultViewModel : ObservableObject
{
    public TestCaseResultViewModel(
        string name,
        bool passed,
        int durationMs = 0,
        string? errorMessage = null,
        string? expected = null,
        string? observed = null)
    {
        Name = name;
        Passed = passed;
        DurationMs = durationMs;
        ErrorMessage = errorMessage;
        Expected = expected;
        Observed = observed;
    }

    public string Name { get; }
    public bool Passed { get; }
    public int DurationMs { get; }
    public string? ErrorMessage { get; }
    public string? Expected { get; }
    public string? Observed { get; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string DurationText => DurationMs <= 0 ? "—" : $"{DurationMs} ms";
    public string StatusIcon => Passed ? "\uE73E" : "\uE711";
    public string StatusColor => Passed ? "#49C979" : "#E5534B";
}

public sealed class TestRunViewModel : ObservableObject
{
    private string _suiteName = "No Academy tests have run";
    private bool _isRunning;
    private int _passCount;
    private int _failCount;
    private int _totalDurationMs;

    public TestRunViewModel()
    {
        TestCases = [];
    }

    public string SuiteName
    {
        get => _suiteName;
        set => SetProperty(ref _suiteName, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(StatusBadgeColor));
            }
        }
    }

    public int PassCount
    {
        get => _passCount;
        set
        {
            if (SetProperty(ref _passCount, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public int FailCount
    {
        get => _failCount;
        set
        {
            if (SetProperty(ref _failCount, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public int TotalDurationMs
    {
        get => _totalDurationMs;
        set
        {
            if (SetProperty(ref _totalDurationMs, value))
            {
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }

    public string SummaryText => $"{PassCount} passed, {FailCount} failed";
    public string DurationText => $"{TotalDurationMs} ms";
    public string StatusBadgeText => IsRunning ? "Running" : !HasResults ? "Not Run" : (FailCount == 0 ? "Passed" : "Failed");
    public string StatusBadgeColor => IsRunning ? "#2EA043" : !HasResults ? "#687169" : (FailCount == 0 ? "#2EA043" : "#DA3633");
    public bool HasResults => TestCases.Count > 0;

    public ObservableCollection<TestCaseResultViewModel> TestCases { get; }

    public void PopulateFromAssertions(string suiteName, string[] passedAssertions, string[] failedAssertions, int durationMs = 480)
    {
        SuiteName = suiteName;
        PassCount = passedAssertions.Length;
        FailCount = failedAssertions.Length;
        TotalDurationMs = durationMs;
        IsRunning = false;

        TestCases.Clear();
        int caseCount = Math.Max(1, passedAssertions.Length + failedAssertions.Length);
        int measuredShare = durationMs <= 0 ? 0 : Math.Max(1, durationMs / caseCount);
        foreach (string passed in passedAssertions)
        {
            string desc = passed.StartsWith("contains:", StringComparison.OrdinalIgnoreCase)
                ? passed["contains:".Length..].Trim()
                : passed;
            TestCases.Add(new TestCaseResultViewModel(desc, true, measuredShare));
        }

        foreach (string failed in failedAssertions)
        {
            string desc = failed.StartsWith("contains:", StringComparison.OrdinalIgnoreCase)
                ? failed["contains:".Length..].Trim()
                : failed.StartsWith("not-contains:", StringComparison.OrdinalIgnoreCase)
                    ? $"Avoid: {failed["not-contains:".Length..].Trim()}"
                    : failed;
            TestCases.Add(new TestCaseResultViewModel(desc, false, measuredShare, $"Requirement failed: {desc}"));
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(StatusBadgeColor));
    }

    public void Begin(string suiteName)
    {
        SuiteName = suiteName;
        TestCases.Clear();
        PassCount = 0;
        FailCount = 0;
        TotalDurationMs = 0;
        IsRunning = true;
        OnPropertyChanged(nameof(HasResults));
    }

    public void PopulateFromExecution(string suiteName, ExerciseExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        SuiteName = suiteName;
        TotalDurationMs = Math.Max(0, (int)Math.Round(result.Duration.TotalMilliseconds));
        IsRunning = false;
        TestCases.Clear();

        RequirementEvaluation[] evaluations = result.RequirementResults ?? [];
        int measuredShare = evaluations.Length == 0 || TotalDurationMs == 0
            ? 0
            : Math.Max(1, TotalDurationMs / evaluations.Length);
        foreach (RequirementEvaluation evaluation in evaluations)
        {
            TestCases.Add(new TestCaseResultViewModel(
                evaluation.Title,
                evaluation.Passed,
                measuredShare,
                evaluation.Passed ? null : evaluation.Message,
                evaluation.Expected,
                evaluation.Observed));
        }

        if (evaluations.Length == 0 && result.CompilerDiagnostics.Length > 0)
        {
            foreach (string diagnostic in result.CompilerDiagnostics)
            {
                TestCases.Add(new TestCaseResultViewModel("Toolchain diagnostic", false, 0, diagnostic));
            }
        }

        PassCount = TestCases.Count(test => test.Passed);
        FailCount = TestCases.Count(test => !test.Passed);
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(StatusBadgeColor));
    }
}
