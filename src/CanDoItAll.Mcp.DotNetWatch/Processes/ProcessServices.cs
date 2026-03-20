using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Logging;
using CanDoItAll.Mcp.DotNetWatch.Persistence;
using CanDoItAll.Mcp.DotNetWatch.Runtime;

namespace CanDoItAll.Mcp.DotNetWatch.Processes;

public sealed record ManagedProcessStartInfo(
    string OwnerKind,
    string OwnerId,
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string CorrelationId,
    int? SessionVersion);

public sealed record ProcessStopResult(bool Graceful, IReadOnlyList<int> KilledPids, int? ExitCode);

public interface IProcessTreeTerminator
{
    Task<ProcessStopResult> TerminateAsync(Process process, bool force, CancellationToken cancellationToken);
}

public interface IProcessCommandRunner
{
    Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    Task<string> RunCaptureAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

public sealed class ProcessCommandRunner : IProcessCommandRunner
{
    public async Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(fileName, arguments);
        if (!process.Start())
        {
            throw new ToolInvocationException("ProcessStartFailed", $"Failed to start helper process '{fileName}'.");
        }

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    public async Task<string> RunCaptureAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(fileName, arguments);
        if (!process.Start())
        {
            throw new ToolInvocationException("ProcessStartFailed", $"Failed to start helper process '{fileName}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)
                ? $"Helper process '{fileName}' exited with code {process.ExitCode}."
                : stderr.Trim());
        }

        return stdout;
    }

    private static Process CreateProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process
        {
            StartInfo = startInfo
        };
    }
}

public interface IPlatformProcessTreeTerminator
{
    Task RequestGracefulStopAsync(Process process, CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> KillTreeAsync(Process process, CancellationToken cancellationToken);
}

public sealed class WindowsProcessTreeTerminator(IProcessCommandRunner commandRunner, ILogger<WindowsProcessTreeTerminator> logger) : IPlatformProcessTreeTerminator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RequestGracefulStopAsync(Process process, CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return;
        }

        if (process.CloseMainWindow())
        {
            return;
        }

        var exitCode = await commandRunner.RunAsync(
            "taskkill",
            ["/PID", process.Id.ToString(CultureInfo.InvariantCulture), "/T"],
            cancellationToken);

        if (exitCode != 0)
        {
            logger.LogDebug("Graceful Windows taskkill returned exit code {ExitCode} for PID {Pid}", exitCode, process.Id);
        }
    }

    public async Task<IReadOnlyList<int>> KillTreeAsync(Process process, CancellationToken cancellationToken)
    {
        var pids = await TryGetProcessTreeAsync(process.Id, cancellationToken);
        var exitCode = await commandRunner.RunAsync(
            "taskkill",
            ["/PID", process.Id.ToString(CultureInfo.InvariantCulture), "/T", "/F"],
            cancellationToken);

        if (exitCode != 0 && IsStillRunning(process))
        {
            throw new InvalidOperationException($"taskkill /F failed with exit code {exitCode} for PID {process.Id}.");
        }

        return pids.Count == 0 ? [process.Id] : pids;
    }

    private async Task<IReadOnlyList<int>> TryGetProcessTreeAsync(int rootPid, CancellationToken cancellationToken)
    {
        try
        {
            var script = $$"""
                $root={{rootPid}}
                $all = New-Object System.Collections.Generic.List[int]
                function Add-Descendants([int]$pid) {
                    $all.Add($pid) | Out-Null
                    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $pid" | Select-Object -ExpandProperty ProcessId
                    foreach ($child in $children) {
                        Add-Descendants([int]$child)
                    }
                }
                Add-Descendants $root
                $all | ConvertTo-Json -Compress
                """;
            var output = await commandRunner.RunCaptureAsync(
                "powershell",
                ["-NoProfile", "-NonInteractive", "-Command", script],
                cancellationToken);

            var parsed = JsonSerializer.Deserialize<List<int>>(output, JsonOptions);
            return parsed?.Distinct().ToArray() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to snapshot Windows process tree for PID {Pid}", rootPid);
            return [];
        }
    }

    private static bool IsStillRunning(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        try
        {
            using var current = Process.GetProcessById(process.Id);
            return !current.HasExited;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class UnixProcessTreeTerminator(IProcessCommandRunner commandRunner, ILogger<UnixProcessTreeTerminator> logger) : IPlatformProcessTreeTerminator
{
    public async Task RequestGracefulStopAsync(Process process, CancellationToken cancellationToken)
    {
        foreach (var pid in (await GetProcessTreeAsync(process.Id, cancellationToken)).Reverse())
        {
            try
            {
                await commandRunner.RunAsync("kill", ["-TERM", pid.ToString(CultureInfo.InvariantCulture)], cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to send SIGTERM to PID {Pid}", pid);
            }
        }
    }

    public async Task<IReadOnlyList<int>> KillTreeAsync(Process process, CancellationToken cancellationToken)
    {
        var pids = await GetProcessTreeAsync(process.Id, cancellationToken);
        foreach (var pid in pids.Reverse())
        {
            try
            {
                await commandRunner.RunAsync("kill", ["-KILL", pid.ToString(CultureInfo.InvariantCulture)], cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to send SIGKILL to PID {Pid}", pid);
            }
        }

        return pids.Count == 0 ? [process.Id] : pids;
    }

    private async Task<IReadOnlyList<int>> GetProcessTreeAsync(int rootPid, CancellationToken cancellationToken)
    {
        try
        {
            var descendants = new List<int>();
            await AddDescendantsAsync(rootPid, descendants, cancellationToken);
            descendants.Insert(0, rootPid);
            return descendants.Distinct().ToArray();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to snapshot Unix process tree for PID {Pid}", rootPid);
            return [];
        }
    }

    private async Task AddDescendantsAsync(int pid, List<int> descendants, CancellationToken cancellationToken)
    {
        var output = await commandRunner.RunCaptureAsync("pgrep", ["-P", pid.ToString(CultureInfo.InvariantCulture)], cancellationToken);
        var childPids = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : (int?)null)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToArray();

        foreach (var childPid in childPids)
        {
            descendants.Add(childPid);
            await AddDescendantsAsync(childPid, descendants, cancellationToken);
        }
    }
}

public sealed class ProcessTreeTerminator(
    RuntimeConfiguration configuration,
    IPlatformProcessTreeTerminator platformTerminator,
    ILogger<ProcessTreeTerminator> logger) : IProcessTreeTerminator
{
    public async Task<ProcessStopResult> TerminateAsync(Process process, bool force, CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return new ProcessStopResult(true, [], process.ExitCode);
        }

        var graceful = false;
        if (!force)
        {
            await platformTerminator.RequestGracefulStopAsync(process, cancellationToken);
            graceful = await WaitForExitAsync(process, configuration.GracefulStopTimeout, cancellationToken);
        }

        IReadOnlyList<int> killedPids = [];
        if (!graceful && !process.HasExited)
        {
            killedPids = await platformTerminator.KillTreeAsync(process, cancellationToken);
            var forceExited = await WaitForExitAsync(process, configuration.ForceKillAfter, cancellationToken);
            if (!forceExited && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Fallback process kill failed for PID {Pid}", process.Id);
                }

                forceExited = await WaitForExitAsync(process, TimeSpan.FromSeconds(2), cancellationToken);
                if (!forceExited && !process.HasExited)
                {
                    logger.LogWarning("Process {Pid} did not exit within the configured force-kill timeout.", process.Id);
                }
            }
        }

        return new ProcessStopResult(graceful, killedPids, process.HasExited ? process.ExitCode : null);
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return true;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return process.HasExited;
        }
    }
}

internal static class ManagedProcessMarkers
{
    private const string OwnerKindProperty = "CanDoItAllMcpOwnerKind";
    private const string OwnerIdProperty = "CanDoItAllMcpOwnerId";
    private const string WorkspaceRootProperty = "CanDoItAllMcpWorkspaceRoot";
    private const string ServerInstanceIdProperty = "CanDoItAllMcpServerInstanceId";

    public static IReadOnlyList<string> CreateApplicationArguments(
        string ownerKind,
        string ownerId,
        string workspaceRoot,
        string serverInstanceId)
    {
        return
        [
            $"--{OwnerKindProperty}={ownerKind}",
            $"--{OwnerIdProperty}={ownerId}",
            $"--{WorkspaceRootProperty}={workspaceRoot}",
            $"--{ServerInstanceIdProperty}={serverInstanceId}"
        ];
    }

    public static IReadOnlyList<string> CreateMsBuildPropertyArguments(
        string ownerKind,
        string ownerId,
        string workspaceRoot,
        string serverInstanceId)
    {
        return
        [
            $"-p:{OwnerKindProperty}={ownerKind}",
            $"-p:{OwnerIdProperty}={ownerId}",
            $"-p:{WorkspaceRootProperty}={workspaceRoot}",
            $"-p:{ServerInstanceIdProperty}={serverInstanceId}"
        ];
    }

    public static bool CommandLineMatches(string commandLine, ManagedProcessRecord record)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        var normalizedCommandLine = commandLine.Replace("\"", string.Empty, StringComparison.Ordinal);
        if (!normalizedCommandLine.Contains(record.Command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var appArguments = CreateApplicationArguments(record.OwnerKind, record.OwnerId, record.WorkspaceRoot, record.RegisteredByServerInstanceId);
        if (appArguments.All(argument => normalizedCommandLine.Contains(argument, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var msBuildArguments = CreateMsBuildPropertyArguments(record.OwnerKind, record.OwnerId, record.WorkspaceRoot, record.RegisteredByServerInstanceId);
        return msBuildArguments.All(argument => normalizedCommandLine.Contains(argument, StringComparison.OrdinalIgnoreCase));
    }

    public static bool RecordContainsOwnershipMarkers(ManagedProcessRecord record)
    {
        var arguments = record.Arguments
            .Select(static argument => argument.Replace("\"", string.Empty, StringComparison.Ordinal))
            .ToArray();

        var appArguments = CreateApplicationArguments(record.OwnerKind, record.OwnerId, record.WorkspaceRoot, record.RegisteredByServerInstanceId);
        if (appArguments.All(arguments.Contains))
        {
            return true;
        }

        var msBuildArguments = CreateMsBuildPropertyArguments(record.OwnerKind, record.OwnerId, record.WorkspaceRoot, record.RegisteredByServerInstanceId);
        return msBuildArguments.All(arguments.Contains);
    }
}

public sealed class ManagedProcess
{
    private readonly Process _process;
    private readonly IProcessTreeTerminator _terminator;
    private readonly TaskCompletionSource<int?> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ManagedProcess(Process process, IProcessTreeTerminator terminator)
    {
        _process = process;
        _terminator = terminator;
    }

    public int Pid => _process.Id;

    public Task<int?> Completion => _completionSource.Task;

    public void Complete(int? exitCode)
    {
        _completionSource.TrySetResult(exitCode);
    }

    public Task<ProcessStopResult> StopAsync(bool force, CancellationToken cancellationToken)
    {
        return _terminator.TerminateAsync(_process, force, cancellationToken);
    }
}

public sealed class ProcessSupervisor(
    RuntimeConfiguration configuration,
    ServerInstanceIdentity serverInstanceIdentity,
    StaleProcessRegistry staleProcessRegistry,
    IProcessTreeTerminator terminator,
    FileLogStore fileLogStore,
    LogRedactor logRedactor,
    ILogger<ProcessSupervisor> logger)
{
    public async Task<ManagedProcess> StartAsync(
        ManagedProcessStartInfo startInfo,
        RingLogBuffer logBuffer,
        Func<LogEntry, Task>? onLogAsync,
        Func<int?, Task>? onExitAsync,
        CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo(startInfo.Command)
        {
            WorkingDirectory = startInfo.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in startInfo.Arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in startInfo.EnvironmentVariables)
        {
            processStartInfo.Environment[variable.Key] = variable.Value;
        }

        ClearInheritedHostVariables(processStartInfo);

        var process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new ToolInvocationException("ProcessStartFailed", $"Failed to start process '{startInfo.Command}'.");
        }

        var managedProcess = new ManagedProcess(process, terminator);
        var startedUtc = TryGetStartedUtc(process) ?? DateTimeOffset.UtcNow;

        await staleProcessRegistry.RegisterAsync(
            new ManagedProcessRecord(
                process.Id,
                startedUtc,
                startInfo.Command,
                startInfo.Arguments,
                startInfo.WorkingDirectory,
                configuration.WorkspaceRoot,
                startInfo.OwnerKind,
                startInfo.OwnerId,
                serverInstanceIdentity.Id),
            cancellationToken);

        var startupEntry = logBuffer.Append("System", null, startInfo.SessionVersion, startInfo.CorrelationId, $"Started process {process.Id}: {startInfo.Command} {string.Join(' ', startInfo.Arguments)}");
        fileLogStore.Append(startInfo.OwnerKind, startInfo.OwnerId, startupEntry);
        if (onLogAsync is not null)
        {
            await onLogAsync(startupEntry);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var stdoutTask = ReadStreamAsync(process.StandardOutput, "ProcessStdOut", "stdout", startInfo, logBuffer, onLogAsync, CancellationToken.None);
                var stderrTask = ReadStreamAsync(process.StandardError, "ProcessStdErr", "stderr", startInfo, logBuffer, onLogAsync, CancellationToken.None);
                await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(CancellationToken.None));
                await staleProcessRegistry.UnregisterAsync(process.Id, CancellationToken.None);

                var exitEntry = logBuffer.Append("System", null, startInfo.SessionVersion, startInfo.CorrelationId, $"Process {process.Id} exited with code {process.ExitCode}.");
                fileLogStore.Append(startInfo.OwnerKind, startInfo.OwnerId, exitEntry);
                if (onLogAsync is not null)
                {
                    await onLogAsync(exitEntry);
                }

                managedProcess.Complete(process.ExitCode);
                if (onExitAsync is not null)
                {
                    await onExitAsync(process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Managed process reader loop crashed for {OwnerKind}/{OwnerId}", startInfo.OwnerKind, startInfo.OwnerId);
                managedProcess.Complete(process.HasExited ? process.ExitCode : null);
            }
        }, CancellationToken.None);

        return managedProcess;
    }

    private async Task ReadStreamAsync(
        StreamReader reader,
        string source,
        string streamName,
        ManagedProcessStartInfo startInfo,
        RingLogBuffer logBuffer,
        Func<LogEntry, Task>? onLogAsync,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null)
            {
                break;
            }

            var redactedLine = logRedactor.Redact(line);
            var entry = logBuffer.Append(source, streamName, startInfo.SessionVersion, startInfo.CorrelationId, redactedLine);
            fileLogStore.Append(startInfo.OwnerKind, startInfo.OwnerId, entry);

            if (onLogAsync is not null)
            {
                await onLogAsync(entry);
            }
        }
    }

    private static DateTimeOffset? TryGetStartedUtc(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    private static void ClearInheritedHostVariables(ProcessStartInfo processStartInfo)
    {
        foreach (var variableName in new[]
                 {
                     "ASPNETCORE_URLS",
                     "ASPNETCORE_HTTP_PORT",
                     "ASPNETCORE_HTTPS_PORT",
                     "ASPNETCORE_HTTP_PORTS",
                     "ASPNETCORE_HTTPS_PORTS",
                     "HTTP_PORTS",
                     "HTTPS_PORTS",
                     "DOTNET_LAUNCH_PROFILE",
                     "LAUNCH_PROFILE",
                     "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT",
                     "ASPNETCORE_AUTO_RELOAD_WS_KEY",
                     "ASPNETCORE_AUTO_RELOAD_WS_INTERVAL",
                     "DOTNET_STARTUP_HOOKS",
                     "DOTNET_ADDITIONAL_DEPS",
                     "DOTNET_SHARED_STORE"
                 })
        {
            processStartInfo.Environment.Remove(variableName);
        }
    }
}
