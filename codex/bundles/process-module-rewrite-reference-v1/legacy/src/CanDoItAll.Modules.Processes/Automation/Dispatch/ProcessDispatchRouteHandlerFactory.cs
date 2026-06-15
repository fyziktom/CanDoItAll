using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchRouteHandlerFactory
{
    public static ProcessDispatchRouteHandlerPipeline Create(
        IClock clock,
        ILogger<ProcessRunAutomationDispatchService> logger,
        IProcessDispatchDatabaseRequirementRouteFacet databaseRequirementFacet,
        IProcessDispatchUpstreamMaterializationRouteFacet upstreamMaterializationFacet,
        IProcessDispatchRecoveryRouteFacet recoveryFacet,
        IProcessDispatchSubprocessRouteFacet subprocessFacet,
        IProcessDispatchStartTransitionRouteFacet startTransitionFacet,
        IProcessDispatchWorkflowRouteFacet workflowFacet,
        IProcessDispatchDirectAgentRouteFacet directAgentFacet,
        IProcessDispatchGuardRouteFacet guardFacet,
        IProcessDispatchFinalizerRouteFacet finalizerFacet)
    {
        return new ProcessDispatchRouteHandlerPipeline(
            [
                new FreshRecoverySkipRouteHandler(clock, logger),
                new DatabaseRequirementRouteHandler(databaseRequirementFacet),
                new UpstreamMaterializationRouteHandler(upstreamMaterializationFacet),
                new StrandedArtifactRecoveryRouteHandler(recoveryFacet),
                new SubprocessRouteHandler(subprocessFacet),
                new StartTransitionRouteHandler(startTransitionFacet, logger),
                new WorkflowRouteHandler(workflowFacet),
                new DirectAgentExecutionRouteHandler(directAgentFacet),
                new CompetingExecutionGuardRouteHandler(guardFacet, logger),
                new RunClosedGuardRouteHandler(guardFacet, logger),
                new FinalizerTransitionRouteHandler(finalizerFacet)
            ]);
    }
}
