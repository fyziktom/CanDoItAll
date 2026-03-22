using CanDoItAll.Mcp.DotNetWatch.Configuration;
using System.Diagnostics;
using System.Text.Json;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class BackendRegistrationStore(RuntimeConfiguration configuration, ILogger<BackendRegistrationStore> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string RegistrationPath => configuration.BackendRegistrationPath;

    public async Task<BackendRegistrationRecord?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(RegistrationPath))
        {
            return null;
        }

        try
        {
            await using var stream = File.Open(RegistrationPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync<BackendRegistrationRecord>(stream, SerializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to read backend registration from {Path}", RegistrationPath);
            return null;
        }
    }

    public async Task WriteAsync(BackendRegistrationRecord registration, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(RegistrationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Open(RegistrationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, registration, SerializerOptions, cancellationToken);
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(RegistrationPath))
            {
                File.Delete(RegistrationPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to delete backend registration at {Path}", RegistrationPath);
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
}
