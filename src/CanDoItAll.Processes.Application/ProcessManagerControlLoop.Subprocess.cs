using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessManagerControlLoop
{
    public async Task<ProcessSubprocessMessageResult> SendSubprocessMessageAsync(
        ProcessSubprocessMessageDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ValidateUtc(draft.OccurredAtUtc, nameof(draft.OccurredAtUtc));
        ValidatePayloadHash(draft.PayloadHash, nameof(draft.PayloadHash));

        if (draft.Sensitivity == ProcessEventSensitivity.Unspecified)
        {
            throw new ArgumentException("Subprocess message sensitivity must be explicit.", nameof(draft));
        }

        var duplicate = await dependencies.SubprocessMessages.FindByIdempotencyKeyAsync(
            draft.ParentRunId,
            draft.ChildRunId,
            draft.IdempotencyKey,
            cancellationToken);
        if (duplicate is not null)
        {
            var duplicateEvent = CreateEvent(
                duplicate.ParentRunId,
                duplicate.ParentRunId,
                duplicate.CorrelationId,
                duplicate.CausationId,
                ProcessRuntimeEventTypes.ManagerSubprocessMessageQueued,
                duplicate.Sensitivity,
                duplicate.CreatedAtUtc,
                duplicate.PayloadHash);
            var duplicateDecision = NewDecision(
                duplicate.ParentRunId,
                duplicate.ParentRunId,
                null,
                ProcessManagerDecisionKind.SubprocessMessageQueued,
                ProcessManagerDecisionStatus.Duplicate,
                duplicate.IdempotencyKey,
                duplicateEvent,
                ProcessRecoveryPolicyDenial.None,
                duplicate.PayloadHash);
            var duplicateWorkItem = NewWorkItem(
                ProcessManagerWorkItemKind.SubprocessMessage,
                duplicate.ParentRunId,
                duplicate.ChildRunId,
                duplicate.CorrelationId,
                duplicate.CausationId,
                duplicate.IdempotencyKey,
                ProcessManagerWorkPriority.Normal,
                duplicate.Sensitivity,
                duplicate.PayloadHash,
                duplicate.CreatedAtUtc);

            return new ProcessSubprocessMessageResult(
                duplicate,
                duplicateDecision,
                duplicateEvent,
                duplicateWorkItem,
                IsDuplicate: true,
                []);
        }

        var message = new ProcessSubprocessControlMessage(
            draft.MessageId,
            draft.Kind,
            draft.Direction,
            draft.ParentRunId,
            draft.ChildRunId,
            draft.ParentStepInstanceId,
            draft.CorrelationId,
            draft.CausationId,
            draft.SchemaVersion,
            draft.Sensitivity,
            draft.ArtifactReferences,
            draft.IdempotencyKey,
            draft.PayloadHash,
            draft.OccurredAtUtc);
        var decisionEvent = CreateEvent(
            draft.ParentRunId,
            draft.ParentRunId,
            draft.CorrelationId,
            draft.CausationId,
            ProcessRuntimeEventTypes.ManagerSubprocessMessageQueued,
            draft.Sensitivity,
            draft.OccurredAtUtc,
            draft.PayloadHash);
        var decision = NewDecision(
            draft.ParentRunId,
            draft.ParentRunId,
            null,
            ProcessManagerDecisionKind.SubprocessMessageQueued,
            ProcessManagerDecisionStatus.Recorded,
            draft.IdempotencyKey,
            decisionEvent,
            ProcessRecoveryPolicyDenial.None,
            draft.PayloadHash);
        var workItem = NewWorkItem(
            ProcessManagerWorkItemKind.SubprocessMessage,
            draft.ParentRunId,
            draft.ChildRunId,
            draft.CorrelationId,
            draft.CausationId,
            draft.IdempotencyKey,
            ProcessManagerWorkPriority.Normal,
            draft.Sensitivity,
            draft.PayloadHash,
            draft.OccurredAtUtc);

        await dependencies.SubprocessMessages.SaveAsync(message, cancellationToken);
        await dependencies.Decisions.SaveAsync(decision, cancellationToken);
        await dependencies.Queue.EnqueueAsync(workItem, cancellationToken);

        return new ProcessSubprocessMessageResult(
            message,
            decision,
            decisionEvent,
            workItem,
            IsDuplicate: false,
            []);
    }
}
