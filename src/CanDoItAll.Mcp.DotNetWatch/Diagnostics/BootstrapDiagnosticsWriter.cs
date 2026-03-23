using CanDoItAll.Mcp.DotNetWatch.Configuration;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Mcp.DotNetWatch.Diagnostics;

internal sealed class BootstrapDiagnosticsWriter(RuntimeConfiguration configuration)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string DiagnosticsPath => configuration.BootstrapDiagnosticsPath;

    public async Task WriteFailureAsync(
        string phase,
        Exception exception,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["timestampUtc"] = DateTimeOffset.UtcNow,
            ["phase"] = phase,
            ["workspaceRoot"] = configuration.WorkspaceRoot,
            ["serverAssemblyPath"] = configuration.ServerAssemblyPath,
            ["backendEnabled"] = configuration.BackendEnabled,
            ["backendRegistrationPath"] = configuration.BackendRegistrationPath,
            ["backendLaunchLockPath"] = configuration.BackendLaunchLockPath,
            ["logFolder"] = configuration.LogFolder,
            ["context"] = context,
            ["exception"] = new Dictionary<string, object?>
            {
                ["type"] = exception.GetType().FullName,
                ["message"] = exception.Message,
                ["stackTrace"] = exception.ToString()
            }
        };

        var builder = new StringBuilder();
        builder.AppendLine("============================================================");
        builder.AppendLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        }));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(DiagnosticsPath, builder.ToString(), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
