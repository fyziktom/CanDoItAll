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
        var executionRuns = await executionClient.ListExecutionRunsAsync(
            ProcessExecutionRunQueryBuilder.ForCandidate(dispatcherCandidate),
            cancellationToken);

        return ProcessAutomationExecutionRunSelection.ResolveCompetingActiveAutomationExecutionRun(
            executionRuns,
            executionOutcome.ExecutionRun.Id,
            candidate.StepRun.StartedAtUtc,
            clock.GetUtcNow(),
            ProcessRunAutomationDispatchService.AutomationActor,
            staleAutomationExecutionRunTimeout);
    }
}
