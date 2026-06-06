using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchRouteHandlerFactory
{
    public static ProcessDispatchRouteHandlerPipeline Create(
        IClock clock,
        ILogger<ProcessRunAutomationDispatchService> logger,
        ProcessDispatchRouteServices routeServices)
    {
        return new ProcessDispatchRouteHandlerPipeline(
            [
                new FreshRecoverySkipRouteHandler(clock, logger),
                new DatabaseRequirementRouteHandler(routeServices),
                new UpstreamMaterializationRouteHandler(routeServices),
                new StrandedArtifactRecoveryRouteHandler(routeServices),
                new SubprocessRouteHandler(routeServices),
                new StartTransitionRouteHandler(routeServices, logger),
                new WorkflowRouteHandler(routeServices),
                new DirectAgentExecutionRouteHandler(routeServices),
                new CompetingExecutionGuardRouteHandler(routeServices, logger),
                new RunClosedGuardRouteHandler(routeServices, logger),
                new FinalizerTransitionRouteHandler(routeServices)
            ]);
    }
}
