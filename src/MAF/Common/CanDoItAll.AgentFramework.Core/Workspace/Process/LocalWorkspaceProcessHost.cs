using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class LocalWorkspaceProcessHost : IWorkspaceLongRunningProcessHost
{
    private static readonly TimeSpan StreamDrainTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StreamCancellationTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GracefulTerminationTimeout = TimeSpan.FromMilliseconds(750);

    private static readonly ExecutionBoundaryDescriptor Boundary = new(
        Mode: "PolicyOnlyLocal",
        FilesystemScope: "Workspace-relative request shaping only. Child processes still inherit the host OS filesystem rights.",
        NetworkScope: "Not host-enforced. Child processes inherit the host network access that the local machine already has.",
        CredentialScope: "Scrubbed environment allowlist only. No container or remote credential boundary is enforced by AgentFramework.",
        HostLabel: "Local best-effort process host",
        IsEnforcedByHost: false,
        Notes: "This host centralizes process policy, session identity, timeout, output caps, and tree-kill behavior, but it is not a true OS or container sandbox.");

    public ExecutionBoundaryDescriptor DescribeBoundary() => Boundary;

    public async Task<WorkspaceProcessExecutionResult> ExecuteAsync(
        WorkspaceProcessExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAtUtc = DateTimeOffset.UtcNow;
        IWorkspaceProcessSession session;
        try
        {
            session = await StartSessionAsync(
                new WorkspaceProcessSessionRequest(
                    request.ToolName,
                    request.RecipeId,
                    request.ExecutablePath,
                    request.Arguments,
                    request.WorkingDirectory,
                    request.EnvironmentVariables,
                    request.StdoutLimitCharacters,
                    request.StderrLimitCharacters,
                    request.StandardInput),
                cancellationToken).ConfigureAwait(false);
        }
        catch (WorkspaceProcessStartException)
        {
            return new WorkspaceProcessExecutionResult(
                Started: false,
                ExitCode: -1,
                Stdout: string.Empty,
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                TimedOut: false,
                Boundary: Boundary,
                FailureMessage: "The configured workspace process could not be started.",
                TerminationReason: WorkspaceProcessTerminationReason.StartFailed);
        }

        await using (session.ConfigureAwait(false))
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, 3600)));
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                timeout.Token,
                cancellationToken);
            try
            {
                return await session.WaitForExitAsync(linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return await session.TerminateAsync(
                    WorkspaceProcessTerminationReason.CallerCanceled,
                    "Process execution was canceled.",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return await session.TerminateAsync(
                    WorkspaceProcessTerminationReason.TimedOut,
                    $"Process timed out after {Math.Clamp(request.TimeoutSeconds, 1, 3600)} second(s).",
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public Task<IWorkspaceProcessSession> StartSessionAsync(
        WorkspaceProcessSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.StandardIoMode == WorkspaceProcessStandardIoMode.Duplex &&
            request.StandardInput is not null)
        {
            throw new ArgumentException(
                "Duplex process sessions cannot also declare static standard input.",
                nameof(request));
        }

        var startInfo = BuildStartInfo(request);
        var executableIdentityPath = new WorkspaceExecutableLocator().ResolveExecutablePath(
            [request.ExecutablePath],
            request.WorkingDirectory);
        startInfo.FileName = executableIdentityPath;
        var startedAtUtc = DateTimeOffset.UtcNow;
        Process? process;
        ILocalWorkspaceProcessOwnership? ownership = null;
        try
        {
            using var ownershipStart = LocalWorkspaceProcessOwnershipStart.Prepare(
                startInfo,
                executableIdentityPath,
                request.WorkingDirectory);
            process = Process.Start(startInfo);
            if (process is not null)
            {
                ownership = ownershipStart.Attach(process);
            }
        }
        catch (Exception exception) when (IsStartFailure(exception))
        {
            throw new WorkspaceProcessStartException(
                "The configured workspace process could not be started.",
                exception);
        }
        finally
        {
            startInfo.Environment.Clear();
        }

        if (process is null)
        {
            throw new WorkspaceProcessStartException(
                "The configured workspace process could not be started.");
        }

        try
        {
            var identity = new WorkspaceOwnedProcessIdentity(
                process.Id,
                ResolveProcessStartedAtUtc(process, startedAtUtc),
                ComputeExecutablePathFingerprint(executableIdentityPath),
                ownership?.Identity
                    ?? throw new InvalidOperationException("The process ownership boundary was not established."));
            WaitForExecutableIdentity(process, identity.ExecutablePathFingerprint);
            IWorkspaceProcessSession session = new LocalWorkspaceProcessSession(
                process,
                ownership,
                request,
                identity,
                startedAtUtc);
            return Task.FromResult(session);
        }
        catch
        {
            ownership?.ForceTerminate();
            ownership?.Dispose();
            TryKillProcessTree(process);
            process.Dispose();
            throw;
        }
    }

    public async Task<WorkspaceProcessTerminationResult> TerminateOwnedProcessAsync(
        WorkspaceOwnedProcessIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        _ = cancellationToken;
        Process process;
        try
        {
            process = Process.GetProcessById(identity.ProcessId);
        }
        catch (ArgumentException)
        {
            return await TerminateWithoutRootAsync(identity.Boundary).ConfigureAwait(false);
        }

        using (process)
        {
            var identityMatch = MatchIdentity(process, identity);
            if (identityMatch != OwnedProcessIdentityMatch.Match)
            {
                return new WorkspaceProcessTerminationResult(
                    WorkspaceProcessTerminationStatus.IdentityMismatch,
                    ResidualProcessPossible: true,
                    identityMatch switch
                    {
                        OwnedProcessIdentityMatch.StartTimeMismatch =>
                            "The running process start time does not match the recorded owned-process identity and was not terminated.",
                        OwnedProcessIdentityMatch.ExecutableMismatch =>
                            BuildExecutableIdentityMismatchMessage(process, identity),
                        _ =>
                            "The running process identity could not be verified and was not terminated."
                    });
            }

            ILocalWorkspaceProcessOwnership? ownership;
            try
            {
                ownership = LocalWorkspaceProcessOwnershipStart.TryOpen(identity.Boundary);
            }
            catch (Exception exception) when (
                exception is Win32Exception or InvalidOperationException or ArgumentOutOfRangeException)
            {
                return new WorkspaceProcessTerminationResult(
                    WorkspaceProcessTerminationStatus.Failed,
                    ResidualProcessPossible: true,
                    "The owned process boundary could not be opened; a residual process may remain.");
            }

            if (ownership is null)
            {
                return new WorkspaceProcessTerminationResult(
                    WorkspaceProcessTerminationStatus.Failed,
                    ResidualProcessPossible: true,
                    "The owned process boundary is unavailable on this host; no process was terminated.");
            }

            using (ownership)
            {
                return await ForceTerminateOwnershipAsync(ownership).ConfigureAwait(false);
            }
        }
    }

    private static async Task<WorkspaceProcessTerminationResult> TerminateWithoutRootAsync(
        WorkspaceOwnedProcessBoundary boundary)
    {
        ILocalWorkspaceProcessOwnership? ownership;
        try
        {
            ownership = LocalWorkspaceProcessOwnershipStart.TryOpen(boundary);
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return new WorkspaceProcessTerminationResult(
                WorkspaceProcessTerminationStatus.Failed,
                ResidualProcessPossible: true,
                "The root process exited, but its ownership boundary could not be verified.");
        }

        if (ownership is null)
        {
            return new WorkspaceProcessTerminationResult(
                WorkspaceProcessTerminationStatus.AlreadyExited,
                ResidualProcessPossible: false,
                "The recorded owned process boundary no longer exists.");
        }

        using (ownership)
        {
            if (await ownership.WaitForEmptyAsync(TimeSpan.Zero).ConfigureAwait(false))
            {
                return new WorkspaceProcessTerminationResult(
                    WorkspaceProcessTerminationStatus.AlreadyExited,
                    ResidualProcessPossible: false,
                    "The recorded owned process boundary is empty.");
            }

            return await ForceTerminateOwnershipAsync(ownership).ConfigureAwait(false);
        }
    }

    private static async Task<WorkspaceProcessTerminationResult> ForceTerminateOwnershipAsync(
        ILocalWorkspaceProcessOwnership ownership)
    {
        var terminationRequested = ownership.ForceTerminate();
        var empty = await ownership.WaitForEmptyAsync(TerminationTimeout).ConfigureAwait(false);
        if (terminationRequested && empty)
        {
            return new WorkspaceProcessTerminationResult(
                WorkspaceProcessTerminationStatus.Terminated,
                ResidualProcessPossible: false,
                "The recorded owned process boundary was terminated and verified empty.");
        }

        return new WorkspaceProcessTerminationResult(
            WorkspaceProcessTerminationStatus.Failed,
            ResidualProcessPossible: true,
            "Process termination could not be confirmed; a residual process may remain.");
    }

    private static ProcessStartInfo BuildStartInfo(WorkspaceProcessSessionRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.StandardInput is not null ||
                request.StandardIoMode == WorkspaceProcessStandardIoMode.Duplex,
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

        return startInfo;
    }

    private static bool IsStartFailure(Exception exception)
        => exception is Win32Exception or InvalidOperationException or FileNotFoundException or DirectoryNotFoundException;

    private static OwnedProcessIdentityMatch MatchIdentity(
        Process process,
        WorkspaceOwnedProcessIdentity identity)
    {
        try
        {
            var runningStartTimeUtc = process.StartTime.ToUniversalTime();
            if (runningStartTimeUtc != identity.StartedAtUtc.UtcDateTime)
            {
                return OwnedProcessIdentityMatch.StartTimeMismatch;
            }

            var executablePath = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(executablePath) &&
                   string.Equals(
                       ComputeExecutablePathFingerprint(executablePath),
                       identity.ExecutablePathFingerprint,
                       StringComparison.Ordinal)
                ? OwnedProcessIdentityMatch.Match
                : OwnedProcessIdentityMatch.ExecutableMismatch;
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return OwnedProcessIdentityMatch.Unavailable;
        }
    }

    private enum OwnedProcessIdentityMatch
    {
        Match,
        StartTimeMismatch,
        ExecutableMismatch,
        Unavailable
    }

    private static string ComputeExecutablePathFingerprint(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var identityPath = OperatingSystem.IsWindows()
            ? File.ResolveLinkTarget(fullPath, returnFinalTarget: true)?.FullName ?? fullPath
            : ResolveUnixRealPath(fullPath);
        var canonical = OperatingSystem.IsWindows()
            ? identityPath.ToUpperInvariant()
            : identityPath;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string ResolveUnixRealPath(string path)
    {
        var pointer = UnixProcessNativeMethods.RealPath(path, 0);
        if (pointer == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                $"Could not resolve executable identity path '{path}'.");
        }

        try
        {
            return Marshal.PtrToStringUTF8(pointer)
                ?? throw new InvalidOperationException("The resolved executable identity path was empty.");
        }
        finally
        {
            UnixProcessNativeMethods.Free(pointer);
        }
    }

    private static DateTimeOffset ResolveProcessStartedAtUtc(
        Process process,
        DateTimeOffset fallback)
    {
        try
        {
            return process.StartTime.ToUniversalTime();
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return fallback;
        }
    }

    private static string BuildExecutableIdentityMismatchMessage(
        Process process,
        WorkspaceOwnedProcessIdentity identity)
    {
        var currentFingerprint = TryGetExecutablePathFingerprint(process) ?? "unavailable";
        var recordedPrefix = identity.ExecutablePathFingerprint[
            ..Math.Min(12, identity.ExecutablePathFingerprint.Length)];
        var currentPrefix = currentFingerprint[..Math.Min(12, currentFingerprint.Length)];
        return $"The running process executable identity does not match the recorded owned-process identity and was not terminated. Recorded={recordedPrefix}; Current={currentPrefix}.";
    }

    private static string? TryGetExecutablePathFingerprint(Process process)
    {
        try
        {
            return process.MainModule?.FileName is { Length: > 0 } executablePath
                ? ComputeExecutablePathFingerprint(executablePath)
                : null;
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static void WaitForExecutableIdentity(Process process, string expectedFingerprint)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var fingerprint = TryGetExecutablePathFingerprint(process);
            if (string.Equals(fingerprint, expectedFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            if (HasExited(process))
            {
                return;
            }

            Thread.Sleep(5);
            process.Refresh();
        }

        var currentFingerprint = TryGetExecutablePathFingerprint(process) ?? "unavailable";
        throw new InvalidOperationException(
            $"The process executable identity did not stabilize after launch. Expected={expectedFingerprint[..12]}; Current={currentFingerprint[..Math.Min(12, currentFingerprint.Length)]}.");
    }

    private static async Task WriteStandardInputAsync(
        Process process,
        string standardInput,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static void TryCloseStandardInput(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (InvalidOperationException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static async Task CompleteStandardInputAsync(Task standardInputTask, Process process)
    {
        try
        {
            await standardInputTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException) when (HasExited(process))
        {
        }
        catch (ObjectDisposedException) when (HasExited(process))
        {
        }
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        CappedTextCapture capture,
        CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new char[4096];
            while (true)
            {
                var read = await reader.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                capture.Append(buffer, read);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            capture.MarkTruncated();
        }
        catch (ObjectDisposedException)
        {
            capture.MarkTruncated();
        }
        catch (IOException)
        {
            capture.MarkTruncated();
        }
    }

    private static async Task<bool> WaitForExitAfterCancellationAsync(
        Process process,
        CancellationToken cancellationToken,
        TimeSpan? timeoutValue = null)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutValue ?? TerminationTimeout);
            await WaitForProcessExitOnlyAsync(process, timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return HasExited(process);
        }
    }

    private static async Task WaitForProcessExitOnlyAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return;
        }

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnExited(object? sender, EventArgs args)
        {
            completion.TrySetResult(null);
        }

        process.EnableRaisingEvents = true;
        process.Exited += OnExited;
        try
        {
            if (process.HasExited)
            {
                return;
            }

            using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            process.Exited -= OnExited;
        }
    }

    private static async Task<CappedTextResult> CompleteStreamReadAsync(
        Task readTask,
        CappedTextCapture capture,
        CancellationTokenSource cancellation)
    {
        if (readTask.IsCompleted)
        {
            await readTask.ConfigureAwait(false);
            return capture.Snapshot();
        }

        try
        {
            await readTask.WaitAsync(StreamDrainTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            capture.MarkTruncated();
            cancellation.Cancel();
            try
            {
                await readTask.WaitAsync(StreamCancellationTimeout).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        return capture.Snapshot();
    }

    private static bool TryKillProcessTree(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return true;
            }

            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return HasExited(process);
        }
    }

    private static async Task<bool> TerminateProcessTreeAsync(
        Process process,
        ILocalWorkspaceProcessOwnership ownership,
        WorkspaceProcessTerminationMode terminationMode)
    {
        if (terminationMode == WorkspaceProcessTerminationMode.GracefulThenForceTree &&
            ownership.RequestGracefulTermination(process) &&
            await ownership.WaitForEmptyAsync(GracefulTerminationTimeout).ConfigureAwait(false))
        {
            return true;
        }

        return ownership.ForceTerminate() &&
               await ownership.WaitForEmptyAsync(TerminationTimeout).ConfigureAwait(false);
    }

    internal static bool ProcessHasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasExited(Process process) => ProcessHasExited(process);

    private sealed class LocalWorkspaceProcessSession : IWorkspaceDuplexProcessSession
    {
        private readonly Process process;
        private readonly ILocalWorkspaceProcessOwnership ownership;
        private readonly WorkspaceProcessTerminationMode terminationMode;
        private readonly bool isDuplex;
        private readonly DateTimeOffset startedAtUtc;
        private readonly CancellationTokenSource stdoutReadCancellation = new();
        private readonly CancellationTokenSource stderrReadCancellation = new();
        private readonly CancellationTokenSource standardInputCancellation = new();
        private readonly CappedTextCapture stdoutCapture;
        private readonly CappedTextCapture stderrCapture;
        private readonly Task stdoutTask;
        private readonly Task stderrTask;
        private readonly Task standardInputTask;
        private readonly SemaphoreSlim completionGate = new(1, 1);
        private WorkspaceProcessExecutionResult? finalResult;
        private int detached;

        public LocalWorkspaceProcessSession(
            Process process,
            ILocalWorkspaceProcessOwnership ownership,
            WorkspaceProcessSessionRequest request,
            WorkspaceOwnedProcessIdentity identity,
            DateTimeOffset startedAtUtc)
        {
            this.process = process;
            this.ownership = ownership;
            terminationMode = request.TerminationMode;
            isDuplex = request.StandardIoMode == WorkspaceProcessStandardIoMode.Duplex;
            Identity = identity;
            this.startedAtUtc = startedAtUtc;
            stdoutCapture = new CappedTextCapture(request.StdoutLimitCharacters);
            stderrCapture = new CappedTextCapture(
                request.StderrLimitCharacters,
                request.StderrCaptureMode);
            stdoutTask = isDuplex
                ? Task.CompletedTask
                : ReadStreamAsync(
                    process.StandardOutput,
                    stdoutCapture,
                    stdoutReadCancellation.Token);
            stderrTask = ReadStreamAsync(
                process.StandardError,
                stderrCapture,
                stderrReadCancellation.Token);
            standardInputTask = request.StandardInput is null
                ? Task.CompletedTask
                : WriteStandardInputAsync(
                    process,
                    request.StandardInput,
                    standardInputCancellation.Token);
        }

        public WorkspaceOwnedProcessIdentity Identity { get; }

        public bool HasExited => LocalWorkspaceProcessHost.HasExited(process);

        public Stream StandardInput => isDuplex
            ? process.StandardInput.BaseStream
            : throw new InvalidOperationException("Captured process sessions do not expose standard input.");

        public Stream StandardOutput => isDuplex
            ? process.StandardOutput.BaseStream
            : throw new InvalidOperationException("Captured process sessions do not expose standard output.");

        public void CompleteStandardInput()
        {
            if (!isDuplex)
            {
                throw new InvalidOperationException("Captured process sessions do not expose standard input.");
            }

            TryCloseStandardInput(process);
        }

        public WorkspaceProcessOutputSnapshot CaptureOutput()
        {
            var stdout = stdoutCapture.Snapshot();
            var stderr = stderrCapture.Snapshot();
            return new WorkspaceProcessOutputSnapshot(
                stdout.Content,
                stderr.Content,
                stdout.Truncated,
                stderr.Truncated);
        }

        public async Task<WorkspaceProcessExecutionResult> WaitForExitAsync(
            CancellationToken cancellationToken = default)
        {
            await WaitForProcessExitOnlyAsync(process, cancellationToken).ConfigureAwait(false);
            var ownershipEmpty = await EnsureOwnershipEmptyAsync().ConfigureAwait(false);
            return await CompleteAsync(
                ownershipEmpty
                    ? WorkspaceProcessTerminationReason.Completed
                    : WorkspaceProcessTerminationReason.TerminationFailed,
                ownershipEmpty
                    ? string.Empty
                    : "The root process exited, but its owned process boundary could not be verified empty.",
                timedOut: false,
                residualProcessPossible: !ownershipEmpty).ConfigureAwait(false);
        }

        public async Task<WorkspaceProcessExecutionResult> TerminateAsync(
            WorkspaceProcessTerminationReason reason,
            string failureMessage,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            var exited = await TerminateProcessTreeAsync(
                process,
                ownership,
                terminationMode).ConfigureAwait(false);
            var residual = !exited;
            if (residual)
            {
                reason = WorkspaceProcessTerminationReason.TerminationFailed;
                failureMessage = string.IsNullOrWhiteSpace(failureMessage)
                    ? "Process termination could not be confirmed; a residual process may remain."
                    : $"{failureMessage} Process termination could not be confirmed; a residual process may remain.";
            }

            return await CompleteAsync(
                reason,
                failureMessage,
                timedOut: reason == WorkspaceProcessTerminationReason.TimedOut,
                residual).ConfigureAwait(false);
        }

        public WorkspaceOwnedProcessIdentity Detach()
        {
            if (Interlocked.Exchange(ref detached, 1) == 0)
            {
                _ = ObserveDetachedCompletionAsync();
            }

            return Identity;
        }

        public async ValueTask DisposeAsync()
        {
            if (Volatile.Read(ref detached) != 0 || finalResult is not null)
            {
                return;
            }

            await TerminateAsync(
                WorkspaceProcessTerminationReason.CallerCanceled,
                "The process session was disposed before normal completion.",
                CancellationToken.None).ConfigureAwait(false);
        }

        private async Task ObserveDetachedCompletionAsync()
        {
            try
            {
                await WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private async Task<bool> EnsureOwnershipEmptyAsync()
        {
            if (await ownership.WaitForEmptyAsync(TimeSpan.Zero).ConfigureAwait(false))
            {
                return true;
            }

            return ownership.ForceTerminate() &&
                   await ownership.WaitForEmptyAsync(TerminationTimeout).ConfigureAwait(false);
        }

        private async Task<WorkspaceProcessExecutionResult> CompleteAsync(
            WorkspaceProcessTerminationReason reason,
            string failureMessage,
            bool timedOut,
            bool residualProcessPossible)
        {
            if (finalResult is not null)
            {
                return finalResult;
            }

            await completionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (finalResult is not null)
                {
                    return finalResult;
                }

                standardInputCancellation.Cancel();
                if (isDuplex)
                {
                    TryCloseStandardInput(process);
                }

                await CompleteStandardInputAsync(standardInputTask, process).ConfigureAwait(false);
                var stdoutCompletionTask = CompleteStreamReadAsync(
                    stdoutTask,
                    stdoutCapture,
                    stdoutReadCancellation);
                var stderrCompletionTask = CompleteStreamReadAsync(
                    stderrTask,
                    stderrCapture,
                    stderrReadCancellation);
                await Task.WhenAll(stdoutCompletionTask, stderrCompletionTask).ConfigureAwait(false);
                var stdout = await stdoutCompletionTask.ConfigureAwait(false);
                var stderr = await stderrCompletionTask.ConfigureAwait(false);
                finalResult = new WorkspaceProcessExecutionResult(
                    Started: true,
                    ExitCode: HasExited && reason == WorkspaceProcessTerminationReason.Completed
                        ? process.ExitCode
                        : -1,
                    Stdout: stdout.Content,
                    Stderr: stderr.Content,
                    StdoutTruncated: stdout.Truncated,
                    StderrTruncated: stderr.Truncated,
                    StartedAtUtc: startedAtUtc,
                    CompletedAtUtc: DateTimeOffset.UtcNow,
                    TimedOut: timedOut,
                    Boundary: Boundary,
                    FailureMessage: failureMessage,
                    TerminationReason: reason,
                    ResidualProcessPossible: residualProcessPossible);
                process.Dispose();
                ownership.Dispose();
                stdoutReadCancellation.Dispose();
                stderrReadCancellation.Dispose();
                standardInputCancellation.Dispose();
                return finalResult;
            }
            finally
            {
                completionGate.Release();
            }
        }
    }

    private sealed class CappedTextCapture
    {
        private readonly StringBuilder builder = new();
        private readonly object sync = new();
        private readonly int safeLimit;
        private readonly WorkspaceProcessTextCaptureMode captureMode;
        private bool truncated;

        public CappedTextCapture(
            int limit,
            WorkspaceProcessTextCaptureMode captureMode = WorkspaceProcessTextCaptureMode.Prefix)
        {
            safeLimit = Math.Clamp(limit, 256, 1024 * 1024);
            this.captureMode = captureMode;
        }

        public void Append(char[] buffer, int read)
        {
            lock (sync)
            {
                if (captureMode == WorkspaceProcessTextCaptureMode.Tail)
                {
                    AppendTail(buffer, read);
                    return;
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
        }

        private void AppendTail(char[] buffer, int read)
        {
            if (read >= safeLimit)
            {
                builder.Clear();
                builder.Append(buffer, read - safeLimit, safeLimit);
                truncated = true;
                return;
            }

            var overflow = builder.Length + read - safeLimit;
            if (overflow > 0)
            {
                builder.Remove(0, overflow);
                truncated = true;
            }

            builder.Append(buffer, 0, read);
        }

        public void MarkTruncated()
        {
            lock (sync)
            {
                truncated = true;
            }
        }

        public CappedTextResult Snapshot()
        {
            lock (sync)
            {
                return new CappedTextResult(builder.ToString().Trim(), truncated);
            }
        }
    }

    private sealed record CappedTextResult(string Content, bool Truncated);
}
