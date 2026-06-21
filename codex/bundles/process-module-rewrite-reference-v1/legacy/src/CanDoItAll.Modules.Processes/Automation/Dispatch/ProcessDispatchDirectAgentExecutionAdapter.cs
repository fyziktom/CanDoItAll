namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchDirectAgentExecutionAdapter(
    Func<ProcessRunAutomationDispatchService.DispatchCandidate, string, Func<CancellationToken, Task>?, CancellationToken, Task<ProcessRunAutomationDispatchService.DispatchExecutionOutcome>> executeUntilSettledAsync)
{
    public async Task<ProcessRouteExecutionOutcome> ExecuteUntilSettledAsync(
        ProcessDispatchDirectAgentExecutionInput input,
        CancellationToken cancellationToken)
    {
        var executionOutcome = await executeUntilSettledAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate),
            input.Trigger,
            input.RenewLeaseAsync,
            cancellationToken);

        return ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(executionOutcome);
    }
}
