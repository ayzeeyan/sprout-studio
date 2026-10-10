using System.Diagnostics;
using SproutStudio.Academy;
using SproutStudio.Core;
using SproutStudio.Editor;
using SproutStudio.Interop;
using SproutStudio.Services;
using SproutStudio.SourceControl;
using SproutStudio.Toolchains;

if (args is ["--toolchain-test-wait"])
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    return 0;
}
if (args is ["--toolchain-test-echo", string value])
{
    Console.Out.Write(value);
    Console.Error.Write("captured-error");
    return 7;
}

var tests = new (string Name, Func<Task> Run)[]
{
    ("Native engine metadata validates compatibility", NativeEngineMetadataValidatesCompatibility),
    ("Native run returns actual output", NativeRunReturnsOutput),
    ("Native run executes embedded Python", NativeRunExecutesEmbeddedPython),
    ("Native project run isolates Python packages", NativeProjectRunIsolatesPythonPackages),
    ("Native diagnostics preserve locations", NativeDiagnosticsPreserveLocations),
    ("Native diagnostics preserve related data and fixes", NativeDiagnosticsPreserveRelatedDataAndFixes),
    ("Native byte spans navigate Unicode source", NativeSpansNavigateUnicodeSource),
    ("Native language service completes hovers defines and formats", NativeLanguageServiceWorks),
    ("Native AST JSON returns serialized AST tree and nodes", NativeAstJsonReturnsSerializedAst),
    ("Native debugger records variables and canvas commands", NativeDebuggerAndCanvasWork),
    ("Native package hub updates manifests and builds bundles", NativePackageHubAndBuildWork),
    ("Editor workspace supports tabs and additional languages", EditorWorkspaceSupportsAdditionalLanguages),
    ("Editor coordinates normalize line endings", EditorCoordinatesNormalizeLineEndings),
    ("Editor breadcrumbs stay project relative", EditorBreadcrumbsStayProjectRelative),
    ("Editor toggles aligned line comments", EditorTogglesAlignedLineComments),
    ("Learning habits compute streaks goals and calendar", LearningHabitsComputeStreaksGoalsAndCalendar),
    ("Concept detector maps code to Academy concepts", ConceptDetectorMapsCodeToConcepts),
    ("Syntax highlighting loads for every supported language", SyntaxHighlightingLoadsForEveryLanguage),
    ("Academy Handbook guides are complete and reference real docs", AcademyHandbookGuidesAreComplete),
    ("Academy Handbook code samples run on the native engine", AcademyHandbookSamplesRun),
    ("Academy guide reading counts toward streaks and achievements", AcademyGuideReadingCountsTowardProgress),
    ("Language registry supplies shared product identities", LanguageRegistrySuppliesSharedIdentities),
    ("Project search filters source and ignores generated output", ProjectSearchFiltersAndIgnoresGeneratedOutput),
    ("Git status parser preserves branch and file state", GitStatusParserPreservesState),
    ("Git service stages commits diffs and unstages", GitServiceRunsLocalWorkflow),
    ("Studio layout state persists and sanitizes", StudioLayoutStatePersistsAndSanitizes),
    ("Session projects response into panels", SessionProjectsResponse),
    ("Response parser rejects incompatible API", ResponseParserRejectsApiMismatch),
    ("Native project API creates and reloads templates", NativeProjectCreatesAndLoads),
    ("Native project API returns validation diagnostics", NativeProjectValidation),
    ("Recent projects persist without duplicates", RecentProjectsPersistWithoutDuplicates),
    ("Recent projects retain only the ten newest entries", RecentProjectsRetainTenNewest),
    ("Recent projects sanitize metadata and support removal", RecentProjectsSanitizeAndRemove),
    ("Studio session saves edited source atomically", StudioSessionSavesSource),
    ("Studio session saves a new source file", StudioSessionSavesNewSource),
    ("Phase 5 project workflow executes edited source", PhaseFiveProjectWorkflow),
    ("Process runner preserves structured arguments and output", ProcessRunnerPreservesArguments),
    ("Process runner cancels and terminates child process", ProcessRunnerCancelsChild),
    ("Process runner reports validation timeout", ProcessRunnerReportsTimeout),
    ("Rust detector validates complete custom toolchain", RustDetectorValidatesCompleteToolchain),
    ("Rust detector distinguishes partial installation", RustDetectorDistinguishesPartialInstallation),
    ("Rust detector rejects unsupported version", RustDetectorRejectsUnsupportedVersion),
    ("Rust detector reports broken executable", RustDetectorReportsBrokenExecutable),
    ("Go detector validates go and gofmt", GoDetectorValidatesCompleteToolchain),
    ("Python detector validates runtime and actions", PythonDetectorValidatesRuntimeAndActions),
    ("Java detector distinguishes full and partial JDKs", JavaDetectorDistinguishesFullAndPartialJdks),
    ("Toolchain settings persist and clear custom paths", ToolchainSettingsPersistCustomPaths),
    ("Toolchain manager builds safe action requests", ToolchainManagerBuildsSafeActionRequests),
    ("Live toolchain detection returns structured states", LiveToolchainDetectionReturnsStructuredStates),
    ("Academy catalog is complete and internally linked", AcademyCatalogIsComplete),
    ("Academy local profile persists with validation", AcademyLocalProfilePersists),
    ("Academy recommendations are transparent and rule based", AcademyRecommendationsAreTransparent),
    ("Academy progress prevents repeated XP awards", AcademyProgressPreventsXpFarming),
    ("Academy enrollment validates required toolchains", AcademyEnrollmentValidatesToolchains),
    ("Academy concept browser searches transferable examples", AcademyConceptBrowserSearchesExamples),
    ("Java OOP course loads, builds dependency trees, and validates submissions", JavaOopCourseLoadsAndValidates),
    ("Java OOP solutions compile through the installed JDK", JavaOopSolutionsCompileWithInstalledJdk),
    ("Academy external-track solutions execute through installed toolchains", AcademyExternalTrackSolutionsExecuteWithInstalledToolchains),
    ("Update service parses GitHub releases and detects newer versions", UpdateServiceParsesReleasesAndDetectsUpdates),
    ("Update service ignores current or older versions", UpdateServiceIgnoresCurrentOrOlderVersions),
    ("Academy real execution validates output and compiler diagnostics", AcademyRealExecutionValidatesOutput),
    ("Session recovery journals dirty buffers and recovers unclosed sessions", SessionRecoveryJournalsAndRestores),
    ("Academy dashboard prioritizes next action and task queue", AcademyDashboardPrioritizesTasks),
    ("Academy submission system tracks multiple attempts and quiz review", AcademySubmissionsAndAttempts),
    ("Academy Run Tests leaves attempts completion and XP untouched", AcademyRunTestsDoesNotPersistProgress),
    ("Academy Submit records one attempt and awards XP exactly once", AcademySubmitPersistsOneAttemptAndXpOnce),
    ("Academy coding lessons grade their integrated knowledge checks", AcademyCodingLessonGradesKnowledgeCheck),
    ("Academy checkpoints persist failures and complete only after a passing submission", AcademyCheckpointPassFailAndCompletion),
    ("Academy hints consume daily allowance, reset on new day, and support accessibility override", AcademyHintsAndDailyAllowance),
    ("Academy hints reveal four tiers and rereads remain free", AcademyFourTierHintsAndRereads),
    ("Academy state store migrates v1 schema to v2 without data loss", AcademyStateStoreMigratesV1Schema),
    ("Academy practice workspace transition sets explorer activity and loads exercise context", AcademyPracticeWorkspaceTransition),
    ("Academy document tab creates, opens with companion code, and prevents duplicates", AcademyDocumentTabCreatesAndPreventsDuplicates),
    ("Academy in-tab requirements and submission flow validates and persists state", AcademyInTabRequirementsAndSubmissionFlow),
    ("Academy in-tab hints and daily allowance lifecycle tracks and resets", AcademyInTabHintsAndAllowanceLifecycle),
    ("Academy split workspace companions and toolbar commands execute smoothly", AcademySplitWorkspaceCompanionIntegration),
    ("Avalonia integrated Academy mode starts and responds", AvaloniaAcademyStarts),
    ("Avalonia integrated Documentation mode starts and responds", AvaloniaDocumentationStarts),
    ("Avalonia integrated Toolchains mode starts and responds", AvaloniaApplicationStarts),
};

int failures = 0;
foreach ((string name, Func<Task> run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception}");
    }
}

return failures == 0 ? 0 : 1;

static async Task NativeEngineMetadataValidatesCompatibility()
{
    var engine = new NativeSproutEngineClient();
    EngineInfo info = await engine.GetInfoAsync();
    Assert(info.EngineVersion.Length > 0, "engine version was empty");
    Assert(info.AbiVersion == NativeSproutEngineClient.SupportedApiVersion, "engine ABI was incompatible");
    Assert(info.Architecture == "x86_64", "engine architecture was not x64");
#if DEBUG
    Assert(info.BuildConfiguration == "debug", "debug tests loaded a non-debug engine");
#else
    Assert(info.BuildConfiguration == "release", "release tests loaded a non-release engine");
#endif
}

static async Task NativeRunReturnsOutput()
{
    var engine = new NativeSproutEngineClient();
    EngineResponse response = await engine.ExecuteAsync(new EngineRequest(
        EngineOperation.Run,
        SyntaxProfile.Guided,
        "test.spr",
        "print(\"Hello Studio\")\n"));
    Assert(response.Success, "expected success");
    Assert(response.Output.SequenceEqual(["Hello Studio"]), "unexpected engine output");
}

static async Task NativeRunExecutesEmbeddedPython()
{
    var engine = new NativeSproutEngineClient();
    EngineResponse response = await engine.ExecuteAsync(new EngineRequest(
        EngineOperation.Run,
        SyntaxProfile.Guided,
        "python.spr",
        "import python \"math\" as math\nprint(math.gcd(84, 30))\n"));
    Assert(response.Success, "embedded Python execution failed");
    Assert(response.Output.SequenceEqual(["6"]), "embedded Python returned unexpected output");
}

static async Task NativeLanguageServiceWorks()
{
    var engine = new NativeSproutEngineClient();
    const string Source = "has name = \"Sprout\"\nprint(na)\n";
    LanguageServiceResponse inspected = await engine.QueryAsync(new LanguageServiceRequest(
        LanguageServiceOperation.Inspect,
        SyntaxProfile.Guided,
        "language.spr",
        Source,
        Source.LastIndexOf("na", StringComparison.Ordinal) + 2));
    Assert(
        inspected.Completions.Any(item => item.Label == "name"),
        "completion did not include the visible declaration");
    Assert(inspected.Symbols.Any(item => item.Name == "name"), "outline did not include name");

    const string CompleteSource = "has name = \"Sprout\"\nprint(name)\n";
    int hoverCursor = CompleteSource.LastIndexOf("name", StringComparison.Ordinal) + 1;
    LanguageServiceResponse hovered = await engine.QueryAsync(new LanguageServiceRequest(
        LanguageServiceOperation.Inspect,
        SyntaxProfile.Guided,
        "language.spr",
        CompleteSource,
        hoverCursor));
    Assert(hovered.Hover?.Title.Contains("name", StringComparison.Ordinal) == true,
        "hover did not identify name");
    Assert(hovered.Definition?.Name == "name", "go to definition did not resolve name");

    LanguageServiceResponse formatted = await engine.QueryAsync(new LanguageServiceRequest(
        LanguageServiceOperation.Format,
        SyntaxProfile.Guided,
        "language.spr",
        "has name=\"Sprout\"\n"));
    Assert(formatted.Success, "formatter returned diagnostics for valid source");
    Assert(formatted.FormattedSource == "has name = \"Sprout\"\n", "formatter output was not canonical");
}

static async Task NativeAstJsonReturnsSerializedAst()
{
    var engine = new NativeSproutEngineClient();
    const string Source = "import math\nhas x = 42\naction compute(val)\n    return val * 2\nend\nprint(compute(x))\n";
    string json = await engine.GetAstJsonAsync(Source);
    Assert(!string.IsNullOrWhiteSpace(json), "AST JSON was empty");
    Assert(json.Contains("\"kind\":\"Program\""), "AST JSON did not contain Program");
    Assert(json.Contains("\"kind\":\"Import\""), "AST JSON did not contain Import");
    Assert(json.Contains("\"kind\":\"Variable\""), "AST JSON did not contain Variable");
    Assert(json.Contains("\"kind\":\"Function\""), "AST JSON did not contain Function");
    Assert(json.Contains("\"kind\":\"Call\""), "AST JSON did not contain Call");

    var options = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) },
    };
    AstResponse? response = System.Text.Json.JsonSerializer.Deserialize<AstResponse>(json, options);
    Assert(response is not null, "Deserialized AST response was null");
    Assert(response!.Program is not null, "Program root node was null");
    Assert(response.Program!.Kind == "Program", "Root node kind was not Program");
    Assert(response.Program.Children.Count >= 4, "Program children count was less than expected statements");
    Assert(response.Program.Children.Any(c => c.Kind == "Import" && c.Label.Contains("math")), "Import node missing");
    Assert(response.Program.Children.Any(c => c.Kind == "Variable" && c.Label.Contains("x")), "Variable node missing");
    Assert(response.Program.Children.Any(c => c.Kind == "Function" && c.Label.Contains("compute")), "Function node missing");
    Assert(response.Program.Children.Any(c => c.Kind == "ExpressionStatement"), "Expression statement missing");

    AstNode functionNode = response.Program.Children.First(c => c.Kind == "Function");
    Assert(functionNode.Children.Any(c => c.Kind == "Return"), "Function body missing return statement");
}

static async Task NativeDebuggerAndCanvasWork()
{
    var engine = new NativeSproutEngineClient();
    EngineResponse response = await engine.ExecuteAsync(new EngineRequest(
        EngineOperation.Debug,
        SyntaxProfile.Guided,
        "debug.spr",
        "has score = 7\ncanvas_clear(\"#101010\")\ncanvas_circle(40, 50, 10, \"green\")\nprint(score)\n"));
    Assert(response.Success, "debug execution failed");
    Assert(response.Output.SequenceEqual(["7"]), "debug output was incorrect");
    Assert(response.DebugSnapshots?.Count > 0, "debugger returned no snapshots");
    Assert(
        response.DebugSnapshots!.Any(snapshot => snapshot.Variables.Any(
            variable => variable.Name == "score" && variable.Value == "7")),
        "debugger did not expose a real scoped value");
    Assert(
        response.CanvasCommands?.Any(command => command.Kind == "circle" && command.Radius == 10) == true,
        "canvas protocol did not include the circle");
}

static async Task NativePackageHubAndBuildWork()
{
    string parent = TemporaryDirectory("phase9-project");
    string destination = Path.Combine(parent, "PhaseNine");
    Directory.CreateDirectory(parent);
    try
    {
        var client = new NativeSproutEngineClient();
        ProjectResponse created = await client.CreateAsync(
            destination,
            "PhaseNine",
            ProjectTemplate.Guided);
        Assert(created.Success && created.Project is not null, "project creation failed");

        ProjectResponse updated = await client.UpdateDependencyAsync(
            destination,
            ProjectDependencyKind.Python,
            "pygame",
            "2.6.*");
        Assert(updated.Success && updated.Project is not null, "package update failed");
        ProjectInfo updatedProject = updated.Project
            ?? throw new InvalidOperationException("package update returned no project");
        Assert(
            updatedProject.PythonDependencies?.GetValueOrDefault("pygame") == "2.6.*",
            "updated package was not returned from the project API");

        BuildResponse built = await client.BuildAsync(destination);
        Assert(built.Success && built.Artifact is not null, "project build failed");
        BuildArtifact artifact = built.Artifact
            ?? throw new InvalidOperationException("build returned no artifact");
        Assert(Directory.Exists(artifact.Path), "bundle directory was not created");
        Assert(File.Exists(Path.Combine(artifact.Path, "README.txt")), "bundle disclosure is missing");

        ProjectResponse removed = await client.UpdateDependencyAsync(
            destination,
            ProjectDependencyKind.Python,
            "pygame",
            null);
        Assert(
            removed.Project?.PythonDependencies?.ContainsKey("pygame") == false,
            "package removal did not update the manifest");
    }
    finally
    {
        Directory.Delete(parent, recursive: true);
    }
}

static async Task EditorWorkspaceSupportsAdditionalLanguages()
{
    string directory = TemporaryDirectory("editor-workspace");
    Directory.CreateDirectory(directory);
    try
    {
        string sprout = Path.Combine(directory, "main.spr");
        string rust = Path.Combine(directory, "lib.rs");
        string python = Path.Combine(directory, "lesson.py");
        await File.WriteAllTextAsync(sprout, "print(1)\n");
        await File.WriteAllTextAsync(rust, "fn main() {}\n");
        await File.WriteAllTextAsync(python, "print('lesson')\n");

        var workspace = new EditorWorkspace();
        EditorDocument sproutDocument = await workspace.OpenAsync(sprout);
        EditorDocument rustDocument = await workspace.OpenAsync(rust);
        EditorDocument pythonDocument = await workspace.OpenAsync(python);
        Assert(workspace.Documents.Count == 3, "multi-tab workspace did not retain all documents");
        Assert(rustDocument.Language.Id == DocumentLanguage.Rust, "Rust adapter was not selected");
        Assert(
            rustDocument.Language.Capabilities.HasFlag(EditorCapability.Build),
            "Rust build capability was not exposed");
        Assert(pythonDocument.Language.Id == DocumentLanguage.Python, "Python edit adapter was not selected");
        Assert(
            pythonDocument.Language.Capabilities.HasFlag(EditorCapability.Run)
                && pythonDocument.Language.Capabilities.HasFlag(EditorCapability.Build)
                && pythonDocument.Language.Capabilities.HasFlag(EditorCapability.Test),
            "Python toolchain capabilities were not exposed");

        sproutDocument.UpdateSource("print(2)\n");
        await workspace.SaveAsync(sproutDocument);
        Assert(await File.ReadAllTextAsync(sprout) == "print(2)\n", "workspace save did not persist edits");
        IReadOnlyList<WorkspaceFile> scanned = await WorkspaceScanner.ScanAsync(directory);
        Assert(scanned.Count == 3, "workspace scan did not include all supported languages");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task EditorCoordinatesNormalizeLineEndings()
{
    const string EditorText = "first\rsecond\r\nthird";
    string normalized = EditorTextCoordinates.NormalizeLineEndings(EditorText);
    Assert(normalized == "first\nsecond\nthird", "line endings were not normalized");
    Assert(
        EditorTextCoordinates.ToEditorLineEndings(normalized) == "first\rsecond\rthird",
        "normalized source was not converted to WinUI editor line breaks");
    int thirdEditorIndex = EditorText.IndexOf("third", StringComparison.Ordinal);
    int thirdNormalizedIndex = normalized.IndexOf("third", StringComparison.Ordinal);
    Assert(
        EditorTextCoordinates.EditorIndexToNormalizedIndex(EditorText, thirdEditorIndex)
            == thirdNormalizedIndex,
        "editor caret did not map to normalized source");
    Assert(
        EditorTextCoordinates.NormalizedIndexToEditorIndex(EditorText, thirdNormalizedIndex)
            == thirdEditorIndex,
        "normalized source span did not map back to the editor");
    return Task.CompletedTask;
}

static Task LearningHabitsComputeStreaksGoalsAndCalendar()
{
    TimeZoneInfo utc = TimeZoneInfo.Utc;
    var today = new DateOnly(2026, 10, 11);
    DateTimeOffset At(int daysAgo, int hour = 12) =>
        new(today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);

    LearningHabitSummary empty = LearningHabits.Summarize([], today, utc);
    Assert(empty.CurrentStreak == 0 && empty.LongestStreak == 0 && !empty.PracticedToday, "empty history produced a streak");
    Assert(empty.Calendar.Length == LearningHabits.DefaultCalendarDays && empty.Calendar[^1].Date == today,
        "calendar does not end today");

    // Practiced 5,4,3 days ago (run of 3), then yesterday and today (run of 2).
    LearningEvent[] events =
    [
        new(At(5), 10), new(At(4), 0), new(At(3), 20),
        new(At(1), 15), new(At(0, 9), 25), new(At(0, 18), 10),
    ];
    LearningHabitSummary summary = LearningHabits.Summarize(events, today, utc, dailyXpGoal: 30);
    Assert(summary.CurrentStreak == 2, $"current streak was {summary.CurrentStreak}");
    Assert(summary.LongestStreak == 3, $"longest streak was {summary.LongestStreak}");
    Assert(summary.PracticedToday && summary.XpEarnedToday == 35, "today's practice or XP was not aggregated");
    Assert(summary.DailyGoalMet && summary.DailyGoalPercent == 100, "daily goal progress was not capped at 100");
    Assert(summary.Calendar[^1].Attempts == 2 && summary.Calendar[^3].Attempts == 0, "calendar attempts are misplaced");

    // A streak stays alive until the end of today when only yesterday was practiced.
    LearningHabitSummary alive = LearningHabits.Summarize([new(At(2), 5), new(At(1), 5)], today, utc);
    Assert(alive.CurrentStreak == 2 && !alive.PracticedToday, "an unbroken streak from yesterday was dropped");
    LearningHabitSummary broken = LearningHabits.Summarize([new(At(3), 5), new(At(2), 5)], today, utc);
    Assert(broken.CurrentStreak == 0 && broken.LongestStreak == 2, "a missed day did not break the streak");

    // Local midnight decides the day, not UTC.
    TimeZoneInfo plusEight = TimeZoneInfo.CreateCustomTimeZone("plus8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
    var lateUtc = new DateTimeOffset(today.AddDays(-1).ToDateTime(new TimeOnly(20, 0)), TimeSpan.Zero);
    Assert(LearningHabits.Summarize([new(lateUtc, 5)], today, plusEight).PracticedToday,
        "time zone conversion placed the event on the wrong day");
    return Task.CompletedTask;
}

static Task AcademyHandbookGuidesAreComplete()
{
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    Assert(catalog.Guides.Length >= 10, $"expected at least 10 Handbook guides, found {catalog.Guides.Length}");
    Assert(catalog.Guides[0].Id == "start-here", "the Handbook should open with the Start Here guide");
    string docs = Path.Combine(FindRepositoryRoot(), "docs");
    foreach (GuideDefinition guide in catalog.Guides)
    {
        string content = catalog.GetGuideContent(guide.Id);
        Assert(content.StartsWith("# ", StringComparison.Ordinal), $"guide {guide.Id} does not start with a title heading");
        Assert(guide.ReferenceDocId is null || File.Exists(Path.Combine(docs, guide.ReferenceDocId + ".md")),
            $"guide {guide.Id} references missing documentation {guide.ReferenceDocId}");
    }
    // Every concept a lesson teaches should have a guide to read first.
    foreach (string conceptId in catalog.Tracks.Single(track => track.Id == "sprout-fundamentals")
        .Lessons.SelectMany(lesson => lesson.ConceptIds).Distinct())
    {
        Assert(catalog.GuidesForConcepts([conceptId]).Count > 0, $"no Handbook guide covers concept {conceptId}");
    }
    Assert(catalog.GuidesForConcepts(["loops"])[0].Id == "repetition", "the best guide for loops should be the repetition guide");
    return Task.CompletedTask;
}

static async Task AcademyHandbookSamplesRun()
{
    // Guides teach real code, so every ```sprout sample must run cleanly in its profile.
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    var engine = new NativeSproutEngineClient();
    var fence = new System.Text.RegularExpressions.Regex("```sprout(?: (?<profile>\\w+))?\\r?\\n(?<code>.*?)```",
        System.Text.RegularExpressions.RegexOptions.Singleline);
    int samples = 0;
    foreach (GuideDefinition guide in catalog.Guides)
    {
        foreach (System.Text.RegularExpressions.Match match in fence.Matches(catalog.GetGuideContent(guide.Id)))
        {
            SyntaxProfile profile = match.Groups["profile"].Value switch
            {
                "structured" => SyntaxProfile.Structured,
                "advanced" => SyntaxProfile.Advanced,
                _ => SyntaxProfile.Guided,
            };
            EngineResponse response = await engine.ExecuteAsync(new EngineRequest(
                EngineOperation.Run, profile, $"{guide.Id}.spr", match.Groups["code"].Value));
            Assert(response.Success,
                $"guide {guide.Id} sample {samples + 1} failed: {string.Join("; ", response.Diagnostics.Select(d => d.Message))}");
            samples++;
        }
    }
    Assert(samples >= 25, $"expected the Handbook to contain at least 25 runnable samples, found {samples}");
}

static async Task AcademyGuideReadingCountsTowardProgress()
{
    string directory = TemporaryDirectory("academy-guide-reading");
    var service = new AcademyService(
        AcademyCatalog.LoadDefault(),
        new AcademyStateStore(Path.Combine(directory, "academy.json")),
        ToolchainManager.CreateDefault());

    var first = await service.MarkGuideReadAsync("first-program");
    Assert(first.FirstRead && first.State.HasReadGuide("first-program"), "the first read was not recorded");
    Assert(first.NewAchievements.Any(achievement => achievement.Id == "curious-mind"), "reading a guide did not award Curious Mind");

    var again = await service.MarkGuideReadAsync("first-program");
    Assert(!again.FirstRead && again.State.ReadGuides.Length == 1 && again.NewAchievements.Length == 0,
        "re-reading a guide changed progress");

    LearningHabitSummary habits = service.SummarizeHabits(again.State);
    Assert(habits.PracticedToday && habits.CurrentStreak == 1 && habits.XpEarnedToday == 0,
        "reading should count as practice for the streak without awarding XP");

    foreach (string id in new[] { "decisions", "repetition", "actions", "classes" })
    {
        await service.MarkGuideReadAsync(id);
    }
    AcademyState reloaded = await service.LoadStateAsync();
    Assert(reloaded.ReadGuides.Length == 5, "guide reads did not persist");
    Assert(reloaded.AchievementAwards.Any(award => award.Id == "well-read"), "reading five guides did not award Well Read");

    bool rejected = false;
    try
    {
        await service.MarkGuideReadAsync("no-such-guide");
    }
    catch (KeyNotFoundException)
    {
        rejected = true;
    }
    Assert(rejected, "an unknown guide id was accepted");
}

static Task SyntaxHighlightingLoadsForEveryLanguage()
{
    foreach (LanguageDescriptor language in LanguageRegistry.All.Where(item => item.Id != DocumentLanguage.PlainText))
    {
        AvaloniaEdit.Highlighting.IHighlightingDefinition? definition = SyntaxHighlightingRegistry.For(language.Id);
        Assert(definition is not null, $"{language.DisplayName} has no syntax highlighting");
        Assert(definition!.GetNamedColor("Comment") is not null || language.Id is DocumentLanguage.Json or DocumentLanguage.Markdown,
            $"{language.DisplayName} highlighting has no comment color");
        foreach (string extension in language.Extensions)
        {
            Assert(ReferenceEquals(SyntaxHighlightingRegistry.ForPath("file" + extension), definition),
                $"{extension} did not resolve to {language.DisplayName} highlighting");
        }
    }
    Assert(SyntaxHighlightingRegistry.For("python") is not null && SyntaxHighlightingRegistry.For("Python") is not null,
        "highlighting lookup by key or display name failed");

    // Every definition must actually highlight real code (a bad rule can silently swallow text).
    var samples = new Dictionary<DocumentLanguage, string>
    {
        [DocumentLanguage.Sprout] = "has score = 10 # note\nprint(\"hi\")",
        [DocumentLanguage.Python] = "def f(x):\n    return len(\"a\")  # c",
        [DocumentLanguage.Java] = "int score = 10; // c\nSystem.out.println(\"x\");",
        [DocumentLanguage.Rust] = "let mut score = 10;\nscore += 5; // c\nprintln!(\"{}\", score);",
        [DocumentLanguage.Go] = "score := 10 // c\nfmt.Println(\"x\")",
        [DocumentLanguage.C] = "#include <stdio.h>\nint main(void) { return 0; }",
        [DocumentLanguage.Cpp] = "std::vector<int> v; // c",
        [DocumentLanguage.CSharp] = "var x = 10; // c",
        [DocumentLanguage.JavaScript] = "const x = `t`; // c",
        [DocumentLanguage.TypeScript] = "let x: number = 1; // c",
        [DocumentLanguage.Json] = "{\"a\": true, \"b\": 1}",
        [DocumentLanguage.Toml] = "[project]\nname = \"x\" # c",
        [DocumentLanguage.Markdown] = "# Title\nSome `code` and **bold**.",
    };
    foreach ((DocumentLanguage language, string code) in samples)
    {
        var document = new AvaloniaEdit.Document.TextDocument(code);
        var highlighter = new AvaloniaEdit.Highlighting.DocumentHighlighter(document, SyntaxHighlightingRegistry.For(language)!);
        int sections = 0;
        for (int line = 1; line <= document.LineCount; line++)
        {
            AvaloniaEdit.Highlighting.HighlightedLine highlighted = highlighter.HighlightLine(line);
            foreach (AvaloniaEdit.Highlighting.HighlightedSection section in highlighted.Sections)
            {
                Assert(section.Offset >= highlighted.DocumentLine.Offset
                    && section.Offset + section.Length <= highlighted.DocumentLine.EndOffset,
                    $"{language} produced a section outside its line");
                sections++;
            }
        }
        Assert(sections > 0, $"{language} highlighting produced no colored sections for its sample");
    }
    Assert(SyntaxHighlightingRegistry.For(DocumentLanguage.PlainText) is null && SyntaxHighlightingRegistry.ForPath("notes.txt") is null,
        "plain text unexpectedly received highlighting");
    return Task.CompletedTask;
}

static Task ConceptDetectorMapsCodeToConcepts()
{
    string sprout = """
        # repeat is mentioned in a comment, which must not count
        has plants = ["fern", "moss"]
        class Garden
            action water(times)
                repeat times times
                    if times > 1
                        print("watering while it rains")
                    end
                end
            end
        end
        """;
    string[] ids = ConceptDetector.Detect(sprout, "sprout").Select(match => match.ConceptId).ToArray();
    foreach (string expected in new[] { "variables", "collections", "data-modeling", "functions", "loops", "conditions", "oop-encapsulation" })
    {
        Assert(ids.Contains(expected), $"Sprout concept {expected} was not detected");
    }
    ConceptMatch loops = ConceptDetector.Detect(sprout, "sprout").Single(match => match.ConceptId == "loops");
    Assert(loops.FirstLine == 5 && loops.Occurrences == 1, $"loop detection used comments or strings (line {loops.FirstLine}, {loops.Occurrences}x)");

    string[] python = ConceptDetector.Detect("def greet(name):\n    for c in name:\n        print(c)\n", "python")
        .Select(match => match.ConceptId).ToArray();
    Assert(python.SequenceEqual(["functions", "loops"]), $"Python concepts were {string.Join(",", python)}");
    Assert(ConceptDetector.Detect("fn main() { let v = vec![1]; }", "rust").Any(match => match.ConceptId == "collections"),
        "Rust vec! was not detected as a collection");
    Assert(ConceptDetector.Detect("{\"a\": 1}", "json").Count == 0 && !ConceptDetector.Supports("json"),
        "unsupported languages produced concepts");
    return Task.CompletedTask;
}

static Task EditorTogglesAlignedLineComments()
{
    Assert(EditorLineEditing.LineCommentPrefix(DocumentLanguage.Sprout) == "#", "Sprout comment prefix changed");
    Assert(EditorLineEditing.LineCommentPrefix(DocumentLanguage.Rust) == "//", "Rust comment prefix changed");
    Assert(EditorLineEditing.LineCommentPrefix(DocumentLanguage.Json) is null, "JSON unexpectedly has comments");

    string[] source = ["    has x = 1", "", "        print(x)"];
    IReadOnlyList<string> commented = EditorLineEditing.ToggleLineComment(source, "#");
    Assert(
        commented.SequenceEqual(["    # has x = 1", "", "    #     print(x)"]),
        "comments were not aligned at the smallest indentation");
    IReadOnlyList<string> restored = EditorLineEditing.ToggleLineComment(commented, "#");
    Assert(restored.SequenceEqual(source), "toggling twice did not restore the original lines");

    IReadOnlyList<string> mixed = EditorLineEditing.ToggleLineComment(["# done", "todo"], "#");
    Assert(mixed.SequenceEqual(["# # done", "# todo"]), "a partially commented block was not fully commented");
    IReadOnlyList<string> blank = EditorLineEditing.ToggleLineComment(["", "  "], "//");
    Assert(blank.SequenceEqual(["", "  "]), "blank lines were modified");
    return Task.CompletedTask;
}

static Task EditorBreadcrumbsStayProjectRelative()
{
    string root = Path.Combine(Path.GetTempPath(), "sprout-breadcrumb-project");
    string nested = Path.Combine(root, "src", "game", "main.spr");
    Assert(
        EditorBreadcrumbs.Format("main.spr", nested, root) == "src  ›  game  ›  main.spr",
        "project breadcrumb was not compact and relative");
    Assert(
        EditorBreadcrumbs.Format("untitled.spr", null, root) == "untitled.spr",
        "untitled document breadcrumb changed unexpectedly");
    Assert(
        EditorBreadcrumbs.Format("notes.spr", nested, Path.Combine(root, "elsewhere")) == "main.spr",
        "external document breadcrumb exposed an absolute path");
    return Task.CompletedTask;
}

static Task LanguageRegistrySuppliesSharedIdentities()
{
    string[] required =
    [
        "sprout", "rust", "python", "go", "java", "c", "cpp", "csharp",
        "javascript", "typescript",
    ];
    foreach (string key in required)
    {
        LanguageDescriptor language = LanguageRegistry.Get(key);
        Assert(language.IconGlyph.Length > 0, $"{key} has no local icon identity");
        Assert(language.Accent.StartsWith('#') && language.Accent.Length == 7, $"{key} has an invalid accent");
    }
    Assert(
        LanguageRegistry.All.Select(language => language.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            == LanguageRegistry.All.Count,
        "language registry contains duplicate keys");
    Assert(LanguageRegistry.ForPath("program.cpp").Key == "cpp", "C++ extension mapping was lost");
    Assert(LanguageRegistry.ForPath("Program.cs").Key == "csharp", "C# extension mapping was lost");
    Assert(
        LanguageRegistry.ForAcademyTrack("rust-systems").Key == "rust",
        "Academy track did not resolve through the shared registry");
    return Task.CompletedTask;
}

static async Task ProjectSearchFiltersAndIgnoresGeneratedOutput()
{
    string directory = TemporaryDirectory("project-search");
    Directory.CreateDirectory(Path.Combine(directory, "src"));
    Directory.CreateDirectory(Path.Combine(directory, "build", "generated"));
    Directory.CreateDirectory(Path.Combine(directory, ".git"));
    try
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "src", "main.spr"), "has project = \"Sprout\"\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "notes.md"), "Project notes\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "build", "generated", "main.spr"), "project project\n");
        await File.WriteAllTextAsync(Path.Combine(directory, ".git", "ignored.txt"), "project\n");

        var search = new ProjectSearchService();
        IReadOnlyList<ProjectSearchResult> all = await search.SearchAsync(
            directory,
            new ProjectSearchQuery("project", string.Empty, MatchCase: false, WholeWord: true));
        Assert(all.Count == 2, "search included generated output or missed a source result");
        Assert(all.All(result => !result.RelativePath.StartsWith("build", StringComparison.OrdinalIgnoreCase)),
            "search returned a generated build artifact");

        IReadOnlyList<ProjectSearchResult> sproutOnly = await search.SearchAsync(
            directory,
            new ProjectSearchQuery("project", "*.spr", MatchCase: false, WholeWord: true));
        Assert(
            sproutOnly.Count == 1 && sproutOnly[0].RelativePath.EndsWith("main.spr", StringComparison.OrdinalIgnoreCase),
            "file filtering did not isolate Sprout source");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task GitStatusParserPreservesState()
{
    string porcelain = string.Join('\0',
    [
        "# branch.oid abcdef",
        "# branch.head main",
        "# branch.upstream origin/main",
        "# branch.ab +2 -1",
        "1 M. N... 100644 100644 100644 abc def src/main.spr",
        "1 .M N... 100644 100644 100644 abc def README.md",
        "2 R. N... 100644 100644 100644 abc def R100 src/new.spr",
        "src/old.spr",
        "? notes.txt",
        string.Empty,
    ]);
    GitRepositoryState state = GitStatusParser.Parse(Path.GetTempPath(), porcelain, hasRemote: true);
    Assert(state.Branch == "main" && state.Upstream == "origin/main", "branch tracking metadata was lost");
    Assert(state.Ahead == 2 && state.Behind == 1, "ahead/behind counts were parsed incorrectly");
    Assert(state.HasRemote && state.Changes.Count == 4, "repository status did not retain all changes");
    GitFileChange renamed = state.Changes.Single(change => change.Path == "src/new.spr");
    Assert(renamed.OriginalPath == "src/old.spr" && renamed.IsStaged, "rename metadata was lost");
    Assert(state.Changes.Single(change => change.Path == "notes.txt").IsUnstaged, "untracked state was lost");
    return Task.CompletedTask;
}

static async Task GitServiceRunsLocalWorkflow()
{
    string directory = TemporaryDirectory("git-workflow");
    Directory.CreateDirectory(directory);
    try
    {
        var runner = new ProcessRunner();
        async Task Git(params string[] arguments)
        {
            ProcessResult result = await runner.RunAsync(new ProcessRequest(
                "git",
                arguments,
                directory,
                TimeSpan.FromSeconds(15)));
            Assert(result.Success, $"git {string.Join(' ', arguments)} failed: {result.StandardError}");
        }

        await Git("init");
        await Git("config", "user.email", "sprout-studio-tests@example.invalid");
        await Git("config", "user.name", "Sprout Studio Tests");
        string source = Path.Combine(directory, "main.spr");
        await File.WriteAllTextAsync(source, "print(\"one\")\n");

        var service = new GitRepositoryService(runner);
        GitRepositoryState untracked = await service.GetStatusAsync(directory);
        Assert(untracked.IsRepository, "Git service did not detect the repository");
        Assert(untracked.Changes.Single().IndexStatus == '?', "untracked file status was not returned");

        await service.StageAsync(directory, "main.spr");
        GitRepositoryState staged = await service.GetStatusAsync(directory);
        Assert(staged.Changes.Single().IsStaged, "stage did not update repository state");
        await service.CommitAsync(directory, "Initial Sprout source");
        Assert((await service.GetStatusAsync(directory)).Changes.Count == 0, "commit did not clean the tree");

        await File.AppendAllTextAsync(source, "print(\"two\")\n");
        string diff = await service.GetDiffAsync(directory, "main.spr", staged: false);
        Assert(diff.Contains("+print(\"two\")", StringComparison.Ordinal), "working-tree diff was incomplete");
        await service.StageAsync(directory, "main.spr");
        await service.UnstageAsync(directory, "main.spr");
        GitFileChange unstaged = (await service.GetStatusAsync(directory)).Changes.Single();
        Assert(unstaged.IsUnstaged && !unstaged.IsStaged, "unstage did not restore working-tree state");
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(
                directory,
                "*",
                SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(directory, recursive: true);
        }
    }
}

static Task StudioLayoutStatePersistsAndSanitizes()
{
    string directory = TemporaryDirectory("layout-state");
    Directory.CreateDirectory(directory);
    try
    {
        string path = Path.Combine(directory, "layout.json");
        var store = new StudioLayoutStateStore(path);
        store.Save(new StudioLayoutState
        {
            WindowX = 120,
            WindowY = 80,
            WindowWidth = 1440,
            WindowHeight = 860,
            ExplorerWidth = 334,
            InspectorWidth = 372,
            BottomPanelHeight = 244,
            ActivityVisible = false,
            ExplorerVisible = true,
            InspectorVisible = false,
            BottomPanelVisible = true,
            SelectedBottomTab = 2,
            SelectedInspectorTab = 1,
            SelectedActivity = "source-control",
        });
        StudioLayoutState loaded = store.Load();
        Assert(loaded.WindowX == 120 && loaded.WindowY == 80, "window position did not round-trip");
        Assert(loaded.WindowWidth == 1440 && loaded.WindowHeight == 860, "window size did not round-trip");
        Assert(loaded.ExplorerWidth == 334, "explorer width did not round-trip");
        Assert(loaded.InspectorWidth == 372, "inspector width did not round-trip");
        Assert(!loaded.ActivityVisible && !loaded.InspectorVisible, "panel visibility did not round-trip");
        Assert(loaded.SelectedBottomTab == 2, "bottom tab did not round-trip");
        Assert(loaded.SelectedActivity == "source-control", "activity selection did not round-trip");

        StudioLayoutState sanitized = StudioLayoutStateStore.Sanitize(new StudioLayoutState
        {
            WindowX = -200_000,
            WindowY = 200_000,
            WindowWidth = 40,
            WindowHeight = 50_000,
            ExplorerWidth = -1,
            InspectorWidth = 9_000,
            BottomPanelHeight = double.PositiveInfinity,
            SelectedBottomTab = 99,
            SelectedInspectorTab = -4,
            SelectedActivity = "unknown",
        });
        Assert(sanitized.ExplorerWidth == 220, "explorer minimum was not enforced");
        Assert(sanitized.WindowX == -100_000 && sanitized.WindowY == 100_000, "window position bounds were not enforced");
        Assert(sanitized.WindowWidth == 900 && sanitized.WindowHeight == 4_320, "window size bounds were not enforced");
        Assert(sanitized.InspectorWidth == 440, "inspector maximum was not enforced");
        Assert(sanitized.BottomPanelHeight == 500, "bottom maximum was not enforced");
        Assert(sanitized.SelectedBottomTab == 4, "bottom tab bounds were not enforced");
        Assert(sanitized.SelectedInspectorTab == 0, "inspector tab bounds were not enforced");
        Assert(sanitized.SelectedActivity == "explorer", "unknown activity was not reset");

        File.WriteAllText(path, "{ malformed");
        Assert(store.Load() == new StudioLayoutState(), "corrupt layout did not fall back to defaults");
        return Task.CompletedTask;
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task NativeProjectRunIsolatesPythonPackages()
{
    string projectRoot = TemporaryDirectory("native-python-project");
    string packages = Path.Combine(projectRoot, ".sprout", "python");
    Directory.CreateDirectory(packages);
    try
    {
        await File.WriteAllTextAsync(
            Path.Combine(packages, "studio_project_value.py"),
            "def read():\n    return 'studio-project'\n");
        var engine = new NativeSproutEngineClient();
        EngineResponse response = await engine.ExecuteAsync(new EngineRequest(
            EngineOperation.Run,
            SyntaxProfile.Guided,
            "python.spr",
            "import python \"studio_project_value\" as value\nprint(value.read())\n",
            projectRoot));
        Assert(response.Success, "project-local Python execution failed");
        Assert(response.Output.SequenceEqual(["studio-project"]), "wrong project package was loaded");
    }
    finally
    {
        Directory.Delete(projectRoot, recursive: true);
    }
}

static async Task NativeDiagnosticsPreserveLocations()
{
    var engine = new NativeSproutEngineClient();
    EngineResponse response = await engine.ExecuteAsync(new EngineRequest(
        EngineOperation.Check,
        SyntaxProfile.Guided,
        "broken.spr",
        "print(missing)\n"));
    StudioDiagnostic diagnostic = response.Diagnostics.Single();
    Assert(diagnostic.Code == "SPT201", "unexpected diagnostic code");
    Assert(diagnostic.Span.StartLine == 1 && diagnostic.Span.StartColumn == 7, "incorrect span");
}

static async Task NativeDiagnosticsPreserveRelatedDataAndFixes()
{
    var engine = new NativeSproutEngineClient();
    EngineResponse response = await engine.ExecuteAsync(new EngineRequest(
        EngineOperation.Check,
        SyntaxProfile.Guided,
        "metadata.spr",
        "has attack = 1\nhas attack = 2\nprint(attck)\n"));
    StudioDiagnostic duplicate = response.Diagnostics.Single(item => item.Code == "SPT210");
    StudioDiagnostic unknown = response.Diagnostics.Single(item => item.Code == "SPT201");
    Assert(duplicate.RelatedInformation.Single().Span.StartLine == 1, "related declaration was lost");
    Assert(unknown.Suggestions.Single() == "Did you mean `attack`?", "symbol suggestion was lost");
    DiagnosticFix fix = unknown.OptionalFixes.Single();
    Assert(fix.Replacement == "attack", "replacement text was lost");
    Assert(fix.Span.StartLine == 3 && fix.Span.StartColumn == 7, "replacement span was lost");
}

static async Task NativeSpansNavigateUnicodeSource()
{
    const string Source = "print(\"🌱\")\nprint(missing)\n";
    var engine = new NativeSproutEngineClient();
    EngineResponse response = await engine.ExecuteAsync(new EngineRequest(
        EngineOperation.Check,
        SyntaxProfile.Guided,
        "unicode.spr",
        Source));
    StudioDiagnostic diagnostic = response.Diagnostics.Single();
    int editorIndex = SourceTextCoordinates.Utf8OffsetToUtf16Index(Source, diagnostic.Span.Start);
    Assert(editorIndex == Source.IndexOf("missing", StringComparison.Ordinal), "UTF-8 span selected the wrong editor text");
}

static async Task SessionProjectsResponse()
{
    var session = new StudioSession(new NativeSproutEngineClient());
    session.UpdateSource("print(\"console\")\nprint(missing)\n");
    EngineResponse response = await session.RunAsync();
    Assert(!response.Success, "invalid program should fail");
    Assert(session.ConsoleLines.Count == 0, "semantic failure must prevent partial output");
    Assert(session.Problems.Single().Code == "SPT201", "problem was not projected");
    Assert(!session.IsRunning, "session should leave running state");
}

static Task ResponseParserRejectsApiMismatch()
{
    const string Json = "{\"apiVersion\":2,\"success\":true,\"output\":[],\"diagnostics\":[]}";
    try
    {
        NativeSproutEngineClient.ParseResponse(Json);
        throw new InvalidOperationException("API mismatch was accepted");
    }
    catch (InvalidDataException)
    {
        return Task.CompletedTask;
    }
}

static async Task NativeProjectCreatesAndLoads()
{
    string parent = TemporaryDirectory("native-project");
    string destination = Path.Combine(parent, "StudioProject");
    try
    {
        var client = new NativeSproutEngineClient();
        ProjectResponse created = await client.CreateAsync(
            destination,
            "StudioProject",
            ProjectTemplate.Structured);
        Assert(created.Success, "project creation failed");
        Assert(created.Project?.Profile == SyntaxProfile.Structured, "profile was not preserved");
        Assert(File.Exists(Path.Combine(destination, "sprout.toml")), "manifest was not created");
        Assert(File.Exists(Path.Combine(destination, "src", "main.spr")), "entry was not created");

        ProjectResponse loaded = await client.LoadAsync(destination);
        Assert(loaded.Success, "project reload failed");
        Assert(loaded.Project?.Name == "StudioProject", "project name was not loaded");
        Assert(loaded.Project?.SourceFiles.Count == 1, "project sources were not enumerated");
    }
    finally
    {
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }
    }
}

static async Task NativeProjectValidation()
{
    string directory = TemporaryDirectory("invalid-project");
    Directory.CreateDirectory(directory);
    try
    {
        await File.WriteAllTextAsync(
            Path.Combine(directory, "sprout.toml"),
            "manifest_version = 42\n[project]\nname = \"Broken\"\nversion = \"0.1.0\"\nentry = \"src/main.spr\"\n");
        var client = new NativeSproutEngineClient();
        ProjectResponse response = await client.LoadAsync(directory);
        Assert(!response.Success, "invalid manifest was accepted");
        Assert(response.Diagnostics.Any(item => item.Code == "SPP003"), "version error was lost");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task RecentProjectsPersistWithoutDuplicates()
{
    string directory = TemporaryDirectory("recent-projects");
    Directory.CreateDirectory(directory);
    try
    {
        string settingsPath = Path.Combine(directory, "recent-projects.json");
        var store = new RecentProjectStore(settingsPath);
        var project = new ProjectInfo(
            directory,
            Path.Combine(directory, "sprout.toml"),
            "RecentProject",
            "0.1.0",
            Path.Combine(directory, "src", "main.spr"),
            SyntaxProfile.Guided,
            []);
        await store.AddAsync(project);
        string otherRoot = Path.Combine(directory, "OtherProject");
        await store.AddAsync(project with
        {
            RootPath = otherRoot,
            ManifestPath = Path.Combine(otherRoot, "sprout.toml"),
            Name = "OtherProject",
            EntryPath = Path.Combine(otherRoot, "src", "main.spr"),
        });
        await store.AddAsync(project);
        IReadOnlyList<RecentProject> recent = await new RecentProjectStore(settingsPath).LoadAsync();
        Assert(recent.Count == 2, "recent project metadata was not persisted");
        Assert(recent[0].Name == "RecentProject", "recent project was not persisted");
        Assert(recent.Count(item => item.Name == "RecentProject") == 1, "duplicate project was retained");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task RecentProjectsRetainTenNewest()
{
    string directory = TemporaryDirectory("recent-project-limit");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new RecentProjectStore(Path.Combine(directory, "recent-projects.json"));
        IReadOnlyList<RecentProject> recent = [];
        for (int index = 0; index < 12; index++)
        {
            string root = Path.Combine(directory, $"Project{index}");
            recent = await store.AddAsync(new ProjectInfo(
                root,
                Path.Combine(root, "sprout.toml"),
                $"Project{index}",
                "0.1.0",
                Path.Combine(root, "src", "main.spr"),
                SyntaxProfile.Guided,
                []));
        }
        Assert(recent.Count == 10, "recent project limit was not enforced");
        Assert(recent[0].Name == "Project11", "newest project was not first");
        Assert(recent[^1].Name == "Project2", "oldest retained project was incorrect");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task RecentProjectsSanitizeAndRemove()
{
    string directory = TemporaryDirectory("recent-project-sanitize");
    Directory.CreateDirectory(directory);
    try
    {
        string settingsPath = Path.Combine(directory, "recent-projects.json");
        string projectRoot = Path.Combine(directory, "Project");
        var store = new RecentProjectStore(settingsPath);
        await store.AddAsync(new ProjectInfo(
            projectRoot,
            Path.Combine(projectRoot, "sprout.toml"),
            "Project",
            "0.1.0",
            Path.Combine(projectRoot, "src", "main.spr"),
            SyntaxProfile.Guided,
            []));

        IReadOnlyList<RecentProject> removed = await store.RemoveAsync(projectRoot);
        Assert(removed.Count == 0, "recent project removal did not persist");
        await File.WriteAllTextAsync(settingsPath, "[{\"Name\":\"\",\"RootPath\":\"\",\"LastOpenedUtc\":\"2026-01-01T00:00:00Z\"}]");
        Assert((await store.LoadAsync()).Count == 0, "invalid recent metadata was not ignored");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task StudioSessionSavesSource()
{
    string directory = TemporaryDirectory("save-document");
    Directory.CreateDirectory(directory);
    string filePath = Path.Combine(directory, "main.spr");
    try
    {
        await File.WriteAllTextAsync(filePath, "print(1)\n");
        var session = new StudioSession(new NativeSproutEngineClient());
        await session.LoadDocumentAsync(filePath);
        session.UpdateSource("print(2)\n");
        await session.SaveDocumentAsync();
        Assert(await File.ReadAllTextAsync(filePath) == "print(2)\n", "edited source was not saved");
        Assert(!File.Exists(filePath + ".tmp"), "temporary save file was retained");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task StudioSessionSavesNewSource()
{
    string directory = TemporaryDirectory("save-new-document");
    Directory.CreateDirectory(directory);
    string filePath = Path.Combine(directory, "created.spr");
    try
    {
        var session = new StudioSession(new NativeSproutEngineClient());
        session.UpdateSource("print(\"created\")\n");
        await session.SaveDocumentAsAsync(filePath);
        Assert(session.Document.FilePath == Path.GetFullPath(filePath), "new document path was not retained");
        Assert(session.Document.DisplayName == "created.spr", "new document name was not retained");
        Assert(await File.ReadAllTextAsync(filePath) == "print(\"created\")\n", "new source was not saved");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task PhaseFiveProjectWorkflow()
{
    string parent = TemporaryDirectory("phase-five-workflow");
    string destination = Path.Combine(parent, "VerticalSlice");
    Directory.CreateDirectory(parent);
    try
    {
        var native = new NativeSproutEngineClient();
        var recentStore = new RecentProjectStore(Path.Combine(parent, "recent-projects.json"));
        var workspace = new ProjectWorkspace(native, recentStore);
        ProjectInfo project = await workspace.CreateAsync(
            destination,
            "VerticalSlice",
            ProjectTemplate.Guided);

        var session = new StudioSession(native);
        await session.LoadDocumentAsync(project.EntryPath);
        session.UpdateProfile(project.Profile);
        session.UpdateSource("print(\"Phase 5 works\")\n");
        await session.SaveDocumentAsync();
        EngineResponse response = await session.RunAsync();

        Assert(response.Success, "edited project entry did not execute");
        Assert(response.Output.SequenceEqual(["Phase 5 works"]), "project output was incorrect");
        ProjectInfo reloaded = await workspace.OpenAsync(project.ManifestPath);
        Assert(reloaded.Name == "VerticalSlice", "saved project could not be reopened");
        Assert(workspace.RecentProjects.Count == 1, "recent project was not retained");
        Assert(
            await File.ReadAllTextAsync(project.EntryPath) == "print(\"Phase 5 works\")\n",
            "saved project source was incorrect");
    }
    finally
    {
        Directory.Delete(parent, recursive: true);
    }
}

static async Task ProcessRunnerPreservesArguments()
{
    const string Value = "value with spaces & shell symbols";
    var runner = new ProcessRunner();
    ProcessResult result = await runner.RunAsync(SelfProcessRequest(
        ["--toolchain-test-echo", Value],
        TimeSpan.FromSeconds(5)));
    Assert(result.Started, "child process did not start");
    Assert(result.ExitCode == 7, "child exit code was not captured");
    Assert(result.StandardOutput == Value, "structured argument changed before reaching the child");
    Assert(result.StandardError == "captured-error", "stderr was not captured");
    Assert(!result.TimedOut && result.Duration > TimeSpan.Zero, "process metadata was incomplete");
}

static async Task ProcessRunnerCancelsChild()
{
    var runner = new ProcessRunner();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await runner.RunAsync(SelfProcessRequest(["--toolchain-test-wait"]), cancellation.Token);
        throw new InvalidOperationException("cancelled process completed normally");
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
    {
        Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "child process was not terminated promptly");
    }
}

static async Task ProcessRunnerReportsTimeout()
{
    var runner = new ProcessRunner();
    ProcessResult result = await runner.RunAsync(SelfProcessRequest(
        ["--toolchain-test-wait"],
        TimeSpan.FromMilliseconds(150)));
    Assert(result.Started, "timed process never started");
    Assert(result.TimedOut, "timeout was not reported");
    Assert(result.ExitCode is null, "timed-out process reported a successful exit code");
    Assert(result.Duration < TimeSpan.FromSeconds(5), "timed-out child was not terminated promptly");
}

static async Task RustDetectorValidatesCompleteToolchain()
{
    string directory = TemporaryDirectory("rust-ready");
    Directory.CreateDirectory(directory);
    try
    {
        CreateExecutables(directory, "rustc", "cargo", "rustfmt");
        var runner = RustFakeRunner("1.95.1");
        ToolchainManager manager = ManagerWith(
            new RustToolchainAdapter(runner),
            runner,
            Path.Combine(directory, "settings.json"));
        ToolchainDetectionResult result = await manager.DetectAsync("rust", directory);
        Assert(result.Status == ToolchainStatus.CustomLocation, "custom Rust was not distinguished");
        Assert(result.LocationSource == ToolchainLocationSource.Explicit, "explicit source was lost");
        Assert(result.Version == "1.95.1", "Rust version was not parsed");
        Assert(result.IsUsable, "complete Rust toolchain was not usable");
        Assert(result.Components.Count == 3 && result.Components.All(item => item.Valid), "Rust components were not validated");
        Assert(runner.Requests.All(item => item.Arguments.SequenceEqual(["--version"])), "Rust validation used unexpected arguments");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task RustDetectorDistinguishesPartialInstallation()
{
    string directory = TemporaryDirectory("rust-partial");
    Directory.CreateDirectory(directory);
    try
    {
        CreateExecutables(directory, "rustc");
        var runner = RustFakeRunner("1.95.1");
        ToolchainDetectionResult result = await ManagerWith(
                new RustToolchainAdapter(runner),
                runner,
                Path.Combine(directory, "settings.json"))
            .DetectAsync("rust", directory);
        Assert(result.Status == ToolchainStatus.PartialInstallation, "partial Rust was misclassified");
        Assert(result.Message.Contains("cargo", StringComparison.Ordinal), "missing cargo was not explained");
        Assert(result.Message.Contains("rustfmt", StringComparison.Ordinal), "missing rustfmt was not explained");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task RustDetectorRejectsUnsupportedVersion()
{
    string directory = TemporaryDirectory("rust-old");
    Directory.CreateDirectory(directory);
    try
    {
        CreateExecutables(directory, "rustc", "cargo", "rustfmt");
        var runner = RustFakeRunner("1.80.0");
        ToolchainDetectionResult result = await ManagerWith(
                new RustToolchainAdapter(runner),
                runner,
                Path.Combine(directory, "settings.json"))
            .DetectAsync("rust", directory);
        Assert(result.Status == ToolchainStatus.UnsupportedVersion, "old Rust was accepted");
        Assert(!result.IsUsable, "unsupported Rust was marked usable");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task RustDetectorReportsBrokenExecutable()
{
    string directory = TemporaryDirectory("rust-broken");
    Directory.CreateDirectory(directory);
    try
    {
        CreateExecutables(directory, "rustc", "cargo", "rustfmt");
        var runner = new FakeProcessRunner(request =>
            Path.GetFileNameWithoutExtension(request.FileName).Equals("rustc", StringComparison.OrdinalIgnoreCase)
                ? ProcessFailure("rustc failed to initialize")
                : ProcessSuccess($"{Path.GetFileNameWithoutExtension(request.FileName)} 1.95.1"));
        ToolchainDetectionResult result = await ManagerWith(
                new RustToolchainAdapter(runner),
                runner,
                Path.Combine(directory, "settings.json"))
            .DetectAsync("rust", directory);
        Assert(result.Status == ToolchainStatus.FoundButBroken, "broken Rust was misclassified");
        Assert(result.Message.Contains("failed to initialize", StringComparison.Ordinal), "validation failure was hidden");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task GoDetectorValidatesCompleteToolchain()
{
    string directory = TemporaryDirectory("go-ready");
    Directory.CreateDirectory(directory);
    try
    {
        CreateExecutables(directory, "go", "gofmt");
        var runner = new FakeProcessRunner(request =>
            Path.GetFileNameWithoutExtension(request.FileName).Equals("go", StringComparison.OrdinalIgnoreCase)
                ? ProcessSuccess("go version go1.24.5 windows/amd64")
                : new ProcessResult(true, 2, string.Empty, "usage: gofmt [flags] [path ...]", TimeSpan.FromMilliseconds(1), false, null));
        ToolchainDetectionResult result = await ManagerWith(
                new GoToolchainAdapter(runner),
                runner,
                Path.Combine(directory, "settings.json"))
            .DetectAsync("go", directory);
        Assert(result.Status == ToolchainStatus.CustomLocation, "custom Go was not distinguished");
        Assert(result.Version == "1.24.5", "Go version was not parsed");
        Assert(result.Components.All(item => item.Valid), "Go components were not validated");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task PythonDetectorValidatesRuntimeAndActions()
{
    string directory = TemporaryDirectory("python-ready");
    Directory.CreateDirectory(directory);
    try
    {
        CreateExecutables(directory, "python");
        var runner = new FakeProcessRunner(request =>
            request.Arguments.SequenceEqual(["--version"])
                ? ProcessSuccess("Python 3.13.7")
                : ProcessSuccess("python action completed"));
        ToolchainManager manager = ManagerWith(
            new PythonToolchainAdapter(runner),
            runner,
            Path.Combine(directory, "settings.json"));
        ToolchainDetectionResult result = await manager.DetectAsync("python", directory);
        Assert(result.Status == ToolchainStatus.CustomLocation, "custom Python was not distinguished");
        Assert(result.Version == "3.13.7", "Python version was not parsed");
        ProcessResult action = await manager.ExecuteAsync(
            "python",
            ToolchainAction.Run,
            directory,
            ["script with spaces.py"]);
        Assert(action.Success, "Python action did not complete");
        Assert(
            runner.Requests.Last().Arguments.SequenceEqual(["script with spaces.py"]),
            "Python script argument was not preserved");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task JavaDetectorDistinguishesFullAndPartialJdks()
{
    string directory = TemporaryDirectory("java-ready");
    Directory.CreateDirectory(directory);
    try
    {
        CreateExecutables(directory, "java", "javac");
        var runner = new FakeProcessRunner(request =>
            ProcessSuccess($"{Path.GetFileNameWithoutExtension(request.FileName)} 21.0.6"));
        ToolchainDetectionResult ready = await ManagerWith(
                new JavaToolchainAdapter(runner),
                runner,
                Path.Combine(directory, "settings.json"))
            .DetectAsync("java", directory);
        Assert(ready.Status == ToolchainStatus.CustomLocation, "complete JDK was not usable");
        Assert(ready.Components.Count == 2 && ready.Components.All(item => item.Valid), "JDK components were not validated");

        File.Delete(Path.Combine(directory, OperatingSystem.IsWindows() ? "javac.exe" : "javac"));
        ToolchainDetectionResult partial = await ManagerWith(
                new JavaToolchainAdapter(runner),
                runner,
                Path.Combine(directory, "partial-settings.json"))
            .DetectAsync("java", directory);
        Assert(partial.Status == ToolchainStatus.PartialInstallation, "JRE-only Java was not classified as partial");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task ToolchainSettingsPersistCustomPaths()
{
    string directory = TemporaryDirectory("toolchain-settings");
    Directory.CreateDirectory(directory);
    try
    {
        string settingsPath = Path.Combine(directory, "toolchains.json");
        var first = new ToolchainSettingsStore(settingsPath);
        await first.SetCustomPathAsync("rust", directory);
        IReadOnlyDictionary<string, string> loaded = await new ToolchainSettingsStore(settingsPath).LoadAsync();
        Assert(loaded["rust"] == Path.GetFullPath(directory), "custom path was not persisted");
        await first.SetCustomPathAsync("rust", null);
        loaded = await new ToolchainSettingsStore(settingsPath).LoadAsync();
        Assert(!loaded.ContainsKey("rust"), "cleared custom path was retained");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task ToolchainManagerBuildsSafeActionRequests()
{
    const string UserArgument = "feature with spaces & symbols";
    string directory = TemporaryDirectory("toolchain-action");
    Directory.CreateDirectory(directory);
    try
    {
        CreateExecutables(directory, "rustc", "cargo", "rustfmt");
        var runner = RustFakeRunner("1.95.1");
        ToolchainManager manager = ManagerWith(
            new RustToolchainAdapter(runner),
            runner,
            Path.Combine(directory, "settings.json"));
        ToolchainDetectionResult configured = await manager.ConfigureCustomPathAsync("rust", directory);
        Assert(configured.Status == ToolchainStatus.CustomLocation, "saved Rust path was not validated");
        ProcessResult action = await manager.ExecuteAsync(
            "rust",
            ToolchainAction.Run,
            directory,
            ["--features", UserArgument]);
        Assert(action.Success, "mock cargo action failed");
        ProcessRequest request = runner.Requests.Last();
        Assert(Path.GetFileNameWithoutExtension(request.FileName) == "cargo", "action did not use cargo");
        Assert(request.Arguments.SequenceEqual(["run", "--features", UserArgument]), "action arguments were concatenated or reordered");
        Assert(request.WorkingDirectory == Path.GetFullPath(directory), "action working directory was lost");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task LiveToolchainDetectionReturnsStructuredStates()
{
    string directory = TemporaryDirectory("live-toolchain-detection");
    Directory.CreateDirectory(directory);
    try
    {
        ToolchainManager manager = ToolchainManager.CreateDefault(
            Path.Combine(directory, "toolchains.json"));
        IReadOnlyList<ToolchainDetectionResult> results = await manager.DetectAllAsync();
        Assert(
            results.Select(item => item.Id).Order().SequenceEqual(["go", "java", "python", "rust"]),
            "registered toolchains were missing");
        foreach (ToolchainDetectionResult result in results)
        {
            Assert(
                !string.IsNullOrWhiteSpace(result.Message),
                $"{result.DisplayName} did not explain its status");
            if (result.Status != ToolchainStatus.NotInstalled)
            {
                Assert(
                    result.Components.Count > 0,
                    $"{result.DisplayName} did not return component validation");
            }
        }
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task AcademyCatalogIsComplete()
{
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    Assert(catalog.Tracks.Length == 7, "expected all seven tracks including Java OOP");
    Assert(catalog.Tracks.All(track => track.Lessons.Length >= 3), "a track lacks a usable lesson sequence");
    Assert(catalog.Tracks.Single(track => track.Id == "rust-systems").RequiredToolchainId == "rust", "Rust prerequisite is missing");
    Assert(catalog.Tracks.Single(track => track.Id == "go-backend").RequiredToolchainId == "go", "Go prerequisite is missing");
    Assert(catalog.Tracks.Single(track => track.Id == "java-oop-zero-to-hero").RequiredToolchainId == "java", "Java prerequisite is missing");
    Assert(catalog.GetTrack("sprout-fundamentals").Lessons
        .All(lesson => !string.IsNullOrWhiteSpace(lesson.Exercise?.Objective)), "a Sprout fundamentals exercise has no objective");
    Assert(catalog.Concepts.All(concept => concept.Examples.Length >= 2), "concepts are not cross-language");
    Assert(catalog.GetConcept("functions").Examples.Any(item => item.Language == "Sprout"), "Sprout function example is missing");
    return Task.CompletedTask;
}

static async Task AcademyLocalProfilePersists()
{
    string directory = TemporaryDirectory("academy-profile");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        AcademyService service = AcademyTestService(statePath, ToolchainStatus.NotInstalled);
        AcademyProfile saved = new(
            "  Ada Sprout  ",
            " ada.codes ",
            ExperienceLevel.Intermediate,
            [ProgrammingGoal.SystemsProgramming, ProgrammingGoal.SystemsProgramming],
            true);
        await service.SaveProfileAsync(saved);
        AcademyState reloaded = await AcademyTestService(statePath, ToolchainStatus.NotInstalled)
            .LoadStateAsync();
        Assert(reloaded.Profile?.DisplayName == "Ada Sprout", "display name was not normalized and persisted");
        Assert(reloaded.Profile?.PreferredUsername == "ada.codes", "username was not normalized and persisted");
        Assert(reloaded.Profile?.Goals.SequenceEqual([ProgrammingGoal.SystemsProgramming]) == true, "profile goals were not normalized");

        try
        {
            await service.SaveProfileAsync(saved with { PreferredUsername = "invalid user" });
            throw new InvalidOperationException("invalid username was accepted");
        }
        catch (ArgumentException)
        {
        }
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task AcademyRecommendationsAreTransparent()
{
    var engine = new RecommendationEngine(AcademyCatalog.LoadDefault());
    IReadOnlyList<TrackRecommendation> beginner = engine.Recommend(new AcademyProfile(
        "Beginner",
        "beginner",
        ExperienceLevel.AbsoluteBeginner,
        [ProgrammingGoal.GameDevelopment],
        true));
    Assert(beginner.Select(item => item.Track.Id).SequenceEqual([
        "sprout-fundamentals",
        "sprout-game-development",
    ]), "beginner learning path was not ordered from fundamentals to goal");
    Assert(beginner.All(item => item.Rule.StartsWith("Rule:", StringComparison.Ordinal)), "recommendation rules are not exposed");
    Assert(beginner.All(item => !item.Explanation.Contains("AI", StringComparison.OrdinalIgnoreCase)), "recommendations falsely claim AI");

    IReadOnlyList<TrackRecommendation> advanced = engine.Recommend(new AcademyProfile(
        "Advanced",
        "advanced",
        ExperienceLevel.Advanced,
        [ProgrammingGoal.SystemsProgramming],
        false));
    Assert(advanced.Select(item => item.Track.Id).SequenceEqual(["rust-systems"]), "advanced user was forced through Sprout fundamentals");
    return Task.CompletedTask;
}

static async Task AcademyProgressPreventsXpFarming()
{
    string directory = TemporaryDirectory("academy-progress");
    Directory.CreateDirectory(directory);
    try
    {
        AcademyService service = AcademyTestService(
            Path.Combine(directory, "academy.json"),
            ToolchainStatus.NotInstalled);
        await service.SaveProfileAsync(new AcademyProfile(
            "Learner",
            "learner",
            ExperienceLevel.AbsoluteBeginner,
            [ProgrammingGoal.GeneralProgramming],
            true));
        EnrollmentResult enrollment = await service.EnrollAsync("sprout-fundamentals");
        Assert(enrollment.Status == EnrollmentStatus.Enrolled, "track did not enroll");
        AcademyState enrolled = await service.LoadStateAsync();
        Assert(enrolled.AchievementAwards.Any(item => item.Id == "seedling-planted"), "enrollment achievement was not awarded");

        TrackDefinition fundamentals = service.Catalog.GetTrack("sprout-fundamentals");
        LessonDefinition firstLesson = fundamentals.Lessons.Single(lesson => lesson.Id == "sprout-first-program");
        var (_, firstExecution, first) = await service.SubmitCodingExerciseAsync(
            fundamentals.Id,
            firstLesson.Id,
            firstLesson.Exercise!.SolutionCode);
        var (_, repeatedExecution, repeated) = await service.SubmitCodingExerciseAsync(
            fundamentals.Id,
            firstLesson.Id,
            firstLesson.Exercise.SolutionCode);
        Assert(firstExecution.Success && repeatedExecution.Success, "passing solution did not satisfy the graded lesson");
        Assert(first is { AwardedXp: 30, AlreadyCompleted: false }, "first submission did not award its defined XP");
        Assert(repeated is { AwardedXp: 0, AlreadyCompleted: true }, "repeated submission farmed XP");

        foreach (LessonDefinition lesson in fundamentals.Lessons.Where(lesson => lesson.Id != firstLesson.Id))
        {
            var (_, execution, completion) = await service.SubmitCodingExerciseAsync(
                fundamentals.Id,
                lesson.Id,
                lesson.Exercise!.SolutionCode);
            Assert(execution.Success && completion is { AlreadyCompleted: false }, $"{lesson.Title} did not complete through graded submission");
        }

        AcademyDashboard dashboard = service.CreateDashboard(await service.LoadStateAsync());
        TrackProgress progress = dashboard.Tracks.Single(item => item.Track.Id == "sprout-fundamentals");
        Assert(progress.CompletionPercent == 100, "completed track progress is incorrect");
        Assert(dashboard.TotalXp == 100 && dashboard.Level == 2, "XP or level projection is incorrect");
        Assert(dashboard.EarnedAchievements.Any(item => item.Id == "syntax-adapter"), "track achievement was not awarded");
        Assert(dashboard.EarnedAchievements.Any(item => item.Id == "deep-roots"), "completion achievement was not awarded");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AcademyEnrollmentValidatesToolchains()
{
    string directory = TemporaryDirectory("academy-prerequisite");
    Directory.CreateDirectory(directory);
    try
    {
        string missingState = Path.Combine(directory, "missing.json");
        var missingAdapter = new FixedToolchainAdapter(ToolchainStatus.NotInstalled);
        AcademyService missing = AcademyTestServiceWithAdapter(missingState, missingAdapter);
        EnrollmentResult blocked = await missing.EnrollAsync("rust-systems");
        Assert(blocked.Status == EnrollmentStatus.ToolchainRequired, "missing Rust did not block enrollment");
        Assert(blocked.Toolchain?.Status == ToolchainStatus.NotInstalled, "prerequisite status was lost");
        Assert((await missing.LoadStateAsync()).EnrolledTrackIds.Length == 0, "blocked enrollment was persisted");
        Assert(missingAdapter.DetectionCount == 1, "Rust was not validated exactly when enrollment requested it");

        var readyAdapter = new FixedToolchainAdapter(ToolchainStatus.Ready);
        AcademyService ready = AcademyTestServiceWithAdapter(Path.Combine(directory, "ready.json"), readyAdapter);
        EnrollmentResult enrolled = await ready.EnrollAsync("rust-systems");
        Assert(enrolled.Status == EnrollmentStatus.Enrolled, "ready Rust did not allow enrollment");
        Assert((await ready.LoadStateAsync()).EnrolledTrackIds.SequenceEqual(["rust-systems"]), "ready enrollment was not persisted");
        Assert(readyAdapter.DetectionCount == 1, "ready Rust prerequisite was not validated");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task AcademyConceptBrowserSearchesExamples()
{
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    IReadOnlyList<ConceptDefinition> results = catalog.SearchConcepts("Go");
    Assert(results.Count == catalog.Concepts.Length, "language example search did not find all Go comparisons");
    results = catalog.SearchConcepts("reusable behavior");
    Assert(results.Select(item => item.Id).SequenceEqual(["functions"]), "concept summary search returned the wrong result");
    Assert(catalog.SearchConcepts("no-such-concept").Count == 0, "concept search fabricated a result");
    return Task.CompletedTask;
}

static Task JavaOopCourseLoadsAndValidates()
{
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    TrackDefinition javaTrack = catalog.GetTrack("java-oop-zero-to-hero");
    Assert(javaTrack.Title == "Java: Object Oriented Programming - Zero to Hero", "Java course title mismatch");
    Assert(javaTrack.RequiredToolchainId == "java", "Java course requires java toolchain");
    Assert(javaTrack.Lessons.Length == 22, "Java course must have 22 lessons");
    Assert(javaTrack.Modules?.Length == 6, "Java course must have 6 modules");
    Assert(javaTrack.Modules!.All(module => module.Checkpoint is { Questions: { Length: >= 2 } }), "Every Java module must end with a substantive checkpoint");
    Assert(javaTrack.Lessons.Count(lesson => lesson.Exercise?.Questions is { Length: >= 2 }) >= 6, "Java course must include recurring knowledge checks");
    Assert(javaTrack.Lessons.All(lesson => lesson.Content.Split(
        (char[]?)null,
        StringSplitOptions.RemoveEmptyEntries).Length >= 150), "Java lessons are too short for a Zero-to-Hero curriculum");
    Assert(javaTrack.Lessons.All(lesson => lesson.Exercise is
    {
        Hints: { Length: 4 },
        TestHarness: { Length: > 0 },
    }), "Every Java exercise must include four hint tiers and behavioral tests");

    CourseModel courseModel = catalog.ToCourseModel("java-oop-zero-to-hero");
    Assert(courseModel.Modules.Length == 6, "CourseModel must expose 6 modules");
    Assert(courseModel.Lessons.Length == 22, "CourseModel must expose 22 lessons");

    // Verify all module lesson IDs resolve to real lessons
    foreach (ModuleDefinition module in javaTrack.Modules!)
    {
        Assert(!string.IsNullOrWhiteSpace(module.Title), "Module title was empty");
        Assert(module.LessonIds.Length >= 3, $"Module {module.Id} lacks minimum 3 lessons");
        foreach (string lessonId in module.LessonIds)
        {
            LessonDefinition lesson = catalog.GetLesson(lessonId);
            Assert(lesson.ModuleId == module.Id, $"Lesson {lessonId} moduleId mismatch");
            Assert(lesson.ConceptIds.Length > 0, $"Lesson {lessonId} missing concept references");
            Assert(lesson.Exercise is not null, $"Lesson {lessonId} missing exercise data");
            Assert(!string.IsNullOrWhiteSpace(lesson.Exercise!.TestHarness), $"Lesson {lessonId} missing behavioral tests");
        }
    }

    // Verify exercise validation
    LessonDefinition capstoneLesson = catalog.GetLesson("java-capstone-bank-system");
    string tempDir = TemporaryDirectory("java-oop-test");
    try
    {
        AcademyService service = AcademyTestService(Path.Combine(tempDir, "state.json"), ToolchainStatus.Ready);

        // Test passing submission
        ExerciseValidationResult validResult = service.ValidateExercise(capstoneLesson, capstoneLesson.Exercise!.SolutionCode);
        Assert(validResult.Success, "Solution code failed automated validation assertions");
        Assert(validResult.PassedAssertions.Length > 0, "No assertions passed");
        Assert(validResult.FailedAssertions.Length == 0, "Assertions failed on valid solution");

        // Test failing submission
        ExerciseValidationResult invalidResult = service.ValidateExercise(capstoneLesson, "public class Empty {}");
        Assert(!invalidResult.Success, "Invalid code unexpectedly passed assertions");
        Assert(invalidResult.FailedAssertions.Length > 0, "Invalid code should have failed assertions");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    return Task.CompletedTask;
}

static Task UpdateServiceParsesReleasesAndDetectsUpdates()
{
    const string SampleReleaseJson = """
    {
      "tag_name": "v0.2.0",
      "body": "## What's Changed\n* Added Live AST graph inspector\n* Added Java OOP Zero to Hero academy course",
      "published_at": "2026-08-16T12:00:00Z",
      "assets": [
        {
          "name": "SproutStudio-Setup-x64.exe",
          "size": 75497472,
          "browser_download_url": "https://github.com/ayzeeyan/sprout-studio/releases/download/v0.2.0/SproutStudio-Setup-x64.exe"
        },
        {
          "name": "SproutStudio-Setup.exe",
          "size": 75497472,
          "browser_download_url": "https://github.com/ayzeeyan/sprout-studio/releases/download/v0.2.0/SproutStudio-Setup.exe"
        },
        {
          "name": "SHA256SUMS.txt",
          "size": 128,
          "browser_download_url": "https://github.com/ayzeeyan/sprout-studio/releases/download/v0.2.0/SHA256SUMS.txt"
        }
      ]
    }
    """;

    UpdateInfo? update = UpdateService.ParseReleaseResponse(SampleReleaseJson, "0.1.0");
    Assert(update is not null, "Update was not detected for newer version 0.2.0 vs 0.1.0");
    Assert(update!.Version == "0.2.0", "Version parsed incorrectly");
    Assert(update.TagName == "v0.2.0", "TagName parsed incorrectly");
    Assert(update.DownloadUrl == "https://github.com/ayzeeyan/sprout-studio/releases/download/v0.2.0/SproutStudio-Setup.exe", "Did not select SproutStudio-Setup.exe asset");
    Assert(update.ChecksumsUrl == "https://github.com/ayzeeyan/sprout-studio/releases/download/v0.2.0/SHA256SUMS.txt", "ChecksumsUrl was not detected");
    Assert(update.FileName == "SproutStudio-Setup.exe", "Filename mismatch");
    Assert(update.FileSize == 75497472, "Filesize mismatch");
    Assert(update.ReleaseNotes.Contains("Live AST graph"), "Release notes missing expected text");
    Assert(update.PublishedAt.HasValue, "Published date was not parsed");

    // Test SHA256 checksum manifest extraction
    const string SampleManifest = """
    e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 *SproutStudio-Setup.exe
    a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3 *SproutStudio-Setup-x64.exe
    """;
    string? extractedHash = UpdateService.ExtractHashForFile(SampleManifest, "SproutStudio-Setup.exe");
    Assert(extractedHash == "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "Extracted SHA-256 hash mismatch");

    return Task.CompletedTask;
}

static Task UpdateServiceIgnoresCurrentOrOlderVersions()
{
    const string SampleReleaseJson = """
    {
      "tag_name": "v0.1.0",
      "body": "Initial release",
      "assets": [
        {
          "name": "SproutStudio-Setup.exe",
          "size": 50000000,
          "browser_download_url": "https://github.com/ayzeeyan/sprout-studio/releases/download/v0.1.0/SproutStudio-Setup.exe"
        }
      ]
    }
    """;

    // Same version
    UpdateInfo? same = UpdateService.ParseReleaseResponse(SampleReleaseJson, "0.1.0");
    Assert(same is null, "Update should not be offered when running the same version");

    // Newer running version (e.g. local 0.3.0 vs remote 0.1.0)
    UpdateInfo? older = UpdateService.ParseReleaseResponse(SampleReleaseJson, "0.3.0");
    Assert(older is null, "Update should not be offered when remote version is older");

    // Invalid JSON or missing tag
    UpdateInfo? empty = UpdateService.ParseReleaseResponse("{}", "0.1.0");
    Assert(empty is null, "Empty JSON unexpectedly produced update");

    return Task.CompletedTask;
}

static async Task JavaOopSolutionsCompileWithInstalledJdk()
{
    ToolchainManager toolchains = ToolchainManager.CreateDefault();
    ToolchainDetectionResult detection = await toolchains.DetectAsync("java");
    if (!detection.IsUsable)
    {
        Console.WriteLine($"SKIP installed-JDK Academy compilation: {detection.Message}");
        return;
    }

    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    TrackDefinition track = catalog.GetTrack("java-oop-zero-to-hero");
    var runner = new AcademyExecutionService(toolchains);
    var failures = new List<string>();
    foreach (LessonDefinition lesson in track.Lessons)
    {
        ExerciseRuntimeResult result = await runner.ExecuteAsync(
            track,
            lesson,
            lesson.Exercise!.SolutionCode);
        if (!result.Success
            || result.TestResults is not { Length: > 0 }
            || result.TestResults.Any(test => !test.Passed))
        {
            string testFailures = string.Join(
                " | ",
                result.TestResults?.Where(test => !test.Passed)
                    .Select(test => $"{test.Id}: expected {test.Expected}; observed {test.Observed}")
                ?? []);
            failures.Add($"{lesson.Id}: {result.Summary} {string.Join(" | ", result.Diagnostics.Take(3))} {testFailures}");
        }
    }

    LessonDefinition behaviorLesson = track.Lessons.Single(lesson => lesson.Id == "java-types-memory-model");
    string plausibleButWrong = behaviorLesson.Exercise!.SolutionCode.Replace(
        "new String(\"Sprout\")",
        "new String(\"Wrong\")",
        StringComparison.Ordinal);
    var service = new AcademyService(
        catalog,
        new AcademyStateStore(Path.Combine(TemporaryDirectory("java-behavior-grading"), "academy.json")),
        toolchains);
    ExerciseExecutionResult wrongBehavior = await service.ExecuteExerciseAsync(
        behaviorLesson,
        plausibleButWrong,
        (_, source, cancellationToken) => runner.ExecuteAsync(track, behaviorLesson, source, cancellationToken));
    Assert(!wrongBehavior.Success, "behaviorally incorrect Java code passed because it matched source requirements");
    Assert(
        wrongBehavior.RequirementResults.Any(result => result.Id == "runtime:test:memory-output" && !result.Passed),
        "behavioral Java failure did not produce an actionable runtime test requirement");

    Assert(
        failures.Count == 0,
        $"{failures.Count} Java Academy solution(s) failed javac: {string.Join(Environment.NewLine, failures)}");
}

static async Task AcademyExternalTrackSolutionsExecuteWithInstalledToolchains()
{
    ToolchainManager toolchains = ToolchainManager.CreateDefault();
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    var runner = new AcademyExecutionService(toolchains);
    string tempDir = TemporaryDirectory("academy-external-solutions");
    var failures = new List<string>();
    int executedLessons = 0;

    try
    {
        var service = new AcademyService(
            catalog,
            new AcademyStateStore(Path.Combine(tempDir, "academy.json")),
            toolchains);

        foreach (string trackId in new[] { "rust-systems", "go-backend", "python-automation" })
        {
            TrackDefinition track = catalog.GetTrack(trackId);
            string toolchainId = track.RequiredToolchainId
                ?? throw new InvalidOperationException($"{track.Id} does not declare its required toolchain.");
            ToolchainDetectionResult detection = await toolchains.DetectAsync(toolchainId);
            if (!detection.IsUsable)
            {
                Console.WriteLine($"SKIP {track.Title} installed-toolchain execution: {detection.Message}");
                continue;
            }

            foreach (LessonDefinition lesson in track.Lessons.Where(candidate => candidate.Exercise is not null))
            {
                ExerciseExecutionResult result = await service.ExecuteExerciseAsync(
                    lesson,
                    lesson.Exercise!.SolutionCode,
                    (_, source, cancellationToken) => runner.ExecuteAsync(track, lesson, source, cancellationToken));
                executedLessons++;
                if (!result.Success)
                {
                    string requirements = string.Join(
                        " | ",
                        result.RequirementResults.Where(requirement => !requirement.Passed)
                            .Select(requirement => $"{requirement.Id}: expected {requirement.Expected}; observed {requirement.Observed}"));
                    failures.Add($"{track.Id}/{lesson.Id}: {result.ExecutionSummary} {requirements}");
                }
            }
        }

        Assert(executedLessons > 0, "No installed external Academy toolchain was exercised.");
        Assert(
            failures.Count == 0,
            $"{failures.Count} external Academy solution(s) failed real execution:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static async Task AcademyRealExecutionValidatesOutput()
{
    string catalogPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "academy", "catalog.json");
    if (!File.Exists(catalogPath))
    {
        catalogPath = Path.Combine(AppContext.BaseDirectory, "academy", "catalog.json");
    }
    if (!File.Exists(catalogPath))
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        catalogPath = Path.Combine(root, "academy", "catalog.json");
    }

    string tempDir = TemporaryDirectory("academy-real-exec-tests");
    Directory.CreateDirectory(tempDir);
    try
    {
        string statePath = Path.Combine(tempDir, "academy.json");
        AcademyCatalog catalog = AcademyCatalog.LoadDefault();
        var service = new AcademyService(catalog, new AcademyStateStore(statePath), ToolchainManager.CreateDefault());
        TrackDefinition javaTrack = catalog.GetTrack("java-oop-zero-to-hero");
        LessonDefinition lessonDef = javaTrack.Lessons.First(l => l.Exercise is not null);

        // Test with simulated successful toolchain compilation and runtime stdout
        ExerciseExecutionResult successResult = await service.ExecuteExerciseAsync(
            lessonDef,
            lessonDef.Exercise!.SolutionCode,
            (lessonId, code, ct) => Task.FromResult(new ExerciseRuntimeResult(
                true,
                lessonDef.Exercise.ExpectedOutput,
                [],
                TimeSpan.FromMilliseconds(12),
                "Simulated executor completed.",
                RanProgram: true)));

        Assert(successResult.Success, "Expected real toolchain execution to succeed");
        Assert(successResult.ExecutedWithRealToolchain, "ExecutedWithRealToolchain flag was false");
        Assert(successResult.CompilerDiagnostics.Length == 0, "Expected no compiler diagnostics");
        Assert(successResult.PassedAssertions.Length > 0, "Expected passed assertions");

        // Test with simulated compilation error
        ExerciseExecutionResult compilerFailResult = await service.ExecuteExerciseAsync(
            lessonDef,
            lessonDef.Exercise.SolutionCode,
            (lessonId, code, ct) => Task.FromResult(new ExerciseRuntimeResult(
                false,
                string.Empty,
                ["Main.java:5: error: ';' expected"],
                TimeSpan.FromMilliseconds(8),
                "Compilation failed.",
                RanProgram: false)));

        Assert(!compilerFailResult.Success, "Expected compilation failure to report failure");
        Assert(compilerFailResult.CompilerDiagnostics.Length == 1, "Expected 1 compiler diagnostic");
        Assert(compilerFailResult.CompilerDiagnostics[0].Contains("error: ';' expected"), "Diagnostic message mismatch");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static async Task SessionRecoveryJournalsAndRestores()
{
    string tempDir = TemporaryDirectory("session-recovery-tests");
    Directory.CreateDirectory(tempDir);
    try
    {
        string recoveryPath = Path.Combine(tempDir, "recovery_session.json");
        var recoveryService = new SessionRecoveryService(recoveryPath);

        // Initially no recoverable session
        SessionRecoveryState? initial = await recoveryService.CheckForRecoverableSessionAsync();
        Assert(initial is null, "Initial state should have no recoverable session");

        // Save heartbeat with dirty unsaved document
        var docs = new List<DocumentRecoverySnapshot>
        {
            new DocumentRecoverySnapshot(
                FilePath: Path.Combine(tempDir, "main.spr"),
                Title: "main.spr",
                Content: "has message = 'recovered!'\nprint(message)\n",
                CaretOffset: 15,
                IsDirty: true,
                SavedAt: DateTimeOffset.UtcNow),
        };

        await recoveryService.SaveHeartbeatAsync(
            activeProjectPath: tempDir,
            selectedIndex: 0,
            documents: docs);

        // Check crash recovery after unexpected termination (CleanShutdown = false)
        SessionRecoveryState? recovered = await recoveryService.CheckForRecoverableSessionAsync();
        Assert(recovered is not null, "Expected recoverable session to be detected");
        Assert(!recovered!.CleanShutdown, "CleanShutdown should be false for crashed session");
        Assert(recovered.Documents.Count == 1, "Recovered document count mismatch");
        Assert(recovered.Documents[0].Content.Contains("recovered!"), "Recovered buffer content mismatch");
        Assert(recovered.Documents[0].IsDirty, "Recovered document was not marked dirty");

        // After clean shutdown, recovery should not offer crash prompt
        await recoveryService.MarkCleanShutdownAsync();
        SessionRecoveryState? afterClean = await recoveryService.CheckForRecoverableSessionAsync();
        Assert(afterClean is null, "Clean shutdown should not trigger crash recovery prompt");

        // Clear recovery
        recoveryService.ClearRecovery();
        Assert(!File.Exists(recoveryPath), "Recovery file should be deleted on clear");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static Task AcademyDashboardPrioritizesTasks()
{
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    string tempDir = TemporaryDirectory("academy-dashboard-tests");
    Directory.CreateDirectory(tempDir);
    try
    {
        string statePath = Path.Combine(tempDir, "academy.json");
        var service = new AcademyService(catalog, new AcademyStateStore(statePath), ToolchainManager.CreateDefault());

        TrackDefinition track = catalog.GetTrack("sprout-fundamentals");
        LessonDefinition firstLesson = track.Lessons[0];
        LessonDefinition secondLesson = track.Lessons[1];

        // State with 1 completed lesson
        var state = AcademyState.Empty with
        {
            Profile = new AcademyProfile("Jane Doe", "janedoe", ExperienceLevel.Intermediate, [ProgrammingGoal.GeneralProgramming], true),
            EnrolledTrackIds = [track.Id],
            CompletedLessonIds = [firstLesson.Id],
            ActiveTrackId = track.Id,
        };

        AcademyDashboard dashboard = service.CreateDashboard(state);

        // Continue Task must point to the 2nd incomplete lesson
        Assert(dashboard.ContinueTask is not null, "ContinueTask should not be null");
        Assert(dashboard.ContinueTask!.LessonId == secondLesson.Id, $"Expected continue task {secondLesson.Id}, got {dashboard.ContinueTask.LessonId}");
        Assert(dashboard.ContinueTask.ProgressPercent > 0, "ProgressPercent should be calculated");

        // Prioritized Tasks should have continue task at priority 1
        Assert(dashboard.PrioritizedTasks.Length > 0, "Prioritized tasks should not be empty");
        Assert(dashboard.PrioritizedTasks[0].LessonId == secondLesson.Id, "Top task should be the next lesson");
        Assert(dashboard.PrioritizedTasks[0].Priority == 1, "Top task priority must be 1");

        // Add a failed submission for a 3rd lesson -> should appear as Revision task
        LessonDefinition thirdLesson = track.Lessons[2];
        var failedSub = new SubmissionRecord(
            Id: "sub-test-1",
            TrackId: track.Id,
            LessonId: thirdLesson.Id,
            ModuleId: thirdLesson.ModuleId,
            ExerciseTitle: thirdLesson.Title,
            AttemptNumber: 1,
            SubmittedAt: DateTimeOffset.UtcNow,
            Type: SubmissionType.CodeExercise,
            Answer: "print('test')",
            Passed: false,
            Score: 5,
            Feedback: "Missing requirement",
            PassedRequirements: [],
            FailedRequirements: ["Missing requirement"],
            CompilerDiagnostics: []);

        var stateWithFailedSub = state with
        {
            Submissions = [failedSub],
        };

        AcademyDashboard dashboardWithRevision = service.CreateDashboard(stateWithFailedSub);
        AcademyTask? revisionTask = dashboardWithRevision.PrioritizedTasks.FirstOrDefault(t => t.Type == TaskType.Revision);
        Assert(revisionTask is not null, "Expected a Revision task for failed submission");
        Assert(revisionTask!.LessonId == thirdLesson.Id, "Revision task should target failed lesson");
        Assert(revisionTask.Priority == 2, "Revision task should have priority 2");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
    return Task.CompletedTask;
}

static async Task AcademySubmissionsAndAttempts()
{
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    string tempDir = TemporaryDirectory("academy-submission-tests");
    Directory.CreateDirectory(tempDir);
    try
    {
        string statePath = Path.Combine(tempDir, "academy.json");
        var service = new AcademyService(catalog, new AcademyStateStore(statePath), ToolchainManager.CreateDefault());

        TrackDefinition track = catalog.GetTrack("sprout-fundamentals");
        await service.EnrollAsync(track.Id);
        LessonDefinition exerciseLesson = track.Lessons.First(l => l.Exercise is not null);

        // 1. Submit failing attempt
        var (sub1, exec1, comp1) = await service.SubmitCodingExerciseAsync(
            track.Id,
            exerciseLesson.Id,
            "// incomplete code");

        Assert(!sub1.Passed, "First submission should fail");
        Assert(sub1.AttemptNumber == 1, "Attempt number should be 1");
        Assert(comp1 is null, "Incomplete attempt should not complete lesson");

        AcademyState stateAfterFail = await service.LoadStateAsync();
        Assert(!stateAfterFail.CompletedLessonIds.Contains(exerciseLesson.Id), "Lesson must not be completed on fail");
        Assert(stateAfterFail.Submissions.Length == 1, "Submission history should have 1 entry");

        // 2. Submit passing attempt
        var (sub2, exec2, comp2) = await service.SubmitCodingExerciseAsync(
            track.Id,
            exerciseLesson.Id,
            exerciseLesson.Exercise!.SolutionCode);

        Assert(sub2.Passed, "Second submission should pass with solution code");
        Assert(sub2.AttemptNumber == 2, "Attempt number should be 2");
        Assert(comp2 is not null, "Passing attempt should complete lesson");

        AcademyState stateAfterPass = await service.LoadStateAsync();
        Assert(stateAfterPass.CompletedLessonIds.Contains(exerciseLesson.Id), "Lesson must be completed on pass");
        Assert(stateAfterPass.Submissions.Length == 2, "Submission history should have 2 entries");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static async Task AcademyRunTestsDoesNotPersistProgress()
{
    string directory = TemporaryDirectory("academy-run-tests-state");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        AcademyService service = AcademyTestService(statePath, ToolchainStatus.Ready);
        TrackDefinition track = service.Catalog.GetTrack("sprout-fundamentals");
        LessonDefinition lesson = track.Lessons.First(candidate => candidate.Exercise is not null);
        await service.EnrollAsync(track.Id);

        AcademyState before = await service.LoadStateAsync();
        int xpBefore = service.CreateDashboard(before).TotalXp;
        ExerciseExecutionResult runResult = await service.ExecuteExerciseAsync(
            lesson,
            lesson.Exercise!.SolutionCode);
        AcademyState after = await service.LoadStateAsync();

        Assert(runResult.Success, "Run Tests did not accept the catalog solution");
        Assert(runResult.PassedAssertions.Length == lesson.Exercise.Assertions.Length, "Run Tests did not evaluate every source requirement");
        Assert(after.Submissions.SequenceEqual(before.Submissions), "Run Tests persisted an official submission attempt");
        Assert(after.CompletedLessonIds.SequenceEqual(before.CompletedLessonIds), "Run Tests completed a graded lesson");
        Assert(service.CreateDashboard(after).TotalXp == xpBefore, "Run Tests awarded XP");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AcademySubmitPersistsOneAttemptAndXpOnce()
{
    string directory = TemporaryDirectory("academy-submit-state");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        AcademyService service = AcademyTestService(statePath, ToolchainStatus.Ready);
        TrackDefinition track = service.Catalog.GetTrack("sprout-fundamentals");
        LessonDefinition lesson = track.Lessons.First(candidate => candidate.Exercise is not null);
        await service.EnrollAsync(track.Id);

        var (submission, execution, completion) = await service.SubmitCodingExerciseAsync(
            track.Id,
            lesson.Id,
            lesson.Exercise!.SolutionCode);
        AcademyState state = await service.LoadStateAsync();
        AcademyDashboard dashboard = service.CreateDashboard(state);

        Assert(execution.Success && submission.Passed, "passing Submit was not graded as successful");
        Assert(submission.AttemptNumber == 1, "first Submit did not receive attempt number 1");
        Assert(state.Submissions.Length == 1 && state.Submissions[0].Id == submission.Id, "one Submit did not persist exactly one attempt");
        Assert(state.CompletedLessonIds.Count(id => string.Equals(id, lesson.Id, StringComparison.OrdinalIgnoreCase)) == 1, "one Submit did not persist exactly one completion");
        Assert(completion is { AlreadyCompleted: false } && completion.AwardedXp == lesson.Xp, "first successful Submit did not award the lesson XP once");
        Assert(dashboard.TotalXp == lesson.Xp, "projected XP did not equal the single completed lesson award");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AcademyCodingLessonGradesKnowledgeCheck()
{
    string directory = TemporaryDirectory("academy-composite-activity");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        AcademyService service = AcademyTestService(statePath, ToolchainStatus.Ready);
        TrackDefinition track = service.Catalog.GetTrack("java-oop-zero-to-hero");
        LessonDefinition lesson = track.Lessons.First(candidate =>
            candidate.Exercise is { Assertions.Length: > 0, Questions: { Length: > 0 } });
        await service.EnrollAsync(track.Id);

        var (unanswered, unansweredExecution, unansweredCompletion) = await service.SubmitCodingExerciseAsync(
            track.Id,
            lesson.Id,
            lesson.Exercise!.SolutionCode);
        Assert(!unanswered.Passed && !unansweredExecution.Success, "an unanswered integrated knowledge check unexpectedly passed");
        Assert(unansweredCompletion is null, "an unanswered integrated knowledge check completed the lesson");
        Assert(
            unansweredExecution.RequirementResults.Any(result =>
                lesson.Exercise.Questions!.Any(question => question.Id == result.Id) && !result.Passed),
            "integrated knowledge failures were not returned as actionable requirements");

        Dictionary<string, string[]> correctAnswers = lesson.Exercise.Questions!.ToDictionary(
            question => question.Id,
            question => question.CorrectAnswers.ToArray());
        var (answered, answeredExecution, answeredCompletion) = await service.SubmitCodingExerciseAsync(
            track.Id,
            lesson.Id,
            lesson.Exercise.SolutionCode,
            executor: null,
            quizAnswers: correctAnswers);
        Assert(answered.Passed && answeredExecution.Success, "correct code and knowledge answers did not pass together");
        Assert(answered.AttemptNumber == 2, "composite activity did not preserve official attempt history");
        Assert(answeredCompletion is { AlreadyCompleted: false }, "passing composite activity did not complete the lesson");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AcademyCheckpointPassFailAndCompletion()
{
    string directory = TemporaryDirectory("academy-checkpoint-state");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        AcademyService service = AcademyTestService(statePath, ToolchainStatus.Ready);
        TrackDefinition track = service.Catalog.GetTrack("java-oop-zero-to-hero");
        ModuleDefinition module = track.Modules!.First(candidate => candidate.Checkpoint is not null);
        CheckpointDefinition checkpoint = module.Checkpoint!;
        LessonDefinition activity = service.GetActivity(track.Id, checkpoint.Id);
        await service.EnrollAsync(track.Id);
        AcademyState enrolled = await service.LoadStateAsync();
        await new AcademyStateStore(statePath).SaveAsync(enrolled with
        {
            CompletedLessonIds = module.LessonIds.ToArray(),
        });
        AcademyDashboard unlockedDashboard = service.CreateDashboard(await service.LoadStateAsync());
        int baselineXp = unlockedDashboard.TotalXp;
        Assert(
            unlockedDashboard.ContinueTask?.LessonId == checkpoint.Id,
            "Continue Learning did not advance to the unlocked module checkpoint");
        Assert(
            unlockedDashboard.PrioritizedTasks.Count(task => string.Equals(
                task.LessonId,
                checkpoint.Id,
                StringComparison.OrdinalIgnoreCase)) == 1,
            "the unlocked checkpoint was duplicated or omitted in the task queue");

        Dictionary<string, string[]> wrongAnswers = checkpoint.Questions.ToDictionary(
            question => question.Id,
            _ => new[] { "definitely incorrect" });
        var (failedSubmission, failed, failedScore, _, failedCompletion) = await service.SubmitQuizAnswersAsync(
            track.Id,
            checkpoint.Id,
            wrongAnswers);
        AcademyState afterFailure = await service.LoadStateAsync();

        Assert(!failed && failedScore < 70 && !failedSubmission.Passed, "incorrect checkpoint answers unexpectedly passed");
        Assert(failedSubmission.AttemptNumber == 1 && failedCompletion is null, "failed checkpoint produced a completion");
        Assert(afterFailure.Submissions.Length == 1, "failed checkpoint attempt was not persisted exactly once");
        Assert(!afterFailure.CompletedLessonIds.Contains(checkpoint.Id, StringComparer.OrdinalIgnoreCase), "failed checkpoint was marked complete");
        Assert(service.CreateDashboard(afterFailure).TotalXp == baselineXp, "failed checkpoint awarded XP");

        Dictionary<string, string[]> correctAnswers = checkpoint.Questions.ToDictionary(
            question => question.Id,
            question => question.CorrectAnswers.ToArray());
        var (passedSubmission, passed, passedScore, _, passedCompletion) = await service.SubmitQuizAnswersAsync(
            track.Id,
            checkpoint.Id,
            correctAnswers);
        AcademyState afterPass = await service.LoadStateAsync();
        AcademyDashboard afterPassDashboard = service.CreateDashboard(afterPass);

        Assert(passed && passedScore >= 70 && passedSubmission.Passed, "correct checkpoint answers did not pass");
        Assert(passedSubmission.AttemptNumber == 2, "passing checkpoint did not follow the failed attempt");
        Assert(passedCompletion is { AlreadyCompleted: false } && passedCompletion.AwardedXp == activity.Xp, "first checkpoint pass did not award checkpoint XP");
        Assert(afterPass.Submissions.Length == 2, "checkpoint pass/fail history was not preserved");
        Assert(afterPass.CompletedLessonIds.Count(id => string.Equals(id, checkpoint.Id, StringComparison.OrdinalIgnoreCase)) == 1, "checkpoint completion was not persisted exactly once");
        Assert(afterPassDashboard.TotalXp == baselineXp + activity.Xp, "checkpoint XP projection is incorrect");

        var (repeatSubmission, repeatPassed, _, _, repeatCompletion) = await service.SubmitQuizAnswersAsync(
            track.Id,
            checkpoint.Id,
            correctAnswers);
        AcademyDashboard afterRepeatDashboard = service.CreateDashboard(await service.LoadStateAsync());
        Assert(repeatPassed && repeatSubmission.AttemptNumber == 3, "checkpoint retry was not recorded as the next attempt");
        Assert(repeatCompletion is { AlreadyCompleted: true, AwardedXp: 0 }, "repeated checkpoint pass farmed XP");
        Assert(afterRepeatDashboard.TotalXp == baselineXp + activity.Xp, "repeated checkpoint pass changed projected XP");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AcademyHintsAndDailyAllowance()
{
    AcademyCatalog catalog = AcademyCatalog.LoadDefault();
    string tempDir = TemporaryDirectory("academy-hint-tests");
    Directory.CreateDirectory(tempDir);
    try
    {
        string statePath = Path.Combine(tempDir, "academy.json");
        var service = new AcademyService(catalog, new AcademyStateStore(statePath), ToolchainManager.CreateDefault());

        TrackDefinition track = catalog.GetTrack("sprout-fundamentals");
        await service.EnrollAsync(track.Id);
        LessonDefinition exerciseLesson = track.Lessons.First(l => l.Exercise is not null && l.Exercise.Hints.Length >= 2);

        // 1. Initial daily allowance is 5
        AcademyState state0 = await service.LoadStateAsync();
        Assert(state0.HintUsage.DailyAllowance == 5, "Default hint allowance should be 5");
        Assert(state0.HintUsage.RemainingHintsToday == 5, "Initial remaining hints should be 5");

        // 2. Request hint 0 -> succeeds and consumes 1 hint
        var (ok1, hint1, msg1) = await service.RequestHintAsync(track.Id, exerciseLesson.Id, 0);
        Assert(ok1, "Hint request 1 should succeed");
        Assert(hint1 is not null, "Hint 1 should not be null");
        Assert(hint1!.IsRevealed, "Hint 1 should be marked revealed");

        AcademyState state1 = await service.LoadStateAsync();
        Assert(state1.HintUsage.UsedCountToday == 1, "Used count should be 1");
        Assert(state1.HintUsage.RemainingHintsToday == 4, "Remaining hints should be 4");

        // 3. Request hint 0 AGAIN -> does NOT consume another hint
        var (ok2, hint2, msg2) = await service.RequestHintAsync(track.Id, exerciseLesson.Id, 0);
        Assert(ok2, "Re-requesting hint 0 should succeed");
        AcademyState state2 = await service.LoadStateAsync();
        Assert(state2.HintUsage.UsedCountToday == 1, "Re-requesting already revealed hint should not consume allowance");
        Assert(state2.HintUsage.RemainingHintsToday == 4, "Remaining hints should still be 4");

        // 4. Test Accessibility / Unlimited Mode
        await service.ConfigureHintSettingsAsync(HintAssistanceMode.Unlimited);
        AcademyState stateUnlimited = await service.LoadStateAsync();
        Assert(stateUnlimited.HintUsage.Mode == HintAssistanceMode.Unlimited, "Mode should be Unlimited");
        Assert(stateUnlimited.HintUsage.CanRevealNewHint, "Unlimited mode should always allow hints");
        Assert(stateUnlimited.HintUsage.RemainingHintsToday == int.MaxValue, "Unlimited mode remaining hints is MaxValue");

        await new AcademyStateStore(statePath).SaveAsync(stateUnlimited with
        {
            HintUsage = stateUnlimited.HintUsage with
            {
                LastUsageDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"),
                UsedCountToday = stateUnlimited.HintUsage.DailyAllowance,
                Mode = HintAssistanceMode.Standard,
            },
        });
        AcademyService nextDayService = new(catalog, new AcademyStateStore(statePath), ToolchainManager.CreateDefault());
        AcademyState nextDay = await nextDayService.LoadStateAsync();
        Assert(nextDay.HintUsage.UsedCountToday == 0, "local calendar-day rollover did not reset used hints");
        Assert(nextDay.HintUsage.RemainingHintsToday == nextDay.HintUsage.DailyAllowance, "local calendar-day rollover did not restore the allowance");
        var (nextDaySuccess, _, _) = await nextDayService.RequestHintAsync(track.Id, exerciseLesson.Id, 1);
        Assert(nextDaySuccess, "a new hint could not be revealed after the local day reset");
        Assert((await nextDayService.LoadStateAsync()).HintUsage.UsedCountToday == 1, "first new hint after rollover did not start a fresh counter");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static async Task AcademyFourTierHintsAndRereads()
{
    string directory = TemporaryDirectory("academy-four-tier-hints");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        AcademyService service = AcademyTestService(statePath, ToolchainStatus.Ready);
        TrackDefinition track = service.Catalog.GetTrack("java-oop-zero-to-hero");
        LessonDefinition lesson = track.Lessons.First(candidate => candidate.Exercise?.Hints.Length >= 4);
        await service.EnrollAsync(track.Id);

        string[] expectedTierNames = ["Nudge", "Concept", "Structure", "Detail"];
        var revealed = new List<HintItem>();
        for (int index = 0; index < expectedTierNames.Length; index++)
        {
            var (success, hint, message) = await service.RequestHintAsync(track.Id, lesson.Id, index);
            Assert(success && hint is not null, $"hint tier {index + 1} was not revealed: {message}");
            Assert(hint!.Level == index + 1, $"hint tier {index + 1} has the wrong numeric level");
            Assert(hint.LevelName == expectedTierNames[index], $"hint tier {index + 1} should be {expectedTierNames[index]}");
            revealed.Add(hint);
        }

        AcademyState afterReveal = await service.LoadStateAsync();
        Assert(afterReveal.HintUsage.UsedCountToday == 4, "four newly revealed tiers did not consume four hints");
        Assert(afterReveal.HintUsage.RevealedHintIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4, "revealed hint IDs were not persisted distinctly");

        await service.ConfigureHintSettingsAsync(HintAssistanceMode.Standard, customDailyAllowance: 4);
        AcademyState atLimit = await service.LoadStateAsync();
        Assert(atLimit.HintUsage.RemainingHintsToday == 0, "test did not reach the configured daily hint limit");

        AcademyService reloadedService = AcademyTestService(statePath, ToolchainStatus.Ready);
        for (int index = 0; index < expectedTierNames.Length; index++)
        {
            var (success, reread, message) = await reloadedService.RequestHintAsync(track.Id, lesson.Id, index);
            Assert(success && reread is not null, $"revealed hint tier {index + 1} could not be reread at the daily limit");
            Assert(message == "Hint previously revealed.", $"hint tier {index + 1} was not recognized as a reread");
            Assert(reread!.Id == revealed[index].Id && reread.Content == revealed[index].Content, $"reread hint tier {index + 1} changed content");
        }

        AcademyState afterRereads = await reloadedService.LoadStateAsync();
        Assert(afterRereads.HintUsage.UsedCountToday == 4, "rereading revealed hints consumed additional allowance");
        Assert(afterRereads.HintUsage.RemainingHintsToday == 0, "rereading revealed hints changed remaining allowance");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AcademyStateStoreMigratesV1Schema()
{
    string tempDir = TemporaryDirectory("academy-migration-tests");
    Directory.CreateDirectory(tempDir);
    try
    {
        string statePath = Path.Combine(tempDir, "academy.json");
        // Write raw V1 JSON
        string v1Json = """
        {
          "schemaVersion": 1,
          "profile": {
            "displayName": "Migrated Student",
            "preferredUsername": "migrated-student",
            "experience": "Intermediate",
            "goals": ["GeneralProgramming"],
            "showGamification": true
          },
          "enrolledTrackIds": ["sprout-fundamentals"],
          "completedLessonIds": ["syntax-foundations"],
          "achievementAwards": []
        }
        """;
        await File.WriteAllTextAsync(statePath, v1Json);

        var store = new AcademyStateStore(statePath);
        AcademyState loaded = await store.LoadAsync();

        Assert(loaded.SchemaVersion == AcademyState.CurrentSchemaVersion, "Schema version must be migrated to CurrentSchemaVersion (2)");
        Assert(loaded.Profile?.DisplayName == "Migrated Student", "Profile display name must be preserved");
        Assert(loaded.EnrolledTrackIds.SequenceEqual(["sprout-fundamentals"]), "Enrolled tracks must be preserved");
        Assert(loaded.CompletedLessonIds.SequenceEqual(["syntax-foundations"]), "Completed lessons must be preserved");
        Assert(loaded.Submissions is not null, "Submissions must be initialized");
        Assert(loaded.HintUsage is not null, "HintUsage must be initialized");
        Assert(loaded.HintUsage!.DailyAllowance == 5, "HintUsage should have default allowance 5");
    }
    finally
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

static Task AcademyPracticeWorkspaceTransition()
{
    var workspace = new EditorWorkspace();
    var catalog = AcademyCatalog.LoadDefault();
    var javaTrack = catalog.GetTrack("java-oop-zero-to-hero");
    Assert(javaTrack is not null, "Java track must exist in catalog");

    EditorDocument doc = workspace.CreateDocument("Main.java", "public class Main {\n    public static void main(String[] args) {\n    }\n}\n");
    Assert(doc.Language.Id == DocumentLanguage.Java, "Java language should be assigned to Main.java");
    Assert(doc.DisplayName == "Main.java", "Display name should be Main.java");
    Assert(doc.Source.Contains("public class Main"), "Source must contain starter code");

    doc.UpdateSource("public class Main {\n    // updated\n}\n");
    Assert(doc.Source.Contains("// updated"), "UpdateSource must update document source");
    return Task.CompletedTask;
}

static Task AcademyDocumentTabCreatesAndPreventsDuplicates()
{
    var workspace = new EditorWorkspace();
    var catalog = AcademyCatalog.LoadDefault();
    var track = catalog.GetTrack("java-oop-zero-to-hero");
    var lesson = track.Lessons[0];
    LanguageDescriptor lang = LanguageRegistry.Get(DocumentLanguage.Java);

    EditorDocument doc1 = workspace.OpenOrCreateAcademyDocument(
        track.Id,
        lesson.Id,
        lesson.Title,
        lang,
        isExercise: lesson.Exercise is not null,
        linkedCompanionPath: "Main.java");

    Assert(doc1.IsAcademy, "Document must have IsAcademy = true");
    Assert(doc1.Kind == (lesson.Exercise is not null ? DocumentKind.AcademyExercise : DocumentKind.AcademyLesson), "DocumentKind should match exercise presence");
    Assert(doc1.DisplayName.Contains(lesson.Title), "DisplayName should contain lesson title");
    Assert(doc1.Language.Id == DocumentLanguage.Java, "Language should be Java");
    Assert(doc1.FilePath == $"academy://tracks/{track.Id}/lessons/{lesson.Id}", "FilePath must be the deterministic URI");
    Assert(doc1.LinkedCompanionPath == "Main.java", "Linked companion path must be preserved");

    // Attempting to open again must return the exact same instance without creating duplicate
    EditorDocument doc2 = workspace.OpenOrCreateAcademyDocument(
        track.Id,
        lesson.Id,
        lesson.Title,
        lang,
        isExercise: lesson.Exercise is not null,
        linkedCompanionPath: "Main.java");

    Assert(ReferenceEquals(doc1, doc2), "Subsequent OpenOrCreateAcademyDocument call must return the existing document instance");
    Assert(workspace.Documents.Count == 1, "Documents collection should have exactly 1 document (no duplicates)");
    return Task.CompletedTask;
}

static async Task AcademyInTabRequirementsAndSubmissionFlow()
{
    string directory = TemporaryDirectory("academy-in-tab-submission");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        var service = AcademyTestService(statePath, ToolchainStatus.Ready);
        var catalog = AcademyCatalog.LoadDefault();
        var track = catalog.GetTrack("java-oop-zero-to-hero");
        var lesson = track.Lessons.First(l => l.Exercise is not null);
        await service.EnrollAsync(track.Id);

        string passingCode = lesson.Exercise!.SolutionCode;
        var (submission, execResult, completion) = await service.SubmitCodingExerciseAsync(
            track.Id,
            lesson.Id,
            passingCode);

        Assert(submission.Passed, "Passing code must produce passing submission");
        Assert(submission.Score == 10, "Passing submission must have full score");
        Assert(submission.AttemptNumber == 1, "First submission attempt must be 1");
        Assert(execResult.Success, "Execution result must report success");
        Assert(execResult.PassedAssertions.Length > 0, "Assertions should be recorded as passed");
        Assert(execResult.FailedAssertions.Length == 0, "No failed assertions on passing code");

        // Verify state persistence
        AcademyState state = await service.LoadStateAsync();
        Assert(state.Submissions.Length == 1, "Submission record must be persisted in Academy state");
        Assert(state.Submissions[0].Answer == passingCode, "Submitted answer code must match");
        Assert(state.CompletedLessonIds.Contains(lesson.Id), "Lesson must be marked complete");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AcademyInTabHintsAndAllowanceLifecycle()
{
    string directory = TemporaryDirectory("academy-hints-lifecycle");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        var service = AcademyTestService(statePath, ToolchainStatus.Ready);
        var catalog = AcademyCatalog.LoadDefault();
        var track = catalog.GetTrack("java-oop-zero-to-hero");
        var lesson = track.Lessons.First(l => l.Exercise?.Hints.Length > 1);

        AcademyState initialState = await service.LoadStateAsync();
        Assert(initialState.HintUsage.RemainingHintsToday == HintUsageState.DefaultDailyAllowance, "Initial hints allowance must be 5");

        var (success1, hint1, _) = await service.RequestHintAsync(track.Id, lesson.Id, 0);
        Assert(success1 && hint1 is not null, "Hint 1 request must succeed");
        Assert(hint1!.Level == 1, "First hint level must be 1");

        AcademyState stateAfter1 = await service.LoadStateAsync();
        Assert(stateAfter1.HintUsage.RemainingHintsToday == HintUsageState.DefaultDailyAllowance - 1, "Remaining hints must decrement by 1");
        Assert(stateAfter1.HintUsage.RevealedHintIds.Contains(hint1!.Id), "Revealed hint ID must be stored");

        // Re-reading already revealed hint should NOT consume extra allowance
        var (successReRead, hintReRead, _) = await service.RequestHintAsync(track.Id, lesson.Id, 0);
        Assert(successReRead && hintReRead is not null, "Re-reading revealed hint must succeed");
        AcademyState stateAfterReRead = await service.LoadStateAsync();
        Assert(stateAfterReRead.HintUsage.RemainingHintsToday == stateAfter1.HintUsage.RemainingHintsToday, "Re-reading revealed hint must not decrement daily allowance");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AvaloniaAcademyStarts()
{
    string directory = TemporaryDirectory("academy-startup");
    Directory.CreateDirectory(directory);
    try
    {
        string statePath = Path.Combine(directory, "academy.json");
        await new AcademyStateStore(statePath).SaveAsync(AcademyState.Empty with
        {
            Profile = new AcademyProfile(
                "Startup Learner",
                "startup-learner",
                ExperienceLevel.Intermediate,
                [ProgrammingGoal.ComputerScience],
                true),
        });
        await AssertAcademyWindowStartsAsync(statePath, "Sprout Studio");
        await AssertAcademyWindowStartsAsync(
            Path.Combine(directory, "new-user.json"),
            "Sprout Studio");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task AssertAcademyWindowStartsAsync(string statePath, string expectedTitle)
{
    string executable = StudioExecutablePath();
    var startInfo = new ProcessStartInfo(executable)
    {
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(executable),
        Arguments = "--open-academy",
    };
    startInfo.Environment["SPROUT_STUDIO_TEST_ACADEMY_STATE"] = statePath;
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start Sprout Studio Academy test.");
    try
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            await Task.Delay(250);
            process.Refresh();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Sprout Studio exited during Academy startup with code {process.ExitCode}.");
            }
        }
        while ((process.MainWindowHandle == IntPtr.Zero || !process.Responding)
            && DateTime.UtcNow < deadline);

        Assert(process.MainWindowHandle != IntPtr.Zero, "Academy did not create a main window.");
        Assert(process.Responding, "Academy window is not responding.");
        if (!string.IsNullOrEmpty(process.MainWindowTitle))
        {
            Assert(
                string.Equals(process.MainWindowTitle, expectedTitle, StringComparison.Ordinal),
                $"Sprout Studio did not open `{expectedTitle}`; title was `{process.MainWindowTitle}`.");
        }
        Assert(process.CloseMainWindow(), "Academy did not accept a normal close request.");
        if (!await WaitForExitAsync(process, TimeSpan.FromSeconds(3)))
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
    finally
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
}

static async Task AvaloniaApplicationStarts()
{
    string executable = StudioExecutablePath();

    var startInfo = new ProcessStartInfo(executable)
    {
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(executable),
        Arguments = "--open-toolchains",
    };
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start Sprout Studio.");
    try
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            await Task.Delay(250);
            process.Refresh();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Sprout Studio exited during startup with code {process.ExitCode}.");
            }
        }
        while ((process.MainWindowHandle == IntPtr.Zero || !process.Responding)
            && DateTime.UtcNow < deadline);

        Assert(process.MainWindowHandle != IntPtr.Zero, "Sprout Studio did not create a main window.");
        Assert(process.Responding, "Sprout Studio created a window but is not responding.");
        if (!string.IsNullOrEmpty(process.MainWindowTitle))
        {
            Assert(
                string.Equals(process.MainWindowTitle, "Sprout Studio", StringComparison.Ordinal),
                $"Toolchains did not remain in the integrated Studio shell; title was `{process.MainWindowTitle}`.");
        }
        Assert(process.CloseMainWindow(), "Integrated Toolchains mode did not accept a normal close request.");
        if (!await WaitForExitAsync(process, TimeSpan.FromSeconds(3)))
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
    finally
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
}

static async Task AvaloniaDocumentationStarts()
{
    string executable = StudioExecutablePath();
    var startInfo = new ProcessStartInfo(executable)
    {
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(executable),
        Arguments = "--open-docs",
    };
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start integrated Documentation mode.");
    try
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            await Task.Delay(250);
            process.Refresh();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Sprout Studio exited during Documentation startup with code {process.ExitCode}.");
            }
        }
        while ((process.MainWindowHandle == IntPtr.Zero || !process.Responding)
            && DateTime.UtcNow < deadline);

        Assert(process.MainWindowHandle != IntPtr.Zero, "Documentation did not create the integrated Studio window.");
        Assert(process.Responding, "Integrated Documentation mode is not responding.");
        if (!string.IsNullOrEmpty(process.MainWindowTitle))
        {
            Assert(
                string.Equals(process.MainWindowTitle, "Sprout Studio", StringComparison.Ordinal),
                $"Documentation did not remain in the integrated Studio shell; title was `{process.MainWindowTitle}`.");
        }
        Assert(process.CloseMainWindow(), "Integrated Documentation mode did not accept a normal close request.");
        if (!await WaitForExitAsync(process, TimeSpan.FromSeconds(3)))
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
    finally
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
}

static string StudioExecutablePath()
{
    string repositoryRoot = FindRepositoryRoot();
#if DEBUG
    const string Configuration = "Debug";
#else
    const string Configuration = "Release";
#endif
    string executable = Path.Combine(
        repositoryRoot,
        "studio",
        "src",
        "SproutStudio.Desktop",
        "bin",
        "x64",
        Configuration,
        "net10.0",
        "win-x64",
        "SproutStudio.exe");
    Assert(File.Exists(executable), $"Sprout Studio executable was not built at {executable}");
    return executable;
}

static Task AcademySplitWorkspaceCompanionIntegration()
{
    string statePath = Path.Combine(TemporaryDirectory("split-ws"), "academy.json");
    AcademyService academy = AcademyTestService(statePath, ToolchainStatus.Ready);
    TrackDefinition track = academy.Catalog.GetTrack("java-oop-zero-to-hero");
    LessonDefinition exerciseLesson = track.Lessons.First(l => l.Exercise is not null);

    EditorWorkspace workspace = new();
    string filename = "BankAccount.java";
    string starterCode = exerciseLesson.Exercise?.StarterCode ?? "public class BankAccount {}";
    EditorDocument codeDoc = workspace.CreateDocument(filename, starterCode);

    LanguageDescriptor javaLang = LanguageRegistry.TryGet("java", out var l) ? l! : LanguageRegistry.Get(DocumentLanguage.Sprout);
    EditorDocument academyDoc = workspace.OpenOrCreateAcademyDocument(
        track.Id,
        exerciseLesson.Id,
        exerciseLesson.Title,
        javaLang,
        isExercise: true,
        linkedCompanionPath: codeDoc.FilePath ?? filename);

    Assert(academyDoc.IsAcademy, "expected academy document");
    Assert(academyDoc.Kind == DocumentKind.AcademyExercise, "expected AcademyExercise kind");
    Assert(string.Equals(academyDoc.LinkedCompanionPath, codeDoc.FilePath ?? filename, StringComparison.OrdinalIgnoreCase), "expected linked companion path");
    return Task.CompletedTask;
}

static string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Cargo.toml"))
            && Directory.Exists(Path.Combine(directory.FullName, "studio")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Sprout repository root.");
}

static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
{
    using var cancellation = new CancellationTokenSource(timeout);
    try
    {
        await process.WaitForExitAsync(cancellation.Token);
        return true;
    }
    catch (OperationCanceledException)
    {
        return false;
    }
}

static string TemporaryDirectory(string name)
{
    return Path.Combine(
        Path.GetTempPath(),
        $"sprout-studio-{Environment.ProcessId}-{name}-{Guid.NewGuid():N}");
}

static ProcessRequest SelfProcessRequest(
    IReadOnlyList<string> arguments,
    TimeSpan? timeout = null)
{
    string executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test process path is unavailable.");
    return new ProcessRequest(executable, arguments, Timeout: timeout);
}

static ToolchainManager ManagerWith(
    IToolchainAdapter adapter,
    IProcessRunner runner,
    string settingsPath) =>
    new(
        new ToolchainRegistry([adapter]),
        new ToolchainSettingsStore(settingsPath),
        runner);

static AcademyService AcademyTestService(
    string statePath,
    ToolchainStatus toolchainStatus) =>
    AcademyTestServiceWithAdapter(statePath,
        new FixedToolchainAdapter("rust", toolchainStatus),
        new FixedToolchainAdapter("java", toolchainStatus),
        new FixedToolchainAdapter("go", toolchainStatus),
        new FixedToolchainAdapter("python", toolchainStatus));

static AcademyService AcademyTestServiceWithAdapter(
    string statePath,
    params IToolchainAdapter[] adapters)
{
    var runner = new FakeProcessRunner(_ => ProcessSuccess("unused"));
    var manager = new ToolchainManager(
        new ToolchainRegistry(adapters),
        new ToolchainSettingsStore($"{statePath}.toolchains"),
        runner);
    return new AcademyService(
        AcademyCatalog.LoadDefault(),
        new AcademyStateStore(statePath),
        manager);
}

static FakeProcessRunner RustFakeRunner(string rustVersion) =>
    new(request =>
    {
        string executable = Path.GetFileNameWithoutExtension(request.FileName);
        return executable.ToLowerInvariant() switch
        {
            "rustc" => ProcessSuccess($"rustc {rustVersion} (test 2026-01-01)"),
            "cargo" when request.Arguments.SequenceEqual(["--version"]) =>
                ProcessSuccess($"cargo {rustVersion} (test 2026-01-01)"),
            "rustfmt" => ProcessSuccess($"rustfmt {rustVersion}-stable (test 2026-01-01)"),
            "cargo" => ProcessSuccess("action completed"),
            _ => ProcessFailure($"unexpected executable {executable}"),
        };
    });

static void CreateExecutables(string directory, params string[] names)
{
    foreach (string name in names)
    {
        File.WriteAllText(
            Path.Combine(directory, OperatingSystem.IsWindows() ? $"{name}.exe" : name),
            string.Empty);
    }
}

static ProcessResult ProcessSuccess(string standardOutput) =>
    new(true, 0, standardOutput, string.Empty, TimeSpan.FromMilliseconds(1), false, null);

static ProcessResult ProcessFailure(string standardError) =>
    new(true, 1, string.Empty, standardError, TimeSpan.FromMilliseconds(1), false, null);

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Func<ProcessRequest, ProcessResult> _handler;

    public FakeProcessRunner(Func<ProcessRequest, ProcessResult> handler)
    {
        _handler = handler;
    }

    public List<ProcessRequest> Requests { get; } = [];

    public Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(_handler(request));
    }
}

sealed class FixedToolchainAdapter : IToolchainAdapter
{
    private readonly ToolchainStatus _status;
    private readonly string _id;

    public FixedToolchainAdapter(ToolchainStatus status) : this("rust", status)
    {
    }

    public FixedToolchainAdapter(string id, ToolchainStatus status)
    {
        _id = id;
        _status = status;
    }

    public string Id => _id;

    public string DisplayName => _id;

    public ToolchainCapability Capabilities => ToolchainCapability.Run
        | ToolchainCapability.Build
        | ToolchainCapability.Format
        | ToolchainCapability.Test;

    public int DetectionCount { get; private set; }

    public Task<ToolchainDetectionResult> DetectAsync(
        string? customPath,
        ToolchainLocationSource? customSource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DetectionCount++;
        bool usable = _status is ToolchainStatus.Ready or ToolchainStatus.CustomLocation;
        return Task.FromResult(new ToolchainDetectionResult(
            Id,
            DisplayName,
            _status,
            customSource,
            usable ? _id : null,
            usable ? "1.0.0" : null,
            usable ? Capabilities : ToolchainCapability.None,
            [],
            usable ? "Environment ready." : $"{_id} is not installed."));
    }

    public ProcessRequest CreateActionRequest(
        ToolchainAction action,
        ToolchainDetectionResult detection,
        string workingDirectory,
        IReadOnlyList<string> additionalArguments) =>
        throw new NotSupportedException("Academy prerequisite tests do not execute tools.");
}
