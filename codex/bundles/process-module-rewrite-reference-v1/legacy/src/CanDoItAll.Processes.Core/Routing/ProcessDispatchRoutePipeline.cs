namespace CanDoItAll.Processes.Core.Routing;

public enum ProcessDispatchRouteStage
{
    FreshRecoverySkip,
    DatabaseRequirement,
    UpstreamMaterialization,
    StrandedArtifactRecovery,
    Subprocess,
    StartTransition,
    Workflow,
    DirectAgentExecution,
    CompetingExecutionGuard,
    RunClosedGuard,
    FinalizerTransition
}

public static class ProcessDispatchRoutePipeline
{
    public static IReadOnlyList<ProcessDispatchRouteStage> StageOrder { get; } =
    [
        ProcessDispatchRouteStage.FreshRecoverySkip,
        ProcessDispatchRouteStage.DatabaseRequirement,
        ProcessDispatchRouteStage.UpstreamMaterialization,
        ProcessDispatchRouteStage.StrandedArtifactRecovery,
        ProcessDispatchRouteStage.Subprocess,
        ProcessDispatchRouteStage.StartTransition,
        ProcessDispatchRouteStage.Workflow,
        ProcessDispatchRouteStage.DirectAgentExecution,
        ProcessDispatchRouteStage.CompetingExecutionGuard,
        ProcessDispatchRouteStage.RunClosedGuard,
        ProcessDispatchRouteStage.FinalizerTransition
    ];
}

public static class ProcessDispatchRouteOrderAssertion
{
    public static void ThrowIfStageOrderInvalid(IReadOnlyList<ProcessDispatchRouteStage> actualStageOrder)
    {
        if (ProcessDispatchRoutePipeline.StageOrder.SequenceEqual(actualStageOrder))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Process dispatch route handler order must match the canonical route stage order. Expected: {FormatStageOrder(ProcessDispatchRoutePipeline.StageOrder)}. Actual: {FormatStageOrder(actualStageOrder)}.");
    }

    private static string FormatStageOrder(IReadOnlyList<ProcessDispatchRouteStage> stageOrder)
    {
        return string.Join(" -> ", stageOrder);
    }
}
