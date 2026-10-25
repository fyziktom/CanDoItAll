using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessCancellationProgressReader(IDbContextFactory<ProcessPersistenceDbContext> factory) : IProcessCancellationProgressReader {
    public async Task<ProcessCancellationProgress?> ReadAsync(ProcessRunId runId, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        cancellationToken.ThrowIfCancellationRequested();
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!context.Database.IsNpgsql()) {
            throw new InvalidOperationException("Cancellation progress requires PostgreSQL's bounded read contract.");
        }
        context.Database.SetCommandTimeout(5);
        var row = await context.Database.SqlQuery<ProcessCancellationProgressRow>($"""
            SELECT run."RunId", run."RootRunId", run."Status" AS "RuntimeStatus", run."UpdatedAtUtc" AS "RuntimeUpdatedAtUtc",
                recovery."RootRunId" AS "RecoveryRootRunId", recovery."Origin", recovery."BarrierCommandId",
                recovery."ProgressRevision", recovery."UpdatedAtUtc" AS "RecoveryUpdatedAtUtc", recovery."Cycle",
                CASE WHEN octet_length(recovery."PositionJson") BETWEEN 2 AND {ProcessCancellationRecoveryCodec.MaximumPositionBytes}
                    THEN recovery."PositionJson" ELSE NULL END AS "PositionJson",
                recovery."Pending", recovery."Completed", recovery."IntentJson" IS NOT NULL AS "HasIntent",
                (recovery."LeaseToken" IS NOT NULL OR recovery."LeaseExpiresAtUtc" IS NOT NULL) AS "HasLease"
            FROM process_runtime_states AS run
            LEFT JOIN process_cancellation_recovery AS recovery ON recovery."RootRunId" = run."RunId"
            WHERE run."RunId" = {runId.Value}
            """).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    private static ProcessCancellationProgress Map(ProcessCancellationProgressRow row) {
        if (row.RunId == Guid.Empty || row.RootRunId == Guid.Empty ||
                !Enum.TryParse<ProcessRuntimeStatus>(row.RuntimeStatus, out var status) || !Enum.IsDefined(status) ||
                !string.Equals(row.RuntimeStatus, status.ToString(), StringComparison.Ordinal)) {
            throw new InvalidDataException("The selected runtime record has an invalid identity or status.");
        }
        ProcessCancellationProgress progress = new(new(row.RunId), new(row.RootRunId), status, row.RuntimeUpdatedAtUtc,
            ProcessCancellationTracking.ReconciliationRequired);
        if (row.RecoveryRootRunId is null) {
            return progress with { Tracking = row.RunId != row.RootRunId ? ProcessCancellationTracking.ChildRun
                : status is ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled
                    ? ProcessCancellationTracking.LegacyUntracked : ProcessCancellationTracking.NotRequested };
        }
        if (row.RunId != row.RootRunId || row.Origin is not { } origin || !Enum.IsDefined((ProcessCancellationRecoveryOrigin)origin) ||
                row.BarrierCommandId == Guid.Empty || ((ProcessCancellationRecoveryOrigin)origin == ProcessCancellationRecoveryOrigin.LiveBarrier) != (row.BarrierCommandId is not null) ||
                row.ProgressRevision is not >= 0 || row.Cycle is not >= 0 || row.RecoveryUpdatedAtUtc is null ||
                row.Pending is not { } pending || !Enum.IsDefined((ProcessCancellationRecoveryPending)pending) || row.Completed is null) {
            return progress;
        }
        ProcessCancellationRecoveryPosition? position = null;
        try {
            if (row.PositionJson is not null) {
                position = ProcessCancellationRecoveryCodec.ReadPosition(row.PositionJson);
            }
        } catch (InvalidDataException) {
            pending = (int)ProcessCancellationRecoveryPending.InvalidEvidence;
        }
        if (position is null) {
            pending = (int)ProcessCancellationRecoveryPending.InvalidEvidence;
        }
        var recovery = new ProcessCancellationRecoveryProgress((ProcessCancellationRecoveryOrigin)origin,
            row.BarrierCommandId is { } barrier ? new(barrier) : null, row.ProgressRevision.Value, row.RecoveryUpdatedAtUtc.Value,
            position?.Stage, (ProcessCancellationRecoveryPending)pending);
        progress = progress with { Recovery = recovery };
        if (position is null || status is not (ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled) ||
                recovery.Pending is ProcessCancellationRecoveryPending.InvalidEvidence or ProcessCancellationRecoveryPending.ConflictingState ||
                row.Completed != (position.Stage == ProcessCancellationRecoveryStage.Completed)) {
            return progress;
        }
        if (row.Completed == false) {
            return progress with { Tracking = ProcessCancellationTracking.Pending };
        }
        if (position.CoverageVersion < ProcessCancellationRecoveryPosition.CurrentCoverageVersion) {
            return progress;
        }
        return progress with { Tracking = status == ProcessRuntimeStatus.Cancelled && row.ProgressRevision > 0 &&
            !row.HasIntent && !row.HasLease && recovery.Pending == ProcessCancellationRecoveryPending.None
                ? ProcessCancellationTracking.Completed : ProcessCancellationTracking.ReconciliationRequired };
    }
}

internal sealed record ProcessCancellationProgressRow(Guid RunId, Guid RootRunId, string RuntimeStatus, DateTimeOffset RuntimeUpdatedAtUtc,
    Guid? RecoveryRootRunId, int? Origin, Guid? BarrierCommandId, long? ProgressRevision, DateTimeOffset? RecoveryUpdatedAtUtc,
    int? Cycle, string? PositionJson, int? Pending, bool? Completed, bool HasIntent, bool HasLease);
