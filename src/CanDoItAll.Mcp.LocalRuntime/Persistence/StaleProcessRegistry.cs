using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.LocalRuntime.Processes;

namespace CanDoItAll.Mcp.LocalRuntime.Persistence;

public class StaleProcessRegistry(
    LocalProcessRuntimeOptions options,
    ServerInstanceIdentity serverInstanceIdentity,
    IProcessCommandRunner commandRunner,
    ILogger<StaleProcessRegistry> logger)
{
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task RegisterAsync(ManagedProcessRecord record, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = await ReadUnsafeAsync(cancellationToken);
            records.RemoveAll(existing => existing.Pid == record.Pid);
            records.Add(record);
            await WriteUnsafeAsync(records, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UnregisterAsync(int pid, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = await ReadUnsafeAsync(cancellationToken);
            records.RemoveAll(record => record.Pid == pid);
            await WriteUnsafeAsync(records, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CleanupStaleProcessesData> CleanupAsync(IProcessTreeTerminator terminator, bool dryRun, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = await ReadUnsafeAsync(cancellationToken);
            List<CleanupKilledProcessData> killed = [];
            List<CleanupSkippedProcessData> skipped = [];
            List<ManagedProcessRecord> remaining = [];

            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.Equals(record.RegisteredByServerInstanceId, serverInstanceIdentity.Id, StringComparison.OrdinalIgnoreCase))
                {
                    skipped.Add(new CleanupSkippedProcessData(record.Pid, "Owned by current server instance"));
                    remaining.Add(record);
                    continue;
                }

                Process? process = null;
                try
                {
                    process = Process.GetProcessById(record.Pid);
                    if (process.HasExited)
                    {
                        skipped.Add(new CleanupSkippedProcessData(record.Pid, "Process no longer exists"));
                        continue;
                    }

                    var ownership = await VerifyOwnershipAsync(process, record, cancellationToken);
                    if (!ownership.IsOwned)
                    {
                        skipped.Add(new CleanupSkippedProcessData(record.Pid, ownership.Reason));
                        if (ownership.KeepRecord)
                        {
                            remaining.Add(record);
                        }

                        continue;
                    }

                    if (dryRun)
                    {
                        skipped.Add(new CleanupSkippedProcessData(record.Pid, "Dry run"));
                        remaining.Add(record);
                        continue;
                    }

                    await terminator.TerminateAsync(process, force: true, cancellationToken);
                    if (!await EnsureProcessExitedAsync(process, cancellationToken))
                    {
                        skipped.Add(new CleanupSkippedProcessData(record.Pid, "Process did not exit after forced stale cleanup."));
                        remaining.Add(record);
                        continue;
                    }

                    killed.Add(new CleanupKilledProcessData(record.Pid, record.OwnerKind, record.OwnerId));
                }
                catch (ArgumentException)
                {
                    skipped.Add(new CleanupSkippedProcessData(record.Pid, "Process no longer exists"));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to clean stale managed process PID {Pid}", record.Pid);
                    skipped.Add(new CleanupSkippedProcessData(record.Pid, ex.Message));
                    remaining.Add(record);
                }
                finally
                {
                    process?.Dispose();
                }
            }

            await WriteUnsafeAsync(remaining, cancellationToken);
            return new CleanupStaleProcessesData(records.Count, killed, skipped, dryRun);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<OwnershipResult> VerifyOwnershipAsync(Process process, ManagedProcessRecord record, CancellationToken cancellationToken)
    {
        if (!TryMatchesStartTime(process, record.StartedUtc))
        {
            return new OwnershipResult(false, KeepRecord: false, "Process start time does not match the registered managed process.");
        }

        string? commandLine = null;
        try
        {
            commandLine = await GetCommandLineAsync(process.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to inspect command line for PID {Pid}", process.Id);
        }

        if (string.IsNullOrWhiteSpace(commandLine))
        {
            var keepRecord = ManagedProcessMarkers.RecordContainsOwnershipMarkers(record);
            return new OwnershipResult(false, keepRecord, keepRecord
                ? "Could not inspect process command line safely."
                : "Record does not contain explicit ownership markers.");
        }

        if (!ManagedProcessMarkers.CommandLineMatches(commandLine, record))
        {
            return new OwnershipResult(false, KeepRecord: false, "Command line does not match the registered managed process ownership markers.");
        }

        return new OwnershipResult(true, KeepRecord: false, string.Empty);
    }

    private static bool TryMatchesStartTime(Process process, DateTimeOffset expectedStartedUtc)
    {
        try
        {
            var actual = process.StartTime.ToUniversalTime();
            return Math.Abs((actual - expectedStartedUtc).TotalSeconds) <= StartTimeTolerance.TotalSeconds;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> GetCommandLineAsync(int pid, CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var script = $"(Get-CimInstance Win32_Process -Filter \"ProcessId = {pid}\").CommandLine";
            var output = await commandRunner.RunCaptureAsync(
                "powershell",
                ["-NoProfile", "-NonInteractive", "-Command", script],
                cancellationToken);
            return output.Trim();
        }

        var unixOutput = await commandRunner.RunCaptureAsync(
            "ps",
            ["-o", "command=", "-p", pid.ToString(CultureInfo.InvariantCulture)],
            cancellationToken);
        return unixOutput.Trim();
    }

    private static async Task<bool> EnsureProcessExitedAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            if (process.HasExited)
            {
                return true;
            }
        }
        catch
        {
            return true;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ignore races where the process exits between inspection and the fallback kill.
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return process.HasExited;
        }
        catch
        {
            return process.HasExited;
        }
    }

    private async Task<List<ManagedProcessRecord>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(options.RegistryPath))
        {
            return [];
        }

        await using var stream = File.Open(options.RegistryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var records = await JsonSerializer.DeserializeAsync<List<ManagedProcessRecord>>(stream, _serializerOptions, cancellationToken);
        return records ?? [];
    }

    private async Task WriteUnsafeAsync(List<ManagedProcessRecord> records, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(options.RegistryPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Open(options.RegistryPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, records, _serializerOptions, cancellationToken);
    }

    private readonly record struct OwnershipResult(bool IsOwned, bool KeepRecord, string Reason);
}
