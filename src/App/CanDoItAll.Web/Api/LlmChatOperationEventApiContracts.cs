using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using System.Text.Json.Serialization;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Data of one server-sent event of <c>GET /api/llm-chat-operations/{operationId}/events</c>, contract
/// <c>candoitall.llm-chat-operation-event.v1</c>. The stream serializer writes camel-case member names and camel-case
/// string enum values. Members of <c>payload</c> that do not apply to the event type are null.
/// </summary>
/// <param name="Schema">Contract identifier, always <c>candoitall.llm-chat-operation-event.v1</c>.</param>
/// <param name="OperationId">Identifier of the operation.</param>
/// <param name="ConversationId">Identifier of the conversation the operation belongs to.</param>
/// <param name="Sequence">
/// Sequence of the event within the operation, starting at 1; equal to the SSE <c>id</c>.
/// </param>
/// <param name="OccurredAtUtc">Instant the event occurred, in UTC.</param>
/// <param name="EventKind">
/// Event type, equal to the SSE <c>event</c> field, for example <c>llm.response.delta</c>.
/// </param>
/// <param name="OperationState">
/// Operation status set by a state event (<c>pending</c>, <c>running</c>, <c>cancellationRequested</c>,
/// <c>succeeded</c>, <c>failed</c>, <c>cancelled</c> or <c>recoveryRequired</c>); always <c>running</c> for attempt and
/// delta events.
/// </param>
/// <param name="Payload">Event-specific details.</param>
internal sealed record LlmChatOperationEventApiResponse(
    string Schema,
    Guid OperationId,
    Guid ConversationId,
    long Sequence,
    DateTimeOffset OccurredAtUtc,
    string EventKind,
    LlmChatOperationStatus OperationState,
    LlmChatOperationEventPayloadApiResponse Payload)
{
    /// <summary>
    /// True for the events that end the stream (succeeded, failed, cancelled, recovery required). Not serialized.
    /// </summary>
    [JsonIgnore]
    public bool IsTerminal { get; init; }
}

/// <summary>
/// Event-specific details of an LLM Chat operation event. Every member is written; members that do not apply to the
/// event type are null.
/// </summary>
/// <param name="AttemptOrdinal">Provider attempt the event belongs to (attempt and delta events).</param>
/// <param name="Text">
/// A chunk of reply text (delta events). It is provisional: it becomes part of the transcript only when
/// <c>llm.operation.succeeded</c> follows.
/// </param>
/// <param name="AggregateCharacterCount">
/// Total characters (UTF-16 code units) of reply text delivered by delta events up to and including this one (delta
/// events).
/// </param>
/// <param name="Model">Model of the attempt (attempt events) or of the committed reply (succeeded event).</param>
/// <param name="DeliveryMode">
/// <c>incremental</c> (streamed) or <c>completedFallback</c> (obtained in one piece), for attempt events.
/// </param>
/// <param name="FinishReason">Finish reason of a successful attempt (<c>llm.response.completed</c>).</param>
/// <param name="Outcome">
/// <c>succeeded</c>, <c>failed</c> or <c>cancelled</c>, for <c>llm.response.completed</c> and
/// <c>llm.provider.attempt-finished</c>.
/// </param>
/// <param name="Usage">
/// Token usage of the attempt (finished-attempt events), of the committed reply (succeeded event) or of all attempts
/// (failed and cancelled events).
/// </param>
/// <param name="FailureCode">
/// Failure code of a failed or cancelled attempt, or of a failed, cancelled or recovery-required operation.
/// </param>
/// <param name="Retryable">Whether that failure is transient; present whenever <c>failureCode</c> is.</param>
/// <param name="OutputIncomplete">
/// On state events: true when the operation failed, was cancelled or requires recovery, so streamed text must be
/// discarded; false otherwise.
/// </param>
/// <param name="AssistantMessageId">Entry identifier of the committed reply (succeeded event).</param>
/// <param name="TranscriptRevision">Transcript revision after the reply was committed (succeeded event).</param>
/// <param name="CancellationGeneration">
/// Cancellation generation of the operation: greater than 0 once cancellation was requested, 0 when it never was
/// (cancellation-requested and cancelled events).
/// </param>
/// <param name="CancellationRequestedAtUtc">
/// Instant cancellation was requested, in UTC (cancellation-requested and cancelled events).
/// </param>
internal sealed record LlmChatOperationEventPayloadApiResponse(
    int? AttemptOrdinal = null,
    string? Text = null,
    int? AggregateCharacterCount = null,
    string? Model = null,
    LlmStreamingDeliveryMode? DeliveryMode = null,
    string? FinishReason = null,
    LlmChatInvocationOutcome? Outcome = null,
    LlmChatUsageApiResponse? Usage = null,
    string? FailureCode = null,
    bool? Retryable = null,
    bool? OutputIncomplete = null,
    Guid? AssistantMessageId = null,
    long? TranscriptRevision = null,
    long? CancellationGeneration = null,
    DateTimeOffset? CancellationRequestedAtUtc = null);

internal static class LlmChatOperationEventNames
{
    public const string Accepted = "llm.operation.accepted";
    public const string Claimed = "llm.operation.claimed";
    public const string AttemptStarted = "llm.provider.attempt-started";
    public const string AttemptFinished = "llm.provider.attempt-finished";
    public const string ResponseDelta = "llm.response.delta";
    public const string ResponseCompleted = "llm.response.completed";
    public const string CancellationRequested = "llm.operation.cancellation-requested";
    public const string Succeeded = "llm.operation.succeeded";
    public const string Failed = "llm.operation.failed";
    public const string Cancelled = "llm.operation.cancelled";
    public const string RecoveryRequired = "llm.operation.recovery-required";
}
