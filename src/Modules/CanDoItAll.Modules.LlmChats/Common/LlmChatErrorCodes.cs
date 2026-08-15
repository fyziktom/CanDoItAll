namespace CanDoItAll.Modules.LlmChats.Common;

public static class LlmChatErrorCodes
{
    public const string Prefix = "llm-chat.";
    public const string InvalidRequest = Prefix + "invalid-request";
    public const string DefinitionNotFound = Prefix + "definition-not-found";
    public const string DefinitionConcurrencyConflict = Prefix + "definition-concurrency-conflict";
    public const string DefinitionNotActive = Prefix + "definition-not-active";
    public const string ConversationNotFound = Prefix + "conversation-not-found";
    public const string ConversationArchived = Prefix + "conversation-archived";
    public const string TranscriptRevisionConflict = Prefix + "transcript-revision-conflict";
    public const string ActiveTurnConflict = Prefix + "active-turn-conflict";
    public const string OperationNotFound = Prefix + "operation-not-found";
    public const string OperationIdConflict = Prefix + "operation-id-conflict";
    public const string OperationRecoveryRequired = Prefix + "operation-recovery-required";
    public const string DispatcherUnavailable = Prefix + "dispatcher-unavailable";
    public const string ProviderNotFound = Prefix + "provider-not-found";
    public const string ProviderKindMismatch = Prefix + "provider-kind-mismatch";
    public const string ModelNotSupported = Prefix + "model-not-supported";
    public const string ModelSettingsInvalid = Prefix + "model-settings-invalid";
    public const string ThinkingEffortNotSupported = Prefix + "thinking-effort-not-supported";
    public const string RuntimeProfileChanged = Prefix + "runtime-profile-changed";
    public const string Cancelled = Prefix + "cancelled";
    public const string DeadlineExceeded = Prefix + "deadline-exceeded";
    public const string ProviderUnavailable = Prefix + "provider-unavailable";
    public const string StreamLimitExceeded = Prefix + "stream-limit-exceeded";
    public const string StreamCursorInvalid = Prefix + "stream-cursor-invalid";
    public const string StorageConflict = Prefix + "storage-conflict";
    public const string StorageCorrupted = Prefix + "storage-corrupted";
}
