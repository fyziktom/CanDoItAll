#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(CanDoItAll.Workbench.Operators.UiSandbox.OperatorsWatchState))]
#endif

namespace CanDoItAll.Workbench.Operators.UiSandbox;

internal static class OperatorsWatchState {
    public const string Endpoint = "/_dev/runtime";
    private const string WatchIterationVariable = "DOTNET_WATCH_ITERATION";
    private static long generation;

    internal static void UpdateApplication(Type[]? updatedTypes) {
        Interlocked.Increment(ref generation);
    }

    public static OperatorsRuntimeStatus Read(IConfiguration configuration) => new(
        true,
        "Ready",
        OperatorsAssets.Mode,
        Environment.ProcessId,
        int.TryParse(Environment.GetEnvironmentVariable(WatchIterationVariable), out var iteration)
            ? iteration
            : null,
        Interlocked.Read(ref generation),
        configuration["CanDoItAllMcpOwnerKind"],
        configuration["CanDoItAllMcpOwnerId"],
        configuration["CanDoItAllMcpServerInstanceId"]);
}

internal sealed record OperatorsRuntimeStatus(
    bool IsReady,
    string Summary,
    OperatorsAssetMode AssetMode,
    int RuntimePid,
    int? WatchIteration,
    long HotReloadGeneration,
    string? OwnerKind,
    string? OwnerId,
    string? ServerInstanceId);
