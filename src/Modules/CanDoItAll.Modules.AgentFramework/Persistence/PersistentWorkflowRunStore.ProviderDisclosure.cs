using System.Data;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class PersistentWorkflowRunStore {
    private static bool IsWorkflowEventPrimaryKeyViolation(DbUpdateException exception) {
        for (Exception? current = exception; current is not null; current = current.InnerException) {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_AgentFramework_WorkflowEvents" }) {
                return true;
            }
        }
        return false;
    }

    private static void StageStartedEvent(WorkflowDbContext database, WorkflowRunSnapshot run, WorkflowEventRecord startedEvent) {
        var append = WorkflowProviderDisclosureJournal.Prepare(run, [], startedEvent, allowDeclaration: true);
        database.Set<WorkflowEventRecordEntity>().Add(WorkflowEventRecordEntity.FromEvent(
            WorkflowProviderDisclosureJournal.PublicEvent(startedEvent)));
        StagePrivateEvents(database, append.PrivateEvents);
    }

    private async Task SaveEventWithDisclosureAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(workflowEvent);
        ArgumentNullException.ThrowIfNull(workflowEvent.ProviderReadEvidence);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var memoryLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(database, cancellationToken);
        await using var transaction = WorkflowPersistenceProvider.IsInMemory(database) ? null :
            await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var originalRunId = await database.Set<WorkflowEventRecordEntity>().AsNoTracking()
            .Where(row => row.Id == workflowEvent.Id).Select(row => (Guid?)row.RunId).SingleOrDefaultAsync(cancellationToken);
        var runIds = new[] { workflowEvent.RunId.Value, originalRunId ?? workflowEvent.RunId.Value }.Distinct().Order().ToArray();
        IReadOnlyList<WorkflowRunRecordEntity> lockedRuns;
        if (WorkflowPersistenceProvider.IsInMemory(database)) {
            lockedRuns = await database.Set<WorkflowRunRecordEntity>().Where(row => runIds.Contains(row.RunId)).ToArrayAsync(cancellationToken);
        } else {
            lockedRuns = await database.Set<WorkflowRunRecordEntity>().FromSqlInterpolated($"""
                SELECT * FROM "AgentFramework_WorkflowRuns"
                WHERE "RunId" = ANY ({runIds}) ORDER BY "RunId" FOR UPDATE
                """).ToArrayAsync(cancellationToken);
        }
        var record = await database.Set<WorkflowEventRecordEntity>().SingleOrDefaultAsync(row => row.Id == workflowEvent.Id, cancellationToken);
        if (record?.RunId != originalRunId) {
            throw new InvalidOperationException("The Workflow event changed owners while its write was being admitted.");
        }
        if (record is { Kind: WorkflowEventKind.ProviderReadEvidence }) {
            throw new InvalidOperationException("A public Workflow event cannot overwrite private disclosure history.");
        }
        if (record is not null && record.RunId != workflowEvent.RunId.Value) {
            if (WorkflowProviderDisclosureJournal.HasDisclosureMetadata(workflowEvent)) {
                throw new InvalidOperationException("An existing event cannot move to another run and acquire new disclosure evidence.");
            }
            var originalLinkId = WorkflowProviderDisclosureJournal.LinkId(new(record.RunId), record.Id);
            if (await database.Set<WorkflowEventRecordEntity>().AnyAsync(row => row.Id == originalLinkId, cancellationToken)) {
                throw new InvalidOperationException("A retained disclosure event cannot move to a different Workflow run.");
            }
        }
        var existing = record is null && !WorkflowProviderDisclosureJournal.HasDisclosureMetadata(workflowEvent)
            ? [] : await ReadDisclosureEventsAsync(database, workflowEvent.RunId, cancellationToken, [workflowEvent.Id]);
        var run = lockedRuns.SingleOrDefault(row => row.RunId == workflowEvent.RunId.Value)?.ToSnapshot();
        var append = WorkflowProviderDisclosureJournal.Prepare(run, existing, workflowEvent);
        if (!append.VisibleAlreadyRecorded) {
            var visible = WorkflowProviderDisclosureJournal.PublicEvent(workflowEvent);
            if (record is null) {
                database.Set<WorkflowEventRecordEntity>().Add(WorkflowEventRecordEntity.FromEvent(visible));
            } else {
                record.RunId = visible.RunId.Value;
                record.Kind = visible.Kind;
                record.NodeId = visible.NodeId?.Value;
                record.Message = visible.Message;
                record.PayloadJson = visible.PayloadJson;
                record.CreatedAtUtc = visible.CreatedAtUtc;
            }
        }
        StagePrivateEvents(database, append.PrivateEvents);
        await database.SaveChangesAsync(cancellationToken);
        await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);
    }

    public async Task<WorkflowProviderDisclosureHistory> ReadProviderDisclosureAsync(WorkflowRunId runId,
        CancellationToken cancellationToken = default) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var memoryLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(database, cancellationToken);
        await using var transaction = WorkflowPersistenceProvider.IsInMemory(database) ? null :
            await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var run = await database.Set<WorkflowRunRecordEntity>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.RunId == runId.Value, cancellationToken);
        var events = await ReadDisclosureEventsAsync(database, runId, cancellationToken);
        var history = WorkflowProviderDisclosureJournal.Read(run?.ToSnapshot(), runId, events);
        await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);
        return history;
    }

    internal static async Task StageBackendDisclosureEventsAsync(WorkflowDbContext database, WorkflowRunSnapshot run,
        IReadOnlyList<WorkflowEventRecord> events, CancellationToken cancellationToken) {
        if (!WorkflowPersistenceProvider.IsInMemory(database) && database.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Backend disclosure staging requires the existing owner transaction and run lock.");
        }
        var existing = (await ReadDisclosureEventsAsync(database, run.RunId, cancellationToken,
            events.Select(value => value.Id).ToArray())).ToList();
        foreach (var incoming in events) {
            if (incoming.RunId != run.RunId) {
                throw new InvalidOperationException("Backend disclosure staging cannot change the original Workflow run.");
            }
            var retained = existing.SingleOrDefault(value => value.Id == incoming.Id);
            if (retained is { Kind: WorkflowEventKind.ProviderReadEvidence }) {
                throw new InvalidOperationException("A backend event cannot overwrite private disclosure history.");
            }
            var append = WorkflowProviderDisclosureJournal.Prepare(run, existing, incoming);
            if (retained is null) {
                var visible = WorkflowProviderDisclosureJournal.PublicEvent(incoming);
                database.Set<WorkflowEventRecordEntity>().Add(WorkflowEventRecordEntity.FromEvent(visible));
                existing.Add(visible);
            }
            StagePrivateEvents(database, append.PrivateEvents);
            existing.AddRange(append.PrivateEvents);
        }
    }

    private static async Task<IReadOnlyList<WorkflowEventRecord>> ReadDisclosureEventsAsync(WorkflowDbContext database,
        WorkflowRunId runId, CancellationToken cancellationToken, IReadOnlyCollection<Guid>? incomingEventIds = null) {
        var explicitIds = incomingEventIds?.ToArray() ?? [];
        var rows = await database.Set<WorkflowEventRecordEntity>().AsNoTracking()
            .Where(row => row.RunId == runId.Value && (row.Kind == WorkflowEventKind.ProviderReadEvidence ||
                row.Kind == WorkflowEventKind.Started || row.Kind == WorkflowEventKind.ExecutorCompleted || explicitIds.Contains(row.Id)))
            .OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id)
            .ToArrayAsync(cancellationToken);
        return rows.Select(row => row.ToEvent()).ToArray();
    }

    private static void StagePrivateEvents(WorkflowDbContext database, IReadOnlyList<WorkflowEventRecord> events) {
        foreach (var value in events) {
            database.Set<WorkflowEventRecordEntity>().Add(new() {
                Id = value.Id,
                RunId = value.RunId.Value,
                Kind = value.Kind,
                NodeId = value.NodeId?.Value,
                Message = value.Message,
                PayloadJson = value.PayloadJson,
                CreatedAtUtc = value.CreatedAtUtc
            });
        }
    }
}
