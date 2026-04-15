using System.Diagnostics;
using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class LocalWorkspaceProcessHost : IWorkspaceProcessHost
{
    private static readonly ExecutionBoundaryDescriptor Boundary = new(
        Mode: "PolicyOnlyLocal",
        FilesystemScope: "Workspace-relative request shaping only. Child processes still inherit the host OS filesystem rights.",
        NetworkScope: "Not host-enforced. Child processes inherit the host network access that the local machine already has.",
        CredentialScope: "Scrubbed environment allowlist only. No container or remote credential boundary is enforced by AgentFramework.",
        HostLabel: "Local best-effort process host",
        IsEnforcedByHost: false,
        Notes: "This host centralizes process policy, timeout, output caps, and tree-kill behavior, but it is not a true OS or container sandbox.");

    public ExecutionBoundaryDescriptor DescribeBoundary() => Boundary;

    public async Task<WorkspaceProcessExecutionResult> ExecuteAsync(WorkspaceProcessExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAtUtc = DateTimeOffset.UtcNow;
        var completedAtUtc = startedAtUtc;
        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = request.ExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = request.WorkingDirectory
            };

            startInfo.Environment.Clear();
            foreach (var environmentVariable in request.EnvironmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value ?? string.Empty;
            }

            foreach (var argument in request.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            process = Process.Start(startInfo);
            if (process is null)
            {
                completedAtUtc = DateTimeOffset.UtcNow;
                return new WorkspaceProcessExecutionResult(
                    Started: false,
                    ExitCode: -1,
                    Stdout: string.Empty,
                    Stderr: string.Empty,
                    StdoutTruncated: false,
                    StderrTruncated: false,
                    StartedAtUtc: startedAtUtc,
                    CompletedAtUtc: completedAtUtc,
                    TimedOut: false,
                    Boundary: Boundary,
                    FailureMessage: $"Failed to start '{request.ExecutablePath}'.");
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, 3600)));
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
            var stdoutTask = ReadStreamAsync(process.StandardOutput, request.StdoutLimitCharacters, CancellationToken.None);
            var stderrTask = ReadStreamAsync(process.StandardError, request.StderrLimitCharacters, CancellationToken.None);
            var timedOut = false;
            var canceled = false;
            var failureMessage = string.Empty;

            try
            {
                await process.WaitForExitAsync(linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                timedOut = true;
                failureMessage = $"Process timed out after {Math.Clamp(request.TimeoutSeconds, 1, 3600)} second(s).";
                TryKillProcessTree(process);
                await WaitForExitAfterCancellationAsync(process).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                failureMessage = "Process execution was canceled.";
                TryKillProcessTree(process);
                await WaitForExitAfterCancellationAsync(process).ConfigureAwait(false);
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            completedAtUtc = DateTimeOffset.UtcNow;

            return new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: timedOut || canceled ? -1 : process.ExitCode,
                Stdout: stdout.Content,
                Stderr: stderr.Content,
                StdoutTruncated: stdout.Truncated,
                StderrTruncated: stderr.Truncated,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: completedAtUtc,
                TimedOut: timedOut,
                Boundary: Boundary,
                FailureMessage: failureMessage);
        }
        catch (Exception exception)
        {
            completedAtUtc = DateTimeOffset.UtcNow;
            return new WorkspaceProcessExecutionResult(
                Started: false,
                ExitCode: -1,
                Stdout: string.Empty,
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: completedAtUtc,
                TimedOut: false,
                Boundary: Boundary,
                FailureMessage: exception.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static async Task<CappedTextResult> ReadStreamAsync(StreamReader reader, int limit, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 256, 1024 * 1024);
        var builder = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (builder.Length < safeLimit)
            {
                var available = safeLimit - builder.Length;
                var toAppend = Math.Min(available, read);
                builder.Append(buffer, 0, toAppend);
                if (toAppend < read)
                {
                    truncated = true;
                }
            }
            else
            {
                truncated = true;
            }
        }

        return new CappedTextResult(builder.ToString().Trim(), truncated);
    }

    private static async Task WaitForExitAfterCancellationAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort only after timeout or cancellation.
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort only. The timeout result remains more useful than throwing here.
        }
    }

    private sealed record CappedTextResult(string Content, bool Truncated);
}
