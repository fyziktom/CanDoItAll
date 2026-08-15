using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Application;

namespace CanDoItAll.Web.Api;

internal static class LlmChatOperationApiMapper
{
    public static LlmChatOperationApiResponse ToResponse(LlmChatOperationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var operation = details.Operation;
        var assistant = details.AssistantMessage;
        return new LlmChatOperationApiResponse(
            LlmChatOperationApiSchemas.Operation,
            operation.Id.Value,
            operation.ConversationId.Value,
            operation.Status,
            operation.RequestFingerprint.Value,
            details.Replayed,
            operation.ExpectedTranscriptRevision,
            operation.ResultingTranscriptRevision,
            details.LastEventSequence,
            LlmChatOperationApiRoutes.Status(operation.Id.Value),
            LlmChatOperationApiRoutes.Events(operation.Id.Value),
            LlmChatOperationApiRoutes.Cancel(operation.Id.Value),
            assistant is null
                ? null
                : new LlmChatMessageApiResponse(
                    assistant.EntryId,
                    assistant.TurnId.Value,
                    LlmMessageRole.Assistant,
                    assistant.Content,
                    assistant.CreatedAtUtc,
                    assistant.Model,
                    new LlmChatUsageApiResponse(
                        assistant.Usage.InputTokens,
                        assistant.Usage.OutputTokens,
                        assistant.Usage.CachedInputTokens)),
            string.IsNullOrWhiteSpace(operation.FailureCode)
                ? null
                : new LlmChatOperationFailureApiResponse(
                    operation.FailureCode,
                    LlmChatApiResults.IsRetryable(operation.FailureCode)),
            operation.StartedAtUtc,
            operation.CompletedAtUtc);
    }
}
