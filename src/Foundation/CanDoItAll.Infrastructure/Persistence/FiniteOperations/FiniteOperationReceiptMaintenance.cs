using System.Collections.Immutable;
using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed record FiniteMetadataCleanupResult(int DeletedEpochs, int DeletedStates, bool MoreEligibleMetadata);

public sealed class FiniteOperationReceiptMaintenance {
    private readonly DbContextOptions<FiniteOperationDbContext> options;
    private readonly CoordinatedDatabaseTransaction transactions;
    private readonly ImmutableDictionary<FiniteOperationPoolId, FiniteOperationPoolRegistration> pools;
    private readonly ImmutableDictionary<(FiniteOperationPoolId Pool, FiniteReceiptFamilyId Family), IFiniteOperationReceiptRetentionParticipant> participants;

    public FiniteOperationReceiptMaintenance(DbContextOptions<FiniteOperationDbContext> options, CoordinatedDatabaseTransaction transactions,
        IEnumerable<FiniteOperationPoolRegistration> pools, IEnumerable<IFiniteOperationReceiptRetentionParticipant> participants) {
        this.options = options;
        this.transactions = transactions;
        var installed = pools.ToImmutableArray();
        var owners = participants.ToImmutableArray();
        if (installed.IsEmpty || installed.Any(pool => pool is null) || installed.Select(pool => pool.Pool).Distinct().Count() != installed.Length) {
            throw new InvalidOperationException("Finite operation maintenance requires a nonempty unique installed pool set.");
        }
        foreach (var pool in installed) {
            foreach (var family in pool.Families) {
                if (owners.Count(owner => owner.Pool == pool.Pool && owner.Family == family.Family) != 1) {
                    throw FiniteOperationEpochStore.Failure(pool, FiniteOperationProtocolFailure.InvalidRegistration);
                }
            }
        }
        if (owners.Any(owner => !installed.Any(pool => pool.Pool == owner.Pool && pool.Families.Any(family => family.Family == owner.Family)))) {
            throw new InvalidOperationException("A receipt participant has no exact installed pool and family registration.");
        }
        this.pools = installed.ToImmutableDictionary(pool => pool.Pool);
        this.participants = owners.ToImmutableDictionary(owner => (owner.Pool, owner.Family));
    }

    public async Task<FiniteReceiptPurgeObservation> PurgeRetiredBatchAsync(DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, FiniteReceiptFamilyId family, CancellationToken cancellationToken) {
        RequireRegistration(pool);
        var request = new FiniteReceiptPurgeRequest(binding, pool, family);
        var participant = participants[(pool.Pool, family)];
        FiniteReceiptPurgeObservation? confirmed = null;
        try {
            await using var context = new FiniteOperationDbContext(options);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await FiniteOperationEpochStore.InstallMaintenanceDeadlinesAsync(context, cancellationToken);
            await RequireCleanupContextAsync(context, binding, pool, cancellationToken);
            FiniteReceiptPurgeObservation staged;
            using (transactions.Enter(context)) {
                staged = await participant.PurgeRetiredBatchEnlistedAsync(request, cancellationToken);
                ValidateObservation(request, staged);
            }
            await RequireCleanupContextAsync(context, binding, pool, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            confirmed = staged;
        } catch (Exception exception) when (confirmed is not null) {
            throw new FiniteReceiptPurgeCleanupException(confirmed, exception);
        }
        return confirmed;
    }

    public async Task<FiniteMetadataCleanupResult> CleanupMetadataAsync(DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        RequireRegistration(pool);
        ImmutableArray<FiniteOperationEpochAddress> candidates;
        await using (var context = new FiniteOperationDbContext(options)) {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await FiniteOperationEpochStore.InstallMaintenanceDeadlinesAsync(context, cancellationToken);
            await RequireCleanupContextAsync(context, binding, pool, cancellationToken);
            candidates = (await ReadCandidatesAsync(context, binding, pool, cancellationToken))
                .Select(candidate => new FiniteOperationEpochAddress(new(candidate.NamespaceId), pool.Pool, candidate.Epoch)).ToImmutableArray();
        }
        var empty = candidates.ToHashSet();
        foreach (var family in pool.Families) {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidates.IsEmpty) {
                break;
            }
            var request = new FiniteEpochInspectionRequest(binding, pool, family.Family, candidates);
            await using var context = new FiniteOperationDbContext(options);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await FiniteOperationEpochStore.InstallMaintenanceDeadlinesAsync(context, cancellationToken);
            await RequireCleanupContextAsync(context, binding, pool, cancellationToken);
            using (transactions.Enter(context)) {
                var nonempty = await participants[(pool.Pool, family.Family)].FindNonemptyEpochsEnlistedAsync(request, cancellationToken);
                empty.ExceptWith(ValidateInspection(request, nonempty));
            }
            await RequireCleanupContextAsync(context, binding, pool, cancellationToken);
        }
        await using var owner = new FiniteOperationDbContext(options);
        await using var deletion = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await FiniteOperationEpochStore.InstallMaintenanceDeadlinesAsync(owner, cancellationToken);
        await RequireCleanupContextAsync(owner, binding, pool, cancellationToken);
        var namespaces = empty.Select(candidate => candidate.Namespace.Value).ToArray();
        var epochs = empty.Select(candidate => candidate.Epoch).ToArray();
        var deletedEpochs = await owner.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "Infrastructure_FiniteOperationEpochs" AS e
            USING unnest({namespaces}, {epochs}) AS candidate("NamespaceId", "Epoch"), "Infrastructure_FiniteOperationState" AS s
            WHERE e."NamespaceId" = candidate."NamespaceId" AND e."Epoch" = candidate."Epoch" AND e."PoolId" = {pool.Pool.Value}
                AND s."NamespaceId" = e."NamespaceId" AND s."PoolId" = e."PoolId" AND s."RetentionProtocolVersion" = {pool.ProtocolVersion}
                AND e."State" = {(int)FiniteOperationEpochState.Closed}
                AND ((e."NamespaceId" = {binding.Namespace.Value} AND e."Epoch" <= s."RetiredThroughEpoch")
                    OR (e."NamespaceId" <> {binding.Namespace.Value}
                        AND e."ClosedAtUtc" + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()
                        AND e."AdmissionClosesAtUtc" + e."MutationDeadlineMilliseconds" * interval '1 millisecond'
                            + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()))
                AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                    AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                    AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                    AND d."State" = {(int)DatasetLifecycleState.Active})
            """, cancellationToken);
        var deletedStates = await owner.Database.ExecuteSqlInterpolatedAsync($"""
            WITH empty_states AS (
                SELECT s."NamespaceId", s."PoolId" FROM "Infrastructure_FiniteOperationState" AS s
                WHERE s."PoolId" = {pool.Pool.Value} AND s."RetentionProtocolVersion" = {pool.ProtocolVersion}
                    AND s."NamespaceId" <> {binding.Namespace.Value}
                    AND NOT EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" AS e
                        WHERE e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId")
                ORDER BY s."NamespaceId" LIMIT {FiniteOperationBounds.BatchRows} FOR UPDATE OF s SKIP LOCKED
            ) DELETE FROM "Infrastructure_FiniteOperationState" AS s USING empty_states AS candidate
            WHERE s."NamespaceId" = candidate."NamespaceId" AND s."PoolId" = candidate."PoolId"
                AND s."PoolId" = {pool.Pool.Value} AND s."RetentionProtocolVersion" = {pool.ProtocolVersion}
                AND s."NamespaceId" <> {binding.Namespace.Value}
                AND NOT EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" AS e
                    WHERE e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId")
                AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                    AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                    AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                    AND d."State" = {(int)DatasetLifecycleState.Active})
            """, cancellationToken);
        await RequireCleanupContextAsync(owner, binding, pool, cancellationToken);
        var more = (await ReadCandidatesAsync(owner, binding, pool, cancellationToken)).Length != 0;
        if (!more) {
            more = await owner.Database.SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1 FROM "Infrastructure_FiniteOperationState" AS s
                    WHERE s."PoolId" = {pool.Pool.Value} AND s."RetentionProtocolVersion" = {pool.ProtocolVersion}
                        AND s."NamespaceId" <> {binding.Namespace.Value}
                        AND NOT EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" AS e
                            WHERE e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId")
                        AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                            AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                            AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                            AND d."State" = {(int)DatasetLifecycleState.Active})
                ) AS "Value"
                """).SingleAsync(cancellationToken);
        }
        await deletion.CommitAsync(cancellationToken);
        return new(deletedEpochs, deletedStates, more);
    }

    public static void ValidateObservation(FiniteReceiptPurgeRequest request, FiniteReceiptPurgeObservation observation) {
        if (observation is null || observation.DeletedReceipts < 0 || observation.DeletedReceipts > request.MaximumRows ||
                observation.DeletedBytes < observation.DeletedReceipts || observation.DeletedBytes > request.MaximumBytes ||
                observation.DeletedReceipts == 0 && observation.DeletedBytes != 0 ||
                observation.ObservedAtUtc == default || observation.ObservedAtUtc.Offset != TimeSpan.Zero ||
                !Enum.IsDefined(observation.EligibleWork) ||
                observation.EligibleWork == FiniteOperationEligibleWork.Present && observation.EligibleAtUtc is null ||
                observation.EligibleWork != FiniteOperationEligibleWork.Present && observation.EligibleAtUtc is not null ||
                observation.EligibleAtUtc is { } eligible && (eligible.Offset != TimeSpan.Zero || eligible > observation.ObservedAtUtc)) {
            throw FiniteOperationEpochStore.Failure(request.Pool, FiniteOperationProtocolFailure.InvalidParticipantResponse);
        }
    }

    public static ImmutableArray<FiniteOperationEpochAddress> ValidateInspection(FiniteEpochInspectionRequest request,
        IReadOnlyList<FiniteOperationEpochAddress> nonempty) {
        if (nonempty is null || nonempty.Count > request.Candidates.Length) {
            throw FiniteOperationEpochStore.Failure(request.Pool, FiniteOperationProtocolFailure.InvalidParticipantResponse);
        }
        var captured = nonempty.ToImmutableArray();
        var allowed = request.Candidates.ToHashSet();
        if (captured.Any(candidate => candidate is null || !allowed.Remove(candidate))) {
            throw FiniteOperationEpochStore.Failure(request.Pool, FiniteOperationProtocolFailure.InvalidParticipantResponse);
        }
        return captured;
    }

    private void RequireRegistration(FiniteOperationPoolRegistration pool) {
        if (!pools.TryGetValue(pool.Pool, out var installed) || installed.ProtocolVersion != pool.ProtocolVersion ||
                installed.Timing != pool.Timing || !installed.Families.SequenceEqual(pool.Families)) {
            throw FiniteOperationEpochStore.Failure(pool, FiniteOperationProtocolFailure.InvalidRegistration);
        }
    }

    private static async Task RequireCleanupContextAsync(FiniteOperationDbContext context, DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken) {
        _ = await FiniteOperationEpochStore.RequireCurrentAsync(context, binding, pool, cancellationToken);
        if (await context.Set<FiniteOperationStateRecord>().AsNoTracking().AnyAsync(
                state => state.PoolId == pool.Pool.Value && state.RetentionProtocolVersion != pool.ProtocolVersion, cancellationToken)) {
            throw FiniteOperationEpochStore.Failure(pool, FiniteOperationProtocolFailure.UnsupportedProtocol);
        }
    }

    private static Task<FiniteCleanupCandidate[]> ReadCandidatesAsync(FiniteOperationDbContext context, DatasetActivationBinding binding,
        FiniteOperationPoolRegistration pool, CancellationToken cancellationToken)
        => context.Database.SqlQuery<FiniteCleanupCandidate>($"""
            SELECT e."NamespaceId", e."Epoch" FROM "Infrastructure_FiniteOperationEpochs" AS e
            JOIN "Infrastructure_FiniteOperationState" AS s ON s."NamespaceId" = e."NamespaceId" AND s."PoolId" = e."PoolId"
            WHERE e."PoolId" = {pool.Pool.Value} AND s."RetentionProtocolVersion" = {pool.ProtocolVersion}
                AND e."State" = {(int)FiniteOperationEpochState.Closed}
                AND ((e."NamespaceId" = {binding.Namespace.Value} AND e."Epoch" <= s."RetiredThroughEpoch")
                    OR (e."NamespaceId" <> {binding.Namespace.Value}
                        AND e."ClosedAtUtc" + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()
                        AND e."AdmissionClosesAtUtc" + e."MutationDeadlineMilliseconds" * interval '1 millisecond'
                            + e."ReplayRetentionSeconds" * interval '1 second' <= clock_timestamp()))
                AND EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" AS d WHERE d."Id" = 1
                    AND d."NamespaceId" = {binding.Namespace.Value} AND d."MaintenanceId" = {binding.MaintenanceId}
                    AND d."SchemaVersion" = {binding.SchemaVersion} AND d."GuardProtocol" = {(int)binding.GuardProtocol}
                    AND d."State" = {(int)DatasetLifecycleState.Active})
            ORDER BY e."NamespaceId", e."Epoch" LIMIT {FiniteOperationBounds.BatchRows}
            """).ToArrayAsync(cancellationToken);
}

internal sealed class FiniteCleanupCandidate {
    public Guid NamespaceId { get; set; }
    public long Epoch { get; set; }
}
