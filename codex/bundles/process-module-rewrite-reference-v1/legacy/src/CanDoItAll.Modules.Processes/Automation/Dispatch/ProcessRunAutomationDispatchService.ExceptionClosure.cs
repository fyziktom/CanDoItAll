using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private ProcessDispatchFailureClosureService CreateFailureClosureService()
    {
        return new ProcessDispatchFailureClosureService(
            CreateRunClosureGuardService(),
            CreateStepTransitionService(),
            IsStepDispatchClaimHeldAsync,
            logger);
    }

    private ProcessClaimedDispatchResult HandleDispatchHeartbeatClaimLost(
        ProcessRouteExecutionContext execution)
    {
        return CreateFailureClosureService().HandleDispatchHeartbeatClaimLost(execution);
    }

    private ProcessClaimedDispatchResult HandleDispatchClaimLost(
        ProcessRouteExecutionContext execution,
        ProcessDispatchClaimLostException exception)
    {
        return CreateFailureClosureService().HandleDispatchClaimLost(execution, exception);
    }

    private async Task<ProcessClaimedDispatchResult> HandleDispatchFailureAsync(
        ProcessRouteExecutionContext execution,
        Exception exception)
    {
        return await CreateFailureClosureService().HandleDispatchFailureAsync(execution, exception);
    }
}
