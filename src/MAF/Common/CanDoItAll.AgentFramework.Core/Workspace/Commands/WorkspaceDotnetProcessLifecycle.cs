using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceDotnetProcessLifecycle
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly HttpClient LoopbackClient = CreateLoopbackClient();

    private readonly IWorkspaceLongRunningProcessHost processHost;
    private readonly WorkspaceCommandProcessRunner processRunner;
    private readonly WorkspacePathPolicy pathPolicy;
    private readonly Func<Uri, CancellationToken, Task<bool>> probeAsync;

    public WorkspaceDotnetProcessLifecycle(
        IWorkspaceLongRunningProcessHost processHost,
        WorkspaceCommandProcessRunner processRunner,
        WorkspacePathPolicy pathPolicy,
        Func<Uri, CancellationToken, Task<bool>>? probeAsync = null)
    {
        this.processHost = processHost;
        this.processRunner = processRunner;
        this.pathPolicy = pathPolicy;
        this.probeAsync = probeAsync ?? ProbeLoopbackAsync;
    }

    public async Task<WorkspaceCommandExecutionResult> RunAsync(
        WorkspaceCommandPlan plan,
        CancellationToken cancellationToken = default)
    {
        var lifecycle = plan.DotnetRunLifecycle
            ?? throw new InvalidOperationException("Managed dotnet run requires a typed lifecycle plan.");
        var environmentVariables = processRunner.BuildEnvironmentVariables(plan);
        var startedAtUtc = DateTimeOffset.UtcNow;
        IWorkspaceProcessSession? session = null;
        try
        {
            EnsureArtifactDirectory(lifecycle.StartupReceiptFullPath);
            pathPolicy.ValidatePathForUse(plan.WorkingDirectoryPath);
            session = await processHost.StartSessionAsync(
                new WorkspaceProcessSessionRequest(
                    plan.Decision.ToolName,
                    plan.Decision.RecipeId,
                    processRunner.ResolveExecutablePath(plan),
                    plan.Arguments,
                    plan.WorkingDirectoryPath,
                    environmentVariables,
                    plan.StdoutLimitCharacters,
                    plan.StderrLimitCharacters),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var canceled = CreateStartFailure(
                plan,
                startedAtUtc,
                WorkspaceProcessTerminationReason.CallerCanceled,
                "Process launch was canceled.");
            return PersistRunResult(plan, lifecycle, canceled, environmentVariables.Keys);
        }
        catch (WorkspaceProcessStartException)
        {
            var failed = CreateStartFailure(
                plan,
                startedAtUtc,
                WorkspaceProcessTerminationReason.StartFailed,
                "The configured workspace process could not be started.");
            return PersistRunResult(plan, lifecycle, failed, environmentVariables.Keys);
        }

        await using (session.ConfigureAwait(false))
        {
            var readiness = await WaitForReadinessAsync(
                session,
                new Uri(lifecycle.ProbeUrl, UriKind.Absolute),
                lifecycle.StartupTimeoutSeconds,
                cancellationToken).ConfigureAwait(false);
            if (readiness == WorkspaceDotnetReadiness.Ready)
            {
                var snapshot = session.CaptureOutput();
                WriteOutputLogs(lifecycle, snapshot);
                var message = lifecycle.KeepAlive
                    ? $"Application started and {lifecycle.ProbeUrl} returned success. The owned process remains available through workspace_dotnet_stop."
                    : $"Application started and {lifecycle.ProbeUrl} returned success. The owned process was stopped after smoke validation.";

                WorkspaceProcessExecutionResult processResult;
                WorkspaceOwnedProcessIdentity identity = session.Identity;
                if (lifecycle.KeepAlive)
                {
                    session.Detach();
                    processResult = CreateSyntheticRunResult(
                        snapshot,
                        startedAtUtc,
                        exitCode: 0,
                        WorkspaceProcessTerminationReason.Running,
                        failureMessage: string.Empty,
                        residualProcessPossible: false);
                }
                else
                {
                    var terminated = await session.TerminateAsync(
                        WorkspaceProcessTerminationReason.Completed,
                        string.Empty,
                        CancellationToken.None).ConfigureAwait(false);
                    snapshot = new WorkspaceProcessOutputSnapshot(
                        terminated.Stdout,
                        terminated.Stderr,
                        terminated.StdoutTruncated,
                        terminated.StderrTruncated);
                    WriteOutputLogs(lifecycle, snapshot);
                    var cleanupSucceeded = !terminated.ResidualProcessPossible;
                    processResult = CreateSyntheticRunResult(
                        snapshot,
                        startedAtUtc,
                        cleanupSucceeded ? 0 : 1,
                        cleanupSucceeded
                            ? WorkspaceProcessTerminationReason.Completed
                            : WorkspaceProcessTerminationReason.TerminationFailed,
                        cleanupSucceeded ? string.Empty : terminated.FailureMessage,
                        terminated.ResidualProcessPossible);
                }

                var startup = CreateStartupReceipt(
                    plan,
                    lifecycle,
                    identity,
                    succeeded: processResult.ExitCode == 0,
                    message,
                    processResult,
                    cleanupAttempted: !lifecycle.KeepAlive,
                    cleanupSucceeded: lifecycle.KeepAlive ? null : processResult.ExitCode == 0);
                var startupJson = WriteJson(lifecycle.StartupReceiptFullPath, startup);
                processResult = processResult with
                {
                    Stdout = AppendJson(processResult.Stdout, startupJson)
                };
                return processRunner.CreateExecutionResult(plan, processResult, environmentVariables.Keys);
            }

            WorkspaceProcessExecutionResult failedResult;
            string failureMessage;
            if (readiness == WorkspaceDotnetReadiness.ProcessExited)
            {
                failedResult = await session.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                failureMessage = $"dotnet run exited before {lifecycle.ProbeUrl} returned success.";
                failedResult = failedResult with
                {
                    FailureMessage = failureMessage
                };
            }
            else
            {
                var callerCanceled = readiness == WorkspaceDotnetReadiness.Canceled;
                failureMessage = callerCanceled
                    ? "Process readiness validation was canceled."
                    : $"Timed out after {lifecycle.StartupTimeoutSeconds} second(s) waiting for {lifecycle.ProbeUrl}.";
                failedResult = await session.TerminateAsync(
                    callerCanceled
                        ? WorkspaceProcessTerminationReason.CallerCanceled
                        : WorkspaceProcessTerminationReason.TimedOut,
                    failureMessage,
                    CancellationToken.None).ConfigureAwait(false);
            }

            var failedSnapshot = new WorkspaceProcessOutputSnapshot(
                failedResult.Stdout,
                failedResult.Stderr,
                failedResult.StdoutTruncated,
                failedResult.StderrTruncated);
            WriteOutputLogs(lifecycle, failedSnapshot);
            var failedStartup = CreateStartupReceipt(
                plan,
                lifecycle,
                session.Identity,
                succeeded: false,
                failureMessage,
                failedResult,
                cleanupAttempted: true,
                cleanupSucceeded: !failedResult.ResidualProcessPossible);
            var failedStartupJson = WriteJson(lifecycle.StartupReceiptFullPath, failedStartup);
            failedResult = failedResult with
            {
                ExitCode = failedResult.ExitCode == 0 ? 1 : failedResult.ExitCode,
                Stderr = AppendJson(failedResult.Stderr, failedStartupJson)
            };
            return processRunner.CreateExecutionResult(plan, failedResult, environmentVariables.Keys);
        }
    }

    public async Task<WorkspaceCommandExecutionResult> StopAsync(
        WorkspaceCommandPlan plan,
        CancellationToken cancellationToken = default)
    {
        var lifecycle = plan.DotnetStopLifecycle
            ?? throw new InvalidOperationException("Managed dotnet stop requires a typed lifecycle plan.");
        var startedAtUtc = DateTimeOffset.UtcNow;
        WorkspaceDotnetStartupReceipt startup;
        try
        {
            pathPolicy.ValidatePathForUse(lifecycle.StartupReceiptFullPath);
            startup = JsonSerializer.Deserialize<WorkspaceDotnetStartupReceipt>(
                await File.ReadAllTextAsync(lifecycle.StartupReceiptFullPath, cancellationToken).ConfigureAwait(false),
                JsonOptions)
                ?? throw new InvalidDataException("The startup receipt is empty.");
            ValidateStartupIdentity(startup);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            var message = "The startup receipt is invalid or inaccessible; no process was terminated.";
            return PersistStopResult(
                plan,
                lifecycle,
                startedAtUtc,
                new WorkspaceProcessTerminationResult(
                    WorkspaceProcessTerminationStatus.Failed,
                    ResidualProcessPossible: true,
                    message));
        }

        var termination = await processHost.TerminateOwnedProcessAsync(
            new WorkspaceOwnedProcessIdentity(
                startup.AppProcessId,
                startup.AppProcessStartedAtUtc,
                startup.AppProcessExecutableFingerprint),
            cancellationToken).ConfigureAwait(false);
        var succeeded = termination.Status is
            WorkspaceProcessTerminationStatus.Terminated or
            WorkspaceProcessTerminationStatus.AlreadyExited;
        var completedAtUtc = DateTimeOffset.UtcNow;
        var cleanup = new WorkspaceDotnetCleanupReceipt(
            SchemaVersion: 2,
            Succeeded: succeeded,
            Message: termination.Message,
            StartupReceiptPath: lifecycle.StartupReceiptRelativePath,
            CleanupReceiptPath: lifecycle.CleanupReceiptRelativePath,
            ProcessId: startup.AppProcessId,
            TerminationStatus: termination.Status,
            ResidualProcessPossible: termination.ResidualProcessPossible,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc);
        var cleanupJson = WriteJson(lifecycle.CleanupReceiptFullPath, cleanup);
        var updatedStartup = startup with
        {
            CleanupAttempted = true,
            CleanupSucceeded = succeeded,
            CleanupCompletedAtUtc = completedAtUtc
        };
        WriteJson(lifecycle.StartupReceiptFullPath, updatedStartup);
        var processResult = new WorkspaceProcessExecutionResult(
            Started: true,
            ExitCode: succeeded ? 0 : 1,
            Stdout: succeeded ? cleanupJson : string.Empty,
            Stderr: succeeded ? string.Empty : cleanupJson,
            StdoutTruncated: false,
            StderrTruncated: false,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            TimedOut: false,
            Boundary: processHost.DescribeBoundary(),
            FailureMessage: succeeded ? string.Empty : termination.Message,
            TerminationReason: succeeded
                ? WorkspaceProcessTerminationReason.Completed
                : WorkspaceProcessTerminationReason.TerminationFailed,
            ResidualProcessPossible: termination.ResidualProcessPossible);
        return processRunner.CreateExecutionResult(plan, processResult, []);
    }

    private WorkspaceCommandExecutionResult PersistRunResult(
        WorkspaceCommandPlan plan,
        WorkspaceDotnetRunLifecyclePlan lifecycle,
        WorkspaceProcessExecutionResult processResult,
        IEnumerable<string> environmentVariableNames)
    {
        EnsureArtifactDirectory(lifecycle.StartupReceiptFullPath);
        WriteOutputLogs(
            lifecycle,
            new WorkspaceProcessOutputSnapshot(
                processResult.Stdout,
                processResult.Stderr,
                processResult.StdoutTruncated,
                processResult.StderrTruncated));
        var startup = new WorkspaceDotnetStartupReceipt(
            SchemaVersion: 2,
            Succeeded: false,
            Message: processResult.FailureMessage,
            ListenUrl: lifecycle.ListenUrl,
            ProbeUrl: lifecycle.ProbeUrl,
            AppProcessId: 0,
            AppProcessStartedAtUtc: processResult.StartedAtUtc,
            AppProcessExecutableFingerprint: string.Empty,
            AppProcessTreeIds: [],
            KeepAlive: lifecycle.KeepAlive,
            LifetimeScope: lifecycle.LifetimeScope,
            StartupReceiptPath: lifecycle.StartupReceiptRelativePath,
            CleanupReceiptPath: lifecycle.CleanupReceiptRelativePath,
            StdoutLogPath: lifecycle.StdoutLogRelativePath,
            StderrLogPath: lifecycle.StderrLogRelativePath,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            CleanupAttempted: false,
            CleanupSucceeded: null,
            CleanupCompletedAtUtc: null);
        var json = WriteJson(lifecycle.StartupReceiptFullPath, startup);
        processResult = processResult with
        {
            Stderr = AppendJson(processResult.Stderr, json)
        };
        return processRunner.CreateExecutionResult(plan, processResult, environmentVariableNames);
    }

    private WorkspaceCommandExecutionResult PersistStopResult(
        WorkspaceCommandPlan plan,
        WorkspaceDotnetStopLifecyclePlan lifecycle,
        DateTimeOffset startedAtUtc,
        WorkspaceProcessTerminationResult termination)
    {
        var completedAtUtc = DateTimeOffset.UtcNow;
        var cleanup = new WorkspaceDotnetCleanupReceipt(
            SchemaVersion: 2,
            Succeeded: false,
            Message: termination.Message,
            StartupReceiptPath: lifecycle.StartupReceiptRelativePath,
            CleanupReceiptPath: lifecycle.CleanupReceiptRelativePath,
            ProcessId: 0,
            TerminationStatus: termination.Status,
            ResidualProcessPossible: termination.ResidualProcessPossible,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc);
        var cleanupJson = WriteJson(lifecycle.CleanupReceiptFullPath, cleanup);
        var processResult = new WorkspaceProcessExecutionResult(
            Started: true,
            ExitCode: 1,
            Stdout: string.Empty,
            Stderr: cleanupJson,
            StdoutTruncated: false,
            StderrTruncated: false,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            TimedOut: false,
            Boundary: processHost.DescribeBoundary(),
            FailureMessage: termination.Message,
            TerminationReason: WorkspaceProcessTerminationReason.TerminationFailed,
            ResidualProcessPossible: termination.ResidualProcessPossible);
        return processRunner.CreateExecutionResult(plan, processResult, []);
    }

    private async Task<WorkspaceDotnetReadiness> WaitForReadinessAsync(
        IWorkspaceProcessSession session,
        Uri probeUri,
        int startupTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(startupTimeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return WorkspaceDotnetReadiness.Canceled;
            }

            if (session.HasExited)
            {
                return WorkspaceDotnetReadiness.ProcessExited;
            }

            try
            {
                if (await probeAsync(probeUri, cancellationToken).ConfigureAwait(false))
                {
                    return WorkspaceDotnetReadiness.Ready;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return WorkspaceDotnetReadiness.Canceled;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return WorkspaceDotnetReadiness.Canceled;
            }
        }

        return WorkspaceDotnetReadiness.TimedOut;
    }

    private WorkspaceDotnetStartupReceipt CreateStartupReceipt(
        WorkspaceCommandPlan plan,
        WorkspaceDotnetRunLifecyclePlan lifecycle,
        WorkspaceOwnedProcessIdentity identity,
        bool succeeded,
        string message,
        WorkspaceProcessExecutionResult processResult,
        bool cleanupAttempted,
        bool? cleanupSucceeded)
        => new(
            SchemaVersion: 2,
            Succeeded: succeeded,
            Message: SensitiveTextRedactor.Redact(message),
            ListenUrl: lifecycle.ListenUrl,
            ProbeUrl: lifecycle.ProbeUrl,
            AppProcessId: identity.ProcessId,
            AppProcessStartedAtUtc: identity.StartedAtUtc,
            AppProcessExecutableFingerprint: identity.ExecutablePathFingerprint,
            AppProcessTreeIds: [identity.ProcessId],
            KeepAlive: lifecycle.KeepAlive,
            LifetimeScope: lifecycle.LifetimeScope,
            StartupReceiptPath: lifecycle.StartupReceiptRelativePath,
            CleanupReceiptPath: lifecycle.CleanupReceiptRelativePath,
            StdoutLogPath: lifecycle.StdoutLogRelativePath,
            StderrLogPath: lifecycle.StderrLogRelativePath,
            CapturedAtUtc: processResult.CompletedAtUtc,
            CleanupAttempted: cleanupAttempted,
            CleanupSucceeded: cleanupSucceeded,
            CleanupCompletedAtUtc: cleanupAttempted ? processResult.CompletedAtUtc : null);

    private WorkspaceProcessExecutionResult CreateStartFailure(
        WorkspaceCommandPlan plan,
        DateTimeOffset startedAtUtc,
        WorkspaceProcessTerminationReason reason,
        string message)
        => new(
            Started: false,
            ExitCode: -1,
            Stdout: string.Empty,
            Stderr: string.Empty,
            StdoutTruncated: false,
            StderrTruncated: false,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            TimedOut: false,
            Boundary: processHost.DescribeBoundary(),
            FailureMessage: message,
            TerminationReason: reason);

    private WorkspaceProcessExecutionResult CreateSyntheticRunResult(
        WorkspaceProcessOutputSnapshot snapshot,
        DateTimeOffset startedAtUtc,
        int exitCode,
        WorkspaceProcessTerminationReason reason,
        string failureMessage,
        bool residualProcessPossible)
        => new(
            Started: true,
            ExitCode: exitCode,
            Stdout: snapshot.Stdout,
            Stderr: snapshot.Stderr,
            StdoutTruncated: snapshot.StdoutTruncated,
            StderrTruncated: snapshot.StderrTruncated,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            TimedOut: reason == WorkspaceProcessTerminationReason.TimedOut,
            Boundary: processHost.DescribeBoundary(),
            FailureMessage: failureMessage,
            TerminationReason: reason,
            ResidualProcessPossible: residualProcessPossible);

    private static void ValidateStartupIdentity(WorkspaceDotnetStartupReceipt startup)
    {
        if (startup.SchemaVersion != 2 ||
            !startup.Succeeded ||
            startup.AppProcessId <= 0 ||
            startup.AppProcessStartedAtUtc == default ||
            string.IsNullOrWhiteSpace(startup.AppProcessExecutableFingerprint) ||
            startup.AppProcessExecutableFingerprint.Length != 64 ||
            startup.AppProcessExecutableFingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                "The startup receipt does not contain a valid owned-process identity.");
        }
    }

    private static void WriteOutputLogs(
        WorkspaceDotnetRunLifecyclePlan lifecycle,
        WorkspaceProcessOutputSnapshot snapshot)
    {
        File.WriteAllText(
            lifecycle.StdoutLogFullPath,
            SensitiveTextRedactor.Redact(snapshot.Stdout));
        File.WriteAllText(
            lifecycle.StderrLogFullPath,
            SensitiveTextRedactor.Redact(snapshot.Stderr));
    }

    private static string WriteJson<T>(string path, T value)
    {
        EnsureArtifactDirectory(path);
        var json = SensitiveTextRedactor.Redact(JsonSerializer.Serialize(value, JsonOptions));
        File.WriteAllText(path, json);
        return json;
    }

    private static void EnsureArtifactDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The lifecycle artifact path has no parent directory.");
        Directory.CreateDirectory(directory);
    }

    private static string AppendJson(string text, string json)
        => string.IsNullOrWhiteSpace(text)
            ? json
            : $"{text.TrimEnd()}{Environment.NewLine}{json}";

    private static HttpClient CreateLoopbackClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
    }

    private static async Task<bool> ProbeLoopbackAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var response = await LoopbackClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        return response.StatusCode >= HttpStatusCode.OK &&
               response.StatusCode < HttpStatusCode.BadRequest;
    }

    private enum WorkspaceDotnetReadiness
    {
        Ready,
        ProcessExited,
        TimedOut,
        Canceled
    }
}

internal sealed record WorkspaceDotnetStartupReceipt(
    int SchemaVersion,
    bool Succeeded,
    string Message,
    string ListenUrl,
    string ProbeUrl,
    int AppProcessId,
    DateTimeOffset AppProcessStartedAtUtc,
    string AppProcessExecutableFingerprint,
    IReadOnlyList<int> AppProcessTreeIds,
    bool KeepAlive,
    WorkspaceProcessLifetimeScope LifetimeScope,
    string StartupReceiptPath,
    string CleanupReceiptPath,
    string StdoutLogPath,
    string StderrLogPath,
    DateTimeOffset CapturedAtUtc,
    bool CleanupAttempted,
    bool? CleanupSucceeded,
    DateTimeOffset? CleanupCompletedAtUtc);

internal sealed record WorkspaceDotnetCleanupReceipt(
    int SchemaVersion,
    bool Succeeded,
    string Message,
    string StartupReceiptPath,
    string CleanupReceiptPath,
    int ProcessId,
    WorkspaceProcessTerminationStatus TerminationStatus,
    bool ResidualProcessPossible,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);
