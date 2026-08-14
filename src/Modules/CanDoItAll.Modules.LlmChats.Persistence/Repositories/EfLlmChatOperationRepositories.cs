using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.Repositories;

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

    public async Task<LlmChatOperationAdmission> AdmitAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LlmChats_Operations"
                ("Id", "ConversationId", "Kind", "RequestFingerprint", "ExpectedTranscriptRevision",
                 "Status", "CancellationRequestedAtUtc", "TurnAdmittedAtUtc",
                 "ProviderDispatchStartedAtUtc", "ProviderDispatchReturnedAtUtc",
                 "TranscriptCompletedAtUtc", "StartedAtUtc", "CompletedAtUtc",
                 "ResultingTranscriptRevision", "AssistantEntryId", "FailureCode", "ConcurrencyToken")
            VALUES
                ({operation.Id.Value}, {operation.ConversationId.Value}, {(int)operation.Kind},
                 {operation.RequestFingerprint.Value}, {operation.ExpectedTranscriptRevision},
                 {(int)operation.Status}, {operation.CancellationRequestedAtUtc}, {operation.TurnAdmittedAtUtc},
                 {operation.ProviderDispatchStartedAtUtc}, {operation.ProviderDispatchReturnedAtUtc},
                 {operation.TranscriptCompletedAtUtc}, {operation.StartedAtUtc}, {operation.CompletedAtUtc},
                 {operation.ResultingTranscriptRevision}, {operation.AssistantEntryId}, {operation.FailureCode},
                 {operation.ConcurrencyToken})
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken).ConfigureAwait(false);
        var stored = await TryGetAsync(operation.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The admitted LLM Chat operation could not be reloaded.");
        return new LlmChatOperationAdmission(stored, inserted == 1);
    }

    public async Task<LlmChatOperation?> TryClaimDispatchAsync(
        LlmChatOperationId id,
        LlmChatRequestFingerprint requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var affected = await dbContext.Set<LlmChatOperationRow>()
            .Where(row => row.Id == id.Value &&
                          row.RequestFingerprint == requestFingerprint.Value &&
                          row.Status == LlmChatOperationStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, LlmChatOperationStatus.Running)
                .SetProperty(row => row.ConcurrencyToken, row => row.ConcurrencyToken + 1),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1
            ? await TryGetAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
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
