using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal static class ProcessCancellationRuntimeReceipt {
    internal static void RequireCancellation(ProcessRunId rootRunId, ProcessCancellationRecoveryIntent intent,
        ProcessRuntimeCommitResult result, bool confirmation = false) {
        if (!result.Succeeded || result.State.RunId != intent.RunId || result.State.RootRunId != rootRunId ||
                result.State.Status != ProcessRuntimeStatus.Cancelled || result.Decision is not null || result.DecisionBudget is not null ||
                result.DecisionRunScope is not null || result.ArtifactLedgerEvents.Count != 0 || result.Diagnostics.Count != 0) {
            throw new InvalidDataException("The cancellation receipt differs from the original selected run.");
        }
        if (result.Events.Count == 0 && (!result.CommittedNow || confirmation) && result.OutboxMessages.Count == 0) {
            return;
        }
        if (confirmation || result.Events.Count != 1 || result.Events[0] is not { } applied ||
                applied.EventType != ProcessRuntimeEventTypes.ProcessRunCancelled ||
                applied.RootRunId != rootRunId || applied.RunId != intent.RunId || applied.Actor != intent.Context.Actor ||
                applied.CorrelationId != intent.Context.CorrelationId || applied.OccurredAtUtc != intent.Context.OccurredAtUtc ||
                result.OutboxMessages.Count != 1 || result.OutboxMessages[0] is not { } notification ||
                notification.EventId != applied.EventId || notification.PayloadHash != applied.PayloadHash ||
                notification.SubscriberKind != ProcessOutboxSubscriberKind.RuntimeProjection) {
            throw new InvalidDataException("The cancellation receipt differs from the original selected run and command context.");
        }
    }
}
