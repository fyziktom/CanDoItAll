using System.Diagnostics;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.LocalRuntime.Persistence;

namespace CanDoItAll.Mcp.LocalRuntime.Processes;

public class ProcessSupervisor(
    LocalProcessRuntimeOptions options,
    ServerInstanceIdentity serverInstanceIdentity,
    StaleProcessRegistry staleProcessRegistry,
    IProcessTreeTerminator terminator,
    FileLogStore fileLogStore,
    SecretRedactor secretRedactor,
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

        ClearInheritedHostVariables(processStartInfo, options.ClearedInheritedEnvironmentVariables);

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
                options.WorkspaceRoot,
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

            var redactedLine = secretRedactor.Redact(line);
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

    private static void ClearInheritedHostVariables(ProcessStartInfo processStartInfo, IReadOnlyList<string> variables)
    {
        foreach (var variableName in variables)
        {
            processStartInfo.Environment.Remove(variableName);
        }
    }
}
