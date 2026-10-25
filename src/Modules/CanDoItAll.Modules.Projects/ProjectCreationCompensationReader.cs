using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectCreationCompensationReader(IDbContextFactory<ProjectsDbContext> contexts,
    ICanonicalRuntimeDatabase database, DatasetActivationService activation, ILogger<ProjectCreationCompensationReader> logger) {
    public async Task<ProjectCreationCompensationObservation> ObserveAsync(ProjectCreationCompensationRequest original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        var profile = database.Profile;
        var generation = database.Generation;
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new NotSupportedException("Original creation compensation observation requires PostgreSQL.");
        }
        if (profile.Profile.Id != original.Original.Completion.DatabaseProfileId) {
            return ContextChanged();
        }
        try {
            var before = await activation.ObserveActiveBindingAsync(profile.Profile.Id, budget.Token);
            if (!Current() || before.Record.Plan.Binding.Namespace != original.Original.Completion.Namespace) {
                return ContextChanged();
            }
            var row = await ReadIndependentAsync(profile, original, budget.Token);
            if (!Current()) {
                return ContextChanged();
            }
            var after = await activation.ObserveActiveBindingAsync(profile.Profile.Id, budget.Token);
            if (!Current() || after != before || row is null || !row.Matches(before.Record.Plan.Binding)) {
                return ContextChanged();
            }
            return row.Interpret(original);
        } catch (Exception failure) when (ReadFailure(failure)) {
            var diagnosticsUnavailable = false;
            try {
                logger.LogWarning("Original creation compensation {CompletionId} in dataset {NamespaceId} could not be observed; failure type {FailureType}.",
                    original.Original.Completion.Id.Value, original.Original.Completion.Namespace.Value, failure.GetType().FullName);
            } catch (Exception) {
                diagnosticsUnavailable = true;
            }
            return new ProjectCreationCompensationObservation.Unavailable(ProjectCreationCompensationUnavailableReason.ReadUnavailable,
                diagnosticsUnavailable);
        }

        bool Current() => database.Profile == profile && database.Generation == generation;
    }

    private async Task<CompensationRow?> ReadIndependentAsync(ResolvedDatabaseProfile profile,
        ProjectCreationCompensationRequest original, CancellationToken cancellationToken) {
        var context = await contexts.CreateDbContextAsync(cancellationToken);
        Exception? primary = null;
        try {
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not null ||
                    System.Transactions.Transaction.Current is not null ||
                    System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                    context.ChangeTracker.Entries().Any() || context.Database.GetDbConnection() is not NpgsqlConnection connection ||
                    connection.State != ConnectionState.Closed) {
                throw new InvalidOperationException("Compensation observation requires a fresh independent PostgreSQL context.");
            }
            CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(context);
            context.Database.SetCommandTimeout(5);
            return await ReadRowAsync(context, original, cancellationToken);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanupFailure) when (primary is not null) {
                throw new AggregateException("Compensation observation and context disposal both failed.", primary, cleanupFailure);
            }
        }
    }

    internal static async Task<ProjectCreationCompensationObservation> ReadEnlistedAsync(ProjectsDbContext context,
        DatasetActivationBinding binding, ProjectCreationCompensationRequest original, CancellationToken cancellationToken) {
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null ||
                binding.Namespace != original.Original.Completion.Namespace) {
            throw new InvalidOperationException("Compensation replay admission requires the original lifecycle-held owner transaction.");
        }
        var row = await ReadRowAsync(context, original, cancellationToken);
        return row is null || !row.Matches(binding) ? ContextChanged() : row.Interpret(original);
    }

    private static Task<CompensationRow?> ReadRowAsync(ProjectsDbContext context,
        ProjectCreationCompensationRequest original, CancellationToken cancellationToken)
        => context.Database.SqlQuery<CompensationRow>($"""
            SELECT d."NamespaceId" AS "ActiveNamespaceId", d."MaintenanceId", d."SchemaVersion" AS "DatasetSchemaVersion",
                d."GuardProtocol", d."State" AS "DatasetState", c."CompletionId", c."DatabaseProfileId", c."ProjectId", c."LifetimeId",
                c."SchemaVersion", c."OriginKind", c."CreatedAtUtc", c."ReleaseState", c."ReleasedAtUtc",
                CASE WHEN octet_length(c."ReferencePayload") BETWEEN 1 AND {ProjectDeletionCompletionCodec.MaximumEncodedBytes}
                    THEN c."ReferencePayload" ELSE NULL END AS "ReferencePayload",
                CASE WHEN octet_length(c."RecoveryPayload") BETWEEN 1 AND {ProjectDeletionRecoveryCodec.MaximumEncodedBytes}
                    THEN c."RecoveryPayload" ELSE NULL END AS "RecoveryPayload",
                CASE WHEN octet_length(c."RecoveryDigest") = 32 THEN c."RecoveryDigest" ELSE NULL END AS "RecoveryDigest",
                EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS r WHERE r."ProjectId" = {original.Receipt.Project.ProjectId}
                    AND r."LifetimeId" = {original.Receipt.Project.LifetimeId} AND r."ImportedHistory" IS NULL) AS "NativeRetirementMatches"
            FROM "Infrastructure_DatasetLifecycle" AS d
            LEFT JOIN "Projects_DeletionCompletions" AS c ON c."NamespaceId" = {original.Original.Completion.Namespace.Value}
                AND c."CompletionId" = {original.Original.Completion.Id.Value}
            WHERE d."Id" = 1
            """).SingleOrDefaultAsync(cancellationToken);

    private static bool ReadFailure(Exception failure) => failure switch {
        OperationCanceledException => false,
        AggregateException aggregate => aggregate.InnerExceptions.All(ReadFailure),
        DbException or IOException or InvalidDataException => true,
        InvalidOperationException invalid => invalid.InnerException is null || ReadFailure(invalid.InnerException),
        _ => false
    };

    private static ProjectCreationCompensationObservation ContextChanged()
        => new ProjectCreationCompensationObservation.Unavailable(ProjectCreationCompensationUnavailableReason.OriginalContextChanged);

    private sealed class CompensationRow {
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
        public byte[]? RecoveryPayload { get; set; }
        public byte[]? RecoveryDigest { get; set; }
        public bool NativeRetirementMatches { get; set; }

        internal bool Matches(DatasetActivationBinding binding) => Metadata().Matches(binding);

        internal ProjectCreationCompensationObservation Interpret(ProjectCreationCompensationRequest original) {
            if (CompletionId is null) {
                return new ProjectCreationCompensationObservation.NotObserved();
            }
            try {
                if (OriginKind != (int)ProjectDeletionCompletionOrigin.CreationCompensation || ReferencePayload is null ||
                        RecoveryPayload is null || RecoveryDigest is not { Length: 32 } ||
                        !CryptographicOperations.FixedTimeEquals(SHA256.HashData(RecoveryPayload), RecoveryDigest)) {
                    return Invalid();
                }
                var evidence = ProjectDeletionRecoveryCodec.Read(RecoveryPayload, original.Original);
                if (evidence.Origin != new ProjectDeletionOriginEvidence.Compensation(original.Receipt.Project,
                        original.Receipt.Fingerprint, original.Receipt.Reservation)) {
                    return Invalid();
                }
                var completion = ProjectDeletionCompletionCodec.Read(ReferencePayload, original.Original);
                return Metadata().Interpret(completion) is ProjectDeletionCompletionObservation.Released or
                    ProjectDeletionCompletionObservation.AwaitingOwnerRelease
                    ? new ProjectCreationCompensationObservation.Committed(completion) : Invalid();
            } catch (InvalidDataException) {
                return Invalid();
            }
        }

        private ProjectDeletionCompletionRow Metadata() => new() {
            ActiveNamespaceId = ActiveNamespaceId, MaintenanceId = MaintenanceId, DatasetSchemaVersion = DatasetSchemaVersion,
            GuardProtocol = GuardProtocol, DatasetState = DatasetState, CompletionId = CompletionId,
            DatabaseProfileId = DatabaseProfileId, ProjectId = ProjectId, LifetimeId = LifetimeId,
            SchemaVersion = SchemaVersion, OriginKind = OriginKind, CreatedAtUtc = CreatedAtUtc,
            ReleaseState = ReleaseState, ReleasedAtUtc = ReleasedAtUtc, ReferencePayload = ReferencePayload,
            NativeRetirementMatches = NativeRetirementMatches
        };

        private static ProjectCreationCompensationObservation Invalid()
            => new ProjectCreationCompensationObservation.Unavailable(ProjectCreationCompensationUnavailableReason.InvalidStoredEvidence);
    }
}
