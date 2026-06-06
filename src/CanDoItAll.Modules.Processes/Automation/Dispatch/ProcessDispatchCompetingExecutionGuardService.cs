using CanDoItAll.Processes.Contracts;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchCompetingExecutionGuardService(
    IProcessAutomationExecutionClient executionClient,
    IClock clock,
    TimeSpan staleAutomationExecutionRunTimeout)
{
    public async Task<ProcessAutomationExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        CancellationToken cancellationToken)
    {
        var dispatcherCandidate = ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate);
        var dispatcherOutcome = ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(executionOutcome);
        var executionRuns = await executionClient.ListExecutionRunsAsync(
            ProcessExecutionRunQueryBuilder.ForCandidate(dispatcherCandidate),
            cancellationToken);

        return ProcessAutomationExecutionRunSelection.ResolveCompetingActiveAutomationExecutionRun(
            executionRuns,
            dispatcherOutcome.Detail.Run.Id,
            candidate.StepRun.StartedAtUtc,
            clock.GetUtcNow(),
            ProcessRunAutomationDispatchService.AutomationActor,
            staleAutomationExecutionRunTimeout);
    }
}
