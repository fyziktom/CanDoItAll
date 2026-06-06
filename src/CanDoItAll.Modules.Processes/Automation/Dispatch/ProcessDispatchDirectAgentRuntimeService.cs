namespace CanDoItAll.Modules.Processes;

internal delegate Task<ProcessRunAutomationDispatchService.DispatchExecutionOutcome> ProcessDirectAgentExecutionRunner(
    ProcessRunAutomationDispatchService.DispatchCandidate candidate,
    string trigger,
    Func<CancellationToken, Task>? renewLeaseAsync,
    CancellationToken cancellationToken);

internal sealed class ProcessDispatchDirectAgentRuntimeService(
    ProcessDirectAgentExecutionRunner executeUntilSettledAsync)
{
    public async Task<ProcessRouteExecutionOutcome> ExecuteUntilSettledAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        var executionOutcome = await executeUntilSettledAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            trigger,
            renewLeaseAsync,
            cancellationToken);

        return ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(executionOutcome);
    }
}
