using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class LocalWorkspaceProcessHostTests
{
    [Fact]
    public void Dotnet_run_lifecycle_uses_the_typed_host_without_generated_shell_launchers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var builderPath = Path.Combine(
            repositoryRoot,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Core",
            "Workspace",
            "Commands",
            "WorkspaceCommandPlanBuilder.cs");
        var source = File.ReadAllText(builderPath);
        var runStart = source.IndexOf("public WorkspaceCommandPlan BuildDotnetRun(", StringComparison.Ordinal);
        var stopStart = source.IndexOf("public WorkspaceCommandPlan BuildDotnetStop(", runStart, StringComparison.Ordinal);

        Assert.True(runStart >= 0);
        Assert.True(stopStart > runStart);
        var runSource = source[runStart..stopStart];
        Assert.Contains("executableCandidates: [\"dotnet\"]", runSource, StringComparison.Ordinal);
        Assert.Contains("WorkspaceDotnetRunLifecyclePlan", runSource, StringComparison.Ordinal);
        Assert.DoesNotContain("pwsh", runSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell", runSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-File", runSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildDotnetHttpRunPowerShellScript", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildDotnetStopPowerShellScript", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detached_session_can_be_terminated_by_recorded_identity_from_a_new_host()
    {
        var host = new LocalWorkspaceProcessHost();
        var session = await host.StartSessionAsync(CreateSessionRequest(BuildWaitCommand(30)));
        var identity = session.Detach();
        var persistedIdentity = JsonSerializer.Deserialize<WorkspaceOwnedProcessIdentity>(
            JsonSerializer.Serialize(identity))
            ?? throw new InvalidOperationException("The owned-process identity did not round-trip.");

        try
        {
            Assert.True(IsProcessRunning(identity.ProcessId));

            var termination = await new LocalWorkspaceProcessHost()
                .TerminateOwnedProcessAsync(persistedIdentity);

            Assert.True(
                termination.Status == WorkspaceProcessTerminationStatus.Terminated,
                termination.Message);
            Assert.False(termination.ResidualProcessPossible);
            Assert.True(SpinWait.SpinUntil(
                () => !IsProcessRunning(identity.ProcessId),
                TimeSpan.FromSeconds(5)));

            var repeatedTermination = await new LocalWorkspaceProcessHost()
                .TerminateOwnedProcessAsync(persistedIdentity);

            Assert.Equal(WorkspaceProcessTerminationStatus.AlreadyExited, repeatedTermination.Status);
            Assert.False(repeatedTermination.ResidualProcessPossible);
        }
        finally
        {
            await session.DisposeAsync();
            TryKillProcess(identity.ProcessId);
        }
    }

    [Fact]
    public async Task TerminateOwnedProcessAsync_does_not_kill_a_process_with_subsecond_mismatched_identity()
    {
        var host = new LocalWorkspaceProcessHost();
        await using var session = await host.StartSessionAsync(CreateSessionRequest(BuildWaitCommand(30)));
        var mismatchedIdentity = session.Identity with
        {
            StartedAtUtc = session.Identity.StartedAtUtc.AddMilliseconds(-250)
        };

        var termination = await new LocalWorkspaceProcessHost()
            .TerminateOwnedProcessAsync(mismatchedIdentity);

        Assert.Equal(WorkspaceProcessTerminationStatus.IdentityMismatch, termination.Status);
        Assert.True(termination.ResidualProcessPossible);
        Assert.True(IsProcessRunning(session.Identity.ProcessId));
    }

    [Fact]
    public async Task Disposing_an_attached_session_terminates_its_owned_process()
    {
        var host = new LocalWorkspaceProcessHost();
        var session = await host.StartSessionAsync(CreateSessionRequest(BuildWaitCommand(30)));
        var processId = session.Identity.ProcessId;

        await session.DisposeAsync();

        Assert.True(SpinWait.SpinUntil(
            () => !IsProcessRunning(processId),
            TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Manager_session_requests_graceful_unix_termination_before_force_fallback()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var markerPath = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.LocalWorkspaceProcessHostTests.{Guid.NewGuid():N}.term");
        var command = $"trap 'printf terminated > {EscapeShellArgument(markerPath)}; exit 0' TERM; while :; do :; done";
        var request = CreateSessionRequest(command) with
        {
            TerminationMode = WorkspaceProcessTerminationMode.GracefulThenForceTree
        };
        var host = new LocalWorkspaceProcessHost();
        await using var session = await host.StartSessionAsync(request);

        try
        {
            var termination = await session.TerminateAsync(
                WorkspaceProcessTerminationReason.CallerCanceled,
                "test termination");

            Assert.False(termination.ResidualProcessPossible);
            Assert.True(File.Exists(markerPath));
        }
        finally
        {
            TryDeleteFile(markerPath);
        }
    }

    [Fact]
    public async Task Graceful_unix_termination_forces_surviving_child_after_root_exits()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var childPidFilePath = CreateChildPidFilePath();
        var command =
            $"trap 'exit 0' TERM; (trap '' TERM; while :; do sleep 1; done) & child_pid=$!; " +
            $"printf '%s' \"$child_pid\" > {EscapeShellArgument(childPidFilePath)}; " +
            "while :; do sleep 1; done";
        var request = CreateSessionRequest(command) with
        {
            TerminationMode = WorkspaceProcessTerminationMode.GracefulThenForceTree
        };
        var host = new LocalWorkspaceProcessHost();
        await using var session = await host.StartSessionAsync(request);

        try
        {
            Assert.True(
                SpinWait.SpinUntil(() => File.Exists(childPidFilePath), TimeSpan.FromSeconds(5)),
                "Expected the child PID to be published before termination.");

            var result = await session.TerminateAsync(
                WorkspaceProcessTerminationReason.CallerCanceled,
                "test termination");

            Assert.False(result.ResidualProcessPossible);
            AssertChildExited(childPidFilePath);
        }
        finally
        {
            TryKillProcessFromFile(childPidFilePath);
            TryDeleteFile(childPidFilePath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_writes_standard_input_and_closes_the_stream()
    {
        var host = new LocalWorkspaceProcessHost();
        var request = CreateProcessRequest(
            OperatingSystem.IsWindows() ? "[Console]::In.ReadToEnd()" : "cat",
            timeoutSeconds: 10,
            standardInput: "portable-input");

        var result = await host.ExecuteAsync(request);

        Assert.True(result.Started);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("portable-input", result.Stdout);
        Assert.Equal(WorkspaceProcessTerminationReason.Completed, result.TerminationReason);
        Assert.False(result.ResidualProcessPossible);
    }

    [Fact]
    public async Task Duplex_session_exposes_protocol_streams_while_the_host_owns_lifecycle_and_stderr()
    {
        var host = new LocalWorkspaceProcessHost();
        var request = CreateSessionRequest(
            OperatingSystem.IsWindows() ? "[Console]::In.ReadLine()" : "cat") with
        {
            StandardIoMode = WorkspaceProcessStandardIoMode.Duplex
        };
        await using var baseSession = await host.StartSessionAsync(request);
        var session = Assert.IsAssignableFrom<IWorkspaceDuplexProcessSession>(baseSession);

        await session.StandardInput.WriteAsync("portable-duplex\n"u8.ToArray());
        await session.StandardInput.FlushAsync();
        session.CompleteStandardInput();
        using var reader = new StreamReader(session.StandardOutput, Encoding.UTF8);
        var stdout = await reader.ReadToEndAsync(CancellationToken.None);
        var result = await session.WaitForExitAsync(CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("portable-duplex", stdout, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.False(result.ResidualProcessPossible);
    }

    [Fact]
    public async Task Duplex_session_can_retain_a_bounded_stderr_tail()
    {
        var host = new LocalWorkspaceProcessHost();
        var padding = new string('x', 400);
        var command = OperatingSystem.IsWindows()
            ? $"[Console]::Error.Write('prefix-{padding}-tail-marker')"
            : $"printf '%s' 'prefix-{padding}-tail-marker' >&2";
        var request = CreateSessionRequest(command) with
        {
            StandardIoMode = WorkspaceProcessStandardIoMode.Duplex,
            StderrLimitCharacters = 256,
            StderrCaptureMode = WorkspaceProcessTextCaptureMode.Tail
        };
        await using var session = await host.StartSessionAsync(request);

        var result = await session.WaitForExitAsync(CancellationToken.None);

        Assert.True(result.StderrTruncated);
        Assert.DoesNotContain("prefix-", result.Stderr, StringComparison.Ordinal);
        Assert.EndsWith("-tail-marker", result.Stderr, StringComparison.Ordinal);
        Assert.False(result.ResidualProcessPossible);
    }

    [Fact]
    public async Task ExecuteAsync_reports_timeout_and_kills_the_process_tree()
    {
        var childPidFilePath = CreateChildPidFilePath();
        var host = new LocalWorkspaceProcessHost();
        var request = CreateProcessRequest(
            BuildChildAndWaitCommand(childPidFilePath),
            timeoutSeconds: 1);

        try
        {
            var result = await host.ExecuteAsync(request);

            Assert.True(result.Started);
            Assert.True(result.TimedOut);
            Assert.Equal(WorkspaceProcessTerminationReason.TimedOut, result.TerminationReason);
            Assert.False(result.ResidualProcessPossible);
            AssertChildExited(childPidFilePath);
        }
        finally
        {
            TryKillProcessFromFile(childPidFilePath);
            TryDeleteFile(childPidFilePath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_reports_caller_cancellation_and_kills_the_process_tree()
    {
        var childPidFilePath = CreateChildPidFilePath();
        var host = new LocalWorkspaceProcessHost();
        var request = CreateProcessRequest(
            BuildChildAndWaitCommand(childPidFilePath),
            timeoutSeconds: 20);
        using var cancellation = new CancellationTokenSource();

        try
        {
            var execution = host.ExecuteAsync(request, cancellation.Token);
            Assert.True(
                SpinWait.SpinUntil(() => File.Exists(childPidFilePath), TimeSpan.FromSeconds(5)),
                "Expected the child PID to be published before cancellation.");
            cancellation.Cancel();

            var result = await execution.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(result.Started);
            Assert.False(result.TimedOut);
            Assert.Equal(WorkspaceProcessTerminationReason.CallerCanceled, result.TerminationReason);
            Assert.False(result.ResidualProcessPossible);
            AssertChildExited(childPidFilePath);
        }
        finally
        {
            TryKillProcessFromFile(childPidFilePath);
            TryDeleteFile(childPidFilePath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_terminates_descendant_after_parent_exits()
    {
        var host = new LocalWorkspaceProcessHost();
        var childSleepSeconds = 30;
        var childPidFilePath = Path.Combine(Path.GetTempPath(), $"CanDoItAll.LocalWorkspaceProcessHostTests.{Guid.NewGuid():N}.pid");
        var command = OperatingSystem.IsWindows()
            ? BuildWindowsDetachedChildCommand(childPidFilePath, childSleepSeconds)
            : BuildPortableDetachedChildCommand(childPidFilePath, childSleepSeconds);
        var request = CreateProcessRequest(command, timeoutSeconds: 20);

        Task<WorkspaceProcessExecutionResult>? executionTask = null;

        try
        {
            var stopwatch = Stopwatch.StartNew();
            executionTask = host.ExecuteAsync(request);
            var result = await executionTask.WaitAsync(TimeSpan.FromSeconds(12));
            stopwatch.Stop();

            Assert.True(result.Started);
            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("parent-done", result.Stdout, StringComparison.Ordinal);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(12),
                $"Expected the host to return before the child released the inherited pipe. Elapsed: {stopwatch.Elapsed}.");
            Assert.False(result.ResidualProcessPossible);
            AssertChildExited(childPidFilePath);
        }
        finally
        {
            TryKillProcessFromFile(childPidFilePath);

            if (executionTask is not null)
            {
                try
                {
                    await executionTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                }
            }

            try
            {
                File.Delete(childPidFilePath);
            }
            catch
            {
            }
        }
    }

    private static void TryKillProcessFromFile(string childPidFilePath)
    {
        if (!File.Exists(childPidFilePath))
        {
            return;
        }

        try
        {
            var childPidText = File.ReadAllText(childPidFilePath).Trim();
            if (!int.TryParse(childPidText, out var childPid))
            {
                return;
            }

            var childProcess = Process.GetProcessById(childPid);
            childProcess.Kill(entireProcessTree: true);
            childProcess.WaitForExit(5000);
        }
        catch
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the CanDoItAll repository root.");
    }

    private static WorkspaceProcessExecutionRequest CreateProcessRequest(
        string command,
        int timeoutSeconds,
        string? standardInput = null)
    {
        var executablePath = OperatingSystem.IsWindows() ? "pwsh" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? new[]
            {
                "-NoLogo",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                command
            }
            : new[] { "-c", command };
        return new(
            ToolName: "workspace_pwsh_run_script",
            RecipeId: "pwsh_run_script",
            ExecutablePath: executablePath,
            Arguments: arguments,
            WorkingDirectory: Path.GetTempPath(),
            EnvironmentVariables: new Dictionary<string, string?>(),
            TimeoutSeconds: timeoutSeconds,
            StdoutLimitCharacters: 4096,
            StderrLimitCharacters: 4096,
            StandardInput: standardInput);
    }

    private static WorkspaceProcessSessionRequest CreateSessionRequest(string command)
    {
        var executionRequest = CreateProcessRequest(command, timeoutSeconds: 30);
        var executablePath = new WorkspaceExecutableLocator().ResolveExecutablePath(
            [executionRequest.ExecutablePath],
            executionRequest.WorkingDirectory);
        return new WorkspaceProcessSessionRequest(
            executionRequest.ToolName,
            executionRequest.RecipeId,
            executablePath,
            executionRequest.Arguments,
            executionRequest.WorkingDirectory,
            executionRequest.EnvironmentVariables,
            executionRequest.StdoutLimitCharacters,
            executionRequest.StderrLimitCharacters,
            executionRequest.StandardInput);
    }

    private static string BuildWaitCommand(int seconds)
        => OperatingSystem.IsWindows()
            ? $"Start-Sleep -Seconds {seconds}"
            : $"sleep {seconds}; :";

    private static string CreateChildPidFilePath()
        => Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.LocalWorkspaceProcessHostTests.{Guid.NewGuid():N}.pid");

    private static string BuildChildAndWaitCommand(string childPidFilePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            var escapedUnixPidPath = EscapeShellArgument(childPidFilePath);
            return $"sleep 30 & child_pid=$!; printf '%s' \"$child_pid\" > {escapedUnixPidPath}; wait \"$child_pid\"";
        }

        var escapedPidPath = childPidFilePath.Replace("'", "''");
        return string.Join(
            Environment.NewLine,
            [
                "$startInfo = [System.Diagnostics.ProcessStartInfo]::new()",
                "$startInfo.FileName = (Get-Process -Id $PID).Path",
                "$startInfo.UseShellExecute = $false",
                "$startInfo.CreateNoWindow = $true",
                "$null = $startInfo.ArgumentList.Add('-NoLogo')",
                "$null = $startInfo.ArgumentList.Add('-NoProfile')",
                "$null = $startInfo.ArgumentList.Add('-NonInteractive')",
                "$null = $startInfo.ArgumentList.Add('-Command')",
                "$null = $startInfo.ArgumentList.Add('Start-Sleep -Seconds 30')",
                "$child = [System.Diagnostics.Process]::Start($startInfo)",
                $"Set-Content -LiteralPath '{escapedPidPath}' -Value $child.Id -NoNewline",
                "$child.WaitForExit()"
            ]);
    }

    private static void AssertChildExited(string childPidFilePath)
    {
        Assert.True(File.Exists(childPidFilePath), "Expected the child PID file to exist.");
        Assert.True(int.TryParse(File.ReadAllText(childPidFilePath).Trim(), out var childPid));
        Assert.True(
            SpinWait.SpinUntil(
                () => !IsProcessRunning(childPid),
                TimeSpan.FromSeconds(5)),
            $"Expected child process {childPid} to be terminated.");
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TryKillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static string BuildWindowsDetachedChildCommand(string childPidFilePath, int childSleepSeconds)
    {
        var escapedChildPidFilePath = childPidFilePath.Replace("'", "''");
        var childCommand = $"Set-Content -LiteralPath '{escapedChildPidFilePath}' -Value $PID -NoNewline; Write-Output 'child-start'; Start-Sleep -Seconds {childSleepSeconds}";
        var encodedChildCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(childCommand));

        return string.Join(
            Environment.NewLine,
            [
                "$childPwshPath = Join-Path $PSHOME 'pwsh.exe'",
                "if (-not (Test-Path -LiteralPath $childPwshPath)) {",
                "    $childPwshPath = 'pwsh'",
                "}",
                "$cmdPath = $env:ComSpec",
                "if ([string]::IsNullOrWhiteSpace($cmdPath)) {",
                "    $cmdPath = 'cmd.exe'",
                "}",
                "$cmdStartInfo = [System.Diagnostics.ProcessStartInfo]::new()",
                "$cmdStartInfo.FileName = $cmdPath",
                "$cmdStartInfo.UseShellExecute = $false",
                "$cmdStartInfo.RedirectStandardOutput = $false",
                "$cmdStartInfo.RedirectStandardError = $false",
                "$cmdStartInfo.CreateNoWindow = $true",
                "$null = $cmdStartInfo.ArgumentList.Add('/c')",
                "$null = $cmdStartInfo.ArgumentList.Add('start')",
                "$null = $cmdStartInfo.ArgumentList.Add('\"\"')",
                "$null = $cmdStartInfo.ArgumentList.Add('/b')",
                "$null = $cmdStartInfo.ArgumentList.Add($childPwshPath)",
                "$null = $cmdStartInfo.ArgumentList.Add('-NoLogo')",
                "$null = $cmdStartInfo.ArgumentList.Add('-NoProfile')",
                "$null = $cmdStartInfo.ArgumentList.Add('-NonInteractive')",
                "$null = $cmdStartInfo.ArgumentList.Add('-EncodedCommand')",
                $"$null = $cmdStartInfo.ArgumentList.Add('{encodedChildCommand}')",
                "$cmdProcess = [System.Diagnostics.Process]::Start($cmdStartInfo)",
                "if ($cmdProcess -ne $null) {",
                "    $cmdProcess.WaitForExit(5000) | Out-Null",
                "}",
                $"for ($attempt = 0; $attempt -lt 100 -and -not (Test-Path -LiteralPath '{escapedChildPidFilePath}'); $attempt++) {{",
                "    Start-Sleep -Milliseconds 10",
                "}",
                "Write-Output 'parent-done'"
            ]);
    }

    private static string BuildPortableDetachedChildCommand(string childPidFilePath, int childSleepSeconds)
        => $"sleep {childSleepSeconds} & child_pid=$!; printf '%s' \"$child_pid\" > {EscapeShellArgument(childPidFilePath)}; printf 'parent-done\\n'";

    private static string EscapeShellArgument(string value)
        => "'" + value.Replace("'", "'\"'\"'") + "'";
}
