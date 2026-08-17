using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SendLlmChatTurnApiRequest(
    Guid OperationId,
    long ExpectedTranscriptRevision,
    string Message);

internal sealed record LlmChatOperationApiResponse(
    string Schema,
    Guid OperationId,
    Guid ConversationId,
    LlmChatOperationStatus Status,
    bool Replayed,
    long ExpectedTranscriptRevision,
    long? ResultingTranscriptRevision,
    long LastEventSequence,
    string StatusUrl,
    string EventsUrl,
    string CancelUrl,
    IReadOnlyList<LlmChatInvocationAttemptApiResponse> InvocationAttempts,
    LlmChatMessageApiResponse? AssistantMessage,
    LlmChatOperationFailureApiResponse? Failure,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

internal sealed record LlmChatOperationFailureApiResponse(
    string Code,
    bool Retryable);

internal sealed record LlmChatInvocationAttemptApiResponse(
    int Ordinal,
    ProviderKind ProviderKind,
    string Model,
    LlmStreamingDeliveryMode DeliveryMode,
    string? FinishReason,
    AgentReasoningEffortLevel? RequestedThinkingEffort,
    AgentReasoningEffortLevel? EffectiveThinkingEffort,
    LlmChatInvocationOutcome Outcome,
    LlmChatUsageApiResponse Usage,
    LlmChatOperationFailureApiResponse? Failure,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

internal static class LlmChatOperationApiRoutes
{
    public static string Status(Guid operationId)
        => $"/api/llm-chat-operations/{operationId:D}";

    public static string Events(Guid operationId)
        => $"/api/llm-chat-operations/{operationId:D}/events";

    public static string Cancel(Guid operationId)
        => $"/api/llm-chat-operations/{operationId:D}/cancel";

    public static string Reconcile(Guid operationId)
        => $"/api/llm-chat-operations/{operationId:D}/reconcile";
}

internal static class LlmChatOperationApiSchemas
{
    public const string Operation = "candoitall.llm-chat-operation.v1";
}
