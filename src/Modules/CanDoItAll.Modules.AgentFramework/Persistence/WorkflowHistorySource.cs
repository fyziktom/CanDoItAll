using System.Text.Json;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowHistorySource(
    IDbContextFactory<AppDbContext> factory,
    HistoryOutboxWriter outbox) : IProviderHistorySource, IHistorySourceMaintenance {
    public HistorySourceKind Kind => HistorySourceKind.Workflow;

    public Task<HistorySourceProgress> ProcessAsync(HistoryMaintenanceContext context, string? cursor,
        int maximumItems, CancellationToken cancellationToken) => context.DatabaseAsync(
            token => ProcessCoreAsync(context.Partition, cursor, maximumItems, token), cancellationToken);

    private async Task<HistorySourceProgress> ProcessCoreAsync(HistoryPartition partition, string? cursor,
        int maximumItems, CancellationToken cancellationToken) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        var position = cursor is null ? new Position(Guid.Empty, false) : JsonSerializer.Deserialize<Position>(cursor) ?? throw new InvalidDataException("Invalid history source cursor.");
        if (position.Complete) {
            return new(cursor, true);
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        var rows = await db.Set<WorkflowUsageObservationRecordEntity>().AsNoTracking()
            .Where(row => row.Id.CompareTo(position.Id) > 0).OrderBy(row => row.Id).Take(maximumItems).ToArrayAsync(cancellationToken);
        foreach (var row in rows) {
            if (WorkflowHistoryProjection.Create(row.ToObservation(), partition) is { } mutation) {
                outbox.Stage(db, mutation);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var next = new Position(rows.LastOrDefault()?.Id ?? position.Id, rows.Length < maximumItems);
        return new(JsonSerializer.Serialize(next), next.Complete);
    }

    public async Task<HistorySourceMutation?> ReadAsync(CanonicalEvidenceReference source, CancellationToken cancellationToken) {
        if (source.Kind != Kind || !Guid.TryParseExact(source.Owner.Value, "N", out var run) ||
            !Guid.TryParseExact(source.Evidence.Value, "N", out var evidence)) {
            return null;
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, source.Partition, cancellationToken);
        var row = await db.Set<WorkflowUsageObservationRecordEntity>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == evidence && row.RunId == run, cancellationToken);
        return row is null ? null : WorkflowHistoryProjection.Create(row.ToObservation(), source.Partition);
    }

    public async Task<HistoryDetail> ReadDetailAsync(CanonicalEvidenceReference source, HistoryEntryId entryId,
        CancellationToken cancellationToken) {
        var evidence = await ReadAsync(source, cancellationToken);
        if (evidence is null || evidence.Entry?.Id != entryId && !evidence.Attempts.Any(entry => entry.Id == entryId)) {
            return new(entryId, HistoryDetailState.Unavailable);
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, source.Partition, cancellationToken);
        var runId = Guid.ParseExact(source.Owner.Value, "N");
        var evidenceId = Guid.ParseExact(source.Evidence.Value, "N");
        var observation = await db.Set<WorkflowUsageObservationRecordEntity>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == evidenceId && row.RunId == runId, cancellationToken);
        var run = await db.Set<WorkflowRunRecordEntity>().AsNoTracking()
            .Where(row => row.RunId == runId).Select(row => new { row.State, Summary = row.Summary.Substring(0, 32768) })
            .SingleOrDefaultAsync(cancellationToken);
        if (observation is null || run is null) {
            return new(entryId, HistoryDetailState.Unavailable);
        }
        var events = await db.Set<WorkflowEventRecordEntity>().AsNoTracking()
            .Where(row => row.RunId == runId && row.NodeId == observation.NodeId)
            .OrderByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Id)
            .Select(row => new { row.CreatedAtUtc, row.Kind, Message = row.Message.Substring(0, 4096), Length = row.Message.Length })
            .Take(50).ToArrayAsync(cancellationToken);
        var eventText = string.Join("\n\n", events.Reverse().Select(row =>
            $"{row.CreatedAtUtc:u} · {row.Kind}\n{row.Message}{(row.Length > 4096 ? " [truncated]" : string.Empty)}"));
        return new(entryId, HistoryDetailState.Canonical) {
            Sections = [
                new("Workflow run", HistoryTextCapture.Capture($"{runId:D} · {run.State}\n{run.Summary}", 32 * 1024, [])),
                new($"Node {observation.NodeId} · latest 50 events (may include other attempts)",
                    HistoryTextCapture.Capture(eventText, 32 * 1024, []))
            ]
        };
    }

    private sealed record Position(Guid Id, bool Complete);
}
