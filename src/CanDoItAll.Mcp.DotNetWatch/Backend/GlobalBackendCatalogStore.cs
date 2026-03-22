using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class GlobalBackendCatalogStore(RuntimeConfiguration configuration, ILogger<GlobalBackendCatalogStore> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string DirectoryPath => configuration.GlobalBackendCatalogDirectory;

    public async Task UpsertAsync(BackendRegistrationRecord registration, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = GetRecordPath(registration.BackendId);
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, registration, SerializerOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<BackendRegistrationRecord>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return [];
        }

        List<BackendRegistrationRecord> records = [];
        foreach (var path in Directory.GetFiles(DirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var record = await JsonSerializer.DeserializeAsync<BackendRegistrationRecord>(stream, SerializerOptions, cancellationToken);
                if (record is not null)
                {
                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to read backend catalog record from {Path}", path);
            }
        }

        return records
            .OrderBy(static record => record.Identity.WorkspaceRoot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static record => record.RegisteredUtc)
            .ToArray();
    }

    public Task DeleteAsync(string? backendId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(backendId))
        {
            return Task.CompletedTask;
        }

        try
        {
            var path = GetRecordPath(backendId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to delete backend catalog record for {BackendId}", backendId);
        }

        return Task.CompletedTask;
    }

    public async Task DeleteManyAsync(IEnumerable<string> backendIds, CancellationToken cancellationToken)
    {
        foreach (var backendId in backendIds.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            await DeleteAsync(backendId, cancellationToken);
        }
    }

    public bool IsLiveProcess(BackendRegistrationRecord registration)
    {
        try
        {
            using var process = Process.GetProcessById(registration.ProcessId);
            if (process.HasExited)
            {
                return false;
            }

            var startedUtc = process.StartTime.ToUniversalTime();
            return Math.Abs((startedUtc - registration.ProcessStartedUtc).TotalSeconds) <= 60;
        }
        catch
        {
            return false;
        }
    }

    private string GetRecordPath(string backendId)
    {
        var sanitized = string.Concat(backendId.Select(static character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return Path.Combine(DirectoryPath, $"{sanitized}.json");
    }
}
