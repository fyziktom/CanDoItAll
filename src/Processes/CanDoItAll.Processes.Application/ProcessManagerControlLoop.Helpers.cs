using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessManagerControlLoop
{
    private static ProcessManagerWorkItem NewWorkItem(
        ProcessManagerWorkItemKind kind,
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessCorrelationId correlationId,
        RuntimeEventId? causationId,
        ProcessManagerIdempotencyKey idempotencyKey,
        ProcessManagerWorkPriority priority,
        ProcessEventSensitivity sensitivity,
        string payloadHash,
        DateTimeOffset enqueuedAtUtc)
    {
        return new ProcessManagerWorkItem(
            ProcessManagerWorkItemId.New(),
            kind,
            rootRunId,
            runId,
            correlationId,
            causationId,
            idempotencyKey,
            priority,
            sensitivity,
            payloadHash,
            enqueuedAtUtc);
    }

    private static ProcessManagerDecision NewDecision(
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessIncidentId? incidentId,
        ProcessManagerDecisionKind kind,
        ProcessManagerDecisionStatus status,
        ProcessManagerIdempotencyKey idempotencyKey,
        ProcessRuntimeEventEnvelope decisionEvent,
        ProcessRecoveryPolicyDenial policyDenial,
        string payloadHash)
    {
        return new ProcessManagerDecision(
            ProcessManagerDecisionId.New(),
            rootRunId,
            runId,
            incidentId,
            kind,
            status,
            idempotencyKey,
            decisionEvent.EventId,
            policyDenial,
            payloadHash,
            decisionEvent.OccurredAtUtc);
    }

    private static ProcessRuntimeEventEnvelope CreateEvent(
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessCorrelationId correlationId,
        RuntimeEventId? causationId,
        ProcessEventType eventType,
        ProcessEventSensitivity sensitivity,
        DateTimeOffset occurredAtUtc,
        string payloadHash)
    {
        return new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            rootRunId,
            runId,
            correlationId,
            causationId,
            ManagerActor,
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            sensitivity,
            occurredAtUtc,
            eventType,
            payloadHash);
    }

    private static void ValidateUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Manager timestamps must be UTC.", parameterName);
        }
    }

    private static void ValidatePayloadHash(string payloadHash, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(payloadHash))
        {
            throw new ArgumentException("Manager payload hash must be present.", parameterName);
        }
    }
}
