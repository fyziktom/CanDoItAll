using System.Diagnostics;
using System.Globalization;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Logging;
using CanDoItAll.Mcp.DotNetWatch.Persistence;

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
    Task<IReadOnlyList<int>> TerminateAsync(Process process, CancellationToken cancellationToken);
}

public sealed class ProcessTreeTerminator : IProcessTreeTerminator
{
    public async Task<IReadOnlyList<int>> TerminateAsync(Process process, CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return [];
        }

        var pid = process.Id;
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(CancellationToken.None);
        return [pid];
    }
}

public sealed class ManagedProcess
{
    private readonly Process _process;
    private readonly RuntimeConfiguration _configuration;
    private readonly IProcessTreeTerminator _terminator;
    private readonly TaskCompletionSource<int?> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ManagedProcess(Process process, RuntimeConfiguration configuration, IProcessTreeTerminator terminator)
    {
        _process = process;
        _configuration = configuration;
        _terminator = terminator;
    }

    public int Pid => _process.Id;

    public Task<int?> Completion => _completionSource.Task;

    public void Complete(int? exitCode)
    {
        _completionSource.TrySetResult(exitCode);
    }

    public async Task<ProcessStopResult> StopAsync(bool force, CancellationToken cancellationToken)
    {
        if (_process.HasExited)
        {
            return new ProcessStopResult(true, [], _process.ExitCode);
        }

        var graceful = false;
        if (!force && _process.CloseMainWindow())
        {
            graceful = await Task.Run(() => _process.WaitForExit((int)_configuration.GracefulStopTimeout.TotalMilliseconds), cancellationToken);
        }

        IReadOnlyList<int> killedPids = [];
        if (!graceful && !_process.HasExited)
        {
            killedPids = await _terminator.TerminateAsync(_process, cancellationToken);
        }

        await _process.WaitForExitAsync(CancellationToken.None);
        return new ProcessStopResult(graceful, killedPids, _process.ExitCode);
    }
}

public sealed class ProcessSupervisor(
    RuntimeConfiguration configuration,
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

        var managedProcess = new ManagedProcess(process, configuration, terminator);

        await staleProcessRegistry.RegisterAsync(
            new ManagedProcessRecord(
                process.Id,
                DateTimeOffset.UtcNow,
                startInfo.Command,
                startInfo.Arguments,
                startInfo.WorkingDirectory,
                configuration.WorkspaceRoot,
                startInfo.OwnerKind,
                startInfo.OwnerId,
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture)),
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
                var stdoutTask = ReadStreamAsync(process.StandardOutput, "ProcessStdOut", "stdout", startInfo, logBuffer, onLogAsync, cancellationToken);
                var stderrTask = ReadStreamAsync(process.StandardError, "ProcessStdErr", "stderr", startInfo, logBuffer, onLogAsync, cancellationToken);
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
