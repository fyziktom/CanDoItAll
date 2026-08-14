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
            operation.Id.Value,
            operation.ConversationId.Value,
            operation.Status,
            operation.ExpectedTranscriptRevision,
            operation.ResultingTranscriptRevision,
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
