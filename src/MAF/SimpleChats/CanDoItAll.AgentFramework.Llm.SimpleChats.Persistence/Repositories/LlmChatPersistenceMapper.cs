using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;

internal static class LlmChatPersistenceMapper
{
    public static LlmChatDefinitionRow ToRow(LlmChatDefinition definition)
        => new()
        {
            Id = definition.Id.Value,
            Name = definition.Name,
            Summary = definition.Summary,
            AvatarImageUrl = definition.AvatarImageUrl,
            Status = definition.Status,
            CurrentRevision = definition.CurrentRevision.Value,
            CreatedAtUtc = definition.CreatedAtUtc,
            UpdatedAtUtc = definition.UpdatedAtUtc,
            ConcurrencyToken = definition.ConcurrencyToken
        };

    public static LlmChatDefinition ToDomain(LlmChatDefinitionRow row)
        => new(
            new LlmChatDefinitionId(row.Id),
            row.Name,
            row.Summary,
            row.AvatarImageUrl,
            row.Status,
            new LlmChatDefinitionRevisionNumber(row.CurrentRevision),
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.ConcurrencyToken);

    public static LlmChatDefinitionRevisionRow ToRow(LlmChatDefinitionRevision revision)
        => new()
        {
            DefinitionId = revision.DefinitionId.Value,
            Revision = revision.Revision.Value,
            Name = revision.Name,
            Summary = revision.Summary,
            AvatarImageUrl = revision.AvatarImageUrl,
            SystemPrompt = revision.SystemPrompt,
            ProviderProfileId = revision.ProviderProfileId,
            ProviderKind = revision.ProviderKind,
            ProviderName = revision.ProviderName,
            Model = revision.Model,
            Temperature = revision.Settings.Temperature,
            ThinkingEffort = revision.Settings.ThinkingEffort,
            ModelParameterConfigurationJson = revision.Settings.ModelParameterConfigurationJson,
            TimeoutTicks = revision.Timeout?.Ticks,
            HasResponseFormat = revision.ResponseFormat is not null,
            ResponseRequireJson = revision.ResponseFormat?.RequireJson ?? false,
            ResponseSchemaJson = revision.ResponseFormat?.SchemaJson ?? string.Empty,
            ResponseSchemaName = revision.ResponseFormat?.SchemaName ?? string.Empty,
            ResponseSchemaDescription = revision.ResponseFormat?.SchemaDescription ?? string.Empty,
            SettingsFingerprint = revision.SettingsFingerprint.Value,
            CreatedAtUtc = revision.CreatedAtUtc,
            Reason = revision.Reason
        };

    public static LlmChatDefinitionRevision ToDomain(LlmChatDefinitionRevisionRow row)
    {
        var revision = new LlmChatDefinitionRevision(
            new LlmChatDefinitionId(row.DefinitionId),
            new LlmChatDefinitionRevisionNumber(row.Revision),
            row.Name,
            row.Summary,
            row.AvatarImageUrl,
            row.SystemPrompt,
            row.ProviderProfileId,
            row.ProviderKind,
            row.ProviderName,
            row.Model,
            new LlmModelSettings(row.Temperature, row.ModelParameterConfigurationJson)
            {
                ThinkingEffort = row.ThinkingEffort
            },
            row.TimeoutTicks is { } timeoutTicks ? TimeSpan.FromTicks(timeoutTicks) : null,
            row.HasResponseFormat
                ? new LlmResponseFormat(
                    row.ResponseRequireJson,
                    row.ResponseSchemaJson,
                    row.ResponseSchemaName,
                    row.ResponseSchemaDescription)
                : null,
            row.CreatedAtUtc,
            row.Reason);
        if (!string.Equals(revision.SettingsFingerprint.Value, row.SettingsFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Stored LLM Chat revision settings do not match their fingerprint.");
        }

        return revision;
    }

    public static LlmChatConversationRow ToRow(LlmChatConversation conversation)
        => new()
        {
            Id = conversation.Id.Value,
            DefinitionId = conversation.DefinitionId.Value,
            DefinitionRevision = conversation.DefinitionRevision.Value,
            Title = conversation.Title,
            Status = conversation.Status,
            Origin = conversation.Origin,
            CreatedAtUtc = conversation.CreatedAtUtc,
            UpdatedAtUtc = conversation.UpdatedAtUtc,
            ConcurrencyToken = conversation.ConcurrencyToken
        };

    public static LlmChatConversation ToDomain(LlmChatConversationRow row)
        => new(
            new LlmChatConversationId(row.Id),
            new LlmChatDefinitionId(row.DefinitionId),
            new LlmChatDefinitionRevisionNumber(row.DefinitionRevision),
            row.Title,
            row.Status,
            row.Origin,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.ConcurrencyToken);

    public static LlmChatOperationRow ToRow(LlmChatOperation operation)
        => new()
        {
            Id = operation.Id.Value,
            ConversationId = operation.ConversationId.Value,
            Kind = operation.Kind,
            RequestFingerprint = operation.RequestFingerprint.Value,
            ExpectedTranscriptRevision = operation.ExpectedTranscriptRevision,
            Status = operation.Status,
            AttributionScopeKind = operation.AttributionScope?.Kind,
            AttributionScopeKey = operation.AttributionScope?.Key ?? string.Empty,
            CancellationRequestedAtUtc = operation.CancellationRequestedAtUtc,
            CancellationGeneration = operation.CancellationGeneration,
            ExecutionOwnerId = operation.ExecutionOwnerId?.Value,
            ExecutionEpoch = operation.ExecutionEpoch,
            ClaimedAtUtc = operation.ClaimedAtUtc,
            HeartbeatAtUtc = operation.HeartbeatAtUtc,
            LeaseExpiresAtUtc = operation.LeaseExpiresAtUtc,
            DispatchPhase = operation.DispatchPhase,
            TurnAdmittedAtUtc = operation.TurnAdmittedAtUtc,
            ProviderDispatchStartedAtUtc = operation.ProviderDispatchStartedAtUtc,
            ProviderDispatchReturnedAtUtc = operation.ProviderDispatchReturnedAtUtc,
            TranscriptCompletedAtUtc = operation.TranscriptCompletedAtUtc,
            StartedAtUtc = operation.StartedAtUtc,
            CompletedAtUtc = operation.CompletedAtUtc,
            ResultingTranscriptRevision = operation.ResultingTranscriptRevision,
            AssistantEntryId = operation.AssistantEntryId,
            FailureCode = operation.FailureCode,
            LastEventSequence = operation.LastEventSequence,
            ConcurrencyToken = operation.ConcurrencyToken
        };

    public static LlmChatOperation ToDomain(LlmChatOperationRow row)
        => new(
            new LlmChatOperationId(row.Id),
            new LlmChatConversationId(row.ConversationId),
            ValidateOperationKind(row.Kind),
            new LlmChatRequestFingerprint(row.RequestFingerprint),
            row.ExpectedTranscriptRevision,
            row.Status,
            row.StartedAtUtc,
            row.ConcurrencyToken,
            row.AttributionScopeKind is { } attributionScopeKind
                ? new WorkspaceScopeDescriptor(attributionScopeKind, row.AttributionScopeKey)
                : null)
        {
            CancellationRequestedAtUtc = row.CancellationRequestedAtUtc,
            CancellationGeneration = row.CancellationGeneration,
            ExecutionOwnerId = row.ExecutionOwnerId is { } ownerId
                ? new LlmChatExecutionOwnerId(ownerId)
                : null,
            ExecutionEpoch = row.ExecutionEpoch,
            ClaimedAtUtc = row.ClaimedAtUtc,
            HeartbeatAtUtc = row.HeartbeatAtUtc,
            LeaseExpiresAtUtc = row.LeaseExpiresAtUtc,
            DispatchPhase = row.DispatchPhase,
            TurnAdmittedAtUtc = row.TurnAdmittedAtUtc,
            ProviderDispatchStartedAtUtc = row.ProviderDispatchStartedAtUtc,
            ProviderDispatchReturnedAtUtc = row.ProviderDispatchReturnedAtUtc,
            TranscriptCompletedAtUtc = row.TranscriptCompletedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            ResultingTranscriptRevision = row.ResultingTranscriptRevision,
            AssistantEntryId = row.AssistantEntryId,
            FailureCode = row.FailureCode,
            LastEventSequence = row.LastEventSequence
        };

    private static LlmChatOperationKind ValidateOperationKind(LlmChatOperationKind kind)
        => kind is LlmChatOperationKind.SendTurn
            ? kind
            : throw new InvalidDataException($"Stored LLM Chat operation kind '{(int)kind}' is invalid.");

    public static LlmChatInvocationRecordRow ToRow(LlmChatInvocationRecord record)
        => new()
        {
            OperationId = record.OperationId.Value,
            ProviderProfileId = record.ProviderProfileId,
            ProviderKind = record.ProviderKind,
            ProviderName = record.ProviderName,
            Model = record.Model,
            RequestedThinkingEffort = record.RequestedThinkingEffort,
            EffectiveThinkingEffort = record.EffectiveThinkingEffort,
            DeliveryMode = record.DeliveryMode,
            FinishReason = record.FinishReason,
            Ordinal = record.Ordinal,
            InputTokens = record.Usage.InputTokens,
            OutputTokens = record.Usage.OutputTokens,
            CachedInputTokens = record.Usage.CachedInputTokens,
            UsageStatus = record.UsageStatus,
            PricingStatus = record.PricingStatus,
            ProviderCostUsd = record.ProviderCostUsd,
            CalculatedCostUsd = record.CalculatedCostUsd,
            PricingProfileHash = record.PricingProfileHash,
            PricingVersion = record.PricingVersion,
            Outcome = record.Outcome,
            FailureCode = record.FailureCode,
            StartedAtUtc = record.StartedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc,
            CorrelationId = record.CorrelationId
        };

    public static LlmChatInvocationRecord ToDomain(LlmChatInvocationRecordRow row)
        => new(
            new LlmChatOperationId(row.OperationId),
            row.ProviderProfileId,
            row.ProviderKind,
            row.ProviderName,
            row.Model,
            row.RequestedThinkingEffort,
            row.EffectiveThinkingEffort,
            row.Ordinal,
            new LlmUsage(row.InputTokens, row.OutputTokens, row.CachedInputTokens),
            row.Outcome,
            row.FailureCode,
            row.StartedAtUtc,
            row.CompletedAtUtc,
            row.CorrelationId,
            row.DeliveryMode,
            row.FinishReason,
            row.UsageStatus,
            row.PricingStatus,
            row.ProviderCostUsd,
            row.CalculatedCostUsd,
            row.PricingProfileHash,
            row.PricingVersion);
}
