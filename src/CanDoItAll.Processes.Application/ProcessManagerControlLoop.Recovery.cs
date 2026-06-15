using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessManagerControlLoop
{
    public async Task<ProcessRecoveryEvaluationResult> EvaluateRecoveryAsync(
        ProcessRecoveryEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUtc(request.OccurredAtUtc, nameof(request.OccurredAtUtc));
        ValidatePayloadHash(request.PayloadHash, nameof(request.PayloadHash));

        if (request.MaximumAttempts <= 0)
        {
            throw new ArgumentException("Recovery maximum attempts must be greater than zero.", nameof(request));
        }

        var incident = await dependencies.Incidents.LoadAsync(request.IncidentId, cancellationToken)
            ?? throw new InvalidOperationException($"Incident '{request.IncidentId}' was not found.");
        var duplicate = await dependencies.RecoveryRequests.FindByIdempotencyKeyAsync(
            incident.RunId,
            request.IdempotencyKey,
            cancellationToken);
        if (duplicate is not null)
        {
            var duplicateEvent = CreateEvent(
                incident.RootRunId,
                incident.RunId,
                incident.CorrelationId,
                incident.SourceEventId,
                EventTypeForRecoveryStatus(duplicate.Status),
                incident.Sensitivity,
                duplicate.CreatedAtUtc,
                duplicate.PayloadHash);
            var duplicateDecision = NewDecision(
                incident.RootRunId,
                incident.RunId,
                incident.IncidentId,
                DecisionKindForRecoveryStatus(duplicate.Status),
                ProcessManagerDecisionStatus.Duplicate,
                request.IdempotencyKey,
                duplicateEvent,
                duplicate.PolicyDenial,
                duplicate.PayloadHash);

            return new ProcessRecoveryEvaluationResult(
                duplicate,
                duplicateDecision,
                duplicateEvent,
                DispatchHandoffFor(duplicate),
                IsDuplicate: true,
                []);
        }

        var policy = await dependencies.RecoveryPolicy.EvaluateAsync(
            new ProcessRecoveryPolicyContext(incident, request),
            cancellationToken);
        if (policy.Decision != ProcessRecoveryPolicyDecision.Allowed)
        {
            return await RecordDeniedRecoveryAsync(
                incident,
                request,
                policy.Denial,
                cancellationToken);
        }

        var consumption = await dependencies.LoopBudgets.ConsumeAsync(
            new ProcessLoopBudgetConsumption(
                incident.RootRunId,
                request.LoopFingerprintId,
                request.MaximumAttempts,
                request.IdempotencyKey,
                request.OccurredAtUtc),
            cancellationToken);
        if (consumption.Outcome == ProcessLoopBudgetOutcome.Exhausted)
        {
            return await RecordEscalatedRecoveryAsync(
                incident,
                request,
                consumption,
                cancellationToken);
        }

        var decisionEvent = CreateEvent(
            incident.RootRunId,
            incident.RunId,
            incident.CorrelationId,
            incident.SourceEventId,
            ProcessRuntimeEventTypes.ManagerRecoveryApproved,
            incident.Sensitivity,
            request.OccurredAtUtc,
            request.PayloadHash);
        var recoveryRequest = new ProcessRecoveryRequest(
            request.RecoveryRequestId,
            incident.IncidentId,
            incident.RootRunId,
            incident.RunId,
            request.RequestedAction,
            ProcessRecoveryRequestStatus.Scheduled,
            request.IdempotencyKey,
            request.LoopFingerprintId,
            consumption.ConsumedCount,
            request.MaximumAttempts,
            ProcessRecoveryPolicyDenial.None,
            request.PayloadHash,
            request.OccurredAtUtc,
            decisionEvent.EventId);
        var decision = NewDecision(
            incident.RootRunId,
            incident.RunId,
            incident.IncidentId,
            ProcessManagerDecisionKind.RecoveryApproved,
            ProcessManagerDecisionStatus.Recorded,
            request.IdempotencyKey,
            decisionEvent,
            ProcessRecoveryPolicyDenial.None,
            request.PayloadHash);
        var handoff = new ProcessRecoveryDispatchHandoff(
            recoveryRequest.RecoveryRequestId,
            incident.IncidentId,
            incident.RunId,
            request.RequestedAction,
            decisionEvent.EventId);

        await dependencies.RecoveryRequests.SaveAsync(recoveryRequest, cancellationToken);
        await dependencies.Decisions.SaveAsync(decision, cancellationToken);
        await dependencies.Incidents.UpdateStatusAsync(
            incident.IncidentId,
            ProcessIncidentStatus.Recovering,
            null,
            cancellationToken);

        return new ProcessRecoveryEvaluationResult(
            recoveryRequest,
            decision,
            decisionEvent,
            handoff,
            IsDuplicate: false,
            []);
    }

    private async Task<ProcessRecoveryEvaluationResult> RecordDeniedRecoveryAsync(
        ProcessIncident incident,
        ProcessRecoveryEvaluationRequest request,
        ProcessRecoveryPolicyDenial denial,
        CancellationToken cancellationToken)
    {
        var decisionEvent = CreateEvent(
            incident.RootRunId,
            incident.RunId,
            incident.CorrelationId,
            incident.SourceEventId,
            ProcessRuntimeEventTypes.ManagerRecoveryDenied,
            incident.Sensitivity,
            request.OccurredAtUtc,
            request.PayloadHash);
        var recoveryRequest = new ProcessRecoveryRequest(
            request.RecoveryRequestId,
            incident.IncidentId,
            incident.RootRunId,
            incident.RunId,
            request.RequestedAction,
            ProcessRecoveryRequestStatus.Denied,
            request.IdempotencyKey,
            request.LoopFingerprintId,
            0,
            request.MaximumAttempts,
            denial,
            request.PayloadHash,
            request.OccurredAtUtc,
            decisionEvent.EventId);
        var decision = NewDecision(
            incident.RootRunId,
            incident.RunId,
            incident.IncidentId,
            ProcessManagerDecisionKind.RecoveryDenied,
            ProcessManagerDecisionStatus.Denied,
            request.IdempotencyKey,
            decisionEvent,
            denial,
            request.PayloadHash);

        await dependencies.RecoveryRequests.SaveAsync(recoveryRequest, cancellationToken);
        await dependencies.Decisions.SaveAsync(decision, cancellationToken);
        await dependencies.Incidents.UpdateStatusAsync(
            incident.IncidentId,
            ProcessIncidentStatus.Escalated,
            decisionEvent.EventId,
            cancellationToken);

        return new ProcessRecoveryEvaluationResult(
            recoveryRequest,
            decision,
            decisionEvent,
            null,
            IsDuplicate: false,
            [new ProcessValidationFailure("ManagerRecovery.PolicyDenied", $"Recovery policy denied action '{request.RequestedAction}'.")]);
    }

    private async Task<ProcessRecoveryEvaluationResult> RecordEscalatedRecoveryAsync(
        ProcessIncident incident,
        ProcessRecoveryEvaluationRequest request,
        ProcessLoopBudgetConsumptionResult consumption,
        CancellationToken cancellationToken)
    {
        var decisionEvent = CreateEvent(
            incident.RootRunId,
            incident.RunId,
            incident.CorrelationId,
            incident.SourceEventId,
            ProcessRuntimeEventTypes.ManagerLoopBudgetEscalated,
            incident.Sensitivity,
            request.OccurredAtUtc,
            request.PayloadHash);
        var recoveryRequest = new ProcessRecoveryRequest(
            request.RecoveryRequestId,
            incident.IncidentId,
            incident.RootRunId,
            incident.RunId,
            request.RequestedAction,
            ProcessRecoveryRequestStatus.Escalated,
            request.IdempotencyKey,
            request.LoopFingerprintId,
            consumption.ConsumedCount,
            request.MaximumAttempts,
            ProcessRecoveryPolicyDenial.BudgetUnavailable,
            request.PayloadHash,
            request.OccurredAtUtc,
            decisionEvent.EventId);
        var decision = NewDecision(
            incident.RootRunId,
            incident.RunId,
            incident.IncidentId,
            ProcessManagerDecisionKind.LoopBudgetEscalated,
            ProcessManagerDecisionStatus.Escalated,
            request.IdempotencyKey,
            decisionEvent,
            ProcessRecoveryPolicyDenial.BudgetUnavailable,
            request.PayloadHash);

        await dependencies.RecoveryRequests.SaveAsync(recoveryRequest, cancellationToken);
        await dependencies.Decisions.SaveAsync(decision, cancellationToken);
        await dependencies.Incidents.UpdateStatusAsync(
            incident.IncidentId,
            ProcessIncidentStatus.Escalated,
            decisionEvent.EventId,
            cancellationToken);

        return new ProcessRecoveryEvaluationResult(
            recoveryRequest,
            decision,
            decisionEvent,
            null,
            IsDuplicate: false,
            [new ProcessValidationFailure("ManagerRecovery.LoopBudgetExhausted", $"Loop fingerprint '{request.LoopFingerprintId}' exhausted its recovery budget.")]);
    }

    private static ProcessEventType EventTypeForRecoveryStatus(ProcessRecoveryRequestStatus status)
    {
        return status switch
        {
            ProcessRecoveryRequestStatus.Denied => ProcessRuntimeEventTypes.ManagerRecoveryDenied,
            ProcessRecoveryRequestStatus.Escalated => ProcessRuntimeEventTypes.ManagerLoopBudgetEscalated,
            _ => ProcessRuntimeEventTypes.ManagerRecoveryApproved
        };
    }

    private static ProcessManagerDecisionKind DecisionKindForRecoveryStatus(ProcessRecoveryRequestStatus status)
    {
        return status switch
        {
            ProcessRecoveryRequestStatus.Denied => ProcessManagerDecisionKind.RecoveryDenied,
            ProcessRecoveryRequestStatus.Escalated => ProcessManagerDecisionKind.LoopBudgetEscalated,
            _ => ProcessManagerDecisionKind.RecoveryApproved
        };
    }

    private static ProcessRecoveryDispatchHandoff? DispatchHandoffFor(ProcessRecoveryRequest request)
    {
        if (request.Status != ProcessRecoveryRequestStatus.Scheduled)
        {
            return null;
        }

        return new ProcessRecoveryDispatchHandoff(
            request.RecoveryRequestId,
            request.IncidentId,
            request.RunId,
            request.RequestedAction,
            request.DecisionEventId);
    }
}
