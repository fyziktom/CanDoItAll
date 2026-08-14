using System.Text.Json.Serialization;
using CanDoItAll.Modules.LlmChats.Operations;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SendLlmChatTurnApiRequest(
    Guid OperationId,
    long ExpectedTranscriptRevision,
    string Message);

internal sealed record LlmChatOperationApiResponse(
    Guid OperationId,
    Guid ConversationId,
    LlmChatOperationStatus Status,
    long ExpectedTranscriptRevision,
    long? ResultingTranscriptRevision,
    LlmChatMessageApiResponse? AssistantMessage,
    LlmChatOperationFailureApiResponse? Failure,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

internal sealed record LlmChatOperationFailureApiResponse(
    string Code,
    bool Retryable);
