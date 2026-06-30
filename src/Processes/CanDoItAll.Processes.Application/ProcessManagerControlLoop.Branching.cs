using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessManagerControlLoop
{
    public async Task<ProcessBranchDecisionHandlingResult> RecordBranchDecisionAsync(
        ProcessBranchDecisionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);
        ValidateUtc(command.OccurredAtUtc, nameof(command.OccurredAtUtc));
        ValidatePayloadHash(command.Request.PayloadHash, nameof(command.Request.PayloadHash));

        var request = command.Request;
        var duplicate = await dependencies.BranchDecisions.FindByIdempotencyKeyAsync(
            request.RunId,
            request.RequestId,
            request.IdempotencyKey,
            cancellationToken);
        if (duplicate is not null)
        {
            var duplicateEvent = CreateEvent(
                duplicate.RootRunId,
                duplicate.RunId,
                request.CorrelationId,
                request.CausationEventId,
                ProcessRuntimeEventTypes.ManagerBranchDecisionRecorded,
                ProcessEventSensitivity.Normal,
                duplicate.CreatedAtUtc,
                request.PayloadHash);
            var duplicateDecision = NewDecision(
                duplicate.RootRunId,
                duplicate.RunId,
                null,
                ProcessManagerDecisionKind.BranchOutcomeSelected,
                ProcessManagerDecisionStatus.Duplicate,
                request.IdempotencyKey,
                duplicateEvent,
                ProcessRecoveryPolicyDenial.None,
                request.PayloadHash);

            return new ProcessBranchDecisionHandlingResult(
                duplicate,
                duplicateDecision,
                duplicateEvent,
                new ProcessBranchRouteHandoff(
                    duplicate.DecisionId,
                    duplicate.RouteTarget,
                    duplicate.DecisionEventId),
                IsDuplicate: true,
                []);
        }

        var selected = request.Outcomes.SingleOrDefault(outcome => outcome.Id == command.SelectedOutcomeId);
        if (selected is null)
        {
            return await RecordRejectedBranchDecisionAsync(
                command,
                new ProcessValidationFailure(
                    "ManagerBranch.UnknownOutcome",
                    $"Branch decision request '{request.RequestId}' does not contain outcome '{command.SelectedOutcomeId}'."),
                cancellationToken);
        }

        if (IsBackwardRoute(selected.RouteTarget.Kind))
        {
            if (selected.LoopBudget is null)
            {
                return await RecordRejectedBranchDecisionAsync(
                    command,
                    new ProcessValidationFailure(
                        "ManagerBranch.BackwardMissingBudget",
                        $"Branch outcome '{selected.Id}' requires a loop budget before it can route backward."),
                    cancellationToken);
            }

            var fingerprintId = CreateLoopFingerprintId(request, selected);
            var consumption = await dependencies.LoopBudgets.ConsumeAsync(
                new ProcessLoopBudgetConsumption(
                    request.RootRunId,
                    fingerprintId,
                    selected.LoopBudget.MaximumRepeats,
                    request.IdempotencyKey,
                    command.OccurredAtUtc),
                cancellationToken);
            if (consumption.Outcome == ProcessLoopBudgetOutcome.Exhausted)
            {
                return await RecordEscalatedBranchDecisionAsync(
                    command,
                    selected,
                    fingerprintId,
                    consumption,
                    cancellationToken);
            }

            return await RecordAcceptedBranchDecisionAsync(
                command,
                selected,
                fingerprintId,
                consumption.Remaining,
                cancellationToken);
        }

        return await RecordAcceptedBranchDecisionAsync(
            command,
            selected,
            null,
            null,
            cancellationToken);
    }

}
