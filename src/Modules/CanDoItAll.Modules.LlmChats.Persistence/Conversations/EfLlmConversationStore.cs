using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class EfLlmConversationStore(AppDbContext dbContext)
    : ILlmConversationStore
{
    public async Task<LlmConversationDocument> CreateAsync(
        LlmConversationDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        dbContext.Add(LlmConversationPersistenceMapper.ToRow(document));
        for (var index = 0; index < document.Entries.Length; index++)
        {
            dbContext.Add(LlmConversationPersistenceMapper.ToRow(document.ConversationId, index + 1, document.Entries[index]));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return await RequireStoredAsync(dbContext, document.ConversationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.AlreadyExists,
                document.ConversationId,
                innerException: exception);
        }
    }

    public async Task<LlmConversationDocument?> TryGetAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await LoadAsync(dbContext, conversationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LlmConversationDocument> ReplaceAsync(
        LlmConversationDocument document,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.TranscriptRevision <= expectedTranscriptRevision)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.InvalidRequest,
                document.ConversationId,
                "A replacement document must advance the transcript revision.");
        }

        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var existing = await LoadAsync(dbContext, document.ConversationId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            throw new LlmConversationException(LlmConversationFailureKind.NotFound, document.ConversationId);
        }

        if (existing.TranscriptRevision != expectedTranscriptRevision)
        {
            throw ConcurrencyConflict(document.ConversationId, existing.TranscriptRevision, expectedTranscriptRevision);
        }

        var delta = DetermineDelta(existing, document);
        var incoming = LlmConversationPersistenceMapper.ToRow(document);
        var affected = await dbContext.Set<LlmChatTranscriptRow>()
            .Where(row => row.ConversationId == document.ConversationId &&
                          row.TranscriptRevision == expectedTranscriptRevision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.ProviderId, incoming.ProviderId)
                .SetProperty(row => row.ProviderName, incoming.ProviderName)
                .SetProperty(row => row.ProviderKind, incoming.ProviderKind)
                .SetProperty(row => row.Model, incoming.Model)
                .SetProperty(row => row.TranscriptRevision, incoming.TranscriptRevision)
                .SetProperty(row => row.EntryCount, incoming.EntryCount)
                .SetProperty(row => row.ActiveTurnId, incoming.ActiveTurnId)
                .SetProperty(row => row.PendingUserEntryId, incoming.PendingUserEntryId)
                .SetProperty(row => row.TurnAdmittedAtUtc, incoming.TurnAdmittedAtUtc)
                .SetProperty(row => row.TurnAdmittedRevision, incoming.TurnAdmittedRevision)
                .SetProperty(row => row.CompensationProviderId, incoming.CompensationProviderId)
                .SetProperty(row => row.CompensationProviderName, incoming.CompensationProviderName)
                .SetProperty(row => row.CompensationProviderKind, incoming.CompensationProviderKind)
                .SetProperty(row => row.CompensationModel, incoming.CompensationModel)
                .SetProperty(row => row.CompensationAccelerationStrategyId, incoming.CompensationAccelerationStrategyId)
                .SetProperty(row => row.CompensationAccelerationProviderName, incoming.CompensationAccelerationProviderName)
                .SetProperty(row => row.CompensationAccelerationModel, incoming.CompensationAccelerationModel)
                .SetProperty(row => row.CompensationAccelerationPayloadJson, incoming.CompensationAccelerationPayloadJson)
                .SetProperty(row => row.AccelerationStrategyId, incoming.AccelerationStrategyId)
                .SetProperty(row => row.AccelerationProviderName, incoming.AccelerationProviderName)
                .SetProperty(row => row.AccelerationModel, incoming.AccelerationModel)
                .SetProperty(row => row.AccelerationPayloadJson, incoming.AccelerationPayloadJson),
                cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
        {
            throw ConcurrencyConflict(document.ConversationId, null, expectedTranscriptRevision);
        }

        var conversationAffected = await dbContext.Set<LlmChatConversationRow>()
            .Where(row => row.Id == document.ConversationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.UpdatedAtUtc, document.UpdatedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        if (conversationAffected != 1)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.StorageCorrupted,
                document.ConversationId,
                "The canonical conversation metadata row could not be updated.");
        }

        switch (delta)
        {
            case AppendDelta append:
                dbContext.Add(LlmConversationPersistenceMapper.ToRow(
                    document.ConversationId,
                    document.Entries.Length,
                    append.Entry));
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                break;
            case RemovePendingDelta remove:
                var removed = await dbContext.Set<LlmChatMessageRow>()
                    .Where(row => row.ConversationId == document.ConversationId &&
                                  row.EntryId == remove.EntryId &&
                                  row.Sequence == remove.Sequence &&
                                  row.TurnId == remove.TurnId &&
                                  row.Role == LlmMessageRole.User)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (removed != 1)
                {
                    throw new LlmConversationException(
                        LlmConversationFailureKind.StorageCorrupted,
                        document.ConversationId,
                        "The exact pending user entry could not be compensated.");
                }

                break;
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await RequireStoredAsync(dbContext, document.ConversationId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LlmConversationSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await (
                from transcript in dbContext.Set<LlmChatTranscriptRow>().AsNoTracking()
                join conversation in dbContext.Set<LlmChatConversationRow>().AsNoTracking()
                    on transcript.ConversationId equals conversation.Id
                orderby conversation.UpdatedAtUtc descending, transcript.ConversationId
                select new LlmConversationSummary(
                    transcript.ConversationId,
                    conversation.Title,
                    transcript.ProviderName,
                    transcript.Model,
                    conversation.CreatedAtUtc,
                    conversation.UpdatedAtUtc,
                    transcript.TranscriptRevision,
                    transcript.EntryCount,
                    transcript.ActiveTurnId != null))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await dbContext.Set<LlmChatTranscriptRow>()
                .Where(row => row.ConversationId == conversationId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            if (affected != 1)
            {
                throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);
            }
        }
        catch (DbUpdateException exception)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.ConcurrencyConflict,
                conversationId,
                "The transcript is referenced by product conversation state.",
                exception);
        }
    }

    private static async Task<LlmConversationDocument?> LoadAsync(
        AppDbContext dbContext,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var state = await (
                from transcript in dbContext.Set<LlmChatTranscriptRow>().AsNoTracking()
                join conversation in dbContext.Set<LlmChatConversationRow>().AsNoTracking()
                    on transcript.ConversationId equals conversation.Id
                where transcript.ConversationId == conversationId
                select new
                {
                    Transcript = transcript,
                    conversation.Title,
                    conversation.CreatedAtUtc,
                    conversation.UpdatedAtUtc
                })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (state is null)
        {
            return null;
        }

        var messages = await dbContext.Set<LlmChatMessageRow>()
            .AsNoTracking()
            .Where(row => row.ConversationId == conversationId)
            .OrderBy(row => row.Sequence)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return LlmConversationPersistenceMapper.ToDocument(
            state.Transcript,
            state.Title,
            state.CreatedAtUtc,
            state.UpdatedAtUtc,
            messages);
    }

    private static async Task<LlmConversationDocument> RequireStoredAsync(
        AppDbContext dbContext,
        Guid conversationId,
        CancellationToken cancellationToken)
        => await LoadAsync(dbContext, conversationId, cancellationToken).ConfigureAwait(false)
           ?? throw new LlmConversationException(
               LlmConversationFailureKind.StorageCorrupted,
               conversationId,
               "The committed transcript could not be reloaded.");

    private static MessageDelta DetermineDelta(
        LlmConversationDocument existing,
        LlmConversationDocument incoming)
    {
        var sharedCount = Math.Min(existing.Entries.Length, incoming.Entries.Length);
        for (var index = 0; index < sharedCount; index++)
        {
            if (!EntriesMatch(existing.Entries[index], incoming.Entries[index]))
            {
                throw InvalidDelta(incoming.ConversationId);
            }
        }

        if (incoming.Entries.Length == existing.Entries.Length)
        {
            return NoMessageDelta.Instance;
        }

        if (incoming.Entries.Length == existing.Entries.Length + 1)
        {
            return new AppendDelta(incoming.Entries[^1]);
        }

        if (incoming.Entries.Length == existing.Entries.Length - 1 && existing.ActiveTurn is { } activeTurn)
        {
            var removed = existing.Entries[^1];
            if (removed.EntryId != activeTurn.PendingUserEntryId || removed.TurnId != activeTurn.TurnId ||
                removed.Role != LlmMessageRole.User || incoming.ActiveTurn is not null)
            {
                throw InvalidDelta(incoming.ConversationId);
            }

            if (activeTurn.Compensation is { } compensation &&
                (incoming.Provider != compensation.Provider || incoming.AccelerationState != compensation.AccelerationState))
            {
                throw InvalidDelta(incoming.ConversationId);
            }

            return new RemovePendingDelta(removed.EntryId, removed.TurnId, existing.Entries.Length);
        }

        throw InvalidDelta(incoming.ConversationId);
    }

    private static bool EntriesMatch(
        LlmConversationTranscriptEntry stored,
        LlmConversationTranscriptEntry incoming)
        => stored.EntryId == incoming.EntryId &&
           stored.TurnId == incoming.TurnId &&
           stored.Role == incoming.Role &&
           string.Equals(stored.Text, incoming.Text, StringComparison.Ordinal) &&
           ToPostgreSqlMicroseconds(stored.CreatedAtUtc) == ToPostgreSqlMicroseconds(incoming.CreatedAtUtc) &&
           string.Equals(stored.Model, incoming.Model, StringComparison.Ordinal) &&
           stored.Usage == incoming.Usage;

    private static long ToPostgreSqlMicroseconds(DateTimeOffset value)
        => value.UtcTicks / TimeSpan.TicksPerMicrosecond;

    private static LlmConversationException InvalidDelta(Guid conversationId)
        => new(
            LlmConversationFailureKind.InvalidRequest,
            conversationId,
            "A replacement may append one entry, remove the exact pending user entry, or leave messages unchanged.");

    private static LlmConversationException ConcurrencyConflict(
        Guid conversationId,
        long? storedRevision,
        long expectedRevision)
    {
        var detail = storedRevision is { } value
            ? $"Stored revision {value}, expected {expectedRevision}."
            : $"Expected revision {expectedRevision} no longer matched.";
        return new LlmConversationException(
            LlmConversationFailureKind.ConcurrencyConflict,
            conversationId,
            detail);
    }

    private abstract record MessageDelta;

    private sealed record NoMessageDelta : MessageDelta
    {
        public static NoMessageDelta Instance { get; } = new();
    }

    private sealed record AppendDelta(LlmConversationTranscriptEntry Entry) : MessageDelta;

    private sealed record RemovePendingDelta(Guid EntryId, Guid TurnId, long Sequence) : MessageDelta;
}
