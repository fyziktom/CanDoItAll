using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Processes;

namespace CanDoItAll.Mcp.DotNetWatch.Persistence;

public sealed record ManagedProcessRecord(
    int Pid,
    DateTimeOffset StartedUtc,
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string WorkspaceRoot,
    string OwnerKind,
    string OwnerId,
    string RegisteredByServerInstanceId);

public sealed class StaleProcessRegistry(RuntimeConfiguration configuration)
{
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

                Process? process = null;
                try
                {
                    process = Process.GetProcessById(record.Pid);
                    if (process.HasExited)
                    {
                        skipped.Add(new CleanupSkippedProcessData(record.Pid, "Process no longer exists"));
                        continue;
                    }

                    if (dryRun)
                    {
                        skipped.Add(new CleanupSkippedProcessData(record.Pid, "Dry run"));
                        remaining.Add(record);
                        continue;
                    }

                    await terminator.TerminateAsync(process, cancellationToken);
                    killed.Add(new CleanupKilledProcessData(record.Pid, record.OwnerKind, record.OwnerId));
                }
                catch (ArgumentException)
                {
                    skipped.Add(new CleanupSkippedProcessData(record.Pid, "Process no longer exists"));
                }
                catch (Exception ex)
                {
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

    private async Task<List<ManagedProcessRecord>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(configuration.RegistryPath))
        {
            return [];
        }

        await using var stream = File.Open(configuration.RegistryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var records = await JsonSerializer.DeserializeAsync<List<ManagedProcessRecord>>(stream, _serializerOptions, cancellationToken);
        return records ?? [];
    }

    private async Task WriteUnsafeAsync(List<ManagedProcessRecord> records, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(configuration.RegistryPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, records, _serializerOptions, cancellationToken);
    }
}

public sealed class StartupCleanupHostedService(
    RuntimeConfiguration configuration,
    StaleProcessRegistry staleProcessRegistry,
    IProcessTreeTerminator terminator,
    ILogger<StartupCleanupHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.CleanupStaleManagedProcessesOnStartup)
        {
            return;
        }

        var result = await staleProcessRegistry.CleanupAsync(terminator, dryRun: false, cancellationToken);
        if (result.Killed.Count > 0 || result.Skipped.Count > 0)
        {
            logger.LogInformation(
                "Startup stale process cleanup completed. Checked={Checked}, Killed={Killed}, Skipped={Skipped}",
                result.Checked,
                result.Killed.Count,
                result.Skipped.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
