using System.Diagnostics;
using SproutStudio.Core;
using SproutStudio.Interop;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Native run returns actual output", NativeRunReturnsOutput),
    ("Native diagnostics preserve locations", NativeDiagnosticsPreserveLocations),
    ("Native diagnostics preserve related data and fixes", NativeDiagnosticsPreserveRelatedDataAndFixes),
    ("Native byte spans navigate Unicode source", NativeSpansNavigateUnicodeSource),
    ("Session projects response into panels", SessionProjectsResponse),
    ("Response parser rejects incompatible API", ResponseParserRejectsApiMismatch),
    ("Native project API creates and reloads templates", NativeProjectCreatesAndLoads),
    ("Native project API returns validation diagnostics", NativeProjectValidation),
    ("Recent projects persist without duplicates", RecentProjectsPersistWithoutDuplicates),
    ("Studio session saves edited source atomically", StudioSessionSavesSource),
    ("Studio session saves a new source file", StudioSessionSavesNewSource),
    ("Phase 5 project workflow executes edited source", PhaseFiveProjectWorkflow),
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

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
