using System.Diagnostics;
using SproutStudio.Academy;
using SproutStudio.Core;
using SproutStudio.Editor;
using SproutStudio.Interop;
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
    ("Native debugger records variables and canvas commands", NativeDebuggerAndCanvasWork),
    ("Native package hub updates manifests and builds bundles", NativePackageHubAndBuildWork),
    ("Editor workspace supports tabs and additional languages", EditorWorkspaceSupportsAdditionalLanguages),
    ("Editor coordinates normalize line endings", EditorCoordinatesNormalizeLineEndings),
    ("Editor breadcrumbs stay project relative", EditorBreadcrumbsStayProjectRelative),
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
        Assert(sanitized.SelectedBottomTab == 3, "bottom tab bounds were not enforced");
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
    Assert(catalog.Tracks.Length == 6, "expected all six Phase 8 tracks");
    Assert(catalog.Tracks.All(track => track.Lessons.Length >= 3), "a track lacks a usable lesson sequence");
    Assert(catalog.Tracks.Single(track => track.Id == "rust-systems").RequiredToolchainId == "rust", "Rust prerequisite is missing");
    Assert(catalog.Tracks.Single(track => track.Id == "go-backend").RequiredToolchainId == "go", "Go prerequisite is missing");
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

        LessonCompletionResult first = await service.CompleteLessonAsync(
            "sprout-fundamentals",
            "sprout-first-program");
        LessonCompletionResult repeated = await service.CompleteLessonAsync(
            "sprout-fundamentals",
            "sprout-first-program");
        Assert(first.AwardedXp == 30 && !first.AlreadyCompleted, "first completion did not award its defined XP");
        Assert(repeated.AwardedXp == 0 && repeated.AlreadyCompleted, "repeated completion farmed XP");
        await service.CompleteLessonAsync("sprout-fundamentals", "sprout-decisions-loops");
        await service.CompleteLessonAsync("sprout-fundamentals", "sprout-actions");

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
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
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
        while ((!string.Equals(process.MainWindowTitle, expectedTitle, StringComparison.Ordinal)
                || process.MainWindowHandle == IntPtr.Zero)
            && DateTime.UtcNow < deadline);

        Assert(process.MainWindowHandle != IntPtr.Zero, "Academy did not create a main window.");
        Assert(process.Responding, "Academy window is not responding.");
        Assert(
            string.Equals(process.MainWindowTitle, expectedTitle, StringComparison.Ordinal),
            $"Sprout Studio did not open `{expectedTitle}`; title was `{process.MainWindowTitle}`.");
        Assert(process.CloseMainWindow(), "Academy did not accept a normal close request.");
        Assert(await WaitForExitAsync(process, TimeSpan.FromSeconds(5)), "Academy did not exit cleanly.");
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
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
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
        while ((!string.Equals(process.MainWindowTitle, "Sprout Studio", StringComparison.Ordinal)
                || process.MainWindowHandle == IntPtr.Zero)
            && DateTime.UtcNow < deadline);

        Assert(process.MainWindowHandle != IntPtr.Zero, "Sprout Studio did not create a main window.");
        Assert(process.Responding, "Sprout Studio created a window but is not responding.");
        Assert(
            string.Equals(process.MainWindowTitle, "Sprout Studio", StringComparison.Ordinal),
            $"Toolchains did not remain in the integrated Studio shell; title was `{process.MainWindowTitle}`.");
        Assert(process.CloseMainWindow(), "Integrated Toolchains mode did not accept a normal close request.");
        Assert(await WaitForExitAsync(process, TimeSpan.FromSeconds(5)), "Sprout Studio did not exit cleanly.");
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
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
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
        while ((!string.Equals(process.MainWindowTitle, "Sprout Studio", StringComparison.Ordinal)
                || process.MainWindowHandle == IntPtr.Zero)
            && DateTime.UtcNow < deadline);

        Assert(process.MainWindowHandle != IntPtr.Zero, "Documentation did not create the integrated Studio window.");
        Assert(process.Responding, "Integrated Documentation mode is not responding.");
        Assert(
            string.Equals(process.MainWindowTitle, "Sprout Studio", StringComparison.Ordinal),
            $"Documentation did not remain in the integrated Studio shell; title was `{process.MainWindowTitle}`.");
        Assert(process.CloseMainWindow(), "Integrated Documentation mode did not accept a normal close request.");
        Assert(await WaitForExitAsync(process, TimeSpan.FromSeconds(5)), "Documentation did not exit cleanly.");
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
    AcademyTestServiceWithAdapter(statePath, new FixedToolchainAdapter(toolchainStatus));

static AcademyService AcademyTestServiceWithAdapter(
    string statePath,
    FixedToolchainAdapter adapter)
{
    var runner = new FakeProcessRunner(_ => ProcessSuccess("unused"));
    var manager = new ToolchainManager(
        new ToolchainRegistry([adapter]),
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

    public FixedToolchainAdapter(ToolchainStatus status)
    {
        _status = status;
    }

    public string Id => "rust";

    public string DisplayName => "Rust";

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
            usable ? "rustc" : null,
            usable ? "1.95.0" : null,
            usable ? Capabilities : ToolchainCapability.None,
            [],
            usable ? "Environment ready." : "Rust is not installed."));
    }

    public ProcessRequest CreateActionRequest(
        ToolchainAction action,
        ToolchainDetectionResult detection,
        string workingDirectory,
        IReadOnlyList<string> additionalArguments) =>
        throw new NotSupportedException("Academy prerequisite tests do not execute tools.");
}
