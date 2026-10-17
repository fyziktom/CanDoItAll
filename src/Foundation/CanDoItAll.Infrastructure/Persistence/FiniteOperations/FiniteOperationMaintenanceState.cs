using System.Collections.Immutable;
using System.Diagnostics;
using CanDoItAll.Infrastructure.ControlPlane;
using Npgsql;

namespace CanDoItAll.Infrastructure.Persistence;

public enum FiniteOperationMaintenanceStop { Empty, Contention, Budget, BatchLimit, Failed, HostCancellation }
public enum FiniteOperationMaintenanceHealthState { Healthy, NoProgress, CapacityPressure }
public enum FiniteOperationMaintenanceHealthTransition { None, Warning, Recovery }

public sealed record FiniteOperationMaintenanceFailure(string Type, string? SqlState) {
    public static FiniteOperationMaintenanceFailure From(Exception exception) {
        var postgres = exception as PostgresException ?? exception.GetBaseException() as PostgresException;
        return new(exception.GetType().Name, postgres?.SqlState);
    }
}

public sealed record FiniteFamilyMaintenanceResult(FiniteReceiptFamilyId Family, int CompletedBatches,
    long DeletedReceipts, long DeletedBytes, FiniteOperationEligibleWork EligibleWork,
    DateTimeOffset? EligibleAtUtc, DateTimeOffset? ObservedAtUtc);

public sealed record FiniteOperationMaintenanceResult(int CompletedBatches, long DeletedReceipts, long DeletedBytes,
    TimeSpan Elapsed, FiniteOperationRetirementStatus? Retirement, FiniteOperationEligibleWork EligibleWork,
    DateTimeOffset? EligibleAtUtc, FiniteOperationMaintenanceStop Stop, FiniteOperationMaintenanceFailure? Failure,
    DateTimeOffset? ObservedAtUtc = null, FiniteOperationRotationResult? Rotation = null,
    FiniteMetadataCleanupResult? Metadata = null, ImmutableArray<FiniteFamilyMaintenanceResult> Families = default);

public sealed record FiniteOperationMaintenanceHealthObservation(FiniteOperationMaintenanceHealthState State,
    FiniteOperationMaintenanceHealthTransition Transition, TimeSpan? SinceProgress);

public sealed record FiniteOperationMaintenanceTurn(FiniteOperationPoolRegistration Pool, TimeSpan ScheduledLateness);

public sealed class FiniteOperationMaintenanceState {
    private readonly ImmutableArray<FiniteOperationPoolRegistration> pools;
    private readonly Dictionary<FiniteOperationPoolId, DateTimeOffset> due;
    private readonly Dictionary<FiniteOperationPoolId, int> familyCursors;
    private int poolCursor;
    private int remainingTurns;
    private (Guid Profile, DatasetActivationBinding Binding)? identity;

    public FiniteOperationMaintenanceState(IEnumerable<FiniteOperationPoolRegistration> registrations) {
        pools = registrations.ToImmutableArray();
        if (pools.IsEmpty || pools.Any(pool => pool is null) || pools.Select(pool => pool.Pool).Distinct().Count() != pools.Length) {
            throw new InvalidOperationException("Finite maintenance scheduling requires a fixed unique pool set.");
        }
        due = pools.ToDictionary(pool => pool.Pool, _ => DateTimeOffset.MinValue);
        familyCursors = pools.ToDictionary(pool => pool.Pool, _ => 0);
    }

    public void ObserveBinding(Guid profile, DatasetActivationBinding binding) {
        remainingTurns = pools.Length;
        if (identity == (profile, binding)) {
            return;
        }
        identity = (profile, binding);
        poolCursor = 0;
        foreach (var pool in pools) {
            due[pool.Pool] = DateTimeOffset.MinValue;
            familyCursors[pool.Pool] = 0;
        }
    }

    public FiniteOperationMaintenanceTurn? TakeDue(DateTimeOffset now) {
        if (remainingTurns == 0) {
            return null;
        }
        for (var offset = 0; offset < pools.Length; offset++) {
            var index = (poolCursor + offset) % pools.Length;
            var pool = pools[index];
            var scheduled = due[pool.Pool];
            if (scheduled > now) {
                continue;
            }
            poolCursor = (index + 1) % pools.Length;
            remainingTurns--;
            due[pool.Pool] = now.Add(pool.Timing.MaintenanceInterval);
            return new(pool, scheduled == DateTimeOffset.MinValue ? TimeSpan.Zero : now - scheduled);
        }
        return null;
    }

    public int TakeFamily(FiniteOperationPoolRegistration pool, IReadOnlyList<bool> finished) {
        if (!familyCursors.TryGetValue(pool.Pool, out var cursor) || finished.Count != pool.Families.Length) {
            throw FiniteOperationEpochStore.Failure(pool, FiniteOperationProtocolFailure.InvalidRegistration);
        }
        for (var offset = 0; offset < finished.Count; offset++) {
            var index = (cursor + offset) % finished.Count;
            if (!finished[index]) {
                familyCursors[pool.Pool] = (index + 1) % finished.Count;
                return index;
            }
        }
        return -1;
    }
}

public sealed class FiniteOperationMaintenanceCycle(FiniteOperationPoolRegistration pool, FiniteOperationMaintenanceState schedule) {
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly FiniteFamilyMaintenanceResult[] families = pool.Families.Select(family =>
        new FiniteFamilyMaintenanceResult(family.Family, 0, 0, 0, FiniteOperationEligibleWork.Unknown, null, null)).ToArray();
    private FiniteOperationRetirementStatus? retirement;
    private FiniteOperationRotationResult? rotation;
    private FiniteMetadataCleanupResult? metadata;
    private FiniteOperationMaintenanceFailure? failure;
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(started);

    public async Task<FiniteOperationMaintenanceResult> RunAsync(FiniteOperationEpochStore epochs,
        FiniteOperationReceiptMaintenance maintenance, DatasetActivationBinding binding,
        CancellationToken budgetToken, CancellationToken stoppingToken = default) {
        try {
            budgetToken.ThrowIfCancellationRequested();
            rotation = await epochs.RotateExpiredAsync(binding, pool, budgetToken);
            retirement = (await epochs.AdvanceRetirementAsync(binding, pool, budgetToken)).Status;
            var finished = new bool[families.Length];
            var attempted = 0;
            while (attempted < FiniteOperationBounds.MaximumBatches) {
                budgetToken.ThrowIfCancellationRequested();
                var index = schedule.TakeFamily(pool, finished);
                if (index < 0) {
                    break;
                }
                attempted++;
                try {
                    var batch = await maintenance.PurgeRetiredBatchAsync(binding, pool, families[index].Family, budgetToken);
                    Add(index, batch);
                    finished[index] = batch.EligibleWork != FiniteOperationEligibleWork.Present || batch.DeletedReceipts == 0;
                } catch (Exception exception) {
                    if (exception is FiniteReceiptPurgeCleanupException cleanup) {
                        Add(index, cleanup.Confirmed);
                    }
                    if (exception is OperationCanceledException) {
                        throw;
                    }
                    failure ??= FiniteOperationMaintenanceFailure.From(exception);
                    finished[index] = true;
                }
            }
            budgetToken.ThrowIfCancellationRequested();
            metadata = await maintenance.CleanupMetadataAsync(binding, pool, budgetToken);
            if (failure is not null) {
                return Complete(FiniteOperationMaintenanceStop.Failed);
            }
            if (families.All(family => family.EligibleWork == FiniteOperationEligibleWork.NoneObserved) &&
                    retirement != FiniteOperationRetirementStatus.Busy && rotation != FiniteOperationRotationResult.Busy &&
                    !metadata.MoreEligibleMetadata) {
                return Complete(FiniteOperationMaintenanceStop.Empty);
            }
            return Complete(attempted == FiniteOperationBounds.MaximumBatches ||
                metadata.MoreEligibleMetadata && metadata.DeletedEpochs + metadata.DeletedStates > 0
                ? FiniteOperationMaintenanceStop.BatchLimit : FiniteOperationMaintenanceStop.Contention);
        } catch (Exception exception) {
            return Failed(exception, budgetToken, stoppingToken);
        }
    }

    public FiniteOperationMaintenanceResult Failed(Exception exception, CancellationToken budgetToken, CancellationToken stoppingToken) {
        var stop = exception is OperationCanceledException && stoppingToken.IsCancellationRequested
            ? FiniteOperationMaintenanceStop.HostCancellation
            : exception is OperationCanceledException && budgetToken.IsCancellationRequested
                ? FiniteOperationMaintenanceStop.Budget : FiniteOperationMaintenanceStop.Failed;
        if (stop == FiniteOperationMaintenanceStop.Failed) {
            failure ??= FiniteOperationMaintenanceFailure.From(exception);
        }
        return Complete(stop);
    }

    private void Add(int index, FiniteReceiptPurgeObservation batch) {
        var family = families[index];
        families[index] = family with {
            CompletedBatches = family.CompletedBatches + 1,
            DeletedReceipts = checked(family.DeletedReceipts + batch.DeletedReceipts),
            DeletedBytes = checked(family.DeletedBytes + batch.DeletedBytes),
            EligibleWork = batch.EligibleWork, EligibleAtUtc = batch.EligibleAtUtc, ObservedAtUtc = batch.ObservedAtUtc
        };
    }

    private FiniteOperationMaintenanceResult Complete(FiniteOperationMaintenanceStop stop) {
        var eligible = families.Any(family => family.EligibleWork == FiniteOperationEligibleWork.Unknown)
            ? FiniteOperationEligibleWork.Unknown
            : families.Any(family => family.EligibleWork == FiniteOperationEligibleWork.Present)
                ? FiniteOperationEligibleWork.Present : FiniteOperationEligibleWork.NoneObserved;
        var observed = families.FirstOrDefault(family => family.EligibleWork == FiniteOperationEligibleWork.Present);
        return new(families.Sum(family => family.CompletedBatches), families.Sum(family => family.DeletedReceipts),
            families.Sum(family => family.DeletedBytes), Elapsed, retirement, eligible, observed?.EligibleAtUtc, stop,
            failure, observed?.ObservedAtUtc, rotation, metadata, families.ToImmutableArray());
    }
}

public sealed class FiniteOperationMaintenanceHealth(TimeSpan warningAfter) {
    private (Guid Profile, DatasetActivationBinding Binding)? identity;
    private DateTimeOffset? lastProgress;
    private DateTimeOffset? noProgressSince;
    private DateTimeOffset? pressureSince;
    private FiniteOperationMaintenanceHealthState state;

    public FiniteOperationMaintenanceHealthObservation Observe(Guid profile, DatasetActivationBinding binding,
        FiniteOperationMaintenanceResult result, DateTimeOffset now) {
        if (identity != (profile, binding)) {
            identity = (profile, binding);
            lastProgress = null;
            noProgressSince = null;
            pressureSince = null;
            state = FiniteOperationMaintenanceHealthState.Healthy;
        }
        if (result.DeletedReceipts > 0) {
            lastProgress = now;
        }
        var previous = state;
        if (result.EligibleWork == FiniteOperationEligibleWork.NoneObserved && result.Stop == FiniteOperationMaintenanceStop.Empty &&
                result.Retirement != FiniteOperationRetirementStatus.Busy && result.Rotation != FiniteOperationRotationResult.Busy) {
            noProgressSince = null;
            pressureSince = null;
            state = FiniteOperationMaintenanceHealthState.Healthy;
        } else if (result.EligibleWork == FiniteOperationEligibleWork.Present) {
            noProgressSince = result.DeletedReceipts > 0 ? null : noProgressSince ?? now;
            var capped = result.Stop is FiniteOperationMaintenanceStop.BatchLimit or FiniteOperationMaintenanceStop.Budget;
            pressureSince = capped ? pressureSince ?? now : null;
            if (noProgressSince is { } stalled && now - stalled >= warningAfter) {
                state = FiniteOperationMaintenanceHealthState.NoProgress;
            } else if (pressureSince is { } pressured && now - pressured >= warningAfter) {
                state = FiniteOperationMaintenanceHealthState.CapacityPressure;
            }
        }
        var transition = state == previous ? FiniteOperationMaintenanceHealthTransition.None
            : state == FiniteOperationMaintenanceHealthState.Healthy ? FiniteOperationMaintenanceHealthTransition.Recovery
                : FiniteOperationMaintenanceHealthTransition.Warning;
        return new(state, transition, lastProgress is { } progressed ? now - progressed : null);
    }
}
