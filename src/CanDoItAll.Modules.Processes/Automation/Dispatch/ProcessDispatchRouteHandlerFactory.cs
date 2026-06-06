using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchRouteHandlerFactory
{
    public static ProcessDispatchRouteHandlerPipeline Create(
        IClock clock,
        ILogger<ProcessRunAutomationDispatchService> logger,
        ProcessDispatchRouteFacetSet routeFacets)
    {
        return new ProcessDispatchRouteHandlerPipeline(
            [
                new FreshRecoverySkipRouteHandler(clock, logger),
                new DatabaseRequirementRouteHandler(routeFacets.DatabaseRequirement),
                new UpstreamMaterializationRouteHandler(routeFacets.UpstreamMaterialization),
                new StrandedArtifactRecoveryRouteHandler(routeFacets.Recovery),
                new SubprocessRouteHandler(routeFacets.Subprocess),
                new StartTransitionRouteHandler(routeFacets.StartTransition, logger),
                new WorkflowRouteHandler(routeFacets.Workflow),
                new DirectAgentExecutionRouteHandler(routeFacets.DirectAgent),
                new CompetingExecutionGuardRouteHandler(routeFacets.Guard, logger),
                new RunClosedGuardRouteHandler(routeFacets.Guard, logger),
                new FinalizerTransitionRouteHandler(routeFacets.Finalizer)
            ]);
    }
}
