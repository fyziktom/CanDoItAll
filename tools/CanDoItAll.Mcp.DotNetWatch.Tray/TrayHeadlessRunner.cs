using System.Text.Json;

namespace CanDoItAll.Mcp.DotNetWatch.Tray;

internal static class TrayHeadlessRunner
{
    public static async Task<int> RunAsync(TrayOptions options)
    {
        using var controller = new BackendTrayController(options);
        controller.WriteLog($"headless start | command={options.HeadlessCommand}");

        var snapshot = await controller.GetSnapshotAsync();
        switch (options.HeadlessCommand?.Trim().ToLowerInvariant())
        {
            case "status":
                break;

            case "recover":
                snapshot = await controller.RecoverAsync(snapshot, forceRestart: false);
                break;

            case "restart":
                snapshot = await controller.RecoverAsync(snapshot, forceRestart: true);
                break;

            default:
                throw new InvalidOperationException("Unsupported headless command. Use status, recover, or restart.");
        }

        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            status = snapshot.StatusKind.ToString(),
            snapshot.MenuText,
            matchingBackendCount = snapshot.MatchingBackends.Count,
            primaryBackendId = snapshot.PrimaryBackend?.Record.Registration.BackendId,
            primaryBackendPid = snapshot.PrimaryBackend?.Record.Registration.ProcessId
        }));
        Console.Out.Flush();

        return 0;
    }
}
