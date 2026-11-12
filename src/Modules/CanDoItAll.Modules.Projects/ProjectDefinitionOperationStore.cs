using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal static class ProjectDefinitionOperationStore {
    internal static Task<ProjectDefinitionReceiptRecord?> FindLockedAsync(ProjectsDbContext context,
        ProjectDefinitionOperation operation, ProjectMutationCallerScope caller, CancellationToken cancellationToken) {
        var key = caller.Key.ToArray();
        return context.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().SingleOrDefaultAsync(record =>
            record.NamespaceId == operation.Namespace.Value && record.Epoch == operation.Epoch.Value &&
            record.CallerKind == caller.Kind && record.CallerKey == key && record.OperationId == operation.Id,
            cancellationToken);
    }

    internal static Task<ProjectDefinitionReceiptObservation?> ObserveAsync(ProjectsDbContext context,
        ProjectDefinitionOperation operation, ProjectMutationCallerScope caller, CancellationToken cancellationToken) {
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Durable definition result observation requires PostgreSQL snapshot semantics.");
        }
        var key = caller.Key.ToArray();
        return context.Database.SqlQuery<ProjectDefinitionReceiptObservation>($"""
            SELECT d."NamespaceId" AS "ActiveNamespaceId", d."MaintenanceId", d."SchemaVersion" AS "DatasetSchemaVersion",
                   d."GuardProtocol", d."State" AS "DatasetState", s."CurrentEpoch", s."RetiredThroughEpoch", s."RetentionProtocolVersion",
                   e."State" AS "EpochState", e."AdmissionClosesAtUtc", e."ClosedAtUtc",
                   e."MutationDeadlineMilliseconds", e."ReplayRetentionSeconds", clock_timestamp() AS "ObservedAtUtc",
                   r."SchemaVersion", r."NormalizationVersion", r."ResultVersion", r."CommandKind",
                   r."SemanticDigest", r."ProjectId", r."LifetimeId", r."AcceptedRevision", r."AcceptedAtUtc",
                   r."AcceptedPayload", r."CreationFingerprint", p."Id" IS NOT NULL AS "TargetAvailable"
            FROM "Infrastructure_DatasetLifecycle" AS d
            LEFT JOIN "Infrastructure_FiniteOperationState" AS s ON s."NamespaceId" = d."NamespaceId" AND s."PoolId" = {ProjectFiniteOperationPool.Pool.Value}
            LEFT JOIN "Infrastructure_FiniteOperationEpochs" AS e ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
                AND e."Epoch" = {operation.Epoch.Value}
            LEFT JOIN "Projects_DefinitionReceipts" AS r ON r."NamespaceId" = d."NamespaceId"
                AND r."NamespaceId" = {operation.Namespace.Value} AND r."Epoch" = e."Epoch"
                AND r."CallerKind" = {(int)caller.Kind} AND r."CallerKey" = {key} AND r."OperationId" = {operation.Id}
            LEFT JOIN "Projects_Projects" AS p ON p."Id" = r."ProjectId" AND p."LifetimeId" = r."LifetimeId"
                AND NOT EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS t WHERE t."LifetimeId" = p."LifetimeId")
            WHERE d."Id" = 1
            """).SingleOrDefaultAsync(cancellationToken);
    }
}

internal sealed class ProjectDefinitionReceiptObservation {
    public Guid ActiveNamespaceId { get; set; }
    public Guid MaintenanceId { get; set; }
    public int DatasetSchemaVersion { get; set; }
    public int GuardProtocol { get; set; }
    public int DatasetState { get; set; }
    public long? CurrentEpoch { get; set; }
    public long? RetiredThroughEpoch { get; set; }
    public int? RetentionProtocolVersion { get; set; }
    public int? EpochState { get; set; }
    public DateTimeOffset? AdmissionClosesAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public int? MutationDeadlineMilliseconds { get; set; }
    public long? ReplayRetentionSeconds { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public int? SchemaVersion { get; set; }
    public int? NormalizationVersion { get; set; }
    public int? ResultVersion { get; set; }
    public int? CommandKind { get; set; }
    public byte[]? SemanticDigest { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? LifetimeId { get; set; }
    public Guid? AcceptedRevision { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public byte[]? AcceptedPayload { get; set; }
    public string? CreationFingerprint { get; set; }
    public bool TargetAvailable { get; set; }
}
