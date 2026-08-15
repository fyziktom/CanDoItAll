using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.Abstractions;

namespace CanDoItAll.AgentFramework.Llm.Conversations;

public sealed class DocumentLlmConversationTurnStore(ILlmConversationStore store) : ILlmConversationTurnStore
{
    public async Task<LlmConversationTurnSnapshot?> TryGetAsync(
        Guid conversationId,
        int maximumContextMessages,
        CancellationToken cancellationToken = default)
    {
        var document = await store.TryGetAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return document is null ? null : ToSnapshot(document, maximumContextMessages);
    }

    public async Task<LlmConversationTurnSnapshot> AdmitAsync(
        LlmConversationTurnAdmissionWrite write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        var current = await RequireAsync(write.Current.ConversationId, cancellationToken).ConfigureAwait(false);
        if (current.TranscriptRevision != write.Current.TranscriptRevision ||
            current.Entries.Length != write.Current.EntryCount ||
            current.ActiveTurn is not null)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.ConcurrencyConflict,
                current.ConversationId);
        }

        var admitted = new LlmConversationDocument(
            current.ConversationId,
            current.Title,
            write.Provider,
            current.CreatedAtUtc,
            write.UpdatedAtUtc,
            current.TranscriptRevision + 1,
            current.Entries.Add(write.UserEntry),
            write.ActiveTurn,
            write.AccelerationState);
        var stored = await store.ReplaceAsync(
            admitted,
            current.TranscriptRevision,
            cancellationToken).ConfigureAwait(false);
        return ToSnapshot(stored, write.MaximumContextMessages);
    }

    public async Task<LlmConversationTurnSnapshot> CompleteAsync(
        LlmConversationTurnCompletionWrite write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        var current = await RequireAsync(write.ConversationId, cancellationToken).ConfigureAwait(false);
        if (current.TranscriptRevision != write.ExpectedTranscriptRevision ||
            current.Entries.Length != write.ExpectedEntryCount ||
            current.ActiveTurn?.TurnId != write.TurnId ||
            current.ActiveTurn.PendingUserEntryId != write.PendingUserEntryId)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.ConcurrencyConflict,
                current.ConversationId);
        }

        var completed = new LlmConversationDocument(
            current.ConversationId,
            current.Title,
            current.Provider,
            current.CreatedAtUtc,
            write.UpdatedAtUtc,
            current.TranscriptRevision + 1,
            current.Entries.Add(write.AssistantEntry),
            activeTurn: null,
            current.AccelerationState);
        var stored = await store.ReplaceAsync(
            completed,
            current.TranscriptRevision,
            cancellationToken).ConfigureAwait(false);
        return ToSnapshot(stored, write.MaximumContextMessages);
    }

    public async Task<LlmConversationTurnSnapshot> CompensateAsync(
        Guid conversationId,
        Guid turnId,
        DateTimeOffset updatedAtUtc,
        int maximumContextMessages,
        CancellationToken cancellationToken = default)
    {
        var current = await RequireAsync(conversationId, cancellationToken).ConfigureAwait(false);
        var activeTurn = current.ActiveTurn;
        if (activeTurn?.TurnId != turnId)
        {
            throw new LlmConversationException(LlmConversationFailureKind.TurnNotActive, conversationId);
        }

        var compensation = activeTurn.Compensation;
        var compensated = new LlmConversationDocument(
            current.ConversationId,
            current.Title,
            compensation?.Provider ?? current.Provider,
            current.CreatedAtUtc,
            updatedAtUtc,
            current.TranscriptRevision + 1,
            [.. current.Entries.Where(entry => entry.EntryId != activeTurn.PendingUserEntryId)],
            activeTurn: null,
            compensation?.AccelerationState ?? current.AccelerationState);
        var stored = await store.ReplaceAsync(
            compensated,
            current.TranscriptRevision,
            cancellationToken).ConfigureAwait(false);
        return ToSnapshot(stored, maximumContextMessages);
    }

    private async Task<LlmConversationDocument> RequireAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
        => await store.TryGetAsync(conversationId, cancellationToken).ConfigureAwait(false)
           ?? throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);

    private static LlmConversationTurnSnapshot ToSnapshot(
        LlmConversationDocument document,
        int maximumContextMessages)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumContextMessages, 1);
        var systemLimit = maximumContextMessages - 1;
        var systemEntries = document.Entries
            .Where(entry => entry.Role == LlmMessageRole.System)
            .Take(systemLimit)
            .ToArray();
        var remaining = maximumContextMessages - systemEntries.Length;
        var recentEntries = document.Entries
            .Where(entry => entry.Role != LlmMessageRole.System)
            .TakeLast(remaining)
            .ToArray();
        return new LlmConversationTurnSnapshot(
            document.ConversationId,
            document.Title,
            document.Provider,
            document.CreatedAtUtc,
            document.UpdatedAtUtc,
            document.TranscriptRevision,
            document.Entries.Length,
            ImmutableArray.CreateRange(systemEntries.Concat(recentEntries)),
            document.ActiveTurn,
            document.AccelerationState);
    }
}
