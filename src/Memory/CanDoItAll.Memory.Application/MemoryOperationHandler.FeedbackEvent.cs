using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed partial class MemoryOperationHandler
{
    public async Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
        MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOperationKind(request.OperationKind, MemoryOperationKind.Feedback);
        var selection = await SelectProviderAsync(request.SelectionPolicy, request.Caller.SelectionContext, cancellationToken);
        if (!selection.DispatchAllowed || selection.SelectedProvider is null)
        {
            return Rejected<MemoryFeedbackRecord>(selection);
        }

        var feedback = MemoryFeedbackRecord.CreateUnmatched(
            MemoryFeedbackRecordId.New(),
            selection.SelectedProvider.InstanceId,
            request.Payload.Stage,
            request.Payload.Feedback.Outcome,
            request.Caller.Requester,
            request.Payload.UnmatchedReason,
            request.Retention,
            timeProvider.GetUtcNow(),
            request.Payload.Feedback.EconomicImpact);
        await feedbackLedgerStore.SubmitAsync(feedback, cancellationToken);
        return new MemoryOperationHandlerResult<MemoryFeedbackRecord>(
            MemoryOperationHandlerStatus.Accepted,
            selection,
            OperationRecord: null,
            feedback,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            "Memory feedback accepted for delivery.");
    }

    public async Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
        MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOperationKind(request.OperationKind, MemoryOperationKind.EventAcknowledge);
        var selection = await SelectProviderAsync(request.SelectionPolicy, request.Caller.SelectionContext, cancellationToken);
        if (!selection.DispatchAllowed || selection.SelectedProvider is null)
        {
            return Rejected<MemoryEventOutboxRecord>(selection);
        }

        var payload = MemoryPayload.FromJson(JsonSerializer.SerializeToElement(new
        {
            request.Payload.EventId,
            request.Payload.Accepted,
            Reason = NormalizeReason(request.Payload.Reason)
        }));
        var outbox = MemoryEventOutboxRecord.CreateAcknowledgement(
            MemoryEventOutboxRecordId.New(),
            selection.SelectedProvider.InstanceId,
            request.Payload.EventId,
            inboxRecordId: null,
            timeProvider.GetUtcNow(),
            payload);
        await eventLedgerStore.EnqueueOutboxAsync(outbox, cancellationToken);
        return new MemoryOperationHandlerResult<MemoryEventOutboxRecord>(
            MemoryOperationHandlerStatus.Accepted,
            selection,
            OperationRecord: null,
            outbox,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            "Memory provider event acknowledgement queued.");
    }
}
