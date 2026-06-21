namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchFinalizerApplicationService(ProcessDispatchFinalizerAdapter finalizerAdapter)
{
    public Task FinalizeWorkflowCompletionAsync(
        ProcessDispatchWorkflowFinalizerInput input,
        CancellationToken cancellationToken)
        => finalizerAdapter.FinalizeWorkflowCompletionAsync(input, cancellationToken);

    public Task FinalizeRecoveredCompletionAsync(
        ProcessDispatchRecoveredFinalizerInput input,
        CancellationToken cancellationToken)
        => finalizerAdapter.FinalizeRecoveredCompletionAsync(input, cancellationToken);

    public Task FinalizeDirectAgentCompletionAsync(
        ProcessDispatchDirectAgentFinalizerInput input,
        CancellationToken cancellationToken)
        => finalizerAdapter.FinalizeDirectAgentCompletionAsync(input, cancellationToken);

    public Task FinalizeSubprocessCompletionAsync(
        ProcessDispatchSubprocessFinalizerInput input,
        CancellationToken cancellationToken)
        => finalizerAdapter.FinalizeSubprocessCompletionAsync(input, cancellationToken);
}
