using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;

namespace CanDoItAll.Modules.LlmChats.Persistence;

internal static class LlmConversationPersistenceMapper
{
    public static LlmChatTranscriptRow ToRow(LlmConversationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var row = new LlmChatTranscriptRow
        {
            ConversationId = document.ConversationId,
            Title = document.Title,
            ProviderId = document.Provider.ProviderId,
            ProviderName = document.Provider.ProviderName,
            ProviderKind = document.Provider.ProviderKind,
            Model = document.Provider.Model,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            TranscriptRevision = document.TranscriptRevision,
            EntryCount = document.Entries.Length
        };
        ApplyActiveTurn(row, document.ActiveTurn);
        ApplyAcceleration(row, document.AccelerationState, compensation: false);
        return row;
    }

    public static LlmChatMessageRow ToRow(Guid conversationId, long sequence, LlmConversationTranscriptEntry entry)
        => new()
        {
            EntryId = entry.EntryId,
            ConversationId = conversationId,
            Sequence = sequence,
            TurnId = entry.TurnId,
            Role = entry.Role,
            Text = entry.Text,
            CreatedAtUtc = entry.CreatedAtUtc,
            Model = entry.Model,
            InputTokens = entry.Usage?.InputTokens,
            OutputTokens = entry.Usage?.OutputTokens,
            CachedInputTokens = entry.Usage?.CachedInputTokens
        };

    public static LlmConversationDocument ToDocument(
        LlmChatTranscriptRow transcript,
        IReadOnlyList<LlmChatMessageRow> messages)
    {
        if (messages.Count != transcript.EntryCount)
        {
            throw StorageCorrupted(transcript.ConversationId, "The transcript entry count does not match its messages.");
        }

        var entries = ImmutableArray.CreateBuilder<LlmConversationTranscriptEntry>(messages.Count);
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            if (message.Sequence != index + 1 || message.ConversationId != transcript.ConversationId)
            {
                throw StorageCorrupted(transcript.ConversationId, "Transcript message ordering is invalid.");
            }

            var hasAnyUsage = message.InputTokens.HasValue || message.OutputTokens.HasValue || message.CachedInputTokens.HasValue;
            var hasCompleteUsage = message.InputTokens.HasValue && message.OutputTokens.HasValue && message.CachedInputTokens.HasValue;
            if (hasAnyUsage != hasCompleteUsage)
            {
                throw StorageCorrupted(transcript.ConversationId, "Transcript message usage is incomplete.");
            }

            entries.Add(new LlmConversationTranscriptEntry(
                message.EntryId,
                message.TurnId,
                message.Role,
                message.Text,
                message.CreatedAtUtc,
                message.Model,
                hasCompleteUsage
                    ? new LlmUsage(message.InputTokens!.Value, message.OutputTokens!.Value, message.CachedInputTokens!.Value)
                    : null));
        }

        try
        {
            var provider = new LlmConversationProviderSnapshot(
                transcript.ProviderId,
                transcript.ProviderName,
                transcript.ProviderKind,
                transcript.Model);
            return new LlmConversationDocument(
                transcript.ConversationId,
                transcript.Title,
                provider,
                transcript.CreatedAtUtc,
                transcript.UpdatedAtUtc,
                transcript.TranscriptRevision,
                entries.MoveToImmutable(),
                ToActiveTurn(transcript),
                ToAcceleration(transcript, compensation: false));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw StorageCorrupted(transcript.ConversationId, "Stored transcript state is invalid.", exception);
        }
    }

    private static void ApplyActiveTurn(LlmChatTranscriptRow row, LlmConversationActiveTurn? activeTurn)
    {
        row.ActiveTurnId = activeTurn?.TurnId;
        row.PendingUserEntryId = activeTurn?.PendingUserEntryId;
        row.TurnAdmittedAtUtc = activeTurn?.AdmittedAtUtc;
        row.TurnAdmittedRevision = activeTurn?.AdmittedRevision;
        row.CompensationProviderId = activeTurn?.Compensation?.Provider.ProviderId;
        row.CompensationProviderName = activeTurn?.Compensation?.Provider.ProviderName;
        row.CompensationProviderKind = activeTurn?.Compensation?.Provider.ProviderKind;
        row.CompensationModel = activeTurn?.Compensation?.Provider.Model;
        ApplyAcceleration(row, activeTurn?.Compensation?.AccelerationState, compensation: true);
    }

    private static void ApplyAcceleration(
        LlmChatTranscriptRow row,
        LlmConversationAccelerationEnvelope? acceleration,
        bool compensation)
    {
        if (compensation)
        {
            row.CompensationAccelerationStrategyId = acceleration?.StrategyId;
            row.CompensationAccelerationProviderName = acceleration?.ProviderName;
            row.CompensationAccelerationModel = acceleration?.Model;
            row.CompensationAccelerationPayloadJson = acceleration?.PayloadJson;
            return;
        }

        row.AccelerationStrategyId = acceleration?.StrategyId;
        row.AccelerationProviderName = acceleration?.ProviderName;
        row.AccelerationModel = acceleration?.Model;
        row.AccelerationPayloadJson = acceleration?.PayloadJson;
    }

    private static LlmConversationActiveTurn? ToActiveTurn(LlmChatTranscriptRow row)
    {
        if (row.ActiveTurnId is null && row.PendingUserEntryId is null && row.TurnAdmittedAtUtc is null &&
            row.TurnAdmittedRevision is null && row.CompensationProviderId is null)
        {
            return null;
        }

        if (row.ActiveTurnId is not { } turnId || row.PendingUserEntryId is not { } pendingEntryId ||
            row.TurnAdmittedAtUtc is not { } admittedAt || row.TurnAdmittedRevision is not { } admittedRevision)
        {
            throw StorageCorrupted(row.ConversationId, "Stored active-turn state is incomplete.");
        }

        LlmConversationTurnCompensation? compensation = null;
        if (row.CompensationProviderId is { } providerId)
        {
            if (row.CompensationProviderName is null || row.CompensationProviderKind is null || row.CompensationModel is null)
            {
                throw StorageCorrupted(row.ConversationId, "Stored turn-compensation state is incomplete.");
            }

            compensation = new LlmConversationTurnCompensation(
                new LlmConversationProviderSnapshot(
                    providerId,
                    row.CompensationProviderName,
                    row.CompensationProviderKind.Value,
                    row.CompensationModel),
                ToAcceleration(row, compensation: true));
        }

        return new LlmConversationActiveTurn(turnId, pendingEntryId, admittedAt, admittedRevision, compensation);
    }

    private static LlmConversationAccelerationEnvelope? ToAcceleration(
        LlmChatTranscriptRow row,
        bool compensation)
    {
        var strategy = compensation ? row.CompensationAccelerationStrategyId : row.AccelerationStrategyId;
        var provider = compensation ? row.CompensationAccelerationProviderName : row.AccelerationProviderName;
        var model = compensation ? row.CompensationAccelerationModel : row.AccelerationModel;
        var payload = compensation ? row.CompensationAccelerationPayloadJson : row.AccelerationPayloadJson;
        if (strategy is null && provider is null && model is null && payload is null)
        {
            return null;
        }

        if (strategy is null || provider is null || model is null || payload is null)
        {
            throw StorageCorrupted(row.ConversationId, "Stored acceleration state is incomplete.");
        }

        return new LlmConversationAccelerationEnvelope(strategy, provider, model, payload);
    }

    private static LlmConversationException StorageCorrupted(
        Guid conversationId,
        string detail,
        Exception? innerException = null)
        => new(LlmConversationFailureKind.StorageCorrupted, conversationId, detail, innerException);
}
