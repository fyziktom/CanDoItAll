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
        if (details.Invocations.Count > LlmChatOperationDetails.MaximumInvocationRecords)
        {
            throw new InvalidOperationException("The operation invocation history exceeds its public bound.");
        }

        return new LlmChatOperationApiResponse(
            LlmChatOperationApiSchemas.Operation,
            operation.Id.Value,
            operation.ConversationId.Value,
            operation.Status,
            details.Replayed,
            operation.ExpectedTranscriptRevision,
            operation.ResultingTranscriptRevision,
            details.LastEventSequence,
            LlmChatOperationApiRoutes.Status(operation.Id.Value),
            LlmChatOperationApiRoutes.Events(operation.Id.Value),
            LlmChatOperationApiRoutes.Cancel(operation.Id.Value),
            [.. details.Invocations
                .OrderBy(invocation => invocation.Ordinal)
                .Select(ToInvocationResponse)],
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

    private static LlmChatInvocationAttemptApiResponse ToInvocationResponse(
        CanDoItAll.Modules.LlmChats.Operations.LlmChatInvocationRecord invocation)
        => new(
            invocation.Ordinal,
            invocation.ProviderKind,
            invocation.Model,
            invocation.DeliveryMode,
            string.IsNullOrEmpty(invocation.FinishReason) ? null : invocation.FinishReason,
            invocation.RequestedThinkingEffort,
            invocation.EffectiveThinkingEffort,
            invocation.Outcome,
            new LlmChatUsageApiResponse(
                invocation.Usage.InputTokens,
                invocation.Usage.OutputTokens,
                invocation.Usage.CachedInputTokens),
            string.IsNullOrEmpty(invocation.FailureCode)
                ? null
                : new LlmChatOperationFailureApiResponse(
                    invocation.FailureCode,
                    LlmChatApiResults.IsRetryable(invocation.FailureCode)),
            invocation.StartedAtUtc,
            invocation.CompletedAtUtc);
}
