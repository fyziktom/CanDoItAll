using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;

public sealed class EfLlmChatOperationRepository(AppDbContext dbContext) : ILlmChatOperationRepository
{
    public async Task<LlmChatOperation?> TryGetAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : LlmChatPersistenceMapper.ToDomain(row);
    }

    public async Task<LlmChatOperation?> TryGetForUpdateAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Set<LlmChatOperationRow>()
            .FromSqlInterpolated($"""
                SELECT *
                FROM "LlmChats_Operations"
                WHERE "Id" = {id.Value}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : LlmChatPersistenceMapper.ToDomain(row);
    }

    public async Task<LlmChatOperationAdmission> AdmitAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LlmChats_Operations"
                ("Id", "ConversationId", "Kind", "RequestFingerprint", "ExpectedTranscriptRevision",
                 "Status", "AttributionScopeKind", "AttributionScopeKey",
                 "CancellationRequestedAtUtc", "CancellationGeneration", "TurnAdmittedAtUtc",
                 "ExecutionOwnerId", "ExecutionEpoch", "ClaimedAtUtc", "HeartbeatAtUtc",
                 "LeaseExpiresAtUtc", "DispatchPhase",
                 "ProviderDispatchStartedAtUtc", "ProviderDispatchReturnedAtUtc",
                 "TranscriptCompletedAtUtc", "StartedAtUtc", "CompletedAtUtc",
                 "ResultingTranscriptRevision", "AssistantEntryId", "FailureCode", "LastEventSequence", "ConcurrencyToken")
            VALUES
                ({operation.Id.Value}, {operation.ConversationId.Value}, {(int)operation.Kind},
                 {operation.RequestFingerprint.Value}, {operation.ExpectedTranscriptRevision},
                 {(int)operation.Status},
                 {(operation.AttributionScope == null ? null : (int?)operation.AttributionScope.Kind)},
                 {operation.AttributionScope?.Key ?? string.Empty},
                 {operation.CancellationRequestedAtUtc}, {operation.CancellationGeneration},
                 {operation.TurnAdmittedAtUtc}, {(operation.ExecutionOwnerId == null ? null : operation.ExecutionOwnerId.Value.Value)},
                 {operation.ExecutionEpoch}, {operation.ClaimedAtUtc}, {operation.HeartbeatAtUtc},
                 {operation.LeaseExpiresAtUtc}, {(int)operation.DispatchPhase},
                 {operation.ProviderDispatchStartedAtUtc}, {operation.ProviderDispatchReturnedAtUtc},
                 {operation.TranscriptCompletedAtUtc}, {operation.StartedAtUtc}, {operation.CompletedAtUtc},
                 {operation.ResultingTranscriptRevision}, {operation.AssistantEntryId}, {operation.FailureCode},
                 {operation.LastEventSequence}, {operation.ConcurrencyToken})
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken).ConfigureAwait(false);
        var stored = await TryGetAsync(operation.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The admitted LLM Chat operation could not be reloaded.");
        return new LlmChatOperationAdmission(stored, inserted == 1);
    }

    public async Task<bool> TryReplaceAsync(
        LlmChatOperation operation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var affected = await dbContext.Set<LlmChatOperationRow>()
            .Where(row => row.Id == operation.Id.Value && row.ConcurrencyToken == expectedConcurrencyToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, operation.Status)
                .SetProperty(row => row.CancellationRequestedAtUtc, operation.CancellationRequestedAtUtc)
                .SetProperty(row => row.CancellationGeneration, operation.CancellationGeneration)
                .SetProperty(row => row.ExecutionOwnerId, operation.ExecutionOwnerId == null
                    ? null
                    : operation.ExecutionOwnerId.Value.Value)
                .SetProperty(row => row.ExecutionEpoch, operation.ExecutionEpoch)
                .SetProperty(row => row.ClaimedAtUtc, operation.ClaimedAtUtc)
                .SetProperty(row => row.HeartbeatAtUtc, operation.HeartbeatAtUtc)
                .SetProperty(row => row.LeaseExpiresAtUtc, operation.LeaseExpiresAtUtc)
                .SetProperty(row => row.DispatchPhase, operation.DispatchPhase)
                .SetProperty(row => row.TurnAdmittedAtUtc, operation.TurnAdmittedAtUtc)
                .SetProperty(row => row.ProviderDispatchStartedAtUtc, operation.ProviderDispatchStartedAtUtc)
                .SetProperty(row => row.ProviderDispatchReturnedAtUtc, operation.ProviderDispatchReturnedAtUtc)
                .SetProperty(row => row.TranscriptCompletedAtUtc, operation.TranscriptCompletedAtUtc)
                .SetProperty(row => row.CompletedAtUtc, operation.CompletedAtUtc)
                .SetProperty(row => row.ResultingTranscriptRevision, operation.ResultingTranscriptRevision)
                .SetProperty(row => row.AssistantEntryId, operation.AssistantEntryId)
                .SetProperty(row => row.FailureCode, operation.FailureCode)
                .SetProperty(row => row.ConcurrencyToken, operation.ConcurrencyToken),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    public async Task<bool> TryReplaceOwnedAsync(
        LlmChatOperation operation,
        long expectedConcurrencyToken,
        LlmChatExecutionLeaseIdentity executionLease,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Id != executionLease.OperationId)
        {
            throw new ArgumentException("The execution lease does not identify the operation being replaced.", nameof(executionLease));
        }

        var affected = await dbContext.Set<LlmChatOperationRow>()
            .Where(row => row.Id == operation.Id.Value &&
                          row.ConcurrencyToken == expectedConcurrencyToken &&
                          row.ExecutionOwnerId == executionLease.OwnerId.Value &&
                          row.ExecutionEpoch == executionLease.Epoch &&
                          row.LeaseExpiresAtUtc > observedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, operation.Status)
                .SetProperty(row => row.CancellationRequestedAtUtc, operation.CancellationRequestedAtUtc)
                .SetProperty(row => row.CancellationGeneration, operation.CancellationGeneration)
                .SetProperty(row => row.ExecutionOwnerId, operation.ExecutionOwnerId == null
                    ? null
                    : operation.ExecutionOwnerId.Value.Value)
                .SetProperty(row => row.ExecutionEpoch, operation.ExecutionEpoch)
                .SetProperty(row => row.ClaimedAtUtc, operation.ClaimedAtUtc)
                .SetProperty(row => row.HeartbeatAtUtc, operation.HeartbeatAtUtc)
                .SetProperty(row => row.LeaseExpiresAtUtc, operation.LeaseExpiresAtUtc)
                .SetProperty(row => row.DispatchPhase, operation.DispatchPhase)
                .SetProperty(row => row.TurnAdmittedAtUtc, operation.TurnAdmittedAtUtc)
                .SetProperty(row => row.ProviderDispatchStartedAtUtc, operation.ProviderDispatchStartedAtUtc)
                .SetProperty(row => row.ProviderDispatchReturnedAtUtc, operation.ProviderDispatchReturnedAtUtc)
                .SetProperty(row => row.TranscriptCompletedAtUtc, operation.TranscriptCompletedAtUtc)
                .SetProperty(row => row.CompletedAtUtc, operation.CompletedAtUtc)
                .SetProperty(row => row.ResultingTranscriptRevision, operation.ResultingTranscriptRevision)
                .SetProperty(row => row.AssistantEntryId, operation.AssistantEntryId)
                .SetProperty(row => row.FailureCode, operation.FailureCode)
                .SetProperty(row => row.ConcurrencyToken, operation.ConcurrencyToken),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }
}

public sealed class EfLlmChatTurnStateRepository(AppDbContext dbContext) : ILlmChatTurnStateRepository
{
    public async Task<LlmChatConversationTurnState> LockAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await dbContext.Set<LlmChatConversationRow>()
            .FromSqlInterpolated($"""
                SELECT *
                FROM "LlmChats_Conversations"
                WHERE "Id" = {conversationId.Value}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return new(false, false, false);
        }

        var hasActiveTurn = await dbContext.Set<LlmChatTranscriptRow>()
            .AsNoTracking()
            .AnyAsync(
                row => row.ConversationId == conversationId.Value && row.ActiveTurnId != null,
                cancellationToken)
            .ConfigureAwait(false);
        var hasNonterminalOperation = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .AnyAsync(
                row => row.ConversationId == conversationId.Value &&
                       row.Status != LlmChatOperationStatus.Succeeded &&
                       row.Status != LlmChatOperationStatus.Failed &&
                       row.Status != LlmChatOperationStatus.Cancelled,
                cancellationToken)
            .ConfigureAwait(false);
        return new(true, hasActiveTurn, hasNonterminalOperation);
    }
}

public sealed class EfLlmChatInvocationRecordRepository(AppDbContext dbContext)
    : ILlmChatInvocationRecordRepository
{
    public Task AppendAsync(LlmChatInvocationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        dbContext.Add(LlmChatPersistenceMapper.ToRow(record));
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<LlmChatInvocationRecord>> ListAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Set<LlmChatInvocationRecordRow>()
            .AsNoTracking()
            .Where(row => row.OperationId == operationId.Value)
            .OrderBy(row => row.Ordinal)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(LlmChatPersistenceMapper.ToDomain)];
    }
}
