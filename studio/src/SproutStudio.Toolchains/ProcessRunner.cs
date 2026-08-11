using System.ComponentModel;
using System.Diagnostics;

namespace SproutStudio.Toolchains;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        if (request.Timeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Process timeout must be positive.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (request.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = Path.GetFullPath(request.WorkingDirectory);
        }
        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!process.Start())
            {
                return NotStarted(stopwatch.Elapsed, "The operating system did not start the process.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return NotStarted(stopwatch.Elapsed, exception.Message);
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var timeoutCancellation = request.Timeout is null
            ? null
            : new CancellationTokenSource(request.Timeout.Value);
        using var linkedCancellation = timeoutCancellation is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            StopProcess(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            timedOut = true;
        }

        string output = await standardOutput.ConfigureAwait(false);
        string error = await standardError.ConfigureAwait(false);
        stopwatch.Stop();
        return new ProcessResult(
            Started: true,
            ExitCode: timedOut ? null : process.ExitCode,
            StandardOutput: output,
            StandardError: error,
            Duration: stopwatch.Elapsed,
            TimedOut: timedOut,
            StartError: null);
    }

    private static ProcessResult NotStarted(TimeSpan duration, string message) =>
        new(false, null, string.Empty, string.Empty, duration, false, message);

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The process exited between the state check and the kill request.
        }
    }
}
