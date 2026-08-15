using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class EfLlmConversationTurnStore(AppDbContext dbContext) : ILlmConversationTurnStore
{
    public async Task<LlmConversationTurnSnapshot?> TryGetAsync(
        Guid conversationId,
        int maximumContextMessages,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumContextMessages, 1);
        var state = await LoadStateAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return state is null
            ? null
            : await ToSnapshotAsync(state, maximumContextMessages, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LlmConversationTurnSnapshot> AdmitAsync(
        LlmConversationTurnAdmissionWrite write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var boundedDocument = new LlmConversationDocument(
            write.Current.ConversationId,
            write.Current.Title,
            write.Provider,
            write.Current.CreatedAtUtc,
            write.UpdatedAtUtc,
            write.Current.TranscriptRevision + 1,
            write.Current.ContextEntries.Add(write.UserEntry),
            write.ActiveTurn,
            write.AccelerationState);
        var incoming = LlmConversationPersistenceMapper.ToRow(boundedDocument);
        var affected = await dbContext.Set<LlmChatTranscriptRow>()
            .Where(row => row.ConversationId == write.Current.ConversationId &&
                          row.TranscriptRevision == write.Current.TranscriptRevision &&
                          row.EntryCount == write.Current.EntryCount &&
                          row.ActiveTurnId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.ProviderId, incoming.ProviderId)
                .SetProperty(row => row.ProviderName, incoming.ProviderName)
                .SetProperty(row => row.ProviderKind, incoming.ProviderKind)
                .SetProperty(row => row.Model, incoming.Model)
                .SetProperty(row => row.TranscriptRevision, incoming.TranscriptRevision)
                .SetProperty(row => row.EntryCount, checked(write.Current.EntryCount + 1))
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
            throw ConcurrencyConflict(write.Current.ConversationId);
        }

        await UpdateConversationTimestampAsync(
            write.Current.ConversationId,
            write.UpdatedAtUtc,
            cancellationToken).ConfigureAwait(false);
        dbContext.Add(LlmConversationPersistenceMapper.ToRow(
            write.Current.ConversationId,
            checked(write.Current.EntryCount + 1L),
            write.UserEntry));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await RequireSnapshotAsync(
            write.Current.ConversationId,
            write.MaximumContextMessages,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<LlmConversationTurnSnapshot> CompleteAsync(
        LlmConversationTurnCompletionWrite write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var affected = await dbContext.Set<LlmChatTranscriptRow>()
            .Where(row => row.ConversationId == write.ConversationId &&
                          row.TranscriptRevision == write.ExpectedTranscriptRevision &&
                          row.EntryCount == write.ExpectedEntryCount &&
                          row.ActiveTurnId == write.TurnId &&
                          row.PendingUserEntryId == write.PendingUserEntryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.TranscriptRevision, checked(write.ExpectedTranscriptRevision + 1))
                .SetProperty(row => row.EntryCount, checked(write.ExpectedEntryCount + 1))
                .SetProperty(row => row.ActiveTurnId, (Guid?)null)
                .SetProperty(row => row.PendingUserEntryId, (Guid?)null)
                .SetProperty(row => row.TurnAdmittedAtUtc, (DateTimeOffset?)null)
                .SetProperty(row => row.TurnAdmittedRevision, (long?)null)
                .SetProperty(row => row.CompensationProviderId, (Guid?)null)
                .SetProperty(row => row.CompensationProviderName, (string?)null)
                .SetProperty(row => row.CompensationProviderKind, (CanDoItAll.AgentFramework.Models.ProviderKind?)null)
                .SetProperty(row => row.CompensationModel, (string?)null)
                .SetProperty(row => row.CompensationAccelerationStrategyId, (string?)null)
                .SetProperty(row => row.CompensationAccelerationProviderName, (string?)null)
                .SetProperty(row => row.CompensationAccelerationModel, (string?)null)
                .SetProperty(row => row.CompensationAccelerationPayloadJson, (string?)null),
                cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
        {
            throw ConcurrencyConflict(write.ConversationId);
        }

        await UpdateConversationTimestampAsync(
            write.ConversationId,
            write.UpdatedAtUtc,
            cancellationToken).ConfigureAwait(false);
        dbContext.Add(LlmConversationPersistenceMapper.ToRow(
            write.ConversationId,
            checked(write.ExpectedEntryCount + 1L),
            write.AssistantEntry));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await RequireSnapshotAsync(
            write.ConversationId,
            write.MaximumContextMessages,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<LlmConversationTurnSnapshot> CompensateAsync(
        Guid conversationId,
        Guid turnId,
        DateTimeOffset updatedAtUtc,
        int maximumContextMessages,
        CancellationToken cancellationToken = default)
    {
        var state = await LoadStateAsync(conversationId, cancellationToken).ConfigureAwait(false)
                    ?? throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);
        var activeTurn = LlmConversationPersistenceMapper.ToActiveTurn(state.Transcript);
        if (activeTurn?.TurnId != turnId)
        {
            throw new LlmConversationException(LlmConversationFailureKind.TurnNotActive, conversationId);
        }

        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var compensation = activeTurn.Compensation;
        var provider = compensation?.Provider ?? new LlmConversationProviderSnapshot(
            state.Transcript.ProviderId,
            state.Transcript.ProviderName,
            state.Transcript.ProviderKind,
            state.Transcript.Model);
        var acceleration = compensation is null
            ? LlmConversationPersistenceMapper.ToAcceleration(state.Transcript, compensation: false)
            : compensation.AccelerationState;
        var affected = await dbContext.Set<LlmChatTranscriptRow>()
            .Where(row => row.ConversationId == conversationId &&
                          row.TranscriptRevision == state.Transcript.TranscriptRevision &&
                          row.EntryCount == state.Transcript.EntryCount &&
                          row.ActiveTurnId == turnId &&
                          row.PendingUserEntryId == activeTurn.PendingUserEntryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.ProviderId, provider.ProviderId)
                .SetProperty(row => row.ProviderName, provider.ProviderName)
                .SetProperty(row => row.ProviderKind, provider.ProviderKind)
                .SetProperty(row => row.Model, provider.Model)
                .SetProperty(row => row.TranscriptRevision, checked(state.Transcript.TranscriptRevision + 1))
                .SetProperty(row => row.EntryCount, checked(state.Transcript.EntryCount - 1))
                .SetProperty(row => row.ActiveTurnId, (Guid?)null)
                .SetProperty(row => row.PendingUserEntryId, (Guid?)null)
                .SetProperty(row => row.TurnAdmittedAtUtc, (DateTimeOffset?)null)
                .SetProperty(row => row.TurnAdmittedRevision, (long?)null)
                .SetProperty(row => row.CompensationProviderId, (Guid?)null)
                .SetProperty(row => row.CompensationProviderName, (string?)null)
                .SetProperty(row => row.CompensationProviderKind, (CanDoItAll.AgentFramework.Models.ProviderKind?)null)
                .SetProperty(row => row.CompensationModel, (string?)null)
                .SetProperty(row => row.CompensationAccelerationStrategyId, (string?)null)
                .SetProperty(row => row.CompensationAccelerationProviderName, (string?)null)
                .SetProperty(row => row.CompensationAccelerationModel, (string?)null)
                .SetProperty(row => row.CompensationAccelerationPayloadJson, (string?)null)
                .SetProperty(row => row.AccelerationStrategyId, acceleration == null ? null : acceleration.StrategyId)
                .SetProperty(row => row.AccelerationProviderName, acceleration == null ? null : acceleration.ProviderName)
                .SetProperty(row => row.AccelerationModel, acceleration == null ? null : acceleration.Model)
                .SetProperty(row => row.AccelerationPayloadJson, acceleration == null ? null : acceleration.PayloadJson),
                cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
        {
            throw ConcurrencyConflict(conversationId);
        }

        var removed = await dbContext.Set<LlmChatMessageRow>()
            .Where(row => row.ConversationId == conversationId &&
                          row.EntryId == activeTurn.PendingUserEntryId &&
                          row.TurnId == turnId &&
                          row.Sequence == state.Transcript.EntryCount &&
                          row.Role == LlmMessageRole.User)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        if (removed != 1)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.StorageCorrupted,
                conversationId,
                "The exact pending user entry could not be compensated.");
        }

        await UpdateConversationTimestampAsync(conversationId, updatedAtUtc, cancellationToken).ConfigureAwait(false);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await RequireSnapshotAsync(
            conversationId,
            maximumContextMessages,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LlmConversationTurnSnapshot> RequireSnapshotAsync(
        Guid conversationId,
        int maximumContextMessages,
        CancellationToken cancellationToken)
        => await TryGetAsync(conversationId, maximumContextMessages, cancellationToken).ConfigureAwait(false)
           ?? throw new LlmConversationException(
               LlmConversationFailureKind.StorageCorrupted,
               conversationId,
               "The committed transcript state could not be reloaded.");

    private async Task<TurnState?> LoadStateAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
        => await (
                from transcript in dbContext.Set<LlmChatTranscriptRow>().AsNoTracking()
                join conversation in dbContext.Set<LlmChatConversationRow>().AsNoTracking()
                    on transcript.ConversationId equals conversation.Id
                where transcript.ConversationId == conversationId
                select new TurnState(
                    transcript,
                    conversation.Title,
                    conversation.CreatedAtUtc,
                    conversation.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<LlmConversationTurnSnapshot> ToSnapshotAsync(
        TurnState state,
        int maximumContextMessages,
        CancellationToken cancellationToken)
    {
        var activeTurn = LlmConversationPersistenceMapper.ToActiveTurn(state.Transcript);
        var systemLimit = maximumContextMessages - 1;
        LlmChatMessageRow[] systemRows = systemLimit == 0
            ? []
            : await dbContext.Set<LlmChatMessageRow>()
                .AsNoTracking()
                .Where(row => row.ConversationId == state.Transcript.ConversationId &&
                              row.Role == LlmMessageRole.System)
                .OrderBy(row => row.Sequence)
                .Take(systemLimit)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        var remaining = maximumContextMessages - systemRows.Length;
        LlmChatMessageRow[] recentRows = remaining == 0
            ? []
            : await dbContext.Set<LlmChatMessageRow>()
                .AsNoTracking()
                .Where(row => row.ConversationId == state.Transcript.ConversationId &&
                              row.Role != LlmMessageRole.System)
                .OrderByDescending(row => row.Sequence)
                .Take(remaining)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        Array.Reverse(recentRows);
        var entries = ImmutableArray.CreateRange(
            systemRows.Concat(recentRows).Select(LlmConversationPersistenceMapper.ToEntry));
        return new LlmConversationTurnSnapshot(
            state.Transcript.ConversationId,
            state.Title,
            new LlmConversationProviderSnapshot(
                state.Transcript.ProviderId,
                state.Transcript.ProviderName,
                state.Transcript.ProviderKind,
                state.Transcript.Model),
            state.CreatedAtUtc,
            state.UpdatedAtUtc,
            state.Transcript.TranscriptRevision,
            state.Transcript.EntryCount,
            entries,
            activeTurn,
            LlmConversationPersistenceMapper.ToAcceleration(state.Transcript, compensation: false));
    }

    private async Task UpdateConversationTimestampAsync(
        Guid conversationId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var affected = await dbContext.Set<LlmChatConversationRow>()
            .Where(row => row.Id == conversationId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.UpdatedAtUtc, updatedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.StorageCorrupted,
                conversationId,
                "The canonical conversation metadata row could not be updated.");
        }
    }

    private static LlmConversationException ConcurrencyConflict(Guid conversationId)
        => new(LlmConversationFailureKind.ConcurrencyConflict, conversationId);

    private sealed record TurnState(
        LlmChatTranscriptRow Transcript,
        string Title,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);
}
