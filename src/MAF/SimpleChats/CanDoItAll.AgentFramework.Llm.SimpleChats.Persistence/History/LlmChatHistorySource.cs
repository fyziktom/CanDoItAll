using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;

public sealed class LlmChatHistorySource(
    IDbContextFactory<AppDbContext> factory,
    HistoryOutboxWriter outbox) : IProviderHistorySource, IHistorySourceMaintenance {
    public HistorySourceKind Kind => HistorySourceKind.SimpleChat;

    public Task<HistorySourceProgress> ProcessAsync(HistoryMaintenanceContext context, string? cursor,
        int maximumItems, CancellationToken cancellationToken) => context.DatabaseAsync(
            token => ProcessCoreAsync(context.Partition, cursor, maximumItems, token), cancellationToken);

    private async Task<HistorySourceProgress> ProcessCoreAsync(HistoryPartition partition, string? cursor,
        int maximumItems, CancellationToken cancellationToken) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        var position = cursor is null ? new Position(Guid.Empty, 0, false) : JsonSerializer.Deserialize<Position>(cursor) ?? throw new InvalidDataException("Invalid history source cursor.");
        if (position.Complete) {
            return new(cursor, true);
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        var rows = await db.Set<LlmChatInvocationRecordRow>().AsNoTracking()
            .Where(row => row.OperationId.CompareTo(position.Operation) > 0 ||
                row.OperationId == position.Operation && row.Ordinal > position.Ordinal)
            .OrderBy(row => row.OperationId).ThenBy(row => row.Ordinal).Take(maximumItems).ToArrayAsync(cancellationToken);
        foreach (var row in rows) {
            outbox.Stage(db, LlmChatHistoryProjection.Create(LlmChatPersistenceMapper.ToDomain(row), partition));
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var last = rows.LastOrDefault();
        var next = new Position(last?.OperationId ?? position.Operation, last?.Ordinal ?? position.Ordinal, rows.Length < maximumItems);
        return new(JsonSerializer.Serialize(next), next.Complete);
    }

    public async Task<HistorySourceMutation?> ReadAsync(CanonicalEvidenceReference source, CancellationToken cancellationToken) {
        if (source.Kind != Kind || !Guid.TryParseExact(source.Owner.Value, "N", out var operation)) {
            return null;
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, source.Partition, cancellationToken);
        if (int.TryParse(source.Evidence.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal)) {
            var row = await db.Set<LlmChatInvocationRecordRow>().AsNoTracking()
                .SingleOrDefaultAsync(row => row.OperationId == operation && row.Ordinal == ordinal, cancellationToken);
            return row is null ? null : LlmChatHistoryProjection.Create(LlmChatPersistenceMapper.ToDomain(row), source.Partition);
        }
        if (source.Evidence.Value != source.Owner.Value) {
            return null;
        }
        var records = await db.Set<LlmChatInvocationRecordRow>().AsNoTracking().Where(row => row.OperationId == operation)
            .OrderBy(row => row.Ordinal).Select(row => new { row.Ordinal, row.HistoryAttemptsJson })
            .Take(HistoryAttemptCollection.MaximumAttempts + 1).ToArrayAsync(cancellationToken);
        if (records.Length == 0) {
            return null;
        }
        var attempts = records.SelectMany(row => LlmChatHistoryProjection.ParseAttempts(row.HistoryAttemptsJson)).ToArray();
        if (records.Length > HistoryAttemptCollection.MaximumAttempts || attempts.Length > HistoryAttemptCollection.MaximumAttempts) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "The chat operation exceeds its history evidence bound.");
        }
        return new(source, new(records[^1].Ordinal), HistorySourceMutationKind.Upsert, null, []) { Attempts = attempts };
    }

    public async Task<HistoryDetail> ReadDetailAsync(CanonicalEvidenceReference source, HistoryEntryId entryId,
        CancellationToken cancellationToken) {
        var evidence = await ReadAsync(source, cancellationToken);
        if (evidence is null || evidence.Entry?.Id != entryId && !evidence.Attempts.Any(entry => entry.Id == entryId)) {
            return new(entryId, HistoryDetailState.Unavailable);
        }
        return await LlmChatHistoryDetail.ReadAsync(factory, source, entryId, cancellationToken);
    }

    private sealed record Position(Guid Operation, int Ordinal, bool Complete);
}
