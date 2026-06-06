namespace CanDoItAll.Modules.Processes;

internal enum ProcessDispatchRouteStage
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

internal static class ProcessDispatchRoutePipeline
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
