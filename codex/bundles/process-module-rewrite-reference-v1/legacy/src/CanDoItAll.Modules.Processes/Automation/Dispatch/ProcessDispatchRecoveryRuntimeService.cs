namespace CanDoItAll.Modules.Processes;

internal delegate Task<ProcessRunAutomationDispatchService.DispatchExecutionOutcome?> ProcessMissingCompletionArtifactRecoveryRunner(
    ProcessRunAutomationDispatchService.DispatchCandidate candidate,
    string trigger,
    ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
    Func<CancellationToken, Task>? renewLeaseAsync,
    CancellationToken cancellationToken);

internal sealed class ProcessDispatchRecoveryRuntimeService(
    ProcessMissingCompletionArtifactRecoveryRunner recoverMissingCompletionArtifactsAsync)
{
    public async Task<ProcessRouteExecutionOutcome?> TryRecoverStrandedMissingCompletionArtifactsAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        ProcessRouteDispatchClaim dispatchClaim,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        var recoveryOutcome = await recoverMissingCompletionArtifactsAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            trigger,
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            renewLeaseAsync,
            cancellationToken);

        return recoveryOutcome is null
            ? null
            : ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(recoveryOutcome);
    }
}
