using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private async Task<ProcessRuntimeCommitResult> CommitAsync(
        ProcessRuntimeStateSnapshot originalState,
        RuntimeCommandId commandId,
        ProcessRuntimeMutation mutation,
        CancellationToken cancellationToken,
        ProcessRuntimeBlockedRecoveryAuthorization? blockedRecoveryAuthorization = null)
    {
        if (mutation.Outcome == ProcessRuntimeTransitionOutcome.Rejected)
        {
            return ProcessRuntimeCommitResult.FromMutation(mutation);
        }

        if (mutation.Outcome == ProcessRuntimeTransitionOutcome.Duplicate)
        {
            return ProcessRuntimeCommitResult.FromMutation(mutation);
        }

        var request = new ProcessRuntimeCommitRequest(commandId, originalState, mutation)
        {
            BlockedRecoveryAuthorization = blockedRecoveryAuthorization
        };

        return await unitOfWork.CommitAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static ProcessRuntimeMutation Applied(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ProcessEventType eventType,
        string payloadHash)
    {
        return Applied(state, [CreateEvent(state, context, eventType, payloadHash)]);
    }

    private static ProcessRuntimeMutation Applied(
        ProcessRuntimeStateSnapshot state,
        IReadOnlyList<ProcessRuntimeEventEnvelope> events)
    {
        return Applied(state, events, []);
    }

    private static ProcessRuntimeMutation Applied(
        ProcessRuntimeStateSnapshot state,
        IReadOnlyList<ProcessRuntimeEventEnvelope> events,
        IReadOnlyList<ProcessArtifactLedgerEvent> artifactLedgerEvents)
    {
        var outbox = new List<ProcessOutboxMessage>(events.Count);
        foreach (var runtimeEvent in events)
        {
            outbox.Add(new ProcessOutboxMessage(
                RuntimeOutboxMessageId.New(),
                runtimeEvent.EventId,
                ProcessOutboxSubscriberKind.RuntimeProjection,
                runtimeEvent.PayloadHash));
        }

        return new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            state,
            events,
            outbox,
            artifactLedgerEvents,
            []);
    }

    private static ProcessRuntimeMutation Duplicate(ProcessRuntimeStateSnapshot state)
    {
        return new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Duplicate,
            state,
            [],
            [],
            [],
            []);
    }

    private static ProcessRuntimeEventEnvelope CreateEvent(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ProcessEventType eventType,
        string payloadHash)
    {
        var envelope = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            state.RootRunId,
            state.RunId,
            context.CorrelationId,
            null,
            context.Actor,
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            context.OccurredAtUtc,
            eventType,
            payloadHash);
        var validation = ProcessRuntimeEventRules.Validate(envelope);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Failures[0].Message);
        }

        return envelope;
    }
}
