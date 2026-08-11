using SproutStudio.Core;
using SproutStudio.Interop;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Native run returns actual output", NativeRunReturnsOutput),
    ("Native diagnostics preserve locations", NativeDiagnosticsPreserveLocations),
    ("Session projects response into panels", SessionProjectsResponse),
    ("Response parser rejects incompatible API", ResponseParserRejectsApiMismatch),
    ("Native project API creates and reloads templates", NativeProjectCreatesAndLoads),
    ("Native project API returns validation diagnostics", NativeProjectValidation),
    ("Recent projects persist without duplicates", RecentProjectsPersistWithoutDuplicates),
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
