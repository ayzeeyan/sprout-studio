using System.Diagnostics;
using SproutStudio.Core;
using SproutStudio.Interop;
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
    ("Native run returns actual output", NativeRunReturnsOutput),
    ("Native run executes embedded Python", NativeRunExecutesEmbeddedPython),
    ("Native project run isolates Python packages", NativeProjectRunIsolatesPythonPackages),
    ("Native diagnostics preserve locations", NativeDiagnosticsPreserveLocations),
    ("Native diagnostics preserve related data and fixes", NativeDiagnosticsPreserveRelatedDataAndFixes),
    ("Native byte spans navigate Unicode source", NativeSpansNavigateUnicodeSource),
    ("Session projects response into panels", SessionProjectsResponse),
    ("Response parser rejects incompatible API", ResponseParserRejectsApiMismatch),
    ("Native project API creates and reloads templates", NativeProjectCreatesAndLoads),
    ("Native project API returns validation diagnostics", NativeProjectValidation),
    ("Recent projects persist without duplicates", RecentProjectsPersistWithoutDuplicates),
    ("Recent projects retain only the ten newest entries", RecentProjectsRetainTenNewest),
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
    ("Toolchain settings persist and clear custom paths", ToolchainSettingsPersistCustomPaths),
    ("Toolchain manager builds safe action requests", ToolchainManagerBuildsSafeActionRequests),
    ("WinUI application starts and responds", WinUiApplicationStarts),
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
        await store.AddAsync(project);
        IReadOnlyList<RecentProject> recent = await new RecentProjectStore(settingsPath).LoadAsync();
        Assert(recent.Count == 1, "duplicate project was retained");
        Assert(recent[0].Name == "RecentProject", "recent project was not persisted");
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

static async Task WinUiApplicationStarts()
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
        "SproutStudio",
        "bin",
        "x64",
        Configuration,
        "net8.0-windows10.0.19041.0",
        "win-x64",
        "SproutStudio.exe");
    Assert(File.Exists(executable), $"Sprout Studio executable was not built at {executable}");

    using var process = Process.Start(new ProcessStartInfo(executable)
    {
        UseShellExecute = true,
        WorkingDirectory = Path.GetDirectoryName(executable),
    }) ?? throw new InvalidOperationException("Could not start Sprout Studio.");
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
        while (process.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < deadline);

        Assert(process.MainWindowHandle != IntPtr.Zero, "Sprout Studio did not create a main window.");
        Assert(process.Responding, "Sprout Studio created a window but is not responding.");
        Assert(
            process.MainWindowTitle.Contains("Sprout Studio", StringComparison.Ordinal),
            "Sprout Studio created an unexpected main window.");
        Assert(process.CloseMainWindow(), "Sprout Studio did not accept a normal close request.");
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
