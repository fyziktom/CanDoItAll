using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

internal static class LlmChatOperationFailureCodes
{
    public static bool TryMap(Exception exception, out string failureCode)
    {
        failureCode = exception switch
        {
            LlmChatConversationEngineException engineException => engineException.Code,
            LlmInvocationException { Kind: LlmInvocationFailureKind.InvalidRequest } =>
                LlmChatErrorCodes.ModelSettingsInvalid,
            LlmInvocationException { Kind: LlmInvocationFailureKind.DeadlineExceeded } =>
                LlmChatErrorCodes.DeadlineExceeded,
            LlmInvocationException => LlmChatErrorCodes.ProviderUnavailable,
            LlmConversationException { Kind: LlmConversationFailureKind.RevisionConflict } =>
                LlmChatErrorCodes.TranscriptRevisionConflict,
            LlmConversationException { Kind: LlmConversationFailureKind.TurnAlreadyActive } =>
                LlmChatErrorCodes.ActiveTurnConflict,
            LlmConversationException { Kind: LlmConversationFailureKind.ConcurrencyConflict } =>
                LlmChatErrorCodes.StorageConflict,
            _ => string.Empty
        };
        return failureCode.Length > 0;
    }
}
