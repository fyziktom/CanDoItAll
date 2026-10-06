#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(CanDoItAll.Workbench.Content.UiSandbox.ContentWatchState))]
#endif

namespace CanDoItAll.Workbench.Content.UiSandbox;

internal static class ContentWatchState {
    public const string Endpoint = "/_dev/runtime";
    private const string WatchIterationVariable = "DOTNET_WATCH_ITERATION";
    private static long generation;

    internal static void UpdateApplication(Type[]? updatedTypes) {
        Interlocked.Increment(ref generation);
    }

    public static ContentRuntimeStatus Read(IConfiguration configuration) => new(
        true,
        "Ready",
        ContentAssets.Mode,
        Environment.ProcessId,
        int.TryParse(Environment.GetEnvironmentVariable(WatchIterationVariable), out var iteration)
            ? iteration
            : null,
        Interlocked.Read(ref generation),
        configuration["CanDoItAllMcpOwnerKind"],
        configuration["CanDoItAllMcpOwnerId"],
        configuration["CanDoItAllMcpServerInstanceId"]);
}

internal sealed record ContentRuntimeStatus(
    bool IsReady,
    string Summary,
    ContentAssetMode AssetMode,
    int RuntimePid,
    int? WatchIteration,
    long HotReloadGeneration,
    string? OwnerKind,
    string? OwnerId,
    string? ServerInstanceId);
