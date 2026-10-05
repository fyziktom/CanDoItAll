#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(CanDoItAll.Workbench.Planning.UiSandbox.PlanningWatchState))]
#endif

namespace CanDoItAll.Workbench.Planning.UiSandbox;

internal static class PlanningWatchState {
    public const string Endpoint = "/_dev/runtime";
    private const string WatchIterationVariable = "DOTNET_WATCH_ITERATION";
    private static long generation;

    internal static void UpdateApplication(Type[]? updatedTypes) {
        Interlocked.Increment(ref generation);
    }

    public static PlanningRuntimeStatus Read(IConfiguration configuration) => new(
        true,
        "Ready",
        PlanningAssets.Mode,
        Environment.ProcessId,
        int.TryParse(Environment.GetEnvironmentVariable(WatchIterationVariable), out var iteration)
            ? iteration
            : null,
        Interlocked.Read(ref generation),
        configuration["CanDoItAllMcpOwnerKind"],
        configuration["CanDoItAllMcpOwnerId"],
        configuration["CanDoItAllMcpServerInstanceId"]);
}

internal sealed record PlanningRuntimeStatus(
    bool IsReady,
    string Summary,
    PlanningAssetMode AssetMode,
    int RuntimePid,
    int? WatchIteration,
    long HotReloadGeneration,
    string? OwnerKind,
    string? OwnerId,
    string? ServerInstanceId);
