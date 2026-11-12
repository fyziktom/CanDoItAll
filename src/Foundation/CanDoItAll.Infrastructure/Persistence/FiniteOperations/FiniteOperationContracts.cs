using System.Collections.Immutable;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed record FiniteOperationPoolId {
    public FiniteOperationPoolId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public sealed record FiniteReceiptFamilyId {
    public FiniteReceiptFamilyId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public static class FiniteOperationBounds {
    public static readonly TimeSpan MaintenanceBudget = TimeSpan.FromSeconds(2);
    public const int BatchRows = 128;
    public const int BatchBytes = 16 * 1024 * 1024;
    public const int MaximumBatches = 64;
}

public sealed record FiniteOperationTiming {
    public FiniteOperationTiming(TimeSpan admissionWindow, TimeSpan replayRetention, TimeSpan mutationDeadline,
        TimeSpan statementDeadline, TimeSpan lockDeadline, TimeSpan maintenanceInterval, TimeSpan warningAfter) {
        if (admissionWindow < TimeSpan.FromSeconds(1) || admissionWindow > TimeSpan.FromDays(30) ||
                replayRetention < TimeSpan.FromSeconds(1) || replayRetention > TimeSpan.FromDays(365) ||
                mutationDeadline < TimeSpan.FromSeconds(1) || mutationDeadline > TimeSpan.FromMinutes(1) ||
                statementDeadline < TimeSpan.FromMilliseconds(1) || statementDeadline > mutationDeadline ||
                lockDeadline < TimeSpan.FromMilliseconds(1) || lockDeadline > statementDeadline ||
                maintenanceInterval < TimeSpan.FromSeconds(5) || maintenanceInterval > TimeSpan.FromMinutes(1) ||
                warningAfter < maintenanceInterval * 2 || warningAfter > TimeSpan.FromHours(1)) {
            throw new ArgumentOutOfRangeException(nameof(admissionWindow), "Finite operation timing exceeds the supported bounds.");
        }
        AdmissionWindow = admissionWindow;
        ReplayRetention = replayRetention;
        MutationDeadline = mutationDeadline;
        StatementDeadline = statementDeadline;
        LockDeadline = lockDeadline;
        MaintenanceInterval = maintenanceInterval;
        WarningAfter = warningAfter;
    }

    public TimeSpan AdmissionWindow { get; }
    public TimeSpan ReplayRetention { get; }
    public TimeSpan MutationDeadline { get; }
    public TimeSpan StatementDeadline { get; }
    public TimeSpan LockDeadline { get; }
    public TimeSpan MaintenanceInterval { get; }
    public TimeSpan WarningAfter { get; }
}

public sealed record FiniteReceiptFamilyRegistration {
    public FiniteReceiptFamilyRegistration(FiniteReceiptFamilyId family, int maximumPayloadBytes) {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumPayloadBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumPayloadBytes, FiniteOperationBounds.BatchBytes);
        Family = family;
        MaximumPayloadBytes = maximumPayloadBytes;
    }

    public FiniteReceiptFamilyId Family { get; }
    public int MaximumPayloadBytes { get; }
}

public sealed record FiniteOperationPoolRegistration {
    public FiniteOperationPoolRegistration(FiniteOperationPoolId pool, int protocolVersion, FiniteOperationTiming timing,
        IReadOnlyList<FiniteReceiptFamilyRegistration> families) {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(timing);
        ArgumentNullException.ThrowIfNull(families);
        ArgumentOutOfRangeException.ThrowIfLessThan(protocolVersion, 1);
        var captured = families.ToImmutableArray();
        if (captured.IsEmpty || captured.Any(family => family is null) ||
                captured.Select(family => family.Family).Distinct().Count() != captured.Length) {
            throw new FiniteOperationProtocolException(FiniteOperationProtocolFailure.InvalidRegistration, pool, protocolVersion);
        }
        Pool = pool;
        ProtocolVersion = protocolVersion;
        Timing = timing;
        Families = captured;
    }

    public FiniteOperationPoolId Pool { get; }
    public int ProtocolVersion { get; }
    public FiniteOperationTiming Timing { get; }
    public ImmutableArray<FiniteReceiptFamilyRegistration> Families { get; }

    public FiniteReceiptFamilyRegistration RequireFamily(FiniteReceiptFamilyId family) {
        ArgumentNullException.ThrowIfNull(family);
        return Families.SingleOrDefault(candidate => candidate.Family == family)
            ?? throw new FiniteOperationProtocolException(FiniteOperationProtocolFailure.InvalidRegistration, Pool, ProtocolVersion);
    }
}

public enum FiniteOperationEpochState { Open = 1, Closed = 2 }
public enum FiniteOperationAdmissionFailure { UnknownFutureEpoch, RetiredEpoch, ClosedEpoch, AdmissionDeadlineElapsed }
public enum FiniteOperationProtocolFailure {
    MissingState, MissingRetainedEpoch, UnsupportedProtocol, InconsistentMetadata, InvalidRegistration, InvalidParticipantResponse
}
public enum FiniteOperationRotationResult { Unchanged, Rotated, Busy }
public enum FiniteOperationRetirementStatus { Unchanged, Advanced, Busy }
public enum FiniteOperationEligibleWork { Unknown, NoneObserved, Present }

public sealed class FiniteOperationAdmissionException(FiniteOperationAdmissionFailure failure)
    : InvalidOperationException("The original finite operation epoch is no longer admitted.") {
    public FiniteOperationAdmissionFailure Failure { get; } = failure;
}

public sealed class FiniteOperationProtocolException(FiniteOperationProtocolFailure failure, FiniteOperationPoolId pool, int protocolVersion)
    : InvalidOperationException("Finite operation metadata or its installed receipt participants are inconsistent.") {
    public FiniteOperationProtocolFailure Failure { get; } = failure;
    public FiniteOperationPoolId Pool { get; } = pool;
    public int ProtocolVersion { get; } = protocolVersion;
}

public sealed record FiniteOperationEpochAddress {
    public FiniteOperationEpochAddress(DatasetNamespaceId @namespace, FiniteOperationPoolId pool, long epoch) {
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentOutOfRangeException.ThrowIfLessThan(epoch, 1);
        Namespace = @namespace;
        Pool = pool;
        Epoch = epoch;
    }

    public DatasetNamespaceId Namespace { get; }
    public FiniteOperationPoolId Pool { get; }
    public long Epoch { get; }
}

public sealed record FiniteOperationEpochSnapshot {
    public FiniteOperationEpochSnapshot(DatasetActivationBinding binding, FiniteOperationPoolId pool, int protocolVersion,
        long epoch, FiniteOperationEpochState state, DateTimeOffset openedAtUtc, DateTimeOffset admissionClosesAtUtc,
        DateTimeOffset? closedAtUtc, int mutationDeadlineMilliseconds, long replayRetentionSeconds, DateTimeOffset observedAtUtc) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentOutOfRangeException.ThrowIfLessThan(protocolVersion, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(epoch, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(mutationDeadlineMilliseconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(replayRetentionSeconds, 1);
        if (openedAtUtc == default || openedAtUtc.Offset != TimeSpan.Zero || admissionClosesAtUtc.Offset != TimeSpan.Zero ||
                observedAtUtc == default || observedAtUtc.Offset != TimeSpan.Zero || admissionClosesAtUtc <= openedAtUtc ||
                state == FiniteOperationEpochState.Open && closedAtUtc is not null ||
                state == FiniteOperationEpochState.Closed && (closedAtUtc is null || closedAtUtc.Value.Offset != TimeSpan.Zero || closedAtUtc < openedAtUtc) ||
                state is not (FiniteOperationEpochState.Open or FiniteOperationEpochState.Closed)) {
            throw new FiniteOperationProtocolException(FiniteOperationProtocolFailure.InconsistentMetadata, pool, protocolVersion);
        }
        Binding = binding;
        Pool = pool;
        ProtocolVersion = protocolVersion;
        Epoch = epoch;
        State = state;
        OpenedAtUtc = openedAtUtc;
        AdmissionClosesAtUtc = admissionClosesAtUtc;
        ClosedAtUtc = closedAtUtc;
        MutationDeadlineMilliseconds = mutationDeadlineMilliseconds;
        ReplayRetentionSeconds = replayRetentionSeconds;
        ObservedAtUtc = observedAtUtc;
    }

    public DatasetActivationBinding Binding { get; }
    public FiniteOperationPoolId Pool { get; }
    public int ProtocolVersion { get; }
    public long Epoch { get; }
    public FiniteOperationEpochState State { get; }
    public DateTimeOffset OpenedAtUtc { get; }
    public DateTimeOffset AdmissionClosesAtUtc { get; }
    public DateTimeOffset? ClosedAtUtc { get; }
    public int MutationDeadlineMilliseconds { get; }
    public long ReplayRetentionSeconds { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public DateTimeOffset ReplayUntilUtc => AdmissionClosesAtUtc.AddMilliseconds(MutationDeadlineMilliseconds).AddSeconds(ReplayRetentionSeconds);
}

public sealed record FiniteOperationRetirementResult(FiniteOperationRetirementStatus Status, long? RetiredThroughEpoch);

public sealed record FiniteReceiptPurgeRequest {
    public FiniteReceiptPurgeRequest(DatasetActivationBinding binding, FiniteOperationPoolRegistration pool,
        FiniteReceiptFamilyId family, int maximumRows = FiniteOperationBounds.BatchRows, int maximumBytes = FiniteOperationBounds.BatchBytes) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(pool);
        var registration = pool.RequireFamily(family);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumRows, FiniteOperationBounds.BatchRows);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, registration.MaximumPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumBytes, FiniteOperationBounds.BatchBytes);
        Binding = binding;
        Pool = pool;
        Family = family;
        MaximumRows = maximumRows;
        MaximumBytes = maximumBytes;
    }

    public DatasetActivationBinding Binding { get; }
    public FiniteOperationPoolRegistration Pool { get; }
    public FiniteReceiptFamilyId Family { get; }
    public int MaximumRows { get; }
    public int MaximumBytes { get; }
}

public sealed record FiniteReceiptPurgeObservation(int DeletedReceipts, long DeletedBytes,
    FiniteOperationEligibleWork EligibleWork, DateTimeOffset ObservedAtUtc, DateTimeOffset? EligibleAtUtc);

public sealed class FiniteReceiptPurgeCleanupException(FiniteReceiptPurgeObservation confirmed, Exception innerException)
    : Exception("A committed finite receipt maintenance batch needs cleanup review.", innerException) {
    public FiniteReceiptPurgeObservation Confirmed { get; } = confirmed;
}

public sealed record FiniteEpochInspectionRequest {
    public FiniteEpochInspectionRequest(DatasetActivationBinding binding, FiniteOperationPoolRegistration pool,
        FiniteReceiptFamilyId family, IReadOnlyList<FiniteOperationEpochAddress> candidates) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(candidates);
        _ = pool.RequireFamily(family);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(candidates.Count, FiniteOperationBounds.BatchRows);
        var captured = candidates.ToImmutableArray();
        if (captured.Any(candidate => candidate is null || candidate.Pool != pool.Pool) ||
                captured.Distinct().Count() != captured.Length) {
            throw new FiniteOperationProtocolException(FiniteOperationProtocolFailure.InvalidParticipantResponse, pool.Pool, pool.ProtocolVersion);
        }
        Binding = binding;
        Pool = pool;
        Family = family;
        Candidates = captured;
    }

    public DatasetActivationBinding Binding { get; }
    public FiniteOperationPoolRegistration Pool { get; }
    public FiniteReceiptFamilyId Family { get; }
    public ImmutableArray<FiniteOperationEpochAddress> Candidates { get; }
}

public interface IFiniteOperationReceiptRetentionParticipant {
    FiniteOperationPoolId Pool { get; }
    FiniteReceiptFamilyId Family { get; }
    Task<FiniteReceiptPurgeObservation> PurgeRetiredBatchEnlistedAsync(FiniteReceiptPurgeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<FiniteOperationEpochAddress>> FindNonemptyEpochsEnlistedAsync(FiniteEpochInspectionRequest request, CancellationToken cancellationToken);
}
