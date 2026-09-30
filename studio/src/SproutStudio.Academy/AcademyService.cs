using SproutStudio.Toolchains;

namespace SproutStudio.Academy;

public sealed class AcademyService
{
    private readonly AcademyStateStore _store;
    private readonly ToolchainManager _toolchains;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AcademyService(
        AcademyCatalog catalog,
        AcademyStateStore store,
        ToolchainManager toolchains)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _toolchains = toolchains ?? throw new ArgumentNullException(nameof(toolchains));
        Recommendations = new RecommendationEngine(Catalog);
    }

    public AcademyCatalog Catalog { get; }

    public RecommendationEngine Recommendations { get; }

    public LessonDefinition GetActivity(string trackId, string activityId)
    {
        TrackDefinition track = Catalog.GetTrack(trackId);
        LessonDefinition? lesson = track.Lessons.FirstOrDefault(item =>
            string.Equals(item.Id, activityId, StringComparison.OrdinalIgnoreCase));
        if (lesson is not null)
        {
            return lesson;
        }

        foreach (ModuleDefinition module in track.Modules ?? [])
        {
            if (module.Checkpoint is not { } checkpoint
                || !string.Equals(checkpoint.Id, activityId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] conceptIds = track.Lessons
                .Where(candidate => module.LessonIds.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase))
                .SelectMany(candidate => candidate.ConceptIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new LessonDefinition(
                checkpoint.Id,
                checkpoint.Title,
                checkpoint.Summary,
                $"Review the core ideas from {module.Title}, then submit every answer. A passing score unlocks the next stage of the course.",
                CheckpointXp(checkpoint),
                conceptIds,
                module.Id,
                new ExerciseDefinition(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    [],
                    [],
                    checkpoint.Title,
                    checkpoint.Summary,
                    checkpoint.Questions),
                TaskType.Checkpoint,
                15);
        }

        throw new ArgumentException(
            $"Activity `{activityId}` does not belong to track `{trackId}`.",
            nameof(activityId));
    }

    public static AcademyService CreateDefault(ToolchainManager toolchains, string? statePath = null) =>
        new(AcademyCatalog.LoadDefault(), new AcademyStateStore(statePath), toolchains);

    public async Task<AcademyState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        AcademyState state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        HintUsageState normalizedHints = EnsureHintUsageReset(state.HintUsage ?? HintUsageState.Default);
        return ReferenceEquals(normalizedHints, state.HintUsage)
            ? state
            : state with { HintUsage = normalizedHints };
    }

    public async Task<AcademyState> SaveProfileAsync(
        AcademyProfile profile,
        CancellationToken cancellationToken = default)
    {
        AcademyProfile normalized = AcademyProfileValidator.ValidateAndNormalize(profile);
        return await ModifyAsync(
            state => state with { Profile = normalized },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<EnrollmentResult> EnrollAsync(
        string trackId,
        CancellationToken cancellationToken = default)
    {
        TrackDefinition track = Catalog.GetTrack(trackId);
        ToolchainDetectionResult? detection = null;
        if (track.RequiredToolchainId is not null)
        {
            detection = await _toolchains.DetectAsync(
                track.RequiredToolchainId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!detection.IsUsable)
            {
                return new EnrollmentResult(
                    EnrollmentStatus.ToolchainRequired,
                    track,
                    detection);
            }
        }

        bool alreadyEnrolled = false;
        await ModifyAsync(
            state =>
            {
                alreadyEnrolled = state.EnrolledTrackIds.Contains(
                    track.Id,
                    StringComparer.OrdinalIgnoreCase);
                if (alreadyEnrolled)
                {
                    return state;
                }
                AcademyState enrolled = state with
                {
                    EnrolledTrackIds = state.EnrolledTrackIds
                        .Append(track.Id)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    ActiveTrackId = state.ActiveTrackId ?? track.Id,
                };
                return AwardAchievements(enrolled).State;
            },
            cancellationToken).ConfigureAwait(false);
        return new EnrollmentResult(
            alreadyEnrolled ? EnrollmentStatus.AlreadyEnrolled : EnrollmentStatus.Enrolled,
            track,
            detection);
    }

    public async Task<LessonCompletionResult> CompleteLessonAsync(
        string trackId,
        string lessonId,
        CancellationToken cancellationToken = default)
    {
        TrackDefinition track = Catalog.GetTrack(trackId);
        LessonDefinition lesson = GetActivity(trackId, lessonId);
        if (lesson.Exercise is { } graded
            && (graded.Assertions.Length > 0 || (graded.Questions?.Length ?? 0) > 0))
        {
            throw new InvalidOperationException(
                $"{lesson.Title} is graded. Use Submit so requirements, attempts, and XP are recorded together.");
        }
        bool alreadyCompleted = false;
        AchievementDefinition[] newlyEarned = [];
        await ModifyAsync(
            state =>
            {
                if (!state.EnrolledTrackIds.Contains(track.Id, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Enroll in {track.Title} before completing its lessons.");
                }
                alreadyCompleted = state.CompletedLessonIds.Contains(
                    lesson.Id,
                    StringComparer.OrdinalIgnoreCase);
                if (alreadyCompleted)
                {
                    return state;
                }
                AcademyState completed = state with
                {
                    CompletedLessonIds = state.CompletedLessonIds
                        .Append(lesson.Id)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    ActiveTrackId = track.Id,
                    ActiveLessonId = lesson.Id,
                };
                (AcademyState awarded, AchievementDefinition[] earned) = AwardAchievements(completed);
                newlyEarned = earned;
                return awarded;
            },
            cancellationToken).ConfigureAwait(false);
        return new LessonCompletionResult(
            lesson,
            alreadyCompleted,
            alreadyCompleted ? 0 : lesson.Xp,
            newlyEarned);
    }

    public ExerciseValidationResult ValidateExercise(LessonDefinition lesson, string submittedCode)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(submittedCode);

        if (lesson.Exercise is null)
        {
            RequirementEvaluation reading = new(
                "reading:completed",
                "Lesson reviewed",
                true,
                "Read the lesson material.",
                "Lesson is ready to complete.",
                "No automated coding requirements apply to this lesson.");
            return new ExerciseValidationResult(
                true,
                "No automated assertions required for this conceptual lesson.",
                [reading.Id],
                [],
                "Completed reading.",
                [reading]);
        }

        var results = new List<RequirementEvaluation>();

        foreach (string assertion in lesson.Exercise.Assertions)
        {
            if (assertion.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
            {
                string expectedText = assertion["contains:".Length..].Trim();
                bool found = submittedCode.Contains(expectedText, StringComparison.Ordinal);
                results.Add(new RequirementEvaluation(
                    assertion,
                    $"Use {expectedText}",
                    found,
                    $"Source contains `{expectedText}`.",
                    found ? "Required source was found." : "Required source was not found.",
                    found
                        ? $"Found `{expectedText}` in the submitted source."
                        : $"Add `{expectedText}` where the requirement is implemented.",
                    expectedText));
            }
            else if (assertion.StartsWith("not-contains:", StringComparison.OrdinalIgnoreCase))
            {
                string forbiddenText = assertion["not-contains:".Length..].Trim();
                bool absent = !submittedCode.Contains(forbiddenText, StringComparison.Ordinal);
                results.Add(new RequirementEvaluation(
                    assertion,
                    $"Avoid {forbiddenText}",
                    absent,
                    $"Source does not contain `{forbiddenText}`.",
                    absent ? "Forbidden source was not found." : "Forbidden source is still present.",
                    absent
                        ? $"The submitted source correctly avoids `{forbiddenText}`."
                        : $"Remove or replace `{forbiddenText}`.",
                    forbiddenText));
            }
            else
            {
                bool found = submittedCode.Contains(assertion, StringComparison.OrdinalIgnoreCase);
                results.Add(new RequirementEvaluation(
                    assertion,
                    assertion,
                    found,
                    $"Source satisfies `{assertion}`.",
                    found ? "Requirement text was found." : "Requirement text was not found.",
                    found ? "Requirement satisfied." : $"Implement the `{assertion}` requirement.",
                    assertion));
            }
        }

        bool success = results.All(result => result.Passed);
        string feedback = success
            ? "All source requirements passed."
            : $"{results.Count(result => !result.Passed)} source requirement(s) need attention.";

        return new ExerciseValidationResult(
            success,
            lesson.Exercise.ExpectedOutput,
            results.Where(result => result.Passed).Select(result => result.Id).ToArray(),
            results.Where(result => !result.Passed).Select(result => result.Id).ToArray(),
            feedback,
            results.ToArray());
    }

    public async Task<ExerciseExecutionResult> ExecuteExerciseAsync(
        LessonDefinition lesson,
        string submittedCode,
        Func<string, string, CancellationToken, Task<ExerciseRuntimeResult>>? executor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(submittedCode);

        // First run static / requirement assertions
        ExerciseValidationResult staticResult = ValidateExercise(lesson, submittedCode);
        if (!staticResult.Success)
        {
            return new ExerciseExecutionResult(
                Success: false,
                ExpectedOutput: lesson.Exercise?.ExpectedOutput,
                ActualOutput: null,
                CompilerDiagnostics: [],
                PassedAssertions: staticResult.PassedAssertions,
                FailedAssertions: staticResult.FailedAssertions,
                Feedback: staticResult.Feedback ?? "Static validation requirements not met.",
                ExecutedWithRealToolchain: false,
                RequirementResults: staticResult.RequirementResults,
                Duration: TimeSpan.Zero,
                ExecutionSummary: "Source validation stopped before toolchain execution.");
        }

        // If an executor is provided (e.g. real Java/Rust/Go toolchain execution)
        if (executor is not null)
        {
            try
            {
                ExerciseRuntimeResult runtime = await executor(lesson.Id, submittedCode, cancellationToken).ConfigureAwait(false);
                var requirementResults = new List<RequirementEvaluation>(staticResult.RequirementResults);

                if (!runtime.Success)
                {
                    requirementResults.Add(new RequirementEvaluation(
                        "runtime:toolchain",
                        "Compile and execute with the configured toolchain",
                        false,
                        "The submitted program compiles and its Academy checks run successfully.",
                        runtime.Diagnostics.FirstOrDefault() ?? runtime.Summary,
                        runtime.Summary,
                        SourceLine: runtime.ErrorLine,
                        SourceColumn: runtime.ErrorColumn));
                    return new ExerciseExecutionResult(
                        Success: false,
                        ExpectedOutput: lesson.Exercise?.ExpectedOutput,
                        ActualOutput: runtime.Output,
                        CompilerDiagnostics: runtime.Diagnostics,
                        PassedAssertions: requirementResults.Where(result => result.Passed).Select(result => result.Id).ToArray(),
                        FailedAssertions: requirementResults.Where(result => !result.Passed).Select(result => result.Id).ToArray(),
                        Feedback: runtime.Summary,
                        ExecutedWithRealToolchain: true,
                        RequirementResults: requirementResults.ToArray(),
                        Duration: runtime.Duration,
                        ExecutionSummary: runtime.Summary);
                }

                requirementResults.Add(new RequirementEvaluation(
                    "runtime:toolchain",
                    runtime.RanProgram ? "Program executes" : "Source compiles",
                    true,
                    runtime.RanProgram
                        ? "The submitted program executes successfully."
                        : "The submitted source compiles successfully.",
                    runtime.Summary,
                    runtime.Summary));

                foreach (RuntimeTestResult test in runtime.TestResults ?? [])
                {
                    requirementResults.Add(new RequirementEvaluation(
                        $"runtime:test:{test.Id}",
                        test.Title,
                        test.Passed,
                        test.Expected,
                        test.Observed,
                        test.Message,
                        test.SourceSearchTerm));
                }

                if (lesson.Exercise?.VerifyExpectedOutput == true
                    && !string.IsNullOrWhiteSpace(lesson.Exercise.ExpectedOutput))
                {
                    string normalizedExpected = lesson.Exercise.ExpectedOutput.Trim().ReplaceLineEndings("\n");
                    string normalizedActual = runtime.Output.Trim().ReplaceLineEndings("\n");
                    bool outputMatches = string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal);
                    requirementResults.Add(new RequirementEvaluation(
                        "runtime:output",
                        "Expected program output",
                        outputMatches,
                        normalizedExpected,
                        string.IsNullOrWhiteSpace(normalizedActual) ? "(no output)" : normalizedActual,
                        outputMatches
                            ? "Program output matched exactly."
                            : "Program output differed from the expected text."));
                }

                bool finalSuccess = requirementResults.All(result => result.Passed);
                return new ExerciseExecutionResult(
                    Success: finalSuccess,
                    ExpectedOutput: lesson.Exercise?.ExpectedOutput,
                    ActualOutput: runtime.Output,
                    CompilerDiagnostics: runtime.Diagnostics,
                    PassedAssertions: requirementResults.Where(result => result.Passed).Select(result => result.Id).ToArray(),
                    FailedAssertions: requirementResults.Where(result => !result.Passed).Select(result => result.Id).ToArray(),
                    Feedback: finalSuccess
                        ? runtime.Summary
                        : $"Execution completed with {requirementResults.Count(result => !result.Passed)} failing requirement(s).",
                    ExecutedWithRealToolchain: true,
                    RequirementResults: requirementResults.ToArray(),
                    Duration: runtime.Duration,
                    ExecutionSummary: runtime.Summary);
            }
            catch (Exception ex)
            {
                return new ExerciseExecutionResult(
                    Success: false,
                    ExpectedOutput: lesson.Exercise?.ExpectedOutput,
                    ActualOutput: null,
                    CompilerDiagnostics: [ex.Message],
                    PassedAssertions: staticResult.PassedAssertions,
                    FailedAssertions: [.. staticResult.FailedAssertions, "runtime:exception"],
                    Feedback: $"Execution aborted: {ex.Message}",
                    ExecutedWithRealToolchain: true,
                    RequirementResults:
                    [
                        .. staticResult.RequirementResults,
                        new RequirementEvaluation(
                            "runtime:exception",
                            "Toolchain execution",
                            false,
                            "The configured toolchain runs the submitted source.",
                            ex.Message,
                            "Toolchain execution raised an exception."),
                    ],
                    Duration: TimeSpan.Zero,
                    ExecutionSummary: "Toolchain execution aborted.");
            }
        }

        return new ExerciseExecutionResult(
            Success: true,
            ExpectedOutput: lesson.Exercise?.ExpectedOutput,
            ActualOutput: lesson.Exercise?.ExpectedOutput,
            CompilerDiagnostics: [],
            PassedAssertions: staticResult.PassedAssertions,
            FailedAssertions: staticResult.FailedAssertions,
            Feedback: staticResult.Feedback ?? "Exercise passed assertions.",
            ExecutedWithRealToolchain: false,
            RequirementResults: staticResult.RequirementResults,
            Duration: TimeSpan.Zero,
            ExecutionSummary: "Source requirements evaluated without a runtime toolchain.");
    }

    public async Task<(SubmissionRecord Submission, ExerciseExecutionResult ExecutionResult, LessonCompletionResult? CompletionResult)> SubmitCodingExerciseAsync(
        string trackId,
        string lessonId,
        string submittedCode,
        Func<string, string, CancellationToken, Task<ExerciseRuntimeResult>>? executor = null,
        IReadOnlyDictionary<string, string[]>? quizAnswers = null,
        CancellationToken cancellationToken = default)
    {
        TrackDefinition track = Catalog.GetTrack(trackId);
        LessonDefinition lesson = GetActivity(trackId, lessonId);

        ExerciseExecutionResult execResult = await ExecuteExerciseAsync(lesson, submittedCode, executor, cancellationToken).ConfigureAwait(false);
        QuizQuestion[] knowledgeQuestions = lesson.Exercise?.Questions ?? [];
        if (knowledgeQuestions.Length > 0)
        {
            QuizEvaluation knowledge = EvaluateQuizAnswers(knowledgeQuestions, quizAnswers);
            RequirementEvaluation[] combinedRequirements =
            [
                .. execResult.RequirementResults,
                .. knowledge.Requirements,
            ];
            bool combinedSuccess = execResult.Success && knowledge.Passed;
            execResult = execResult with
            {
                Success = combinedSuccess,
                PassedAssertions = combinedRequirements
                    .Where(result => result.Passed)
                    .Select(result => result.Id)
                    .ToArray(),
                FailedAssertions = combinedRequirements
                    .Where(result => !result.Passed)
                    .Select(result => result.Id)
                    .ToArray(),
                Feedback = combinedSuccess
                    ? $"{execResult.Feedback} Knowledge check passed ({knowledge.ScorePercent}%)."
                    : $"{execResult.Feedback} Knowledge check: {knowledge.CorrectCount}/{knowledgeQuestions.Length} correct ({knowledge.ScorePercent}%).",
                RequirementResults = combinedRequirements,
            };
        }

        string persistedAnswer = quizAnswers is null || quizAnswers.Count == 0
            ? submittedCode
            : string.Join(Environment.NewLine,
            [
                submittedCode,
                string.Empty,
                "--- Academy knowledge answers ---",
                .. quizAnswers.Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value)}"),
            ]);

        SubmissionRecord? submission = null;
        LessonCompletionResult? completionResult = null;
        await ModifyAsync(
            state =>
            {
                EnsureEnrolled(state, track);
                EnsureActivityUnlocked(state, track, lesson);
                int attemptNumber = state.Submissions.Count(record =>
                    string.Equals(record.LessonId, lessonId, StringComparison.OrdinalIgnoreCase)) + 1;
                submission = new SubmissionRecord(
                    Id: $"sub-{lessonId}-{attemptNumber}-{Guid.NewGuid():N}",
                    TrackId: trackId,
                    LessonId: lessonId,
                    ModuleId: lesson.ModuleId,
                    ExerciseTitle: lesson.Exercise?.Title ?? lesson.Title,
                    AttemptNumber: attemptNumber,
                    SubmittedAt: DateTimeOffset.UtcNow,
                    Type: SubmissionType.CodeExercise,
                    Answer: persistedAnswer,
                    Passed: execResult.Success,
                    Score: execResult.Success ? 10 : Math.Max(0, 10 - execResult.FailedAssertions.Length * 3),
                    Feedback: execResult.Feedback,
                    PassedRequirements: execResult.PassedAssertions,
                    FailedRequirements: execResult.FailedAssertions,
                    CompilerDiagnostics: execResult.CompilerDiagnostics);

                AcademyState updated = state with
                {
                    Submissions = state.Submissions.Append(submission).ToArray(),
                    ActiveTrackId = track.Id,
                    ActiveLessonId = lesson.Id,
                };
                if (!execResult.Success)
                {
                    return updated;
                }

                bool alreadyCompleted = state.CompletedLessonIds.Contains(
                    lesson.Id,
                    StringComparer.OrdinalIgnoreCase);
                if (!alreadyCompleted)
                {
                    updated = updated with
                    {
                        CompletedLessonIds = updated.CompletedLessonIds
                            .Append(lesson.Id)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Order(StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                    };
                }
                (AcademyState awarded, AchievementDefinition[] earned) = AwardAchievements(updated);
                completionResult = new LessonCompletionResult(
                    lesson,
                    alreadyCompleted,
                    alreadyCompleted ? 0 : lesson.Xp,
                    earned);
                return awarded;
            },
            cancellationToken).ConfigureAwait(false);

        return (submission!, execResult, completionResult);
    }

    public async Task<(SubmissionRecord Submission, bool Passed, int Score, string Feedback, LessonCompletionResult? CompletionResult)> SubmitQuizAnswersAsync(
        string trackId,
        string lessonId,
        IReadOnlyDictionary<string, string[]> answers,
        CancellationToken cancellationToken = default)
    {
        TrackDefinition track = Catalog.GetTrack(trackId);
        LessonDefinition lesson = GetActivity(trackId, lessonId);

        QuizQuestion[] questions = lesson.Exercise?.Questions ?? [];
        if (questions.Length == 0)
        {
            throw new InvalidOperationException($"Lesson `{lessonId}` does not have quiz questions.");
        }

        QuizEvaluation evaluation = EvaluateQuizAnswers(questions, answers);
        int correctCount = evaluation.CorrectCount;
        string[] passedReqs = evaluation.Requirements
            .Where(result => result.Passed)
            .Select(result => result.Id)
            .ToArray();
        string[] failedReqs = evaluation.Requirements
            .Where(result => !result.Passed)
            .Select(result => result.Id)
            .ToArray();
        int scorePercent = evaluation.ScorePercent;
        bool passed = evaluation.Passed;

        string feedback = passed
            ? $"Quiz passed! Score: {correctCount}/{questions.Length} ({scorePercent}%)"
            : $"Quiz requires review. Score: {correctCount}/{questions.Length} ({scorePercent}%). Review the questions and try again.";

        SubmissionRecord? submission = null;
        LessonCompletionResult? completionResult = null;
        await ModifyAsync(
            state =>
            {
                EnsureEnrolled(state, track);
                EnsureActivityUnlocked(state, track, lesson);
                int attemptNumber = state.Submissions.Count(record =>
                    string.Equals(record.LessonId, lessonId, StringComparison.OrdinalIgnoreCase)) + 1;
                submission = new SubmissionRecord(
                    Id: $"sub-quiz-{lessonId}-{attemptNumber}-{Guid.NewGuid():N}",
                    TrackId: trackId,
                    LessonId: lessonId,
                    ModuleId: lesson.ModuleId,
                    ExerciseTitle: lesson.Title,
                    AttemptNumber: attemptNumber,
                    SubmittedAt: DateTimeOffset.UtcNow,
                    Type: SubmissionType.Quiz,
                    Answer: string.Join("; ", answers.Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value)}")),
                    Passed: passed,
                    Score: scorePercent,
                    Feedback: feedback,
                    PassedRequirements: passedReqs,
                    FailedRequirements: failedReqs,
                    CompilerDiagnostics: []);

                AcademyState updated = state with
                {
                    Submissions = state.Submissions.Append(submission).ToArray(),
                    ActiveTrackId = track.Id,
                    ActiveLessonId = lesson.Id,
                };
                if (!passed)
                {
                    return updated;
                }

                bool alreadyCompleted = state.CompletedLessonIds.Contains(
                    lesson.Id,
                    StringComparer.OrdinalIgnoreCase);
                if (!alreadyCompleted)
                {
                    updated = updated with
                    {
                        CompletedLessonIds = updated.CompletedLessonIds
                            .Append(lesson.Id)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Order(StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                    };
                }
                (AcademyState awarded, AchievementDefinition[] earned) = AwardAchievements(updated);
                completionResult = new LessonCompletionResult(
                    lesson,
                    alreadyCompleted,
                    alreadyCompleted ? 0 : lesson.Xp,
                    earned);
                return awarded;
            },
            cancellationToken).ConfigureAwait(false);

        return (submission!, passed, scorePercent, feedback, completionResult);
    }

    private static QuizEvaluation EvaluateQuizAnswers(
        QuizQuestion[] questions,
        IReadOnlyDictionary<string, string[]>? answers)
    {
        var requirements = new List<RequirementEvaluation>(questions.Length);
        foreach (QuizQuestion question in questions)
        {
            string[] userAnswers = answers is not null
                && answers.TryGetValue(question.Id, out string[]? supplied)
                ? supplied ?? []
                : [];
            bool correct = question.CorrectAnswers.Length == userAnswers.Length
                && question.CorrectAnswers.All(expected => userAnswers.Any(actual =>
                    string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase)));
            string observed = userAnswers.Length == 0
                ? "No answer selected."
                : string.Join(", ", userAnswers);
            requirements.Add(new RequirementEvaluation(
                question.Id,
                question.Question,
                correct,
                string.Join(", ", question.CorrectAnswers),
                observed,
                correct ? "Correct." : question.Explanation));
        }

        int correctCount = requirements.Count(result => result.Passed);
        int scorePercent = questions.Length == 0
            ? 100
            : (int)Math.Round((double)correctCount / questions.Length * 100);
        return new QuizEvaluation(
            scorePercent >= 70,
            scorePercent,
            correctCount,
            requirements.ToArray());
    }

    private sealed record QuizEvaluation(
        bool Passed,
        int ScorePercent,
        int CorrectCount,
        RequirementEvaluation[] Requirements);

    public async Task<(bool Success, HintItem? Hint, string Message)> RequestHintAsync(
        string trackId,
        string lessonId,
        int hintIndex,
        CancellationToken cancellationToken = default)
    {
        TrackDefinition track = Catalog.GetTrack(trackId);
        LessonDefinition lesson = GetActivity(trackId, lessonId);

        string[] hints = lesson.Exercise?.Hints ?? [];
        if (hintIndex < 0 || hintIndex >= hints.Length)
        {
            return (false, null, "No further hints are available for this exercise.");
        }

        string hintId = $"hint-{lessonId}-{hintIndex}";
        string hintContent = hints[hintIndex];
        int level = Math.Min(4, hintIndex + 1);
        string levelName = level switch
        {
            1 => "Nudge",
            2 => "Concept",
            3 => "Structure",
            _ => "Detail",
        };

        AcademyState state = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
        HintUsageState usage = EnsureHintUsageReset(state.HintUsage ?? HintUsageState.Default);

        bool alreadyRevealed = usage.RevealedHintIds.Contains(hintId, StringComparer.OrdinalIgnoreCase);
        if (alreadyRevealed)
        {
            var revealedHint = new HintItem(hintId, level, levelName, hintContent, IsRevealed: true);
            return (true, revealedHint, "Hint previously revealed.");
        }

        if (!usage.CanRevealNewHint)
        {
            string limitMessage = "You've used today's guided hints.\n\nYou can still:\n• Review the lesson\n• Inspect related documentation\n• Retry the exercise\n• Use the Concept Browser\n• Run/check your code";
            return (false, null, limitMessage);
        }

        // Consume hint from daily allowance
        int newUsedCount = usage.Mode == HintAssistanceMode.Unlimited ? usage.UsedCountToday : usage.UsedCountToday + 1;
        var updatedUsage = usage with
        {
            UsedCountToday = newUsedCount,
            RevealedHintIds = usage.RevealedHintIds.Append(hintId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        };

        await ModifyAsync(
            s => s with { HintUsage = updatedUsage },
            cancellationToken).ConfigureAwait(false);

        var newHint = new HintItem(hintId, level, levelName, hintContent, IsRevealed: true);
        return (true, newHint, "Hint unlocked.");
    }

    public async Task<AcademyState> ConfigureHintSettingsAsync(
        HintAssistanceMode mode,
        int? customDailyAllowance = null,
        CancellationToken cancellationToken = default)
    {
        return await ModifyAsync(
            state =>
            {
                HintUsageState current = EnsureHintUsageReset(state.HintUsage ?? HintUsageState.Default);
                int allowance = customDailyAllowance.HasValue && customDailyAllowance.Value > 0
                    ? customDailyAllowance.Value
                    : current.DailyAllowance;
                var updated = current with
                {
                    Mode = mode,
                    DailyAllowance = allowance,
                };
                return state with { HintUsage = updated };
            },
            cancellationToken).ConfigureAwait(false);
    }

    public AcademyDashboard CreateDashboard(AcademyState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var completed = state.CompletedLessonIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        TrackProgress[] tracks = Catalog.Tracks
            .Select(track =>
            {
                int completedLessons = track.Lessons.Count(lesson => completed.Contains(lesson.Id));
                CheckpointDefinition[] checkpoints = (track.Modules ?? [])
                    .Select(module => module.Checkpoint)
                    .Where(checkpoint => checkpoint is not null)
                    .Cast<CheckpointDefinition>()
                    .ToArray();
                int completedActivities = completedLessons
                    + checkpoints.Count(checkpoint => completed.Contains(checkpoint.Id));
                int totalActivities = track.Lessons.Length + checkpoints.Length;
                return new TrackProgress(
                    track,
                    state.EnrolledTrackIds.Contains(track.Id, StringComparer.OrdinalIgnoreCase),
                    completedLessons,
                    track.Lessons.Length,
                    totalActivities == 0 ? 0 : completedActivities * 100 / totalActivities);
            })
            .ToArray();

        int totalXp = Catalog.Tracks
            .SelectMany(track => track.Lessons)
            .Where(lesson => completed.Contains(lesson.Id))
            .Sum(lesson => lesson.Xp)
            + Catalog.Tracks
                .SelectMany(track => track.Modules ?? [])
                .Select(module => module.Checkpoint)
                .Where(checkpoint => checkpoint is not null && completed.Contains(checkpoint.Id))
                .Sum(checkpoint => CheckpointXp(checkpoint!));

        AchievementDefinition[] achievements = state.AchievementAwards
            .Select(award => Catalog.GetAchievement(award.Id))
            .ToArray();

        HintUsageState hintUsage = EnsureHintUsageReset(state.HintUsage ?? HintUsageState.Default);

        // Determine active/primary track
        TrackDefinition? primaryTrack = null;
        if (!string.IsNullOrEmpty(state.ActiveTrackId))
        {
            primaryTrack = Catalog.Tracks.FirstOrDefault(t => string.Equals(t.Id, state.ActiveTrackId, StringComparison.OrdinalIgnoreCase));
        }
        if (primaryTrack is null && state.EnrolledTrackIds.Length > 0)
        {
            primaryTrack = Catalog.Tracks.FirstOrDefault(t => state.EnrolledTrackIds.Contains(t.Id, StringComparer.OrdinalIgnoreCase));
        }
        primaryTrack ??= Catalog.Tracks.FirstOrDefault();

        // 1. Next "Continue Learning" Task
        AcademyTask? continueTask = null;
        if (primaryTrack is not null)
        {
            LessonDefinition? nextLesson = FindNextActivity(primaryTrack, completed);

            if (nextLesson is not null)
            {
                int trackPercent = tracks.First(item => string.Equals(
                    item.Track.Id,
                    primaryTrack.Id,
                    StringComparison.OrdinalIgnoreCase)).CompletionPercent;
                ModuleDefinition? mod = primaryTrack.Modules?.FirstOrDefault(m => string.Equals(m.Id, nextLesson.ModuleId, StringComparison.OrdinalIgnoreCase));

                continueTask = new AcademyTask(
                    Id: $"task-{nextLesson.Id}",
                    Title: nextLesson.Title,
                    TrackId: primaryTrack.Id,
                    TrackTitle: primaryTrack.Title,
                    ModuleId: mod?.Id,
                    ModuleTitle: mod?.Title ?? "Core Fundamentals",
                    LessonId: nextLesson.Id,
                    Type: nextLesson.TaskType == TaskType.Checkpoint
                        ? TaskType.Checkpoint
                        : nextLesson.Exercise is not null
                            ? TaskType.CodingExercise
                            : nextLesson.TaskType,
                    Status: completed.Contains(nextLesson.Id) ? TaskStatus.Completed : TaskStatus.InProgress,
                    EstimatedMinutes: nextLesson.EstimatedMinutes,
                    Priority: 1,
                    CompletionRequirement: nextLesson.TaskType == TaskType.Checkpoint
                        ? "Pass checkpoint review questions"
                        : nextLesson.Exercise is not null
                            ? "Pass coding requirements"
                            : "Complete reading",
                    ProgressPercent: trackPercent);
            }
        }

        // 2. Prioritized To-Do Task Queue
        var prioritizedTasks = new List<AcademyTask>();

        if (continueTask is not null && continueTask.Status != TaskStatus.Completed)
        {
            prioritizedTasks.Add(continueTask);
        }

        // Add any failed submissions needing revision
        var submissionsByLesson = (state.Submissions ?? [])
            .GroupBy(s => s.LessonId)
            .Select(g => g.OrderByDescending(s => s.SubmittedAt).First())
            .Where(s => !s.Passed)
            .Take(3);

        foreach (var failedSub in submissionsByLesson)
        {
            TrackDefinition? subTrack = Catalog.Tracks.FirstOrDefault(t => string.Equals(t.Id, failedSub.TrackId, StringComparison.OrdinalIgnoreCase));
            LessonDefinition? subLesson = subTrack?.Lessons.FirstOrDefault(l => string.Equals(l.Id, failedSub.LessonId, StringComparison.OrdinalIgnoreCase));
            if (subTrack is not null && subLesson is not null && !completed.Contains(subLesson.Id))
            {
                prioritizedTasks.Add(new AcademyTask(
                    Id: $"rev-{subLesson.Id}",
                    Title: $"Revise: {subLesson.Title}",
                    TrackId: subTrack.Id,
                    TrackTitle: subTrack.Title,
                    ModuleId: subLesson.ModuleId,
                    ModuleTitle: "Exercise Review",
                    LessonId: subLesson.Id,
                    Type: TaskType.Revision,
                    Status: TaskStatus.NeedsRevision,
                    EstimatedMinutes: 10,
                    Priority: 2,
                    CompletionRequirement: "Fix failing requirements & resubmit",
                    ProgressPercent: 50));
            }
        }

        // Add next lessons across other enrolled tracks
        foreach (string enrolledTrackId in state.EnrolledTrackIds)
        {
            if (primaryTrack is not null && string.Equals(primaryTrack.Id, enrolledTrackId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TrackDefinition? enrolledTrack = Catalog.Tracks.FirstOrDefault(t => string.Equals(t.Id, enrolledTrackId, StringComparison.OrdinalIgnoreCase));
            if (enrolledTrack is null) continue;

            LessonDefinition? nextInTrack = FindNextActivity(enrolledTrack, completed);
            if (nextInTrack is not null)
            {
                ModuleDefinition? mod = enrolledTrack.Modules?.FirstOrDefault(m => string.Equals(m.Id, nextInTrack.ModuleId, StringComparison.OrdinalIgnoreCase));
                prioritizedTasks.Add(new AcademyTask(
                    Id: $"task-{nextInTrack.Id}",
                    Title: nextInTrack.Title,
                    TrackId: enrolledTrack.Id,
                    TrackTitle: enrolledTrack.Title,
                    ModuleId: mod?.Id,
                    ModuleTitle: mod?.Title ?? "Course Module",
                    LessonId: nextInTrack.Id,
                    Type: nextInTrack.TaskType == TaskType.Checkpoint
                        ? TaskType.Checkpoint
                        : nextInTrack.Exercise is not null
                            ? TaskType.CodingExercise
                            : nextInTrack.TaskType,
                    Status: TaskStatus.NotStarted,
                    EstimatedMinutes: nextInTrack.EstimatedMinutes,
                    Priority: 4,
                    CompletionRequirement: nextInTrack.TaskType == TaskType.Checkpoint
                        ? "Pass checkpoint review questions"
                        : nextInTrack.Exercise is not null
                            ? "Pass coding requirements"
                            : "Complete reading",
                    ProgressPercent: 0));
            }
        }

        // Checkpoints
        foreach (TrackDefinition t in Catalog.Tracks.Where(tr => state.EnrolledTrackIds.Contains(tr.Id, StringComparer.OrdinalIgnoreCase)))
        {
            if (t.Modules is null) continue;
            foreach (ModuleDefinition m in t.Modules)
            {
                if (m.Checkpoint is not null)
                {
                    bool allLessonsDone = m.LessonIds.All(lid => completed.Contains(lid));
                    bool alreadyQueued = prioritizedTasks.Any(task => string.Equals(
                        task.LessonId,
                        m.Checkpoint.Id,
                        StringComparison.OrdinalIgnoreCase));
                    if (allLessonsDone && !completed.Contains(m.Checkpoint.Id) && !alreadyQueued)
                    {
                        prioritizedTasks.Add(new AcademyTask(
                            Id: $"cp-{m.Checkpoint.Id}",
                            Title: $"Checkpoint: {m.Checkpoint.Title}",
                            TrackId: t.Id,
                            TrackTitle: t.Title,
                            ModuleId: m.Id,
                            ModuleTitle: m.Title,
                            LessonId: m.Checkpoint.Id,
                            Type: TaskType.Checkpoint,
                            Status: TaskStatus.ReadyToSubmit,
                            EstimatedMinutes: 15,
                            Priority: 3,
                            CompletionRequirement: "Pass checkpoint review questions",
                            ProgressPercent: 80));
                    }
                }
            }
        }

        // 3. Recent Submissions (newest first, limited to 15)
        SubmissionRecord[] recentSubmissions = (state.Submissions ?? [])
            .OrderByDescending(s => s.SubmittedAt)
            .Take(15)
            .ToArray();

        // 4. Metrics for today
        SubmissionRecord[] firstPassingSubmissions = (state.Submissions ?? [])
            .Where(submission => submission.Passed)
            .GroupBy(submission => submission.LessonId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(submission => submission.SubmittedAt).First())
            .ToArray();
        SubmissionRecord[] firstPassesToday = firstPassingSubmissions
            .Where(submission => submission.SubmittedAt.LocalDateTime.Date == DateTime.Today)
            .ToArray();
        int completedLessonsToday = firstPassesToday.Length;
        int xpEarnedToday = firstPassesToday.Sum(submission =>
        {
            try
            {
                return GetActivity(submission.TrackId, submission.LessonId).Xp;
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (KeyNotFoundException)
            {
                return 0;
            }
        });

        return new AcademyDashboard(
            State: state,
            Tracks: tracks,
            ContinueTask: continueTask,
            PrioritizedTasks: prioritizedTasks.OrderBy(t => t.Priority).ToArray(),
            HintUsage: hintUsage,
            RecentSubmissions: recentSubmissions,
            TotalXp: totalXp,
            Level: 1 + (totalXp / 100),
            XpTowardNextLevel: totalXp % 100,
            EarnedAchievements: achievements,
            CompletedLessonsToday: completedLessonsToday,
            XpEarnedToday: xpEarnedToday);
    }

    private static HintUsageState EnsureHintUsageReset(HintUsageState usage)
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (!string.Equals(usage.LastUsageDate, today, StringComparison.OrdinalIgnoreCase))
        {
            return usage with
            {
                LastUsageDate = today,
                UsedCountToday = 0,
            };
        }
        return usage;
    }

    private static int CheckpointXp(CheckpointDefinition checkpoint) =>
        50 + (checkpoint.Questions.Length * 10);

    private LessonDefinition? FindNextActivity(
        TrackDefinition track,
        IReadOnlySet<string> completed)
    {
        var assignedLessonIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ModuleDefinition module in track.Modules ?? [])
        {
            foreach (string lessonId in module.LessonIds)
            {
                assignedLessonIds.Add(lessonId);
                if (!completed.Contains(lessonId))
                {
                    return GetActivity(track.Id, lessonId);
                }
            }

            if (module.Checkpoint is { } checkpoint && !completed.Contains(checkpoint.Id))
            {
                return GetActivity(track.Id, checkpoint.Id);
            }
        }

        return track.Lessons.FirstOrDefault(lesson =>
            !assignedLessonIds.Contains(lesson.Id) && !completed.Contains(lesson.Id));
    }

    private static void EnsureEnrolled(AcademyState state, TrackDefinition track)
    {
        if (!state.EnrolledTrackIds.Contains(track.Id, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Enroll in {track.Title} before submitting its activities.");
        }
    }

    private static void EnsureActivityUnlocked(
        AcademyState state,
        TrackDefinition track,
        LessonDefinition activity)
    {
        if (activity.TaskType != TaskType.Checkpoint)
        {
            return;
        }

        ModuleDefinition? module = (track.Modules ?? []).FirstOrDefault(candidate =>
            string.Equals(candidate.Id, activity.ModuleId, StringComparison.OrdinalIgnoreCase));
        string[] incompleteLessons = module?.LessonIds
            .Where(lessonId => !state.CompletedLessonIds.Contains(
                lessonId,
                StringComparer.OrdinalIgnoreCase))
            .ToArray() ?? [];
        if (incompleteLessons.Length > 0)
        {
            throw new InvalidOperationException(
                $"Complete all lessons in {module!.Title} before submitting its checkpoint.");
        }
    }

    private async Task<AcademyState> ModifyAsync(
        Func<AcademyState, AcademyState> change,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AcademyState current = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            AcademyState updated = change(current);
            if (!ReferenceEquals(current, updated))
            {
                await _store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            }
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private (AcademyState State, AchievementDefinition[] NewAchievements) AwardAchievements(
        AcademyState state)
    {
        var earnedIds = state.AchievementAwards
            .Select(award => award.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eligible = new List<string>();
        if (state.EnrolledTrackIds.Length > 0)
        {
            eligible.Add("seedling-planted");
        }
        if (state.CompletedLessonIds.Length > 0)
        {
            eligible.Add("first-execution");
        }
        if (TrackCompleted("sprout-fundamentals"))
        {
            eligible.Add("syntax-adapter");
        }
        if (CompletedAnyTrackLesson("python-automation"))
        {
            eligible.Add("python-pioneer");
        }
        if (Catalog.Tracks.Count(TrackHasCompletedLesson) >= 2)
        {
            eligible.Add("polyglot-explorer");
        }
        if (TrackCompleted("java-oop-zero-to-hero"))
        {
            eligible.Add("java-architect");
        }
        if (Catalog.Tracks.Any(track => TrackCompleted(track.Id)))
        {
            eligible.Add("deep-roots");
        }

        AchievementDefinition[] newlyEarned = eligible
            .Where(earnedIds.Add)
            .Select(Catalog.GetAchievement)
            .ToArray();
        if (newlyEarned.Length == 0)
        {
            return (state, []);
        }
        DateTimeOffset earnedAt = DateTimeOffset.UtcNow;
        AcademyState updated = state with
        {
            AchievementAwards = state.AchievementAwards
                .Concat(newlyEarned.Select(item => new AchievementAward(item.Id, earnedAt)))
                .ToArray(),
        };
        return (updated, newlyEarned);

        bool TrackHasCompletedLesson(TrackDefinition track) =>
            CompletedAnyTrackLesson(track.Id);

        bool CompletedAnyTrackLesson(string id)
        {
            TrackDefinition track = Catalog.GetTrack(id);
            return track.Lessons.Any(lesson => state.CompletedLessonIds.Contains(
                lesson.Id,
                StringComparer.OrdinalIgnoreCase));
        }

        bool TrackCompleted(string id)
        {
            TrackDefinition track = Catalog.GetTrack(id);
            bool lessonsComplete = track.Lessons.All(lesson => state.CompletedLessonIds.Contains(
                lesson.Id,
                StringComparer.OrdinalIgnoreCase));
            bool checkpointsComplete = (track.Modules ?? [])
                .Select(module => module.Checkpoint)
                .Where(checkpoint => checkpoint is not null)
                .All(checkpoint => state.CompletedLessonIds.Contains(
                    checkpoint!.Id,
                    StringComparer.OrdinalIgnoreCase));
            return lessonsComplete && checkpointsComplete;
        }
    }
}
