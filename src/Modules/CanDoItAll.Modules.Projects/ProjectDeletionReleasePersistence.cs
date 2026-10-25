using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal static class ProjectDeletionReleasePersistence {
    internal static ProjectDeletionCompletionOrigin Origin(ProjectDeletionOriginEvidence evidence) => evidence switch {
        ProjectDeletionOriginEvidence.Ordinary => ProjectDeletionCompletionOrigin.OrdinaryDeletion,
        ProjectDeletionOriginEvidence.Compensation => ProjectDeletionCompletionOrigin.CreationCompensation,
        ProjectDeletionOriginEvidence.ReservedCancellation => ProjectDeletionCompletionOrigin.ReservedCancellation,
        ProjectDeletionOriginEvidence.LegacyInstallation {
            Precondition: ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped
        } => ProjectDeletionCompletionOrigin.LegacyInstallation,
        _ => throw new NotSupportedException("This deletion origin cannot be released by the native owner protocol.")
    };

    internal static async Task<int> MarkAsync(ProjectsDbContext context, ProjectDeletionRecoveryEvidence evidence,
        DateTimeOffset createdAtUtc, byte[] references, byte[] recovery, byte[] recoveryDigest,
        DateTimeOffset now, CancellationToken cancellationToken) {
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Deletion release requires an active PostgreSQL owner transaction.");
        }
        var original = evidence.Original;
        var reserved = (evidence.Origin as ProjectDeletionOriginEvidence.ReservedCancellation)?.Target;
        return await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Projects_DeletionCompletions" AS c
            SET "ReleaseState" = {(int)ProjectDeletionCompletionReleaseState.Released}, "ReleasedAtUtc" = {now}
            WHERE c."NamespaceId" = {original.Completion.Namespace.Value} AND c."CompletionId" = {original.Completion.Id.Value}
                AND c."DatabaseProfileId" = {original.Project.DatabaseProfileId} AND c."ProjectId" = {original.Project.ProjectId}
                AND c."LifetimeId" = {original.Project.LifetimeId} AND c."SchemaVersion" = {ProjectDeletionCompletionProtocol.SchemaVersion}
                AND c."OriginKind" = {(int)Origin(evidence.Origin)} AND c."CreatedAtUtc" = {createdAtUtc}
                AND c."ReleaseState" = {(int)ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease} AND c."ReleasedAtUtc" IS NULL
                AND c."ReferencePayload" = {references} AND c."RecoveryPayload" = {recovery}
                AND c."RecoveryDigest" = {recoveryDigest}
                AND ((c."OriginKind" <> {(int)ProjectDeletionCompletionOrigin.ReservedCancellation} AND
                    EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS r
                        WHERE r."ProjectId" = c."ProjectId" AND r."LifetimeId" = c."LifetimeId" AND r."ImportedHistory" IS NULL))
                    OR (c."OriginKind" = {(int)ProjectDeletionCompletionOrigin.ReservedCancellation} AND
                    EXISTS (SELECT 1 FROM "Projects_ProjectCreationReservations" AS r
                        WHERE r."Id" = {reserved?.ReservationId}
                            AND r."NamespaceId" = c."NamespaceId" AND r."DatabaseProfileId" = c."DatabaseProfileId"
                            AND r."ProjectId" = c."ProjectId" AND r."LifetimeId" = c."LifetimeId"
                            AND r."RequesterId" = {reserved?.RequesterId}
                            AND r."ParentProjectId" IS NOT DISTINCT FROM {reserved?.Parent?.ProjectId}
                            AND r."ParentLifetimeId" IS NOT DISTINCT FROM {reserved?.Parent?.LifetimeId}
                            AND r."State" = {(int)ProjectCreationReservationState.Cancelled} AND r."ConsumedAtUtc" IS NULL
                            AND r."CancelledAtUtc" IS NOT NULL AND isfinite(r."CancelledAtUtc") AND isfinite(r."CreatedAtUtc")
                            AND r."CreatedAtUtc" <= r."CancelledAtUtc" AND r."ImportedHistory" IS NULL)))
            """, cancellationToken);
    }
}
