namespace CanDoItAll.Processes.Application;

public sealed class ProcessCancellationRecoveryDispatcher(ProcessCancellationRuntimeRecoveryStep runtime,
    ProcessCancellationDecisionRecoveryStep decisions, ProcessCancellationScopeRecoveryStep scope,
    ProcessCancellationQuiescenceRecoveryStep quiescence) : IProcessCancellationRecoveryStep {
    public Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
        IProcessCancellationRecoveryStep owner = claim.Position.Stage switch {
            ProcessCancellationRecoveryStage.Runtime => runtime,
            ProcessCancellationRecoveryStage.Decisions => decisions,
            ProcessCancellationRecoveryStage.SharedScope => scope,
            ProcessCancellationRecoveryStage.Quiescence => quiescence,
            _ => throw new InvalidDataException("Cancellation recovery cannot advance a completed or unknown stage.")
        };
        return owner.AdvanceAsync(claim, cancellationToken);
    }
}
