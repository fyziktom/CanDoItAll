namespace CanDoItAll.Modules.LlmChats.Common;

public static class LlmChatErrorCodes
{
    public const string InvalidRequest = "llm-chat.invalid-request";
    public const string DefinitionNotFound = "llm-chat.definition-not-found";
    public const string DefinitionConcurrencyConflict = "llm-chat.definition-concurrency-conflict";
    public const string DefinitionNotActive = "llm-chat.definition-not-active";
    public const string ConversationNotFound = "llm-chat.conversation-not-found";
    public const string ConversationArchived = "llm-chat.conversation-archived";
    public const string TranscriptRevisionConflict = "llm-chat.transcript-revision-conflict";
    public const string ActiveTurnConflict = "llm-chat.active-turn-conflict";
    public const string OperationNotFound = "llm-chat.operation-not-found";
    public const string OperationIdConflict = "llm-chat.operation-id-conflict";
    public const string OperationRecoveryRequired = "llm-chat.operation-recovery-required";
    public const string ProviderNotFound = "llm-chat.provider-not-found";
    public const string ProviderKindMismatch = "llm-chat.provider-kind-mismatch";
    public const string ModelNotSupported = "llm-chat.model-not-supported";
    public const string ModelSettingsInvalid = "llm-chat.model-settings-invalid";
    public const string ThinkingEffortNotSupported = "llm-chat.thinking-effort-not-supported";
    public const string RuntimeProfileChanged = "llm-chat.runtime-profile-changed";
    public const string Cancelled = "llm-chat.cancelled";
    public const string DeadlineExceeded = "llm-chat.deadline-exceeded";
    public const string ProviderUnavailable = "llm-chat.provider-unavailable";
    public const string StorageConflict = "llm-chat.storage-conflict";
    public const string StorageCorrupted = "llm-chat.storage-corrupted";
}
