using System.Diagnostics;
using System.Text;

namespace CytoLex.External;

/// <summary>The outcome of a local process run.</summary>
/// <param name="ExitCode">The exit code (−1 when timed out).</param>
/// <param name="StdOut">Standard output.</param>
/// <param name="StdErr">Standard error.</param>
/// <param name="TimedOut">Whether the time limit was reached (the process was killed).</param>
internal sealed record ProcessOutcome(int ExitCode, string StdOut, string StdErr, bool TimedOut);

/// <summary>Starts local processes (a seam for tests).</summary>
internal interface IProcessRunner
{
    Task<ProcessOutcome> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>Runs a process without a shell; cancellation and the time limit kill the whole process tree.</summary>
internal sealed class ProcessRunner : IProcessRunner
{
    public static ProcessRunner Instance { get; } = new();

    public async Task<ProcessOutcome> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in arguments)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (stdout) { stdout.AppendLine(e.Data); } } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (stderr) { stderr.AppendLine(e.Data); } } };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ProcessOutcome(-1, stdout.ToString(), stderr.ToString(), TimedOut: true);
        }

        process.WaitForExit(); // flush the async output readers
        return new ProcessOutcome(process.ExitCode, stdout.ToString(), stderr.ToString(), TimedOut: false);
    }
}
