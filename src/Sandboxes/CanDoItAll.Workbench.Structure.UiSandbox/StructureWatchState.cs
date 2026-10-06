#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(CanDoItAll.Workbench.Structure.UiSandbox.StructureWatchState))]
#endif

namespace CanDoItAll.Workbench.Structure.UiSandbox;

internal static class StructureWatchState {
    public const string Endpoint = "/_dev/runtime";
    private const string WatchIterationVariable = "DOTNET_WATCH_ITERATION";
    private static long generation;

    internal static void UpdateApplication(Type[]? updatedTypes) {
        Interlocked.Increment(ref generation);
    }

    public static StructureRuntimeStatus Read(IConfiguration configuration) => new(
        true,
        "Ready",
        StructureAssets.Mode,
        Environment.ProcessId,
        int.TryParse(Environment.GetEnvironmentVariable(WatchIterationVariable), out var iteration)
            ? iteration
            : null,
        Interlocked.Read(ref generation),
        configuration["CanDoItAllMcpOwnerKind"],
        configuration["CanDoItAllMcpOwnerId"],
        configuration["CanDoItAllMcpServerInstanceId"]);
}

internal sealed record StructureRuntimeStatus(
    bool IsReady,
    string Summary,
    StructureAssetMode AssetMode,
    int RuntimePid,
    int? WatchIteration,
    long HotReloadGeneration,
    string? OwnerKind,
    string? OwnerId,
    string? ServerInstanceId);
