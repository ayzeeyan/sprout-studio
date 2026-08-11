using SproutStudio.Core;
using SproutStudio.Interop;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Native run returns actual output", NativeRunReturnsOutput),
    ("Native diagnostics preserve locations", NativeDiagnosticsPreserveLocations),
    ("Session projects response into panels", SessionProjectsResponse),
    ("Response parser rejects incompatible API", ResponseParserRejectsApiMismatch),
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

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
