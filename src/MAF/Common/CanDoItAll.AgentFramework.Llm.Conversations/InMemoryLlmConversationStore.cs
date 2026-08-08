using CanDoItAll.AgentFramework.Llm.Abstractions;

namespace CanDoItAll.AgentFramework.Llm.Conversations;

/// <summary>
/// In-memory conversation store with the same atomic compare-and-swap contract as the file-backed
/// store. Intended for tests and ephemeral hosts; documents are immutable records, so returned
/// instances are safe to share.
/// </summary>
public sealed class InMemoryLlmConversationStore : ILlmConversationStore
{
    private readonly Dictionary<Guid, LlmConversationDocument> _documents = [];
    private readonly Lock _gate = new();

    public Task<LlmConversationDocument> CreateAsync(
        LlmConversationDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_gate)
        {
            if (!_documents.TryAdd(document.ConversationId, document))
            {
                throw new LlmConversationException(
                    LlmConversationFailureKind.AlreadyExists, document.ConversationId);
            }
        }

        return Task.FromResult(document);
    }

    public Task<LlmConversationDocument?> TryGetAsync(
        Guid conversationId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_documents.TryGetValue(conversationId, out var document) ? document : null);
        }
    }

    public Task<LlmConversationDocument> ReplaceAsync(
        LlmConversationDocument document, long expectedTranscriptRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.TranscriptRevision <= expectedTranscriptRevision)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.InvalidRequest,
                document.ConversationId,
                "A replacement document must advance the transcript revision.");
        }

        lock (_gate)
        {
            if (!_documents.TryGetValue(document.ConversationId, out var stored))
            {
                throw new LlmConversationException(LlmConversationFailureKind.NotFound, document.ConversationId);
            }

            if (stored.TranscriptRevision != expectedTranscriptRevision)
            {
                throw new LlmConversationException(
                    LlmConversationFailureKind.ConcurrencyConflict,
                    document.ConversationId,
                    $"Stored revision {stored.TranscriptRevision}, expected {expectedTranscriptRevision}.");
            }

            _documents[document.ConversationId] = document;
        }

        return Task.FromResult(document);
    }

    public Task<IReadOnlyList<LlmConversationSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<LlmConversationSummary> summaries = [.. _documents.Values.Select(document =>
                new LlmConversationSummary(
                    document.ConversationId,
                    document.Title,
                    document.Provider.ProviderName,
                    document.Provider.Model,
                    document.CreatedAtUtc,
                    document.UpdatedAtUtc,
                    document.TranscriptRevision,
                    document.Entries.Length,
                    document.ActiveTurn is not null))];
            return Task.FromResult(summaries);
        }
    }

    public Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_documents.Remove(conversationId))
            {
                throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);
            }
        }

        return Task.CompletedTask;
    }
}
