using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessManagerControlLoop
{
    private async Task<ProcessBranchDecisionHandlingResult> RecordAcceptedBranchDecisionAsync(
        ProcessBranchDecisionCommand command,
        BranchOutcomeDefinition selected,
        ProcessLoopFingerprintId? fingerprintId,
        int? remainingLoopBudget,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var decisionEvent = CreateEvent(
            request.RootRunId,
            request.RunId,
            request.CorrelationId,
            request.CausationEventId,
            ProcessRuntimeEventTypes.ManagerBranchDecisionRecorded,
            ProcessEventSensitivity.Normal,
            command.OccurredAtUtc,
            request.PayloadHash);
        var branchDecision = new ProcessBranchDecision(
            ProcessBranchDecisionId.New(),
            request.RequestId,
            request.RootRunId,
            request.RunId,
            request.FamilyId,
            selected.Id,
            selected.RouteTarget,
            ProcessBranchDecisionStatus.Recorded,
            request.IdempotencyKey,
            fingerprintId,
            remainingLoopBudget,
            command.Confidence,
            decisionEvent.EventId,
            command.OccurredAtUtc);
        var managerDecision = NewDecision(
            request.RootRunId,
            request.RunId,
            null,
            ProcessManagerDecisionKind.BranchOutcomeSelected,
            ProcessManagerDecisionStatus.Recorded,
            request.IdempotencyKey,
            decisionEvent,
            ProcessRecoveryPolicyDenial.None,
            request.PayloadHash);
        var handoff = new ProcessBranchRouteHandoff(
            branchDecision.DecisionId,
            selected.RouteTarget,
            decisionEvent.EventId);

        await dependencies.BranchDecisions.SaveAsync(branchDecision, cancellationToken);
        await dependencies.Decisions.SaveAsync(managerDecision, cancellationToken);

        return new ProcessBranchDecisionHandlingResult(
            branchDecision,
            managerDecision,
            decisionEvent,
            handoff,
            IsDuplicate: false,
            []);
    }

    private async Task<ProcessBranchDecisionHandlingResult> RecordRejectedBranchDecisionAsync(
        ProcessBranchDecisionCommand command,
        ProcessValidationFailure failure,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var decisionEvent = CreateEvent(
            request.RootRunId,
            request.RunId,
            request.CorrelationId,
            request.CausationEventId,
            ProcessRuntimeEventTypes.ManagerBranchDecisionRejected,
            ProcessEventSensitivity.Normal,
            command.OccurredAtUtc,
            request.PayloadHash);
        var branchDecision = new ProcessBranchDecision(
            ProcessBranchDecisionId.New(),
            request.RequestId,
            request.RootRunId,
            request.RunId,
            request.FamilyId,
            command.SelectedOutcomeId,
            new ProcessRouteTarget(ProcessRouteTargetKind.Escalate),
            ProcessBranchDecisionStatus.Rejected,
            request.IdempotencyKey,
            null,
            null,
            command.Confidence,
            decisionEvent.EventId,
            command.OccurredAtUtc);
        var managerDecision = NewDecision(
            request.RootRunId,
            request.RunId,
            null,
            ProcessManagerDecisionKind.BranchOutcomeRejected,
            ProcessManagerDecisionStatus.Rejected,
            request.IdempotencyKey,
            decisionEvent,
            ProcessRecoveryPolicyDenial.None,
            request.PayloadHash);

        await dependencies.BranchDecisions.SaveAsync(branchDecision, cancellationToken);
        await dependencies.Decisions.SaveAsync(managerDecision, cancellationToken);

        return new ProcessBranchDecisionHandlingResult(
            branchDecision,
            managerDecision,
            decisionEvent,
            null,
            IsDuplicate: false,
            [failure]);
    }

    private async Task<ProcessBranchDecisionHandlingResult> RecordEscalatedBranchDecisionAsync(
        ProcessBranchDecisionCommand command,
        BranchOutcomeDefinition selected,
        ProcessLoopFingerprintId fingerprintId,
        ProcessLoopBudgetConsumptionResult consumption,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var escalationTarget = selected.LoopBudget?.EscalationTarget
            ?? new ProcessRouteTarget(ProcessRouteTargetKind.Escalate);
        var decisionEvent = CreateEvent(
            request.RootRunId,
            request.RunId,
            request.CorrelationId,
            request.CausationEventId,
            ProcessRuntimeEventTypes.ManagerLoopBudgetEscalated,
            ProcessEventSensitivity.Normal,
            command.OccurredAtUtc,
            request.PayloadHash);
        var branchDecision = new ProcessBranchDecision(
            ProcessBranchDecisionId.New(),
            request.RequestId,
            request.RootRunId,
            request.RunId,
            request.FamilyId,
            selected.Id,
            escalationTarget,
            ProcessBranchDecisionStatus.Escalated,
            request.IdempotencyKey,
            fingerprintId,
            consumption.Remaining,
            command.Confidence,
            decisionEvent.EventId,
            command.OccurredAtUtc);
        var managerDecision = NewDecision(
            request.RootRunId,
            request.RunId,
            null,
            ProcessManagerDecisionKind.LoopBudgetEscalated,
            ProcessManagerDecisionStatus.Escalated,
            request.IdempotencyKey,
            decisionEvent,
            ProcessRecoveryPolicyDenial.BudgetUnavailable,
            request.PayloadHash);
        var handoff = new ProcessBranchRouteHandoff(
            branchDecision.DecisionId,
            escalationTarget,
            decisionEvent.EventId);

        await dependencies.BranchDecisions.SaveAsync(branchDecision, cancellationToken);
        await dependencies.Decisions.SaveAsync(managerDecision, cancellationToken);

        return new ProcessBranchDecisionHandlingResult(
            branchDecision,
            managerDecision,
            decisionEvent,
            handoff,
            IsDuplicate: false,
            [new ProcessValidationFailure("ManagerBranch.LoopBudgetExhausted", $"Loop fingerprint '{fingerprintId}' exhausted its branch budget.")]);
    }

    private static ProcessLoopFingerprintId CreateLoopFingerprintId(
        ProcessBranchDecisionRequest request,
        BranchOutcomeDefinition selected)
    {
        var fingerprint = ProcessLoopFingerprint.Create(new LoopFingerprintInput(
            request.RootRunId,
            request.StepDefinitionId,
            request.FamilyId,
            selected.Id,
            selected.LoopBudget!.FingerprintPolicyId,
            request.EvidenceKeys));

        return new ProcessLoopFingerprintId(fingerprint);
    }

    private static bool IsBackwardRoute(ProcessRouteTargetKind kind)
    {
        return kind == ProcessRouteTargetKind.PreviousStep;
    }
}
