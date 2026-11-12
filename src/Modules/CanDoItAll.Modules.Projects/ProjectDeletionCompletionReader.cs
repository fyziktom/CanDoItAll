using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDeletionCompletionReader(
    IDbContextFactory<ProjectsDbContext> factory,
    ICanonicalRuntimeDatabase canonicalDatabase,
    DatasetActivationService activation,
    ILogger<ProjectDeletionCompletionReader> logger) : IProjectDeletionCompletionQuery {
    public Task<ProjectDeletionCompletionObservation> ObserveAsync(ProjectDeletionPreparationContext original,
        ProjectDeletionObligation obligation, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(obligation);
        return ObserveCoreAsync(original, row => row.Interpret(original, obligation), cancellationToken);
    }

    public Task<ProjectDeletionCompletionObservation> ObserveAsync(ProjectDeletionCompletionBinding original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        return ObserveCoreAsync(original.Original, row => row.Interpret(original), cancellationToken);
    }

    private async Task<ProjectDeletionCompletionObservation> ObserveCoreAsync(ProjectDeletionPreparationContext original,
        Func<ProjectDeletionCompletionRow, ProjectDeletionCompletionObservation> interpret, CancellationToken cancellationToken) {
        var profile = canonicalDatabase.Profile;
        var generation = canonicalDatabase.Generation;
        var profileId = profile.Profile.Id;
        if (profileId != original.Completion.DatabaseProfileId) {
            return Unavailable(ProjectDeletionCompletionUnavailableReason.OriginalProfileChanged);
        }
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new NotSupportedException("Durable deletion completion requires PostgreSQL snapshot semantics.");
        }

        try {
            DatasetActivationFileObservation before;
            try {
                before = await activation.ObserveActiveBindingAsync(profileId, cancellationToken);
            } catch (InvalidOperationException failure) {
                return ReadUnavailable(failure);
            }
            if (before.Record.Plan.Binding.Namespace != original.Completion.Namespace) {
                return Unavailable(ProjectDeletionCompletionUnavailableReason.DatasetChanged);
            }
            if (!IsOriginalOwner()) {
                return Unavailable(ProjectDeletionCompletionUnavailableReason.OriginalProfileChanged);
            }

            var row = await ReadIndependentAsync(profile, original, cancellationToken);
            if (!IsOriginalOwner()) {
                return Unavailable(ProjectDeletionCompletionUnavailableReason.OriginalProfileChanged);
            }
            DatasetActivationFileObservation after;
            try {
                after = await activation.ObserveActiveBindingAsync(profileId, cancellationToken);
            } catch (InvalidOperationException failure) {
                return ReadUnavailable(failure);
            }
            if (!IsOriginalOwner()) {
                return Unavailable(ProjectDeletionCompletionUnavailableReason.OriginalProfileChanged);
            }
            if (before != after || row is null || !row.Matches(before.Record.Plan.Binding)) {
                return Unavailable(ProjectDeletionCompletionUnavailableReason.DatasetChanged);
            }
            var result = interpret(row);
            if (result is ProjectDeletionCompletionObservation.Unavailable unavailable) {
                logger.LogWarning("Deletion completion {CompletionId} in dataset {NamespaceId} is unavailable: {Reason}.",
                    original.Completion.Id.Value, original.Completion.Namespace.Value, unavailable.Reason);
            }
            return result;
        } catch (Exception failure) when (IsReadFailure(failure)) {
            return ReadUnavailable(failure);
        }

        bool IsOriginalOwner() => canonicalDatabase.Generation == generation && canonicalDatabase.Profile == profile &&
            canonicalDatabase.Profile.Profile.Id == profileId && canonicalDatabase.Profile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql;

        ProjectDeletionCompletionObservation ReadUnavailable(Exception failure) {
            try {
                logger.LogError("Deletion completion {CompletionId} in dataset {NamespaceId} could not be read; failure type {FailureType}.",
                    original.Completion.Id.Value, original.Completion.Namespace.Value, failure.GetType().FullName);
            } catch (Exception loggingFailure) {
                throw new AggregateException("Deletion completion observation and diagnostic publication both failed.", failure, loggingFailure);
            }
            return Unavailable(ProjectDeletionCompletionUnavailableReason.ReadUnavailable);
        }
    }

    private async Task<ProjectDeletionCompletionRow?> ReadIndependentAsync(ResolvedDatabaseProfile profile,
        ProjectDeletionPreparationContext original, CancellationToken cancellationToken) {
        var context = await factory.CreateDbContextAsync(cancellationToken);
        Exception? readFailure = null;
        try {
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null ||
                System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                context.ChangeTracker.Entries().Any() || context.Database.GetDbConnection() is not NpgsqlConnection connection ||
                connection.State != ConnectionState.Closed) {
                throw new InvalidOperationException("Deletion completion observation requires a fresh, independent PostgreSQL context.");
            }
            CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(context);
            return await ReadAsync(context, original, cancellationToken);
        } catch (Exception failure) {
            readFailure = failure;
            throw;
        } finally {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanupFailure) when (readFailure is not null) {
                throw new AggregateException("Deletion completion observation and context disposal both failed.", readFailure, cleanupFailure);
            }
        }
    }

    private static bool IsReadFailure(Exception failure) => failure switch {
        OperationCanceledException => false,
        AggregateException aggregate => aggregate.InnerExceptions.All(IsReadFailure),
        DbException or IOException or InvalidDataException => true,
        InvalidOperationException invalid => invalid.InnerException is null || IsReadFailure(invalid.InnerException),
        _ => false
    };

    internal static Task<ProjectDeletionCompletionRow?> ReadAsync(ProjectsDbContext context,
        ProjectDeletionPreparationContext original, CancellationToken cancellationToken)
        => ReadAsync(context, original, includeRecovery: false, cancellationToken);

    internal static Task<ProjectDeletionCompletionRow?> ReadForRecoveryAsync(ProjectsDbContext context,
        ProjectDeletionPreparationContext original, CancellationToken cancellationToken)
        => ReadAsync(context, original, includeRecovery: true, cancellationToken);

    private static Task<ProjectDeletionCompletionRow?> ReadAsync(ProjectsDbContext context,
        ProjectDeletionPreparationContext original, bool includeRecovery, CancellationToken cancellationToken)
        => Query(context, original.Completion.Namespace.Value, [original.Completion.Id.Value], includeRecovery)
            .SingleOrDefaultAsync(cancellationToken);

    internal static Task<List<ProjectDeletionCompletionRow>> ReadBatchAsync(ProjectsDbContext context,
        Guid namespaceId, Guid[] completionIds, CancellationToken cancellationToken) {
        if (completionIds.Length is < 1 or > 64) {
            throw new ArgumentOutOfRangeException(nameof(completionIds));
        }
        return Query(context, namespaceId, completionIds, includeRecovery: false).ToListAsync(cancellationToken);
    }

    private static IQueryable<ProjectDeletionCompletionRow> Query(ProjectsDbContext context,
        Guid namespaceId, Guid[] completionIds, bool includeRecovery)
        => context.Database.SqlQuery<ProjectDeletionCompletionRow>($"""
            SELECT d."NamespaceId" AS "ActiveNamespaceId", d."MaintenanceId", d."SchemaVersion" AS "DatasetSchemaVersion",
                   d."GuardProtocol", d."State" AS "DatasetState", c."CompletionId", c."DatabaseProfileId",
                   c."ProjectId", c."LifetimeId", c."SchemaVersion", c."OriginKind", c."CreatedAtUtc",
                   c."ReleaseState", c."ReleasedAtUtc",
                   CASE WHEN octet_length(c."ReferencePayload") BETWEEN 1 AND {ProjectDeletionCompletionCodec.MaximumEncodedBytes}
                       THEN c."ReferencePayload" ELSE NULL END AS "ReferencePayload",
                   CASE WHEN ({includeRecovery} OR c."OriginKind" = {(int)ProjectDeletionCompletionOrigin.ReservedCancellation}) AND
                       octet_length(c."RecoveryPayload") BETWEEN 1 AND {ProjectDeletionRecoveryCodec.MaximumEncodedBytes}
                       THEN c."RecoveryPayload" ELSE NULL END AS "RecoveryPayload",
                   CASE WHEN ({includeRecovery} OR c."OriginKind" = {(int)ProjectDeletionCompletionOrigin.ReservedCancellation}) AND
                       octet_length(c."RecoveryDigest") = 32 THEN c."RecoveryDigest" ELSE NULL END AS "RecoveryDigest",
                   q."Id" AS "ReservationId", q."RequesterId" AS "ReservationRequesterId",
                   q."ParentProjectId" AS "ReservationParentId", q."ParentLifetimeId" AS "ReservationParentLifetimeId",
                   COALESCE(q."State" = {(int)ProjectCreationReservationState.Cancelled} AND q."ConsumedAtUtc" IS NULL
                       AND q."CancelledAtUtc" IS NOT NULL AND isfinite(q."CancelledAtUtc") AND isfinite(q."CreatedAtUtc")
                       AND q."CreatedAtUtc" <= q."CancelledAtUtc" AND q."ImportedHistory" IS NULL, FALSE)
                       AS "NativeReservedCancellationMatches",
                   EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS r
                       WHERE r."ProjectId" = c."ProjectId" AND r."LifetimeId" = c."LifetimeId"
                           AND r."ImportedHistory" IS NULL) AS "NativeRetirementMatches"
            FROM "Infrastructure_DatasetLifecycle" AS d
            LEFT JOIN "Projects_DeletionCompletions" AS c ON c."NamespaceId" = d."NamespaceId"
                AND c."NamespaceId" = {namespaceId} AND c."CompletionId" = ANY({completionIds})
            LEFT JOIN "Projects_ProjectCreationReservations" AS q ON c."OriginKind" = {(int)ProjectDeletionCompletionOrigin.ReservedCancellation}
                AND q."NamespaceId" = c."NamespaceId" AND q."DatabaseProfileId" = c."DatabaseProfileId"
                AND q."ProjectId" = c."ProjectId" AND q."LifetimeId" = c."LifetimeId"
            WHERE d."Id" = 1
            """);

    private static ProjectDeletionCompletionObservation Unavailable(ProjectDeletionCompletionUnavailableReason reason)
        => new ProjectDeletionCompletionObservation.Unavailable(reason);
}

internal sealed class ProjectDeletionCompletionRow {
    public Guid ActiveNamespaceId { get; set; }
    public Guid MaintenanceId { get; set; }
    public int DatasetSchemaVersion { get; set; }
    public int GuardProtocol { get; set; }
    public int DatasetState { get; set; }
    public Guid? CompletionId { get; set; }
    public Guid? DatabaseProfileId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? LifetimeId { get; set; }
    public int? SchemaVersion { get; set; }
    public int? OriginKind { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public int? ReleaseState { get; set; }
    public DateTimeOffset? ReleasedAtUtc { get; set; }
    public byte[]? ReferencePayload { get; set; }
    public bool NativeRetirementMatches { get; set; }
    public byte[]? RecoveryPayload { get; set; }
    public byte[]? RecoveryDigest { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? ReservationRequesterId { get; set; }
    public Guid? ReservationParentId { get; set; }
    public Guid? ReservationParentLifetimeId { get; set; }
    public bool NativeReservedCancellationMatches { get; set; }

    internal bool Matches(DatasetActivationBinding binding)
        => ActiveNamespaceId == binding.Namespace.Value && MaintenanceId == binding.MaintenanceId &&
            DatasetSchemaVersion == binding.SchemaVersion && GuardProtocol == (int)binding.GuardProtocol &&
            DatasetState == (int)DatasetLifecycleState.Active;

    internal ProjectDeletionCompletionObservation Interpret(ProjectDeletionPreparationContext original,
        ProjectDeletionObligation obligation)
        => InterpretCore(original, binding => binding.Obligations.Contains(obligation));

    internal ProjectDeletionCompletionObservation Interpret(ProjectDeletionCompletionBinding original)
        => InterpretCore(original.Original, binding => binding.Obligations.Count == original.Obligations.Count &&
            original.Obligations.All(binding.Obligations.Contains));

    private ProjectDeletionCompletionObservation InterpretCore(ProjectDeletionPreparationContext original,
        Func<ProjectDeletionCompletionBinding, bool> matches) {
        if (CompletionId is null) {
            return Unavailable(ProjectDeletionCompletionUnavailableReason.MissingCompletion);
        }
        if (SchemaVersion != ProjectDeletionCompletionProtocol.SchemaVersion) {
            return Unavailable(ProjectDeletionCompletionUnavailableReason.UnsupportedVersion);
        }
        if (CompletionId != original.Completion.Id.Value || DatabaseProfileId != original.Completion.DatabaseProfileId ||
                ProjectId != original.Project.ProjectId || LifetimeId != original.Project.LifetimeId ||
                OriginKind is not ((int)ProjectDeletionCompletionOrigin.OrdinaryDeletion or
                    (int)ProjectDeletionCompletionOrigin.CreationCompensation or (int)ProjectDeletionCompletionOrigin.LegacyInstallation or
                    (int)ProjectDeletionCompletionOrigin.ReservedCancellation) ||
                CreatedAtUtc is not { } created || created == DateTimeOffset.MinValue || created == DateTimeOffset.MaxValue ||
                created.Offset != TimeSpan.Zero || ReferencePayload is null ||
                ReleasedAtUtc is { } releasedAt && (releasedAt == DateTimeOffset.MinValue ||
                    releasedAt == DateTimeOffset.MaxValue || releasedAt.Offset != TimeSpan.Zero)) {
            return Unavailable(ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        }
        if (OriginKind == (int)ProjectDeletionCompletionOrigin.ReservedCancellation) {
            if (!MatchesReservedCancellation(original)) {
                return Unavailable(ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
            }
        } else if (!NativeRetirementMatches) {
            return Unavailable(ProjectDeletionCompletionUnavailableReason.MissingNativeRetirement);
        }
        ProjectDeletionCompletionBinding binding;
        try {
            binding = ProjectDeletionCompletionCodec.Read(ReferencePayload, original);
        } catch (InvalidDataException) {
            return Unavailable(ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        }
        if (!matches(binding)) {
            return Unavailable(ProjectDeletionCompletionUnavailableReason.ObligationMismatch);
        }
        return (ReleaseState, ReleasedAtUtc) switch {
            ((int)ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease, null) => new ProjectDeletionCompletionObservation.AwaitingOwnerRelease(),
            ((int)ProjectDeletionCompletionReleaseState.Released, { } released) => new ProjectDeletionCompletionObservation.Released(released),
            _ => Unavailable(ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding)
        };
    }

    internal bool MatchesReservedCancellation(ProjectDeletionPreparationContext original,
        ProjectReservationCancellationTarget? expected = null) {
        if (OriginKind != (int)ProjectDeletionCompletionOrigin.ReservedCancellation || !NativeReservedCancellationMatches ||
                RecoveryPayload is null || RecoveryDigest is not { Length: 32 } ||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(RecoveryPayload), RecoveryDigest)) {
            return false;
        }
        try {
            return ProjectDeletionRecoveryCodec.Read(RecoveryPayload, original).Origin is ProjectDeletionOriginEvidence.ReservedCancellation cancellation &&
                (expected is null || cancellation.Target == expected) && cancellation.Target.ReservationId == ReservationId &&
                cancellation.Target.RequesterId == ReservationRequesterId && cancellation.Target.Parent?.ProjectId == ReservationParentId &&
                cancellation.Target.Parent?.LifetimeId == ReservationParentLifetimeId;
        } catch (InvalidDataException) {
            return false;
        }
    }

    private static ProjectDeletionCompletionObservation Unavailable(ProjectDeletionCompletionUnavailableReason reason)
        => new ProjectDeletionCompletionObservation.Unavailable(reason);
}
