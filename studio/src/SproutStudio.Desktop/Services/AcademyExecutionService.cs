using System.Text.RegularExpressions;
using SproutStudio.Academy;
using SproutStudio.Toolchains;

namespace SproutStudio.Services;

/// <summary>
/// Materializes Academy source in an isolated workspace and runs the configured
/// language toolchain. This service never records attempts or awards XP.
/// </summary>
public sealed partial class AcademyExecutionService
{
    private readonly ToolchainManager _toolchains;

    public AcademyExecutionService(ToolchainManager toolchains)
    {
        _toolchains = toolchains ?? throw new ArgumentNullException(nameof(toolchains));
    }

    public async Task<ExerciseRuntimeResult> ExecuteAsync(
        TrackDefinition track,
        LessonDefinition lesson,
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(source);

        string toolchainId = track.RequiredToolchainId
            ?? throw new InvalidOperationException($"{track.Title} does not declare an external toolchain.");
        string runRoot = Path.Combine(
            Path.GetTempPath(),
            "SproutStudio",
            "academy-runs",
            SafeSegment(lesson.Id),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);

        try
        {
            return toolchainId.ToLowerInvariant() switch
            {
                "java" => await ExecuteJavaAsync(runRoot, lesson, source, cancellationToken).ConfigureAwait(false),
                "python" => await ExecutePythonAsync(runRoot, source, cancellationToken).ConfigureAwait(false),
                "go" => await ExecuteGoAsync(runRoot, source, cancellationToken).ConfigureAwait(false),
                "rust" => await ExecuteRustAsync(runRoot, source, cancellationToken).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Academy execution is not available for `{toolchainId}`."),
            };
        }
        finally
        {
            TryDeleteRunDirectory(runRoot);
        }
    }

    private async Task<ExerciseRuntimeResult> ExecuteJavaAsync(
        string runRoot,
        LessonDefinition lesson,
        string source,
        CancellationToken cancellationToken)
    {
        MatchCollection declarations = JavaTopLevelTypeRegex().Matches(source);
        string? declaredEntryPoint = lesson.Exercise?.EntryPoint;
        string? entryPoint = !string.IsNullOrWhiteSpace(declaredEntryPoint)
            ? declaredEntryPoint.Trim()
            : declarations.Count > 0
                ? declarations[0].Groups["name"].Value
                : null;

        string materializedSource = source;
        string sourceFileName;
        if (declarations.Count > 1)
        {
            // Academy snippets often demonstrate several collaborating top-level
            // types in one editor. Java permits that when only one is public, so
            // the isolated compilation copy makes secondary declarations
            // package-private without changing the learner's buffer.
            int declarationIndex = 0;
            materializedSource = JavaTopLevelTypeRegex().Replace(source, match =>
            {
                declarationIndex++;
                return declarationIndex == 1
                    ? match.Value
                    : match.Value["public ".Length..];
            });
            sourceFileName = $"{entryPoint ?? "AcademySubmission"}.java";
        }
        else
        {
            sourceFileName = $"{entryPoint ?? "AcademySubmission"}.java";
        }

        string sourcePath = Path.Combine(runRoot, sourceFileName);
        await File.WriteAllTextAsync(sourcePath, materializedSource, cancellationToken).ConfigureAwait(false);

        string? harnessPath = null;
        if (!string.IsNullOrWhiteSpace(lesson.Exercise?.TestHarness))
        {
            harnessPath = Path.Combine(runRoot, "AcademyHarness.java");
            await File.WriteAllTextAsync(
                harnessPath,
                BuildJavaHarness(lesson.Exercise.TestHarness),
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<string> buildArguments = harnessPath is null
            ? [sourcePath]
            : [sourcePath, harnessPath];
        ProcessResult build = await _toolchains.ExecuteAsync(
            "java",
            ToolchainAction.Build,
            runRoot,
            buildArguments,
            cancellationToken).ConfigureAwait(false);
        if (!build.Success)
        {
            return Failure(build, "Java compilation failed.");
        }

        if (harnessPath is not null)
        {
            ProcessResult harnessRun = await _toolchains.ExecuteAsync(
                "java",
                ToolchainAction.Run,
                runRoot,
                ["-cp", runRoot, "AcademyHarness"],
                cancellationToken).ConfigureAwait(false);
            TimeSpan harnessDuration = build.Duration + harnessRun.Duration;
            if (!harnessRun.Success)
            {
                return Failure(
                    harnessRun,
                    $"Java Academy tests failed to execute after a successful compile ({harnessDuration.TotalMilliseconds:0} ms).",
                    harnessDuration);
            }

            RuntimeTestResult[] testResults = ParseHarnessResults(harnessRun.StandardOutput);
            if (testResults.Length == 0)
            {
                return new ExerciseRuntimeResult(
                    false,
                    VisibleHarnessOutput(harnessRun.StandardOutput),
                    ["The Academy test harness completed without reporting any test cases."],
                    harnessDuration,
                    "Java Academy tests did not return a valid result payload.",
                    RanProgram: true,
                    TestResults: []);
            }

            int passed = testResults.Count(result => result.Passed);
            return new ExerciseRuntimeResult(
                true,
                VisibleHarnessOutput(harnessRun.StandardOutput),
                OutputLines(harnessRun.StandardError),
                harnessDuration,
                $"Compiled with javac and ran {testResults.Length} Academy test(s) in {harnessDuration.TotalMilliseconds:0} ms ({passed} passed).",
                RanProgram: true,
                TestResults: testResults);
        }

        bool hasMain = JavaMainMethodRegex().IsMatch(materializedSource);
        if (!hasMain || string.IsNullOrWhiteSpace(entryPoint))
        {
            return new ExerciseRuntimeResult(
                true,
                string.Empty,
                OutputLines(build.StandardError),
                build.Duration,
                $"Compiled successfully with javac in {build.Duration.TotalMilliseconds:0} ms; this activity has no runnable entry point.",
                RanProgram: false);
        }

        ProcessResult run = await _toolchains.ExecuteAsync(
            "java",
            ToolchainAction.Run,
            runRoot,
            ["-cp", runRoot, entryPoint],
            cancellationToken).ConfigureAwait(false);
        TimeSpan duration = build.Duration + run.Duration;
        if (!run.Success)
        {
            return Failure(run, $"Java execution failed after a successful compile ({duration.TotalMilliseconds:0} ms).", duration);
        }

        return new ExerciseRuntimeResult(
            true,
            run.StandardOutput,
            OutputLines(run.StandardError),
            duration,
            $"Compiled and ran with Java in {duration.TotalMilliseconds:0} ms.",
            RanProgram: true);
    }

    private async Task<ExerciseRuntimeResult> ExecutePythonAsync(
        string runRoot,
        string source,
        CancellationToken cancellationToken)
    {
        string sourcePath = Path.Combine(runRoot, "main.py");
        await File.WriteAllTextAsync(sourcePath, source, cancellationToken).ConfigureAwait(false);
        ProcessResult build = await _toolchains.ExecuteAsync(
            "python", ToolchainAction.Build, runRoot, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!build.Success)
        {
            return Failure(build, "Python syntax validation failed.");
        }
        ProcessResult run = await _toolchains.ExecuteAsync(
            "python", ToolchainAction.Run, runRoot, [sourcePath], cancellationToken).ConfigureAwait(false);
        return CompleteRun("Python", build, run);
    }

    private async Task<ExerciseRuntimeResult> ExecuteGoAsync(
        string runRoot,
        string source,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            Path.Combine(runRoot, "go.mod"),
            "module sprout.academy/exercise\n\ngo 1.24\n",
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(runRoot, "main.go"), source, cancellationToken).ConfigureAwait(false);
        ProcessResult build = await _toolchains.ExecuteAsync(
            "go", ToolchainAction.Build, runRoot, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!build.Success)
        {
            return Failure(build, "Go compilation failed.");
        }
        ProcessResult run = await _toolchains.ExecuteAsync(
            "go", ToolchainAction.Run, runRoot, cancellationToken: cancellationToken).ConfigureAwait(false);
        return CompleteRun("Go", build, run);
    }

    private async Task<ExerciseRuntimeResult> ExecuteRustAsync(
        string runRoot,
        string source,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(runRoot, "src"));
        await File.WriteAllTextAsync(
            Path.Combine(runRoot, "Cargo.toml"),
            "[package]\nname = \"sprout_academy_exercise\"\nversion = \"0.1.0\"\nedition = \"2024\"\n",
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(runRoot, "src", "main.rs"), source, cancellationToken).ConfigureAwait(false);
        ProcessResult build = await _toolchains.ExecuteAsync(
            "rust", ToolchainAction.Build, runRoot, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!build.Success)
        {
            return Failure(build, "Rust compilation failed.");
        }
        ProcessResult run = await _toolchains.ExecuteAsync(
            "rust", ToolchainAction.Run, runRoot, cancellationToken: cancellationToken).ConfigureAwait(false);
        return CompleteRun("Rust", build, run);
    }

    private static ExerciseRuntimeResult CompleteRun(string language, ProcessResult build, ProcessResult run)
    {
        TimeSpan duration = build.Duration + run.Duration;
        return run.Success
            ? new ExerciseRuntimeResult(
                true,
                run.StandardOutput,
                OutputLines(run.StandardError),
                duration,
                $"Compiled and ran with {language} in {duration.TotalMilliseconds:0} ms.",
                RanProgram: true)
            : Failure(run, $"{language} execution failed after a successful compile.", duration);
    }

    private static ExerciseRuntimeResult Failure(ProcessResult result, string summary, TimeSpan? duration = null)
    {
        string combinedDiagnostics = string.Join(
            Environment.NewLine,
            new[] { result.StandardError, result.StandardOutput, result.StartError }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        string[] diagnostics = OutputLines(combinedDiagnostics);
        Match location = DiagnosticLocationRegex().Match(combinedDiagnostics);
        int? line = location.Success && int.TryParse(location.Groups["line"].Value, out int parsedLine)
            ? parsedLine
            : null;
        int? column = location.Success && int.TryParse(location.Groups["column"].Value, out int parsedColumn)
            ? parsedColumn
            : null;
        return new ExerciseRuntimeResult(
            false,
            result.StandardOutput,
            diagnostics,
            duration ?? result.Duration,
            result.TimedOut ? $"{summary} The process timed out." : summary,
            RanProgram: false,
            ErrorLine: line,
            ErrorColumn: column);
    }

    private static string[] OutputLines(string? output) =>
        string.IsNullOrWhiteSpace(output)
            ? []
            : output.ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static RuntimeTestResult[] ParseHarnessResults(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var results = new List<RuntimeTestResult>();
        foreach (string line in output.ReplaceLineEndings("\n").Split('\n'))
        {
            if (!line.StartsWith("SPROUT_TEST|", StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = line.Split('|', 8, StringSplitOptions.None);
            if (parts.Length != 8
                || string.IsNullOrWhiteSpace(parts[1])
                || string.IsNullOrWhiteSpace(parts[3]))
            {
                continue;
            }
            results.Add(new RuntimeTestResult(
                parts[1],
                parts[3],
                string.Equals(parts[2], "PASS", StringComparison.OrdinalIgnoreCase),
                parts[4],
                parts[5],
                parts[6],
                string.IsNullOrWhiteSpace(parts[7]) ? null : parts[7]));
        }
        return results.ToArray();
    }

    private static string BuildJavaHarness(string body)
    {
        if (body.Contains("class AcademyHarness", StringComparison.Ordinal))
        {
            return body;
        }

        return $$"""
        import java.io.ByteArrayOutputStream;
        import java.io.PrintStream;
        import java.lang.reflect.Field;
        import java.util.Objects;

        public final class AcademyHarness {
            @FunctionalInterface
            interface ThrowingAction { void run() throws Exception; }

            private static String clean(Object value) {
                return String.valueOf(value)
                    .replace('|', '/')
                    .replace('\r', ' ')
                    .replace('\n', ' ');
            }

            static void check(
                    String id,
                    String title,
                    boolean passed,
                    Object expected,
                    Object observed,
                    String message,
                    String sourceSearchTerm) {
                System.out.println("SPROUT_TEST|" + clean(id)
                    + "|" + (passed ? "PASS" : "FAIL")
                    + "|" + clean(title)
                    + "|" + clean(expected)
                    + "|" + clean(observed)
                    + "|" + clean(message)
                    + "|" + clean(sourceSearchTerm));
            }

            static Throwable thrown(ThrowingAction action) {
                try {
                    action.run();
                    return null;
                } catch (Throwable error) {
                    return error;
                }
            }

            static String capture(ThrowingAction action) throws Exception {
                PrintStream original = System.out;
                ByteArrayOutputStream buffer = new ByteArrayOutputStream();
                try (PrintStream captured = new PrintStream(buffer)) {
                    System.setOut(captured);
                    action.run();
                } finally {
                    System.setOut(original);
                }
                return buffer.toString().replace("\r\n", "\n").trim();
            }

            static Object field(Object instance, String name) throws Exception {
                Class<?> type = instance.getClass();
                while (type != null) {
                    try {
                        Field field = type.getDeclaredField(name);
                        field.setAccessible(true);
                        return field.get(instance);
                    } catch (NoSuchFieldException ignored) {
                        type = type.getSuperclass();
                    }
                }
                throw new NoSuchFieldException(name);
            }

            static boolean close(double actual, double expected) {
                return Math.abs(actual - expected) < 0.000001d;
            }

            public static void main(String[] args) throws Exception {
        {{body}}
            }
        }
        """;
    }

    private static string VisibleHarnessOutput(string? output) => string.Join(
        Environment.NewLine,
        OutputLines(output).Where(line => !line.StartsWith("SPROUT_TEST|", StringComparison.Ordinal)));

    private static string SafeSegment(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

    private static void TryDeleteRunDirectory(string runRoot)
    {
        try
        {
            string expectedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SproutStudio", "academy-runs"));
            string target = Path.GetFullPath(runRoot);
            if (target.StartsWith(expectedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
        catch (IOException)
        {
            // A toolchain process can briefly retain a file handle. Temp cleanup
            // is best-effort and the OS temp policy handles any remainder.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup only; never turn a successful lesson into a failure.
        }
    }

    [GeneratedRegex("^public\\s+(?:(?:abstract|final|sealed|non-sealed|strictfp)\\s+)*(?:class|interface|record|enum)\\s+(?<name>[A-Za-z_$][A-Za-z0-9_$]*)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex JavaTopLevelTypeRegex();

    [GeneratedRegex("\\bpublic\\s+static\\s+void\\s+main\\s*\\(", RegexOptions.CultureInvariant)]
    private static partial Regex JavaMainMethodRegex();

    [GeneratedRegex(@":(?<line>\d+)(?::(?<column>\d+))?:", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticLocationRegex();
}
