[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(CanDoItAll.Processes.UiSandbox.ProcessesWatchState))]

namespace CanDoItAll.Processes.UiSandbox;

internal static class ProcessesWatchState {
    public const string Endpoint = "/_dev/runtime";
    private const string WatchIterationVariable = "DOTNET_WATCH_ITERATION";
    private static long generation;

    internal static void UpdateApplication(Type[]? updatedTypes) {
        Interlocked.Increment(ref generation);
    }

    public static ProcessesRuntimeStatus Read(IConfiguration configuration) => new(
        true,
        "Ready",
        ProcessesAssets.Mode,
        Environment.ProcessId,
        int.TryParse(Environment.GetEnvironmentVariable(WatchIterationVariable), out var iteration)
            ? iteration
            : null,
        Interlocked.Read(ref generation),
        configuration["CanDoItAllMcpOwnerKind"],
        configuration["CanDoItAllMcpOwnerId"],
        configuration["CanDoItAllMcpServerInstanceId"]);
}

internal sealed record ProcessesRuntimeStatus(
    bool IsReady,
    string Summary,
    ProcessesAssetMode AssetMode,
    int RuntimePid,
    int? WatchIteration,
    long HotReloadGeneration,
    string? OwnerKind,
    string? OwnerId,
    string? ServerInstanceId);
