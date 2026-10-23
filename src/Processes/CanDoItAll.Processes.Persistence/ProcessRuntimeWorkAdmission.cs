using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal readonly record struct ProcessRuntimeWorkAdmission(bool Claims, bool NewWork) {
    internal static ProcessRuntimeWorkAdmission Read(ProcessRuntimeStateEntity? current, ProcessRuntimeCommitRequest request) {
        var proposed = request.Mutation.State;
        var claims = current?.Claims.ToDictionary(item => item.ClaimToken) ?? [];
        var steps = current?.Steps.ToDictionary(item => item.StepInstanceId) ?? [];
        var admitsClaims = proposed.Claims.Any(claim => IsActive(claim.Status) &&
            (!claims.TryGetValue(claim.ClaimToken.Value, out var previous) || !SameClaim(previous, claim)));
        var newWork = request.DecisionBudget is ProcessDecisionBudgetReservation ||
            request.Decision is ProcessDecisionStart or ProcessDecisionEnrollment or ProcessDecisionRefresh or ProcessDecisionBeginRefresh or
                ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.WaitForReview } or
                ProcessDecisionClosure { Completion: ProcessDecisionCompletion.Review } ||
            request.DecisionRunScope is { Kind: ProcessDecisionRunScopeCommandKind.BeginCapture } ||
            current is not null && proposed.Status != ProcessRuntimeStatus.CancelRequested &&
                (proposed.Status == ProcessRuntimeStatus.Active && current.Status != ProcessRuntimeStatus.Active ||
                ProcessRuntimeTerminalStates.IsRunTerminal(current.Status) && !ProcessRuntimeTerminalStates.IsRunTerminal(proposed.Status));
        foreach (var step in proposed.Steps) {
            steps.TryGetValue(step.StepInstanceId.Value, out var previous);
            admitsClaims |= step.ActiveClaimToken is not null && step.ActiveClaimToken.Value.Value != previous?.ActiveClaimToken ||
                step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running && step.Status != previous?.Status;
            var reconciling = request.Decision is ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.BeginReconciliation } phase &&
                phase.StepInstanceId == step.StepInstanceId && step.Status == ProcessRuntimeStepStatus.Waiting && step.ActiveClaimToken is null;
            newWork |= previous is null && !ProcessRuntimeTerminalStates.IsStepTerminal(step.Status) ||
                previous is not null && (step.AttemptNumber > previous.AttemptNumber ||
                    step.Status == ProcessRuntimeStepStatus.Ready && previous.Status != ProcessRuntimeStepStatus.Ready ||
                    ProcessRuntimeTerminalStates.IsStepTerminal(previous.Status) && !ProcessRuntimeTerminalStates.IsStepTerminal(step.Status) && !reconciling);
        }
        return new(admitsClaims, newWork || admitsClaims);
    }

    internal static bool IsActive(DispatchClaimStatus status)
        => status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed;

    private static bool SameClaim(ProcessDispatchClaimEntity previous, DispatchClaimState next)
        => previous.StepInstanceId == next.StepInstanceId.Value && previous.OwnerId == next.OwnerId.Value &&
            previous.Status == next.Status && previous.AttemptNumber == next.AttemptNumber &&
            previous.CreatedAtUtc == next.CreatedAtUtc && previous.ExpiresAtUtc == next.ExpiresAtUtc &&
            previous.RenewedAtUtc == next.RenewedAtUtc && previous.ResultIdempotencyKey == next.ResultIdempotencyKey?.Value;
}
