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

    private static ProcessRuntimeMutation Applied(ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context, ProcessEventType eventType, string payloadHash)
        => ProcessRuntimeMutation.Applied(state, [CreateEvent(state, context, eventType, payloadHash)], []);

    private static ProcessRuntimeMutation Applied(ProcessRuntimeStateSnapshot state,
        IReadOnlyList<ProcessRuntimeEventEnvelope> events)
        => ProcessRuntimeMutation.Applied(state, events, []);

    private static ProcessRuntimeMutation Applied(ProcessRuntimeStateSnapshot state,
        IReadOnlyList<ProcessRuntimeEventEnvelope> events, IReadOnlyList<ProcessArtifactLedgerEvent> artifactLedgerEvents)
        => ProcessRuntimeMutation.Applied(state, events, artifactLedgerEvents);

    private static ProcessRuntimeMutation Duplicate(ProcessRuntimeStateSnapshot state)
        => ProcessRuntimeMutation.Duplicate(state);

    private static ProcessRuntimeEventEnvelope CreateEvent(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessEventType eventType, string payloadHash)
        => ProcessRuntimeEventFactory.Create(state, context, eventType, payloadHash);
}
