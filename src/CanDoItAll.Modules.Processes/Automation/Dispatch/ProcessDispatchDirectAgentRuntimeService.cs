namespace CanDoItAll.Modules.Processes;

internal delegate Task<ProcessRouteExecutionOutcome> ProcessDirectAgentExecutionRunner(
    ProcessDispatchDirectAgentExecutionInput input,
    CancellationToken cancellationToken);

internal sealed class ProcessDispatchDirectAgentRuntimeService(
    ProcessDirectAgentExecutionRunner executeUntilSettledAsync)
{
    public async Task<ProcessRouteExecutionOutcome> ExecuteUntilSettledAsync(
        ProcessDispatchDirectAgentExecutionInput input,
        CancellationToken cancellationToken)
    {
        return await executeUntilSettledAsync(
            input,
            cancellationToken);
    }
}
