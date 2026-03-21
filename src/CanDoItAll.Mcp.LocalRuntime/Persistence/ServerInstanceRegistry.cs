using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.LocalRuntime.Processes;

namespace CanDoItAll.Mcp.LocalRuntime.Persistence;

public sealed class ServerInstanceRegistry(
    LocalProcessRuntimeOptions options,
    ServerInstanceIdentity serverInstanceIdentity,
    ILogger<ServerInstanceRegistry> logger)
{
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(60);
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<IAsyncDisposable> RegisterCurrentAsync(CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();
        var record = new ServerInstanceRecord(
            serverInstanceIdentity.Id,
            process.Id,
            process.StartTime.ToUniversalTime(),
            DateTimeOffset.UtcNow);

        Directory.CreateDirectory(options.ServerInstanceDirectory);
        await using var stream = File.Open(GetRecordPath(record.ServerInstanceId), FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, record, _serializerOptions, cancellationToken);
        return new ServerInstanceRegistration(this, record.ServerInstanceId);
    }

    public async Task<bool> IsLiveInstanceAsync(string? serverInstanceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serverInstanceId))
        {
            return false;
        }

        if (string.Equals(serverInstanceId, serverInstanceIdentity.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var recordPath = GetRecordPath(serverInstanceId);
        if (!File.Exists(recordPath))
        {
            return false;
        }

        ServerInstanceRecord? record;
        try
        {
            await using var stream = File.Open(recordPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            record = await JsonSerializer.DeserializeAsync<ServerInstanceRecord>(stream, _serializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to read server instance registration for {ServerInstanceId}", serverInstanceId);
            return false;
        }

        if (record is null)
        {
            TryDelete(recordPath);
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(record.ProcessId);
            if (process.HasExited)
            {
                TryDelete(recordPath);
                return false;
            }

            var actualStartUtc = process.StartTime.ToUniversalTime();
            if (Math.Abs((actualStartUtc - record.ProcessStartedUtc).TotalSeconds) > StartTimeTolerance.TotalSeconds)
            {
                TryDelete(recordPath);
                return false;
            }

            return true;
        }
        catch (ArgumentException)
        {
            TryDelete(recordPath);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to verify server instance registration for {ServerInstanceId}", serverInstanceId);
            return false;
        }
    }

    public Task UnregisterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryDelete(GetRecordPath(serverInstanceIdentity.Id));
        return Task.CompletedTask;
    }

    private string GetRecordPath(string serverInstanceId)
    {
        return Path.Combine(options.ServerInstanceDirectory, $"{serverInstanceId}.json");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup for stale registrations.
        }
    }

    private sealed class ServerInstanceRegistration(ServerInstanceRegistry owner, string serverInstanceId) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(serverInstanceId))
            {
                await owner.UnregisterAsync(CancellationToken.None);
            }
        }
    }
}

public sealed record ServerInstanceRecord(
    string ServerInstanceId,
    int ProcessId,
    DateTimeOffset ProcessStartedUtc,
    DateTimeOffset RegisteredUtc);
