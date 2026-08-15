using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Operations;
using System.Text.Json.Serialization;

namespace CanDoItAll.Web.Api;

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
    [JsonIgnore]
    public bool IsTerminal { get; init; }
}

internal sealed record LlmChatOperationEventPayloadApiResponse(
    int? AttemptOrdinal = null,
    string? Text = null,
    int? AggregateCharacterCount = null,
    string? Model = null,
    LlmStreamingDeliveryMode? DeliveryMode = null,
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
