using System.Data;
using System.Globalization;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Projects;

internal enum ProjectOperationAdmissionFailure { DatasetChanged, CallerChanged, Expired, EpochClosed, UnknownEpoch }

internal sealed class ProjectOperationAdmissionException(ProjectOperationAdmissionFailure failure)
    : InvalidOperationException("The original project operation is no longer admitted.") {
    internal ProjectOperationAdmissionFailure Failure { get; } = failure;
}

public sealed class ProjectOperationEpochService(
    DbContextOptions<ProjectsDbContext> contextOptions,
    CoordinatedDatabaseTransaction transactions,
    IOptions<ProjectDefinitionOperationOptions> configuredOptions) {
    private readonly ProjectDefinitionOperationOptions options = Validate(configuredOptions.Value);

    public async Task RetireForReactivationAsync(DatasetActivationBinding previousBinding,
        DatasetActivationBinding newBinding, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(previousBinding);
        ArgumentNullException.ThrowIfNull(newBinding);
        if (previousBinding.Namespace == newBinding.Namespace || previousBinding.MaintenanceId == newBinding.MaintenanceId) {
            throw new ArgumentException("Dataset reactivation requires a new namespace and maintenance identity.");
        }
        await using var context = await transactions.CreateEnlistedAsync(contextOptions,
            static configured => new ProjectsDbContext(configured), cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(context, newBinding, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Projects_OperationEpochs" SET "State" = {(int)ProjectOperationEpochState.Closed}, "ClosedAtUtc" = clock_timestamp()
            WHERE "NamespaceId" = {previousBinding.Namespace.Value} AND "State" = {(int)ProjectOperationEpochState.Open}
            """, cancellationToken);
    }

    public async Task InitializeForActivationAsync(DatasetActivationBinding binding,
        CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(contextOptions,
            static configured => new ProjectsDbContext(configured), cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(context, binding, cancellationToken);
        var state = await context.Set<ProjectOperationStateRecord>().SingleOrDefaultAsync(
            record => record.NamespaceId == binding.Namespace.Value, cancellationToken);
        if (state is not null) {
            var existing = await context.Set<ProjectOperationEpochRecord>().SingleOrDefaultAsync(
                record => record.NamespaceId == state.NamespaceId && record.Epoch == state.CurrentEpoch, cancellationToken);
            if (existing is not { State: ProjectOperationEpochState.Open }) {
                throw new InvalidOperationException("The active dataset has inconsistent Projects epoch readiness.");
            }
            return;
        }
        var now = await ReadTimeAsync(context, cancellationToken);
        context.Add(new ProjectOperationStateRecord { NamespaceId = binding.Namespace.Value, CurrentEpoch = 1 });
        context.Add(CreateEpoch(binding.Namespace, 1, now));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectDefinitionWriteContext> GetWriteContextAsync(DatasetActivationBinding binding,
        ProjectMutationCallerScope caller, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(caller);
        await using var context = new ProjectsDbContext(contextOptions);
        var metadata = await context.Database.SqlQuery<ProjectCurrentEpochObservation>($"""
            SELECT e."Epoch", e."AdmissionClosesAtUtc", e."MutationDeadlineMilliseconds", e."ReplayRetentionSeconds"
            FROM "Infrastructure_DatasetLifecycle" AS d
            JOIN "Projects_OperationState" AS s ON s."NamespaceId" = d."NamespaceId"
            JOIN "Projects_OperationEpochs" AS e ON e."NamespaceId" = s."NamespaceId" AND e."Epoch" = s."CurrentEpoch"
            WHERE d."Id" = 1 AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                AND d."State" = {(int)DatasetLifecycleState.Active} AND e."State" = {(int)ProjectOperationEpochState.Open}
                AND e."AdmissionClosesAtUtc" > clock_timestamp()
            """).SingleOrDefaultAsync(cancellationToken);
        if (metadata is null) {
            throw new InvalidOperationException("The captured dataset is inactive or its Projects admission epoch needs maintenance.");
        }
        return new(binding.Namespace, caller.Bind(binding.Namespace), new(metadata.Epoch), metadata.AdmissionClosesAtUtc,
            metadata.AdmissionClosesAtUtc.AddMilliseconds(metadata.MutationDeadlineMilliseconds).AddSeconds(metadata.ReplayRetentionSeconds),
            [ProjectDefinitionRequestVersion.VersionOne], [ProjectDefinitionResultVersion.VersionOne], options.GetLimits());
    }

    internal async Task<ProjectOperationEpochRecord> EnterAsync(ProjectsDbContext context,
        DatasetActivationBinding binding, ProjectDefinitionOperation operation, ProjectMutationCallerScope caller,
        CancellationToken cancellationToken) {
        RequireTransaction(context);
        await InstallDeadlinesAsync(context, options.MutationDeadline, options.StatementDeadline, options.LockDeadline, cancellationToken);
        if (operation.Namespace != binding.Namespace) {
            throw new ProjectOperationAdmissionException(ProjectOperationAdmissionFailure.DatasetChanged);
        }
        if (!caller.Matches(binding.Namespace, operation.CallerBinding)) {
            throw new ProjectOperationAdmissionException(ProjectOperationAdmissionFailure.CallerChanged);
        }
        var precheck = await context.Set<ProjectOperationEpochRecord>().AsNoTracking().SingleOrDefaultAsync(
            epoch => epoch.NamespaceId == operation.Namespace.Value && epoch.Epoch == operation.Epoch.Value, cancellationToken);
        await RequireOpenAsync(context, precheck, cancellationToken);
        var deadline = TimeSpan.FromMilliseconds(Math.Min(precheck!.MutationDeadlineMilliseconds, options.MutationDeadline.TotalMilliseconds));
        await InstallDeadlinesAsync(context, deadline, Min(options.StatementDeadline, deadline), Min(options.LockDeadline, deadline), cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
        var guarded = await context.Set<ProjectOperationEpochRecord>().FromSqlInterpolated($"""
            SELECT * FROM "Projects_OperationEpochs"
            WHERE "NamespaceId" = {operation.Namespace.Value} AND "Epoch" = {operation.Epoch.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        await RequireOpenAsync(context, guarded, cancellationToken);
        return guarded!;
    }

    internal static async Task RequireOpenAsync(ProjectsDbContext context, ProjectOperationEpochRecord? epoch,
        CancellationToken cancellationToken) {
        if (epoch is null) {
            throw new ProjectOperationAdmissionException(ProjectOperationAdmissionFailure.UnknownEpoch);
        }
        if (epoch.State != ProjectOperationEpochState.Open) {
            throw new ProjectOperationAdmissionException(ProjectOperationAdmissionFailure.EpochClosed);
        }
        if (epoch.AdmissionClosesAtUtc <= await ReadTimeAsync(context, cancellationToken)) {
            throw new ProjectOperationAdmissionException(ProjectOperationAdmissionFailure.Expired);
        }
    }

    public async Task<bool> RotateExpiredAsync(DatasetActivationBinding binding,
        CancellationToken cancellationToken = default) {
        await using var context = new ProjectsDbContext(contextOptions);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await InstallDeadlinesAsync(context, ProjectDefinitionOperationOptions.MaintenanceBudget,
            ProjectDefinitionOperationOptions.MaintenanceBudget, ProjectDefinitionOperationOptions.MaintenanceBudget, cancellationToken);
        var precheck = await ReadMaintenanceEpochAsync(context, binding, cancellationToken);
        if (precheck.AdmissionClosesAtUtc > precheck.ObservedAtUtc) {
            return false;
        }
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
        var state = await context.Set<ProjectOperationStateRecord>().SingleAsync(
            record => record.NamespaceId == binding.Namespace.Value, cancellationToken);
        var epoch = await context.Set<ProjectOperationEpochRecord>().FromSqlInterpolated($"""
            SELECT * FROM "Projects_OperationEpochs"
            WHERE "NamespaceId" = {binding.Namespace.Value} AND "Epoch" = {state.CurrentEpoch}
                AND "State" = {(int)ProjectOperationEpochState.Open} AND "AdmissionClosesAtUtc" <= clock_timestamp()
            FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);
        if (epoch is null) {
            return false;
        }
        var now = await ReadTimeAsync(context, cancellationToken);
        epoch.State = ProjectOperationEpochState.Closed;
        epoch.ClosedAtUtc = now;
        await context.SaveChangesAsync(cancellationToken);
        var expired = await context.Set<ProjectOperationEpochRecord>().AsNoTracking()
            .Where(record => record.NamespaceId == binding.Namespace.Value && record.Epoch > state.RetiredThroughEpoch &&
                record.Epoch <= state.CurrentEpoch).OrderBy(record => record.Epoch)
            .Take(ProjectDefinitionOperationOptions.PurgeBatchCount).ToArrayAsync(cancellationToken);
        foreach (var candidate in expired) {
            if (candidate.Epoch != state.RetiredThroughEpoch + 1 || candidate.ClosedAtUtc is not { } closed ||
                closed.AddSeconds(candidate.ReplayRetentionSeconds) > now || ReplayUntil(candidate) > now) {
                break;
            }
            state.RetiredThroughEpoch = candidate.Epoch;
        }
        state.CurrentEpoch = checked(state.CurrentEpoch + 1);
        context.Add(CreateEpoch(binding.Namespace, state.CurrentEpoch, now));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<int> PurgeRetiredBatchAsync(DatasetActivationBinding binding,
        CancellationToken cancellationToken = default) {
        await using var context = new ProjectsDbContext(contextOptions);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await InstallDeadlinesAsync(context, ProjectDefinitionOperationOptions.MaintenanceBudget,
            ProjectDefinitionOperationOptions.MaintenanceBudget, ProjectDefinitionOperationOptions.MaintenanceBudget, cancellationToken);
        _ = await ReadMaintenanceEpochAsync(context, binding, cancellationToken);
        var deleted = await context.Database.ExecuteSqlInterpolatedAsync($"""
            WITH cleanup_epoch AS (
                SELECT e."NamespaceId", e."Epoch" FROM "Projects_OperationEpochs" AS e
                JOIN "Projects_OperationState" AS s ON e."NamespaceId" = s."NamespaceId"
                WHERE ((e."NamespaceId" = {binding.Namespace.Value} AND e."Epoch" <= s."RetiredThroughEpoch")
                    OR (e."NamespaceId" <> {binding.Namespace.Value} AND e."State" = {(int)ProjectOperationEpochState.Closed}
                        AND e."ClosedAtUtc" + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()
                        AND e."AdmissionClosesAtUtc" + e."MutationDeadlineMilliseconds" * interval '1 millisecond'
                            + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()))
                    AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                        AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                        AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                        AND d."State" = {(int)DatasetLifecycleState.Active})
                    AND EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" AS r WHERE r."NamespaceId" = e."NamespaceId" AND r."Epoch" = e."Epoch")
                ORDER BY e."NamespaceId", e."Epoch" LIMIT 1
            ), candidates AS (
                SELECT r.ctid, r."CallerKind", r."CallerKey", r."OperationId", octet_length(r."AcceptedPayload") AS bytes
                FROM "Projects_DefinitionReceipts" AS r
                JOIN cleanup_epoch AS e ON r."NamespaceId" = e."NamespaceId" AND r."Epoch" = e."Epoch"
                ORDER BY r."CallerKind", r."CallerKey", r."OperationId" LIMIT {ProjectDefinitionOperationOptions.PurgeBatchCount}
                FOR UPDATE OF r SKIP LOCKED
            ), bounded AS (
                SELECT ctid, sum(bytes) OVER (ORDER BY "CallerKind", "CallerKey", "OperationId") AS total FROM candidates
            ) DELETE FROM "Projects_DefinitionReceipts" AS r USING bounded AS b
                WHERE r.ctid = b.ctid AND b.total <= {ProjectDefinitionOperationOptions.PurgeBatchBytes}
            """, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            WITH retired AS (
                SELECT e."NamespaceId", e."Epoch" FROM "Projects_OperationEpochs" AS e
                JOIN "Projects_OperationState" AS s ON e."NamespaceId" = s."NamespaceId"
                WHERE ((e."NamespaceId" = {binding.Namespace.Value} AND e."Epoch" <= s."RetiredThroughEpoch")
                    OR (e."NamespaceId" <> {binding.Namespace.Value} AND e."State" = {(int)ProjectOperationEpochState.Closed}
                        AND e."ClosedAtUtc" + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()
                        AND e."AdmissionClosesAtUtc" + e."MutationDeadlineMilliseconds" * interval '1 millisecond'
                            + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()))
                    AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                        AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                        AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                        AND d."State" = {(int)DatasetLifecycleState.Active})
                    AND NOT EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" AS r WHERE r."NamespaceId" = e."NamespaceId" AND r."Epoch" = e."Epoch")
                ORDER BY e."NamespaceId", e."Epoch" LIMIT {ProjectDefinitionOperationOptions.PurgeBatchCount}
            ) DELETE FROM "Projects_OperationEpochs" AS e USING retired AS r
                WHERE e."NamespaceId" = r."NamespaceId" AND e."Epoch" = r."Epoch"
            """, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            WITH empty_namespaces AS (
                SELECT s."NamespaceId" FROM "Projects_OperationState" AS s
                WHERE s."NamespaceId" <> {binding.Namespace.Value}
                    AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                        AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                        AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                        AND d."State" = {(int)DatasetLifecycleState.Active})
                    AND NOT EXISTS (SELECT 1 FROM "Projects_OperationEpochs" AS e WHERE e."NamespaceId" = s."NamespaceId")
                    AND NOT EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" AS r WHERE r."NamespaceId" = s."NamespaceId")
                ORDER BY s."NamespaceId" LIMIT {ProjectDefinitionOperationOptions.PurgeBatchCount}
            ) DELETE FROM "Projects_OperationState" AS s USING empty_namespaces AS n
                WHERE s."NamespaceId" = n."NamespaceId"
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    internal static DateTimeOffset ReplayUntil(ProjectOperationEpochRecord epoch)
        => epoch.AdmissionClosesAtUtc.AddMilliseconds(epoch.MutationDeadlineMilliseconds).AddSeconds(epoch.ReplayRetentionSeconds);

    internal static Task<DateTimeOffset> ReadTimeAsync(ProjectsDbContext context, CancellationToken cancellationToken)
        => context.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);

    private static async Task<ProjectMaintenanceEpochObservation> ReadMaintenanceEpochAsync(ProjectsDbContext context,
        DatasetActivationBinding binding, CancellationToken cancellationToken) {
        var current = await context.Database.SqlQuery<ProjectMaintenanceEpochObservation>($"""
            SELECT e."Epoch", e."State", e."AdmissionClosesAtUtc", clock_timestamp() AS "ObservedAtUtc"
            FROM "Infrastructure_DatasetLifecycle" AS d
            LEFT JOIN "Projects_OperationState" AS s ON s."NamespaceId" = d."NamespaceId"
            LEFT JOIN "Projects_OperationEpochs" AS e ON e."NamespaceId" = s."NamespaceId" AND e."Epoch" = s."CurrentEpoch"
            WHERE d."Id" = 1 AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                AND d."State" = {(int)DatasetLifecycleState.Active}
            """).SingleOrDefaultAsync(cancellationToken)
            ?? throw new DatasetActivationMismatchException(binding);
        if (current.Epoch is null || current.State != (int)ProjectOperationEpochState.Open || current.AdmissionClosesAtUtc is null) {
            throw new InvalidOperationException("The active dataset has inconsistent Projects epoch readiness.");
        }
        return current;
    }

    private ProjectOperationEpochRecord CreateEpoch(DatasetNamespaceId @namespace, long epoch, DateTimeOffset now) => new() {
        NamespaceId = @namespace.Value,
        Epoch = epoch,
        State = ProjectOperationEpochState.Open,
        OpenedAtUtc = now,
        AdmissionClosesAtUtc = now.Add(options.AdmissionWindow),
        MutationDeadlineMilliseconds = checked((int)options.MutationDeadline.TotalMilliseconds),
        ReplayRetentionSeconds = checked((long)Math.Ceiling(options.ReplayRetention.TotalSeconds))
    };

    private static Task<int> InstallDeadlinesAsync(ProjectsDbContext context, TimeSpan transaction,
        TimeSpan statement, TimeSpan locking, CancellationToken cancellationToken) {
        RequireTransaction(context);
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT set_config('transaction_timeout', {Milliseconds(transaction)}, true),
                set_config('statement_timeout', {Milliseconds(statement)}, true), set_config('lock_timeout', {Milliseconds(locking)}, true)
            """, cancellationToken);
    }

    private static string Milliseconds(TimeSpan value)
        => checked((long)Math.Ceiling(value.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture) + "ms";

    private static TimeSpan Min(TimeSpan first, TimeSpan second) => first <= second ? first : second;

    private static void RequireTransaction(ProjectsDbContext context) {
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Project operation epochs require an existing PostgreSQL transaction.");
        }
    }

    private static ProjectDefinitionOperationOptions Validate(ProjectDefinitionOperationOptions options) {
        _ = options.GetLimits();
        return options;
    }
}

internal sealed class ProjectCurrentEpochObservation {
    public long Epoch { get; set; }
    public DateTimeOffset AdmissionClosesAtUtc { get; set; }
    public int MutationDeadlineMilliseconds { get; set; }
    public long ReplayRetentionSeconds { get; set; }
}

internal sealed class ProjectMaintenanceEpochObservation {
    public long? Epoch { get; set; }
    public int? State { get; set; }
    public DateTimeOffset? AdmissionClosesAtUtc { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}
