#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(CanDoItAll.Workbench.Insights.UiSandbox.InsightsWatchState))]
#endif

namespace CanDoItAll.Workbench.Insights.UiSandbox;

internal static class InsightsWatchState {
    public const string Endpoint = "/_dev/runtime";
    private const string WatchIterationVariable = "DOTNET_WATCH_ITERATION";
    private static long generation;

    internal static void UpdateApplication(Type[]? updatedTypes) {
        Interlocked.Increment(ref generation);
    }

    public static InsightsRuntimeStatus Read(IConfiguration configuration) => new(
        true,
        "Ready",
        InsightsAssets.Mode,
        Environment.ProcessId,
        int.TryParse(Environment.GetEnvironmentVariable(WatchIterationVariable), out var iteration)
            ? iteration
            : null,
        Interlocked.Read(ref generation),
        configuration["CanDoItAllMcpOwnerKind"],
        configuration["CanDoItAllMcpOwnerId"],
        configuration["CanDoItAllMcpServerInstanceId"]);
}

internal sealed record InsightsRuntimeStatus(
    bool IsReady,
    string Summary,
    InsightsAssetMode AssetMode,
    int RuntimePid,
    int? WatchIteration,
    long HotReloadGeneration,
    string? OwnerKind,
    string? OwnerId,
    string? ServerInstanceId);
