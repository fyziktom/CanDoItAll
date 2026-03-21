using System.Text;
using CanDoItAll.Mcp.Core.Operations;
using CanDoItAll.Mcp.SshOps.Configuration;
using CanDoItAll.Mcp.SshOps.Security;
using CanDoItAll.Mcp.SshOps.Transport;

namespace CanDoItAll.Mcp.SshOps.Operations;

public sealed record RemoteJobStartRequest(
    string CorrelationId,
    string Kind,
    string InitialSummary,
    string SuccessSummary,
    string FailureSummary,
    string CancelSummary,
    IReadOnlyList<string> Command,
    string? WorkingDirectory = null,
    bool UseSudo = false);

public sealed record RemoteJobStartResult(
    string OperationId,
    string JobDirectory);

public sealed record RemoteOperationSnapshot(
    string OperationId,
    string Target,
    string Kind,
    AsyncOperationState State,
    string Summary,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int? ExitCode,
    long? ProcessId)
{
    public bool IsTerminal => State is AsyncOperationState.Succeeded or AsyncOperationState.Failed or AsyncOperationState.TimedOut or AsyncOperationState.Cancelled;
}

public sealed class RemoteJobRunner(
    RuntimeConfiguration runtimeConfiguration,
    ISshTransport transport,
    RemotePathGuard pathGuard,
    SecretRedactor secretRedactor,
    ILogger<RemoteJobRunner> logger)
{
    private readonly OperationWaitEngine _waitEngine = new();

    public async Task<RemoteJobStartResult> StartAsync(
        ResolvedTargetConfiguration target,
        RemoteJobStartRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Command.Count == 0)
        {
            throw new ToolInvocationException("ValidationFailed", "A detached remote job requires at least one command segment.");
        }

        var operationId = OperationIdFactory.Create();
        var jobDirectory = pathGuard.ResolveInsideStateRoot(target, $"jobs/{operationId}");
        await EnsureWritableJobDirectoryAsync(target, jobDirectory, cancellationToken);

        var scriptPath = $"{jobDirectory}/run.sh";
        var scriptContent = CreateRunnerScript(target, jobDirectory, request);
        await transport.UploadBytesAsync(target, scriptPath, Encoding.UTF8.GetBytes(scriptContent), ensureParentDirectory: true, cancellationToken);

        var launchScript = $"""
            cd {QuoteShell(jobDirectory)}
            chmod 700 {QuoteShell(scriptPath)}
            printf '%s' {QuoteShell("queued")} > state
            printf '%s' {QuoteShell(request.InitialSummary)} > summary
            printf '%s' {QuoteShell(request.CancelSummary)} > cancelSummary
            printf '%s' {QuoteShell(request.Kind)} > kind
            printf '%s' {QuoteShell(request.CorrelationId)} > correlationId
            printf '%s' {QuoteShell(target.Name)} > target
            nohup {QuoteShell(scriptPath)} > stdout.log 2> stderr.log < /dev/null &
            echo $! > pid
            """.ReplaceLineEndings("\n");

        var launchResult = await transport.ExecuteAsync(
            target,
            ["bash", "-lc", launchScript],
            new RemoteExecutionOptions(Timeout: runtimeConfiguration.CommandTimeout),
            cancellationToken);

        EnsureSuccess(launchResult, "ValidationFailed", "Could not start the detached remote job.");
        return new RemoteJobStartResult(operationId, jobDirectory);
    }

    public async Task<RemoteOperationSnapshot> GetSnapshotAsync(
        ResolvedTargetConfiguration target,
        string operationId,
        CancellationToken cancellationToken)
    {
        var jobDirectory = pathGuard.ResolveInsideStateRoot(target, $"jobs/{operationId}");
        var directoryStat = await transport.StatAsync(target, jobDirectory, cancellationToken);
        if (!directoryStat.Exists || !directoryStat.IsDirectory)
        {
            throw new ToolInvocationException("OperationNotFound", $"Operation '{operationId}' was not found for target '{target.Name}'.", new { operationId, target = target.Name });
        }

        var kind = await ReadTextIfExistsAsync(target, $"{jobDirectory}/kind", 1024, cancellationToken) ?? "remote-job";
        var stateText = await ReadTextIfExistsAsync(target, $"{jobDirectory}/state", 128, cancellationToken) ?? "queued";
        var summary = await ReadTextIfExistsAsync(target, $"{jobDirectory}/summary", 8192, cancellationToken) ?? "Queued.";
        var startedText = await ReadTextIfExistsAsync(target, $"{jobDirectory}/startedAtUtc", 128, cancellationToken);
        var endedText = await ReadTextIfExistsAsync(target, $"{jobDirectory}/endedAtUtc", 128, cancellationToken);
        var exitCodeText = await ReadTextIfExistsAsync(target, $"{jobDirectory}/exitCode", 64, cancellationToken);
        var pidText = await ReadTextIfExistsAsync(target, $"{jobDirectory}/pid", 64, cancellationToken);

        var state = ParseState(stateText);
        var startedAt = DateTimeOffset.TryParse(startedText, out var parsedStarted)
            ? parsedStarted
            : DateTimeOffset.UtcNow;
        var endedAt = DateTimeOffset.TryParse(endedText, out var parsedEnded)
            ? parsedEnded
            : (DateTimeOffset?)null;
        var exitCode = int.TryParse(exitCodeText, out var parsedExitCode)
            ? parsedExitCode
            : (int?)null;
        var processId = long.TryParse(pidText, out var parsedPid)
            ? parsedPid
            : (long?)null;

        return new RemoteOperationSnapshot(
            operationId,
            target.Name,
            kind.Trim(),
            state,
            secretRedactor.Redact(summary.Trim()),
            startedAt,
            endedAt,
            exitCode,
            processId);
    }

    public Task<WaitOutcome<RemoteOperationSnapshot>> WaitAsync(
        ResolvedTargetConfiguration target,
        string operationId,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        return _waitEngine.WaitAsync(
            ct => GetSnapshotAsync(target, operationId, ct),
            snapshot => snapshot.IsTerminal,
            timeout,
            pollInterval,
            cancellationToken);
    }

    public async Task<OperationLogsData> ReadLogsAsync(
        ResolvedTargetConfiguration target,
        string operationId,
        string stream,
        long cursor,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var jobDirectory = pathGuard.ResolveInsideStateRoot(target, $"jobs/{operationId}");
        var logPath = string.Equals(stream, "stderr", StringComparison.OrdinalIgnoreCase)
            ? $"{jobDirectory}/stderr.log"
            : $"{jobDirectory}/stdout.log";

        var stat = await transport.StatAsync(target, logPath, cancellationToken);
        if (!stat.Exists)
        {
            return new OperationLogsData(cursor, cursor, string.Empty, true);
        }

        var safeCursor = Math.Max(0, cursor);
        var chunk = await transport.ReadBytesAsync(target, logPath, safeCursor, Math.Clamp(maxBytes, 1, 1024 * 1024), cancellationToken);
        var content = secretRedactor.Redact(Encoding.UTF8.GetString(chunk));
        return new OperationLogsData(safeCursor, safeCursor + chunk.Length, content, true);
    }

    public async Task<RemoteOperationSnapshot> CancelAsync(
        ResolvedTargetConfiguration target,
        string operationId,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken)
    {
        var jobDirectory = pathGuard.ResolveInsideStateRoot(target, $"jobs/{operationId}");
        var snapshot = await GetSnapshotAsync(target, operationId, cancellationToken);
        if (snapshot.IsTerminal)
        {
            return snapshot;
        }

        if (snapshot.ProcessId is null)
        {
            throw new ToolInvocationException("OperationNotFound", $"Operation '{operationId}' does not expose a remote process identifier.");
        }

        var cancelSummary = await ReadTextIfExistsAsync(target, $"{jobDirectory}/cancelSummary", 8192, cancellationToken)
            ?? "Operation cancelled.";
        var graceSeconds = Math.Max(1, (int)Math.Ceiling(gracePeriod.TotalSeconds));
        var cancelScript = $$"""
            PID={{snapshot.ProcessId.Value}}
            JOB_DIR={{QuoteShell(jobDirectory)}}
            CANCEL_SUMMARY={{QuoteShell(cancelSummary)}}

            terminate_children() {
              local parent_pid="$1"
              local child_pid
              for child_pid in $(ps -o pid= --ppid "$parent_pid" 2>/dev/null); do
                terminate_children "$child_pid"
                kill -TERM "$child_pid" 2>/dev/null || true
              done
            }

            kill_children_force() {
              local parent_pid="$1"
              local child_pid
              for child_pid in $(ps -o pid= --ppid "$parent_pid" 2>/dev/null); do
                kill_children_force "$child_pid"
                kill -KILL "$child_pid" 2>/dev/null || true
              done
            }

            if kill -0 "$PID" 2>/dev/null; then
              terminate_children "$PID"
              kill -TERM "$PID" 2>/dev/null || true
              sleep {{graceSeconds}}
              if kill -0 "$PID" 2>/dev/null; then
                kill_children_force "$PID"
                kill -KILL "$PID" 2>/dev/null || true
              fi
            fi

            END_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
            printf '%s' "$END_AT" > "$JOB_DIR/endedAtUtc"
            printf '%s' '130' > "$JOB_DIR/exitCode"
            printf '%s' 'cancelled' > "$JOB_DIR/state"
            printf '%s' "$CANCEL_SUMMARY" > "$JOB_DIR/summary"
            rm -f "$JOB_DIR/pid"
            """.ReplaceLineEndings("\n");

        var result = await transport.ExecuteAsync(
            target,
            ["bash", "-lc", cancelScript],
            new RemoteExecutionOptions(Timeout: gracePeriod + TimeSpan.FromSeconds(10)),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            logger.LogWarning("Cancel script for operation {OperationId} on target {Target} returned exit code {ExitCode}. stderr={Error}", operationId, target.Name, result.ExitCode, result.StandardError);
        }

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        return await GetSnapshotAsync(target, operationId, cancellationToken);
    }

    private static AsyncOperationState ParseState(string stateText)
    {
        return stateText.Trim().ToLowerInvariant() switch
        {
            "queued" => AsyncOperationState.Queued,
            "running" => AsyncOperationState.Running,
            "succeeded" => AsyncOperationState.Succeeded,
            "failed" => AsyncOperationState.Failed,
            "timedout" => AsyncOperationState.TimedOut,
            "cancelled" => AsyncOperationState.Cancelled,
            _ => AsyncOperationState.Running
        };
    }

    private async Task<string?> ReadTextIfExistsAsync(
        ResolvedTargetConfiguration target,
        string remotePath,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var stat = await transport.StatAsync(target, remotePath, cancellationToken);
        if (!stat.Exists || stat.IsDirectory)
        {
            return null;
        }

        return await transport.ReadTextAsync(target, remotePath, maxBytes, cancellationToken);
    }

    private static void EnsureSuccess(RemoteCommandResult result, string code, string message)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new ToolInvocationException(
            code,
            message,
            new
            {
                exitCode = result.ExitCode,
                stdout = result.StandardOutput,
                stderr = result.StandardError,
                command = result.CommandText
            });
    }

    private static string CreateRunnerScript(ResolvedTargetConfiguration target, string jobDirectory, RemoteJobStartRequest request)
    {
        var commandLine = BuildCommandLine(target, request.Command, request.WorkingDirectory, request.UseSudo);
        return $$"""
            #!/usr/bin/env bash
            set -euo pipefail

            JOB_DIR={{QuoteShell(jobDirectory)}}
            SUCCESS_SUMMARY={{QuoteShell(request.SuccessSummary)}}
            FAILURE_SUMMARY={{QuoteShell(request.FailureSummary)}}
            CANCEL_SUMMARY={{QuoteShell(request.CancelSummary)}}

            write_text() {
              printf '%s' "$2" > "$1"
            }

            STARTED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
            write_text "$JOB_DIR/startedAtUtc" "$STARTED_AT"
            write_text "$JOB_DIR/state" "running"

            EXIT_CODE=0

            finalize() {
              END_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
              write_text "$JOB_DIR/endedAtUtc" "$END_AT"
              write_text "$JOB_DIR/exitCode" "$EXIT_CODE"

              if [ "$EXIT_CODE" -eq 0 ]; then
                write_text "$JOB_DIR/state" "succeeded"
                write_text "$JOB_DIR/summary" "$SUCCESS_SUMMARY"
              elif [ "$EXIT_CODE" -eq 130 ]; then
                write_text "$JOB_DIR/state" "cancelled"
                write_text "$JOB_DIR/summary" "$CANCEL_SUMMARY"
              else
                write_text "$JOB_DIR/state" "failed"
                write_text "$JOB_DIR/summary" "$FAILURE_SUMMARY"
              fi
            }

            trap 'EXIT_CODE=130; finalize; exit 130' TERM INT

            set +e
            {{commandLine}}
            EXIT_CODE=$?
            set -e

            finalize
            exit "$EXIT_CODE"
            """;
    }

    private static string BuildCommandLine(
        ResolvedTargetConfiguration target,
        IReadOnlyList<string> command,
        string? workingDirectory,
        bool useSudo)
    {
        var commandText = string.Join(' ', command.Select(QuoteShell));
        var workingCommand = string.IsNullOrWhiteSpace(workingDirectory)
            ? commandText
            : $"cd {QuoteShell(workingDirectory)} && {commandText}";

        if (useSudo && !string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return $"{target.Sudo.Command} bash -lc {QuoteShell(workingCommand)}";
        }

        return $"bash -lc {QuoteShell(workingCommand)}";
    }

    private static string QuoteShell(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'")}'";
    }

    private async Task EnsureWritableJobDirectoryAsync(
        ResolvedTargetConfiguration target,
        string jobDirectory,
        CancellationToken cancellationToken)
    {
        if (string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            await transport.EnsureDirectoryAsync(target, jobDirectory, useSudo: false, cancellationToken);
            return;
        }

        var result = await transport.ExecuteAsync(
            target,
            [
                "bash",
                "-lc",
                $"""
                mkdir -p {QuoteShell(jobDirectory)}
                chown {QuoteShell($"{target.User}:{target.User}")} {QuoteShell(jobDirectory)}
                """
            ],
            new RemoteExecutionOptions(UseSudo: true, Timeout: runtimeConfiguration.CommandTimeout),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", $"Could not prepare writable remote job directory '{jobDirectory}'.");
    }
}
