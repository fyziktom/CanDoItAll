using System.Data;
using System.Globalization;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class FiniteOperationEpochStore(DbContextOptions<FiniteOperationDbContext> options,
    CoordinatedDatabaseTransaction transactions) {
    public async Task RequireInstalledPoolSetEnlistedAsync(DatasetActivationBinding activeBinding,
        DatasetNamespaceId inspectedNamespace, IReadOnlyCollection<FiniteOperationPoolRegistration> registrations,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(inspectedNamespace);
        var installed = CaptureInstallation(registrations);
        await using var context = await CreateEnlistedAsync(cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, activeBinding, cancellationToken);
        await RequireGlobalInstallationAsync(context, installed, inspectedNamespace, cancellationToken);
        var states = await context.Set<FiniteOperationStateRecord>().AsNoTracking()
            .Where(state => state.NamespaceId == inspectedNamespace.Value).OrderBy(state => state.PoolId)
            .Select(state => new { state.PoolId, state.RetentionProtocolVersion })
            .Take(checked(installed.Length + 1)).ToArrayAsync(cancellationToken);
        if (states.Length != installed.Length || installed.Any(pool =>
                !states.Any(state => state.PoolId == pool.Pool.Value && state.RetentionProtocolVersion == pool.ProtocolVersion))) {
            throw Failure(installed[0], FiniteOperationProtocolFailure.InvalidRegistration);
        }
        foreach (var pool in installed) {
            await RequireReadyAsync(context, activeBinding, inspectedNamespace.Value, pool, cancellationToken);
        }
    }

    public async Task RequireUnissuedNamespaceEnlistedAsync(DatasetActivationBinding binding,
        IReadOnlyCollection<FiniteOperationPoolRegistration> registrations, DatasetNamespaceId? previousNamespace,
        CancellationToken cancellationToken) {
        var installed = CaptureInstallation(registrations);
        await using var context = await CreateEnlistedAsync(cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(context, binding, cancellationToken);
        await RequireGlobalInstallationAsync(context, installed, previousNamespace ?? binding.Namespace, cancellationToken);
        if (await context.Set<FiniteOperationStateRecord>().AnyAsync(state => state.NamespaceId == binding.Namespace.Value, cancellationToken) ||
                await context.Set<FiniteOperationEpochRecord>().AnyAsync(epoch => epoch.NamespaceId == binding.Namespace.Value, cancellationToken)) {
            throw Failure(installed[0], FiniteOperationProtocolFailure.InconsistentMetadata);
        }
    }

    private static FiniteOperationPoolRegistration[] CaptureInstallation(IReadOnlyCollection<FiniteOperationPoolRegistration> registrations) {
        ArgumentNullException.ThrowIfNull(registrations);
        var installed = registrations.ToArray();
        if (installed.Length == 0 || installed.Any(pool => pool is null) ||
                installed.Select(pool => pool.Pool).Distinct().Count() != installed.Length) {
            throw new ArgumentException("Dataset readiness requires a nonempty unique compiled pool set.", nameof(registrations));
        }
        return installed;
    }

    private static async Task RequireGlobalInstallationAsync(FiniteOperationDbContext context,
        FiniteOperationPoolRegistration[] installed, DatasetNamespaceId allowedOpenNamespace, CancellationToken cancellationToken) {
        var ids = installed.Select(pool => pool.Pool.Value).ToArray();
        var versions = installed.Select(pool => pool.ProtocolVersion).ToArray();
        var incompatible = await context.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM "Infrastructure_FiniteOperationState" AS s
                WHERE NOT EXISTS (SELECT 1 FROM unnest({ids}, {versions}) AS expected("PoolId", "Version")
                    WHERE expected."PoolId" = s."PoolId" AND expected."Version" = s."RetentionProtocolVersion")
            ) OR EXISTS (
                SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" AS e
                WHERE NOT (e."PoolId" = ANY({ids}))
                    OR NOT EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState" AS s
                        WHERE s."NamespaceId" = e."NamespaceId" AND s."PoolId" = e."PoolId")
                    OR (e."State" = {(int)FiniteOperationEpochState.Open} AND e."NamespaceId" <> {allowedOpenNamespace.Value})
            ) AS "Value"
            """).SingleAsync(cancellationToken);
        if (incompatible) {
            throw Failure(installed[0], FiniteOperationProtocolFailure.InconsistentMetadata);
        }
    }

    public async Task InitializeNewNamespaceEnlistedAsync(DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        await using var context = await CreateEnlistedAsync(cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(context, binding, cancellationToken);
        var state = await context.Set<FiniteOperationStateRecord>().SingleOrDefaultAsync(
            item => item.NamespaceId == binding.Namespace.Value && item.PoolId == pool.Pool.Value, cancellationToken);
        if (state is not null) {
            await RequireReadyAsync(context, binding, binding.Namespace.Value, pool, cancellationToken);
            return;
        }
        if (await context.Set<FiniteOperationEpochRecord>().AnyAsync(
                item => item.NamespaceId == binding.Namespace.Value && item.PoolId == pool.Pool.Value, cancellationToken)) {
            throw Failure(pool, FiniteOperationProtocolFailure.MissingState);
        }
        var now = await ReadTimeAsync(context, cancellationToken);
        context.Add(new FiniteOperationStateRecord {
            NamespaceId = binding.Namespace.Value, PoolId = pool.Pool.Value,
            RetentionProtocolVersion = pool.ProtocolVersion, CurrentEpoch = 1
        });
        context.Add(CreateEpoch(binding, pool, 1, now));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RequireReadyEnlistedAsync(DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        await using var context = await CreateEnlistedAsync(cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
        await RequireReadyAsync(context, binding, binding.Namespace.Value, pool, cancellationToken);
    }

    public async Task CloseForReactivationEnlistedAsync(DatasetActivationBinding previous,
        DatasetActivationBinding current, FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.Namespace == current.Namespace || previous.MaintenanceId == current.MaintenanceId) {
            throw new ArgumentException("Dataset reactivation requires a new namespace and maintenance identity.");
        }
        await using var context = await CreateEnlistedAsync(cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(context, current, cancellationToken);
        var observation = await ObserveAsync(context, current, pool, previous.Namespace.Value, 0, cancellationToken);
        _ = RequireMetadata(observation, current, pool, null);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Infrastructure_FiniteOperationEpochs" AS e SET "State" = {(int)FiniteOperationEpochState.Closed}, "ClosedAtUtc" = clock_timestamp()
            WHERE e."NamespaceId" = {previous.Namespace.Value} AND e."PoolId" = {pool.Pool.Value}
                AND e."State" = {(int)FiniteOperationEpochState.Open}
                AND EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState" AS s
                    WHERE s."NamespaceId" = e."NamespaceId" AND s."PoolId" = e."PoolId"
                        AND s."RetentionProtocolVersion" = {pool.ProtocolVersion})
            """, cancellationToken);
    }

    public async Task<FiniteOperationEpochSnapshot> ReadCurrentAsync(DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        await using var context = new FiniteOperationDbContext(options);
        return await RequireCurrentAsync(context, binding, pool, cancellationToken);
    }

    public async Task<FiniteOperationEpochSnapshot> EnterAsync(DbContext owner, DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, long originalEpoch, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfLessThan(originalEpoch, 1);
        RequireOwner(owner);
        var timing = pool.Timing;
        await InstallDeadlinesAsync(owner, timing.MutationDeadline, timing.StatementDeadline, timing.LockDeadline, cancellationToken);
        var initial = RequireMetadata(await ObserveAsync(owner, binding, pool, binding.Namespace.Value, originalEpoch, cancellationToken),
            binding, pool, originalEpoch);
        RequireOpen(initial, initial.ObservedAtUtc);
        var deadline = TimeSpan.FromMilliseconds(Math.Min(initial.MutationDeadlineMilliseconds, timing.MutationDeadline.TotalMilliseconds));
        await InstallDeadlinesAsync(owner, deadline, Min(timing.StatementDeadline, deadline), Min(timing.LockDeadline, deadline), cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(owner, binding, cancellationToken);
        var locked = await owner.Database.SqlQuery<FiniteEpochObservation>($"""
            SELECT d."NamespaceId" AS "ActiveNamespaceId", d."MaintenanceId", d."SchemaVersion", d."GuardProtocol", d."State" AS "DatasetState",
                s."CurrentEpoch", s."RetiredThroughEpoch", s."RetentionProtocolVersion",
                e."Epoch", e."State", e."OpenedAtUtc", e."AdmissionClosesAtUtc", e."ClosedAtUtc",
                e."MutationDeadlineMilliseconds", e."ReplayRetentionSeconds", clock_timestamp() AS "ObservedAtUtc"
            FROM "Infrastructure_DatasetLifecycle" AS d
            JOIN "Infrastructure_FiniteOperationState" AS s ON s."NamespaceId" = d."NamespaceId" AND s."PoolId" = {pool.Pool.Value}
            JOIN "Infrastructure_FiniteOperationEpochs" AS e ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
                AND e."Epoch" = {originalEpoch}
            WHERE d."Id" = 1 AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                AND d."State" = {(int)DatasetLifecycleState.Active} AND s."RetentionProtocolVersion" = {pool.ProtocolVersion}
            FOR SHARE OF e
            """).SingleOrDefaultAsync(cancellationToken);
        if (locked is null) {
            _ = RequireMetadata(await ObserveAsync(owner, binding, pool, binding.Namespace.Value, originalEpoch, cancellationToken),
                binding, pool, originalEpoch);
            throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        var result = RequireMetadata(locked, binding, pool, originalEpoch);
        await RequireAdmissibleAsync(owner, result, cancellationToken);
        return result;
    }

    public async Task RequireAdmissibleAsync(DbContext owner, FiniteOperationEpochSnapshot snapshot, CancellationToken cancellationToken) {
        RequireOwner(owner);
        RequireOpen(snapshot, await ReadTimeAsync(owner, cancellationToken));
    }

    public static Task<DateTimeOffset> ReadTimeAsync(DbContext owner, CancellationToken cancellationToken)
        => owner.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);

    public async Task<FiniteOperationRotationResult> RotateExpiredAsync(DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        await using var context = new FiniteOperationDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await InstallMaintenanceDeadlinesAsync(context, cancellationToken);
        var initial = await RequireCurrentAsync(context, binding, pool, cancellationToken);
        if (initial.AdmissionClosesAtUtc > initial.ObservedAtUtc) {
            return FiniteOperationRotationResult.Unchanged;
        }
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
        var state = await LockStateAsync(context, binding, pool, cancellationToken);
        if (state is null) {
            return FiniteOperationRotationResult.Busy;
        }
        var current = await RequireCurrentAsync(context, binding, pool, cancellationToken);
        if (current.AdmissionClosesAtUtc > current.ObservedAtUtc) {
            return FiniteOperationRotationResult.Unchanged;
        }
        var epoch = await context.Set<FiniteOperationEpochRecord>().FromSqlInterpolated($"""
            SELECT e.* FROM "Infrastructure_FiniteOperationEpochs" AS e
            WHERE e."NamespaceId" = {binding.Namespace.Value} AND e."PoolId" = {pool.Pool.Value} AND e."Epoch" = {state.CurrentEpoch}
                AND e."State" = {(int)FiniteOperationEpochState.Open} AND e."AdmissionClosesAtUtc" <= clock_timestamp()
                AND EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState" AS s WHERE s."NamespaceId" = e."NamespaceId"
                    AND s."PoolId" = e."PoolId" AND s."RetentionProtocolVersion" = {pool.ProtocolVersion})
            FOR UPDATE OF e SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);
        if (epoch is null) {
            return FiniteOperationRotationResult.Busy;
        }
        var now = await ReadTimeAsync(context, cancellationToken);
        epoch.State = FiniteOperationEpochState.Closed;
        epoch.ClosedAtUtc = now;
        await context.SaveChangesAsync(cancellationToken);
        state.CurrentEpoch = checked(state.CurrentEpoch + 1);
        context.Add(CreateEpoch(binding, pool, state.CurrentEpoch, now));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FiniteOperationRotationResult.Rotated;
    }

    public async Task<FiniteOperationRetirementResult> AdvanceRetirementAsync(DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        await using var context = new FiniteOperationDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await InstallMaintenanceDeadlinesAsync(context, cancellationToken);
        _ = await RequireCurrentAsync(context, binding, pool, cancellationToken);
        var state = await LockStateAsync(context, binding, pool, cancellationToken);
        if (state is null) {
            return new(FiniteOperationRetirementStatus.Busy, null);
        }
        var now = await ReadTimeAsync(context, cancellationToken);
        var candidates = await context.Set<FiniteOperationEpochRecord>().AsNoTracking()
            .Where(epoch => epoch.NamespaceId == binding.Namespace.Value && epoch.PoolId == pool.Pool.Value &&
                epoch.Epoch > state.RetiredThroughEpoch && epoch.Epoch < state.CurrentEpoch)
            .OrderBy(epoch => epoch.Epoch).Take(FiniteOperationBounds.BatchRows).ToArrayAsync(cancellationToken);
        var retired = state.RetiredThroughEpoch;
        foreach (var candidate in candidates) {
            if (candidate.Epoch != retired + 1) {
                throw Failure(pool, FiniteOperationProtocolFailure.MissingRetainedEpoch);
            }
            if (candidate.State != FiniteOperationEpochState.Closed || candidate.ClosedAtUtc is not { } closed) {
                throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
            }
            if (closed.AddSeconds(candidate.ReplayRetentionSeconds) > now ||
                    candidate.AdmissionClosesAtUtc.AddMilliseconds(candidate.MutationDeadlineMilliseconds).AddSeconds(candidate.ReplayRetentionSeconds) > now) {
                break;
            }
            retired = candidate.Epoch;
        }
        if (candidates.Length == 0 && retired + 1 < state.CurrentEpoch) {
            throw Failure(pool, FiniteOperationProtocolFailure.MissingRetainedEpoch);
        }
        if (retired == state.RetiredThroughEpoch) {
            _ = await RequireCurrentAsync(context, binding, pool, cancellationToken);
            return new(FiniteOperationRetirementStatus.Unchanged, retired);
        }
        var changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Infrastructure_FiniteOperationState" AS s SET "RetiredThroughEpoch" = {retired}
            WHERE s."NamespaceId" = {binding.Namespace.Value} AND s."PoolId" = {pool.Pool.Value}
                AND s."RetentionProtocolVersion" = {pool.ProtocolVersion} AND s."RetiredThroughEpoch" = {state.RetiredThroughEpoch}
                AND s."RetiredThroughEpoch" <= {retired} AND s."CurrentEpoch" > {retired}
                AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                    AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                    AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                    AND d."State" = {(int)DatasetLifecycleState.Active})
            """, cancellationToken);
        if (changed != 1) {
            _ = await RequireCurrentAsync(context, binding, pool, cancellationToken);
            throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(FiniteOperationRetirementStatus.Advanced, retired);
    }

    internal static Task<int> InstallMaintenanceDeadlinesAsync(DbContext context, CancellationToken cancellationToken)
        => InstallDeadlinesAsync(context, FiniteOperationBounds.MaintenanceBudget,
            FiniteOperationBounds.MaintenanceBudget, FiniteOperationBounds.MaintenanceBudget, cancellationToken);

    internal static async Task<FiniteOperationEpochSnapshot> RequireCurrentAsync(DbContext context, DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        var snapshot = RequireMetadata(await ObserveAsync(context, binding, pool, binding.Namespace.Value, 0, cancellationToken), binding, pool, null);
        if (snapshot.State != FiniteOperationEpochState.Open) {
            throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        return snapshot;
    }

    private Task<FiniteOperationDbContext> CreateEnlistedAsync(CancellationToken cancellationToken)
        => transactions.CreateEnlistedAsync(options, static configured => new FiniteOperationDbContext(configured), cancellationToken);

    private static async Task RequireReadyAsync(FiniteOperationDbContext context, DatasetActivationBinding binding,
        Guid metadataNamespace, FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        var current = RequireMetadata(await ObserveAsync(context, binding, pool, metadataNamespace, 0, cancellationToken), binding, pool, null);
        if (current.State != FiniteOperationEpochState.Open) {
            throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        var valid = await context.Database.SqlQuery<bool>($"""
            SELECT count(*) FILTER (WHERE e."State" = {(int)FiniteOperationEpochState.Open}) = 1
                AND count(*) FILTER (WHERE e."Epoch" > s."RetiredThroughEpoch" AND e."Epoch" < s."CurrentEpoch"
                    AND e."State" = {(int)FiniteOperationEpochState.Closed}) = s."CurrentEpoch" - s."RetiredThroughEpoch" - 1
                AND count(*) FILTER (WHERE e."Epoch" > s."CurrentEpoch") = 0 AS "Value"
            FROM "Infrastructure_FiniteOperationState" AS s
            LEFT JOIN "Infrastructure_FiniteOperationEpochs" AS e ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
            WHERE s."NamespaceId" = {metadataNamespace} AND s."PoolId" = {pool.Pool.Value}
                AND s."RetentionProtocolVersion" = {pool.ProtocolVersion}
            GROUP BY s."CurrentEpoch", s."RetiredThroughEpoch"
            """).SingleAsync(cancellationToken);
        if (!valid) {
            throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
        }
    }

    private static async Task<FiniteOperationStateRecord?> LockStateAsync(FiniteOperationDbContext context,
        DatasetActivationBinding binding, FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        var state = await context.Set<FiniteOperationStateRecord>().FromSqlInterpolated($"""
            SELECT * FROM "Infrastructure_FiniteOperationState"
            WHERE "NamespaceId" = {binding.Namespace.Value} AND "PoolId" = {pool.Pool.Value} FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);
        if (state is null) {
            _ = await RequireCurrentAsync(context, binding, pool, cancellationToken);
        } else if (state.RetentionProtocolVersion != pool.ProtocolVersion) {
            throw Failure(pool, FiniteOperationProtocolFailure.UnsupportedProtocol);
        }
        return state;
    }

    private static Task<FiniteEpochObservation?> ObserveAsync(DbContext context, DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, Guid metadataNamespace, long requestedEpoch, CancellationToken cancellationToken)
        => context.Database.SqlQuery<FiniteEpochObservation>($"""
            SELECT d."NamespaceId" AS "ActiveNamespaceId", d."MaintenanceId", d."SchemaVersion", d."GuardProtocol", d."State" AS "DatasetState",
                s."CurrentEpoch", s."RetiredThroughEpoch", s."RetentionProtocolVersion",
                e."Epoch", e."State", e."OpenedAtUtc", e."AdmissionClosesAtUtc", e."ClosedAtUtc",
                e."MutationDeadlineMilliseconds", e."ReplayRetentionSeconds", clock_timestamp() AS "ObservedAtUtc"
            FROM "Infrastructure_DatasetLifecycle" AS d
            LEFT JOIN "Infrastructure_FiniteOperationState" AS s ON s."NamespaceId" = {metadataNamespace} AND s."PoolId" = {pool.Pool.Value}
            LEFT JOIN "Infrastructure_FiniteOperationEpochs" AS e ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
                AND e."Epoch" = CASE WHEN {requestedEpoch} = 0 THEN s."CurrentEpoch" ELSE {requestedEpoch} END
            WHERE d."Id" = 1
            """).SingleOrDefaultAsync(cancellationToken);

    private static FiniteOperationEpochSnapshot RequireMetadata(FiniteEpochObservation? observation, DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, long? requestedEpoch) {
        if (observation is null || observation.ActiveNamespaceId != binding.Namespace.Value || observation.MaintenanceId != binding.MaintenanceId ||
                observation.SchemaVersion != binding.SchemaVersion || observation.GuardProtocol != (int)binding.GuardProtocol ||
                observation.DatasetState != (int)DatasetLifecycleState.Active) {
            throw new DatasetActivationMismatchException(binding);
        }
        if (observation.RetentionProtocolVersion is null) {
            throw Failure(pool, FiniteOperationProtocolFailure.MissingState);
        }
        if (observation.RetentionProtocolVersion != pool.ProtocolVersion) {
            throw Failure(pool, FiniteOperationProtocolFailure.UnsupportedProtocol);
        }
        if (observation.CurrentEpoch is not > 0 || observation.RetiredThroughEpoch is not >= 0 ||
                observation.RetiredThroughEpoch >= observation.CurrentEpoch) {
            throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        if (requestedEpoch > observation.CurrentEpoch) {
            throw new FiniteOperationAdmissionException(FiniteOperationAdmissionFailure.UnknownFutureEpoch);
        }
        if (requestedEpoch <= observation.RetiredThroughEpoch) {
            throw new FiniteOperationAdmissionException(FiniteOperationAdmissionFailure.RetiredEpoch);
        }
        if (observation.Epoch is null) {
            throw Failure(pool, FiniteOperationProtocolFailure.MissingRetainedEpoch);
        }
        if (observation.State is null || observation.OpenedAtUtc is null || observation.AdmissionClosesAtUtc is null ||
                observation.MutationDeadlineMilliseconds is null || observation.ReplayRetentionSeconds is null) {
            throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        if (observation.State == (int)FiniteOperationEpochState.Open && observation.Epoch != observation.CurrentEpoch) {
            throw Failure(pool, FiniteOperationProtocolFailure.InconsistentMetadata);
        }
        return new(binding, pool.Pool, pool.ProtocolVersion, observation.Epoch.Value, (FiniteOperationEpochState)observation.State.Value,
            observation.OpenedAtUtc.Value, observation.AdmissionClosesAtUtc.Value, observation.ClosedAtUtc,
            observation.MutationDeadlineMilliseconds.Value, observation.ReplayRetentionSeconds.Value, observation.ObservedAtUtc);
    }

    private static void RequireOpen(FiniteOperationEpochSnapshot snapshot, DateTimeOffset now) {
        if (snapshot.State != FiniteOperationEpochState.Open) {
            throw new FiniteOperationAdmissionException(FiniteOperationAdmissionFailure.ClosedEpoch);
        }
        if (snapshot.AdmissionClosesAtUtc <= now) {
            throw new FiniteOperationAdmissionException(FiniteOperationAdmissionFailure.AdmissionDeadlineElapsed);
        }
    }

    private void RequireOwner(DbContext context) {
        transactions.RequireOwnerProfile(context);
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not { } transaction ||
                !ReferenceEquals(transaction.GetDbTransaction().Connection, context.Database.GetDbConnection()) ||
                transaction.GetDbTransaction().IsolationLevel is not (IsolationLevel.ReadCommitted or IsolationLevel.Serializable)) {
            throw new InvalidOperationException("Finite operation admission requires a matching PostgreSQL owner transaction.");
        }
    }

    private static FiniteOperationEpochRecord CreateEpoch(DatasetActivationBinding binding, FiniteOperationPoolRegistration pool,
        long epoch, DateTimeOffset now) => new() {
        NamespaceId = binding.Namespace.Value, PoolId = pool.Pool.Value, Epoch = epoch,
        State = FiniteOperationEpochState.Open, OpenedAtUtc = now, AdmissionClosesAtUtc = now.Add(pool.Timing.AdmissionWindow),
        MutationDeadlineMilliseconds = checked((int)pool.Timing.MutationDeadline.TotalMilliseconds),
        ReplayRetentionSeconds = checked((long)Math.Ceiling(pool.Timing.ReplayRetention.TotalSeconds))
    };

    private static Task<int> InstallDeadlinesAsync(DbContext context, TimeSpan transaction,
        TimeSpan statement, TimeSpan locking, CancellationToken cancellationToken)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT set_config('transaction_timeout', {Milliseconds(transaction)}, true),
                set_config('statement_timeout', {Milliseconds(statement)}, true), set_config('lock_timeout', {Milliseconds(locking)}, true)
            """, cancellationToken);

    private static string Milliseconds(TimeSpan value)
        => checked((long)Math.Ceiling(value.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture) + "ms";

    private static TimeSpan Min(TimeSpan first, TimeSpan second) => first <= second ? first : second;

    internal static FiniteOperationProtocolException Failure(FiniteOperationPoolRegistration pool, FiniteOperationProtocolFailure failure)
        => new(failure, pool.Pool, pool.ProtocolVersion);
}

internal sealed class FiniteEpochObservation {
    public Guid ActiveNamespaceId { get; set; }
    public Guid MaintenanceId { get; set; }
    public int SchemaVersion { get; set; }
    public int GuardProtocol { get; set; }
    public int DatasetState { get; set; }
    public long? CurrentEpoch { get; set; }
    public long? RetiredThroughEpoch { get; set; }
    public int? RetentionProtocolVersion { get; set; }
    public long? Epoch { get; set; }
    public int? State { get; set; }
    public DateTimeOffset? OpenedAtUtc { get; set; }
    public DateTimeOffset? AdmissionClosesAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public int? MutationDeadlineMilliseconds { get; set; }
    public long? ReplayRetentionSeconds { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}
