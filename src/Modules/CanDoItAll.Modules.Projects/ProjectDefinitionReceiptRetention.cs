using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionReceiptRetention(DbContextOptions<ProjectsDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IFiniteOperationReceiptRetentionParticipant {
    public FiniteOperationPoolId Pool => ProjectFiniteOperationPool.Pool;
    public FiniteReceiptFamilyId Family => ProjectFiniteOperationPool.DefinitionFamily;

    public async Task<FiniteReceiptPurgeObservation> PurgeRetiredBatchEnlistedAsync(FiniteReceiptPurgeRequest request,
        CancellationToken cancellationToken) {
        RequireRegistration(request.Pool, request.Family);
        var binding = request.Binding;
        await using var context = await transactions.CreateEnlistedAsync(options,
            static configured => new ProjectsDbContext(configured), cancellationToken);
        var deleted = (await context.Database.SqlQuery<ProjectReceiptPurgeCount>($"""
            WITH cleanup_epoch AS (
                SELECT e."NamespaceId", e."Epoch" FROM "Infrastructure_FiniteOperationEpochs" AS e
                JOIN "Infrastructure_FiniteOperationState" AS s ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
                WHERE e."PoolId" = {Pool.Value} AND s."RetentionProtocolVersion" = {request.Pool.ProtocolVersion}
                    AND e."State" = {(int)FiniteOperationEpochState.Closed}
                    AND ((e."NamespaceId" = {binding.Namespace.Value} AND e."Epoch" <= s."RetiredThroughEpoch")
                        OR (e."NamespaceId" <> {binding.Namespace.Value}
                            AND e."ClosedAtUtc" + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()
                            AND e."AdmissionClosesAtUtc" + e."MutationDeadlineMilliseconds" * interval '1 millisecond'
                                + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()))
                    AND EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" AS r WHERE r."NamespaceId" = e."NamespaceId" AND r."Epoch" = e."Epoch")
                ORDER BY e."NamespaceId", e."Epoch" LIMIT 1
            ), candidates AS (
                SELECT r.ctid, r."CallerKind", r."CallerKey", r."OperationId", octet_length(r."AcceptedPayload") AS bytes
                FROM "Projects_DefinitionReceipts" AS r
                JOIN cleanup_epoch AS e ON r."NamespaceId" = e."NamespaceId" AND r."Epoch" = e."Epoch"
                ORDER BY r."CallerKind", r."CallerKey", r."OperationId" LIMIT {request.MaximumRows}
                FOR UPDATE OF r SKIP LOCKED
            ), bounded AS (
                SELECT ctid, sum(bytes) OVER (ORDER BY "CallerKind", "CallerKey", "OperationId") AS total FROM candidates
            ), deleted AS (DELETE FROM "Projects_DefinitionReceipts" AS r USING bounded AS b
                WHERE r.ctid = b.ctid AND b.total <= {request.MaximumBytes}
                    AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                        AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                        AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                        AND d."State" = {(int)DatasetLifecycleState.Active})
                RETURNING octet_length(r."AcceptedPayload") AS bytes)
            SELECT count(*)::integer AS "DeletedReceipts", coalesce(sum(bytes), 0)::bigint AS "DeletedBytes" FROM deleted
            """).ToArrayAsync(cancellationToken)).Single();
        var observed = await context.Database.SqlQuery<ProjectReceiptEligibility>($"""
            SELECT clock_timestamp() AS "ObservedAtUtc", work."EligibleAtUtc"
            FROM "Infrastructure_DatasetLifecycle" AS d
            LEFT JOIN LATERAL (
                SELECT greatest(e."ClosedAtUtc" + e."ReplayRetentionSeconds" * interval '1 second',
                    e."AdmissionClosesAtUtc" + e."MutationDeadlineMilliseconds" * interval '1 millisecond'
                        + e."ReplayRetentionSeconds" * interval '1 second') AS "EligibleAtUtc"
                FROM "Infrastructure_FiniteOperationEpochs" AS e
                JOIN "Infrastructure_FiniteOperationState" AS s ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
                WHERE e."PoolId" = {Pool.Value} AND s."RetentionProtocolVersion" = {request.Pool.ProtocolVersion}
                    AND e."State" = {(int)FiniteOperationEpochState.Closed}
                    AND ((e."NamespaceId" = {binding.Namespace.Value} AND e."Epoch" <= s."RetiredThroughEpoch")
                        OR (e."NamespaceId" <> {binding.Namespace.Value}
                            AND e."ClosedAtUtc" + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()
                            AND e."AdmissionClosesAtUtc" + e."MutationDeadlineMilliseconds" * interval '1 millisecond'
                                + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()))
                    AND EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" AS r WHERE r."NamespaceId" = e."NamespaceId" AND r."Epoch" = e."Epoch")
                ORDER BY e."NamespaceId", e."Epoch" LIMIT 1
            ) AS work ON true
            WHERE d."Id" = 1 AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                AND d."State" = {(int)DatasetLifecycleState.Active}
            """).SingleOrDefaultAsync(cancellationToken) ?? throw new DatasetActivationMismatchException(binding);
        return new(deleted.DeletedReceipts, deleted.DeletedBytes,
            observed.EligibleAtUtc is null ? FiniteOperationEligibleWork.NoneObserved : FiniteOperationEligibleWork.Present,
            observed.ObservedAtUtc, observed.EligibleAtUtc);
    }

    public async Task<IReadOnlyList<FiniteOperationEpochAddress>> FindNonemptyEpochsEnlistedAsync(FiniteEpochInspectionRequest request,
        CancellationToken cancellationToken) {
        RequireRegistration(request.Pool, request.Family);
        await using var context = await transactions.CreateEnlistedAsync(options,
            static configured => new ProjectsDbContext(configured), cancellationToken);
        var namespaces = request.Candidates.Select(candidate => candidate.Namespace.Value).ToArray();
        var epochs = request.Candidates.Select(candidate => candidate.Epoch).ToArray();
        var rows = await context.Database.SqlQuery<ProjectReceiptEpoch>($"""
            SELECT candidate."NamespaceId", candidate."Epoch"
            FROM unnest({namespaces}, {epochs}) AS candidate("NamespaceId", "Epoch")
            WHERE EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" AS r
                WHERE r."NamespaceId" = candidate."NamespaceId" AND r."Epoch" = candidate."Epoch")
            """).ToArrayAsync(cancellationToken);
        return rows.Select(row => new FiniteOperationEpochAddress(new(row.NamespaceId), Pool, row.Epoch)).ToArray();
    }

    private void RequireRegistration(FiniteOperationPoolRegistration pool, FiniteReceiptFamilyId family) {
        if (pool.Pool != Pool || family != Family || pool.ProtocolVersion != ProjectFiniteOperationPool.ProtocolVersion) {
            throw new FiniteOperationProtocolException(FiniteOperationProtocolFailure.UnsupportedProtocol, pool.Pool, pool.ProtocolVersion);
        }
    }
}

internal sealed class ProjectReceiptPurgeCount {
    public int DeletedReceipts { get; set; }
    public long DeletedBytes { get; set; }
}

internal sealed class ProjectReceiptEligibility {
    public DateTimeOffset ObservedAtUtc { get; set; }
    public DateTimeOffset? EligibleAtUtc { get; set; }
}

internal sealed class ProjectReceiptEpoch {
    public Guid NamespaceId { get; set; }
    public long Epoch { get; set; }
}
