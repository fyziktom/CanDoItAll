using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessRuntimeDispatchGuard {
    internal static async Task<DateTimeOffset?> RequireAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeCommitRequest request, ProcessRuntimeStateEntity? existing, TimeProvider clock,
        CancellationToken cancellationToken) {
        if (request.DispatchPrecondition is not { } expected) {
            return null;
        }
        expected.Validate();
        if (existing is null || request.InitialPlan is not null ||
                request.Decision is { } decision && decision.StepInstanceId != expected.StepInstanceId ||
                request.DecisionBudget?.Expectation.StepInstanceId is { } budgetStep && budgetStep != expected.StepInstanceId) {
            throw new InvalidDataException("Dispatch attachment must address the guarded step of an existing Process run.");
        }
        if (existing.Status != ProcessRuntimeStatus.Active || request.Mutation.State.Status != ProcessRuntimeStatus.Active) {
            throw Refuse(request, ProcessRuntimeDispatchPreconditionRefusal.RuntimeInactive);
        }
        if (existing.RootRunId != existing.RunId) {
            var rootStatus = await context.RuntimeStates.AsNoTracking().Where(item => item.RunId == existing.RootRunId)
                .Select(item => (ProcessRuntimeStatus?)item.Status).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (rootStatus != ProcessRuntimeStatus.Active) {
                throw Refuse(request, ProcessRuntimeDispatchPreconditionRefusal.RuntimeInactive);
            }
        }
        var step = existing.Steps.SingleOrDefault(item => item.StepInstanceId == expected.StepInstanceId.Value);
        var claim = existing.Claims.SingleOrDefault(item => item.ClaimToken == expected.ClaimToken.Value);
        var nextStep = request.Mutation.State.Steps.SingleOrDefault(item => item.StepInstanceId == expected.StepInstanceId);
        var nextClaim = request.Mutation.State.Claims.SingleOrDefault(item => item.ClaimToken == expected.ClaimToken);
        if (step is null || claim is null || nextStep is null || nextClaim is null ||
                step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running) ||
                step.ActiveClaimToken != expected.ClaimToken.Value || claim.StepInstanceId != step.StepInstanceId ||
                claim.OwnerId != expected.OwnerId.Value || claim.AttemptNumber != step.AttemptNumber ||
                claim.Status is not (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) ||
                claim.ResultIdempotencyKey is not null || nextStep.Status != step.Status ||
                nextStep.ActiveClaimToken != expected.ClaimToken || nextStep.AttemptNumber != step.AttemptNumber ||
                nextClaim != new DispatchClaimState(expected.ClaimToken, expected.StepInstanceId, expected.OwnerId, claim.Status,
                    claim.AttemptNumber, claim.CreatedAtUtc, claim.ExpiresAtUtc, claim.RenewedAtUtc, null)) {
            throw Refuse(request, ProcessRuntimeDispatchPreconditionRefusal.ClaimChanged);
        }
        RequireUnexpired(request, claim.ExpiresAtUtc, clock.GetUtcNow());
        return claim.ExpiresAtUtc;
    }

    internal static void RequireUnexpired(ProcessRuntimeCommitRequest request, DateTimeOffset? expiresAtUtc, DateTimeOffset now) {
        if (expiresAtUtc <= now) {
            throw Refuse(request, ProcessRuntimeDispatchPreconditionRefusal.LeaseExpired);
        }
    }

    private static ProcessRuntimeDispatchPreconditionException Refuse(ProcessRuntimeCommitRequest request,
        ProcessRuntimeDispatchPreconditionRefusal refusal)
        => new(request.OriginalState.RunId, request.DispatchPrecondition!.StepInstanceId, refusal);
}
