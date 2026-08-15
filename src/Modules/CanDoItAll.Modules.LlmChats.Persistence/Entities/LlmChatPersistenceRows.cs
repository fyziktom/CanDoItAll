using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;

namespace CanDoItAll.Modules.LlmChats.Persistence.Entities;

internal sealed class LlmChatDefinitionRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string AvatarImageUrl { get; set; } = string.Empty;

    public LlmChatDefinitionStatus Status { get; set; }

    public int CurrentRevision { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public long ConcurrencyToken { get; set; }
}

internal sealed class LlmChatDefinitionRevisionRow
{
    public Guid DefinitionId { get; set; }

    public int Revision { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string AvatarImageUrl { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    public Guid ProviderProfileId { get; set; }

    public ProviderKind ProviderKind { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public double? Temperature { get; set; }

    public AgentReasoningEffortLevel? ThinkingEffort { get; set; }

    public string ModelParameterConfigurationJson { get; set; } = string.Empty;

    public long? TimeoutTicks { get; set; }

    public bool HasResponseFormat { get; set; }

    public bool ResponseRequireJson { get; set; }

    public string ResponseSchemaJson { get; set; } = string.Empty;

    public string ResponseSchemaName { get; set; } = string.Empty;

    public string ResponseSchemaDescription { get; set; } = string.Empty;

    public string SettingsFingerprint { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string Reason { get; set; } = string.Empty;
}

internal sealed class LlmChatDefinitionTagRow
{
    public Guid DefinitionId { get; set; }

    public string Tag { get; set; } = string.Empty;
}

internal sealed class LlmChatConversationRow
{
    public Guid Id { get; set; }

    public Guid DefinitionId { get; set; }

    public int DefinitionRevision { get; set; }

    public string Title { get; set; } = string.Empty;

    public LlmChatConversationStatus Status { get; set; }

    public LlmChatConversationOrigin Origin { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public long ConcurrencyToken { get; set; }
}

internal sealed class LlmChatTranscriptRow
{
    public Guid ConversationId { get; set; }

    public Guid ProviderId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public ProviderKind ProviderKind { get; set; }

    public string Model { get; set; } = string.Empty;

    public long TranscriptRevision { get; set; }

    public int EntryCount { get; set; }

    public Guid? ActiveTurnId { get; set; }

    public Guid? PendingUserEntryId { get; set; }

    public DateTimeOffset? TurnAdmittedAtUtc { get; set; }

    public long? TurnAdmittedRevision { get; set; }

    public Guid? CompensationProviderId { get; set; }

    public string? CompensationProviderName { get; set; }

    public ProviderKind? CompensationProviderKind { get; set; }

    public string? CompensationModel { get; set; }

    public string? CompensationAccelerationStrategyId { get; set; }

    public string? CompensationAccelerationProviderName { get; set; }

    public string? CompensationAccelerationModel { get; set; }

    public string? CompensationAccelerationPayloadJson { get; set; }

    public string? AccelerationStrategyId { get; set; }

    public string? AccelerationProviderName { get; set; }

    public string? AccelerationModel { get; set; }

    public string? AccelerationPayloadJson { get; set; }
}

internal sealed class LlmChatMessageRow
{
    public Guid EntryId { get; set; }

    public Guid ConversationId { get; set; }

    public long Sequence { get; set; }

    public Guid TurnId { get; set; }

    public LlmMessageRole Role { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string Model { get; set; } = string.Empty;

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public int? CachedInputTokens { get; set; }
}

internal sealed class LlmChatOperationRow
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public LlmChatOperationKind Kind { get; set; }

    public string RequestFingerprint { get; set; } = string.Empty;

    public long ExpectedTranscriptRevision { get; set; }

    public LlmChatOperationStatus Status { get; set; }

    public DateTimeOffset? CancellationRequestedAtUtc { get; set; }

    public long CancellationGeneration { get; set; }

    public DateTimeOffset? TurnAdmittedAtUtc { get; set; }

    public DateTimeOffset? ProviderDispatchStartedAtUtc { get; set; }

    public DateTimeOffset? ProviderDispatchReturnedAtUtc { get; set; }

    public DateTimeOffset? TranscriptCompletedAtUtc { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public long? ResultingTranscriptRevision { get; set; }

    public Guid? AssistantEntryId { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public long ConcurrencyToken { get; set; }
}

internal sealed class LlmChatInvocationRecordRow
{
    public Guid OperationId { get; set; }

    public int Ordinal { get; set; }

    public Guid ProviderProfileId { get; set; }

    public ProviderKind ProviderKind { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public AgentReasoningEffortLevel? RequestedThinkingEffort { get; set; }

    public AgentReasoningEffortLevel? EffectiveThinkingEffort { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int CachedInputTokens { get; set; }

    public LlmChatInvocationOutcome Outcome { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}
