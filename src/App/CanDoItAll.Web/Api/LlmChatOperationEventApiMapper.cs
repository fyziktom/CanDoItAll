using CanDoItAll.Modules.LlmChats.Operations;

namespace CanDoItAll.Web.Api;

internal static class LlmChatOperationEventApiMapper
{
    public const string Schema = "candoitall.llm-chat-operation-event.v1";

    public static LlmChatOperationEventApiResponse ToResponse(
        LlmChatOperation operation,
        LlmChatOperationEvent operationEvent,
        int aggregateCharacterCount)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(operationEvent);
        if (operation.Id != operationEvent.OperationId)
        {
            throw new InvalidOperationException("An LLM Chat operation event was mapped with the wrong operation.");
        }

        var eventName = ResolveEventName(operationEvent);
        var state = operationEvent is LlmChatOperationStateChangedEvent stateChanged
            ? stateChanged.Status
            : LlmChatOperationStatus.Running;
        var payload = operationEvent switch
        {
            LlmChatOperationStateChangedEvent item => new LlmChatOperationEventPayloadApiResponse(
                Usage: ToUsage(item.Usage),
                FailureCode: EmptyToNull(item.FailureCode),
                Retryable: string.IsNullOrWhiteSpace(item.FailureCode)
                    ? null
                    : LlmChatApiResults.IsRetryable(item.FailureCode),
                OutputIncomplete: item.IsOutputIncomplete,
                AssistantMessageId: item.Status == LlmChatOperationStatus.Succeeded
                    ? operation.AssistantEntryId
                    : null,
                TranscriptRevision: item.Status == LlmChatOperationStatus.Succeeded
                    ? operation.ResultingTranscriptRevision
                    : null,
                CancellationGeneration: item.Status is
                    LlmChatOperationStatus.CancellationRequested or
                    LlmChatOperationStatus.Cancelled
                        ? operation.CancellationGeneration
                        : null,
                CancellationRequestedAtUtc: item.Status is
                    LlmChatOperationStatus.CancellationRequested or
                    LlmChatOperationStatus.Cancelled
                        ? operation.CancellationRequestedAtUtc
                        : null,
                Model: EmptyToNull(item.Model)),
            LlmChatOperationAttemptStartedEvent item => new LlmChatOperationEventPayloadApiResponse(
                AttemptOrdinal: item.AttemptOrdinal,
                Model: item.Model,
                DeliveryMode: item.DeliveryMode),
            LlmChatOperationAttemptFinishedEvent item => new LlmChatOperationEventPayloadApiResponse(
                AttemptOrdinal: item.AttemptOrdinal,
                Outcome: item.Outcome,
                Usage: ToUsage(item.Usage),
                FailureCode: EmptyToNull(item.FailureCode),
                Retryable: string.IsNullOrWhiteSpace(item.FailureCode)
                    ? null
                    : LlmChatApiResults.IsRetryable(item.FailureCode)),
            LlmChatOperationTextDeltaEvent item => new LlmChatOperationEventPayloadApiResponse(
                AttemptOrdinal: item.AttemptOrdinal,
                Text: item.Text,
                AggregateCharacterCount: aggregateCharacterCount),
            _ => throw new ArgumentOutOfRangeException(
                nameof(operationEvent),
                operationEvent.Kind,
                "Unknown LLM Chat operation event kind.")
        };
        var isTerminal = operationEvent is LlmChatOperationStateChangedEvent
        {
            Status: LlmChatOperationStatus.Succeeded or
                LlmChatOperationStatus.Failed or
                LlmChatOperationStatus.Cancelled or
                LlmChatOperationStatus.RecoveryRequired
        };
        return new LlmChatOperationEventApiResponse(
            Schema,
            operation.Id.Value,
            operation.ConversationId.Value,
            operationEvent.Sequence,
            operationEvent.OccurredAtUtc,
            eventName,
            state,
            payload)
        {
            IsTerminal = isTerminal
        };
    }

    private static string ResolveEventName(LlmChatOperationEvent operationEvent)
        => operationEvent switch
        {
            LlmChatOperationStateChangedEvent { Status: LlmChatOperationStatus.Pending } =>
                LlmChatOperationEventNames.Accepted,
            LlmChatOperationStateChangedEvent { Status: LlmChatOperationStatus.Running } =>
                LlmChatOperationEventNames.Claimed,
            LlmChatOperationStateChangedEvent { Status: LlmChatOperationStatus.CancellationRequested } =>
                LlmChatOperationEventNames.CancellationRequested,
            LlmChatOperationStateChangedEvent { Status: LlmChatOperationStatus.Succeeded } =>
                LlmChatOperationEventNames.Succeeded,
            LlmChatOperationStateChangedEvent { Status: LlmChatOperationStatus.Failed } =>
                LlmChatOperationEventNames.Failed,
            LlmChatOperationStateChangedEvent { Status: LlmChatOperationStatus.Cancelled } =>
                LlmChatOperationEventNames.Cancelled,
            LlmChatOperationStateChangedEvent { Status: LlmChatOperationStatus.RecoveryRequired } =>
                LlmChatOperationEventNames.RecoveryRequired,
            LlmChatOperationAttemptStartedEvent => LlmChatOperationEventNames.AttemptStarted,
            LlmChatOperationAttemptFinishedEvent { Outcome: LlmChatInvocationOutcome.Succeeded } =>
                LlmChatOperationEventNames.ResponseCompleted,
            LlmChatOperationAttemptFinishedEvent => LlmChatOperationEventNames.AttemptFinished,
            LlmChatOperationTextDeltaEvent => LlmChatOperationEventNames.ResponseDelta,
            _ => throw new ArgumentOutOfRangeException(
                nameof(operationEvent),
                operationEvent.Kind,
                "Unknown LLM Chat operation event kind.")
        };

    private static LlmChatUsageApiResponse? ToUsage(CanDoItAll.AgentFramework.Llm.Abstractions.LlmUsage? usage)
        => usage is null
            ? null
            : new LlmChatUsageApiResponse(
                usage.InputTokens,
                usage.OutputTokens,
                usage.CachedInputTokens);

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
