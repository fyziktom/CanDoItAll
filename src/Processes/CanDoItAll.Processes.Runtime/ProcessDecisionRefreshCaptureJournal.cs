using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public readonly record struct ProcessDecisionCaptureInvocationId(Guid Value) {
    public void Validate() => ArgumentOutOfRangeException.ThrowIfEqual(Value, Guid.Empty);
}

public sealed record ProcessDecisionRefreshCaptureReservation(ProcessRunId RunId, RuntimeCommandId AdmissionId,
    RuntimeCommandId ReservationId, ProcessDecisionBudgetReservation Budget, ProcessDecisionMaterialScope Scope,
    ProcessDecisionContentContract ContentContract, ProcessDecisionMaterialReference Material, string InputFingerprint) {
    public static TimeSpan MaximumWork { get; } = TimeSpan.FromSeconds(10);
    public ProcessRunId RootRunId => Scope.RootRunId;
    public bool IsRoot => Budget.Expectation.StepInstanceId is null;
    public bool InheritsMaterial => !IsRoot && Scope.StepInstanceId is null;

    public void Validate() {
        ArgumentNullException.ThrowIfNull(Budget);
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(ContentContract);
        ArgumentNullException.ThrowIfNull(Material);
        Budget.Validate();
        Scope.Validate();
        ContentContract.Validate();
        Material.Validate();
        if (RunId.Value == Guid.Empty || AdmissionId.Value == Guid.Empty || ReservationId.Value == Guid.Empty ||
                AdmissionId == ReservationId || Budget.Grant is not { WorkTicks: > 0, Replans: 0 } ||
                Budget.Grant.WorkTicks > MaximumWork.Ticks || Material.ScopeId != Scope.Id || Material.Generation < 2 ||
                !ProcessStrategyReceiptValuePolicy.IsSha256Digest(InputFingerprint) ||
                IsRoot && (RunId != RootRunId || Scope.StepInstanceId is not null) ||
                Scope.StepInstanceId is null && Scope.OwnerRunId != RootRunId ||
                Scope.StepInstanceId is not null && (Scope.OwnerRunId != RunId || Scope.StepInstanceId != Budget.Expectation.StepInstanceId)) {
            throw new InvalidDataException("Refresh capture requires its original manual reservation, source, bounded grant and material owner.");
        }
    }
}

public enum ProcessDecisionRefreshCapturePhase { Reserved = 1, Invoking, Returned, Settled }
public enum ProcessDecisionRefreshCaptureOutcomeKind { Captured = 1, Refused, Expired, DeadlineExceeded, Cancelled, Faulted, NeverInvoked }
public enum ProcessDecisionRefreshCaptureDisposition { Advanced = 1, Aborted }

public sealed record ProcessDecisionRefreshCaptureOutcome(ProcessDecisionRefreshCaptureOutcomeKind Kind,
    DecisionBudgetSettlement Work, ProcessDecisionRunScopeMaterial? Material = null) {
    public void Validate(ProcessDecisionRefreshCaptureReservation reservation) {
        ArgumentNullException.ThrowIfNull(Work);
        Work.Validate();
        Material?.Validate();
        if (!Enum.IsDefined(Kind) || Work.OwnerWorkRevision != 1 || Work.Consumed.Replans != 0 ||
                (Kind == ProcessDecisionRefreshCaptureOutcomeKind.Captured) != (Material is not null) ||
                Material is not null && (Material.Stamp.Reference != reservation.Material || Work.Consumed.WorkTicks > reservation.Budget.Grant.WorkTicks) ||
                Kind == ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked && Work.Consumed.WorkTicks != 0) {
            throw new InvalidDataException("Refresh capture must retain its exact closed work and original outcome material.");
        }
    }
}

public sealed record ProcessDecisionRefreshCaptureReturned(ProcessDecisionRefreshCaptureOutcome Outcome, ProcessDecisionMaterial? Payload = null) {
    public void Validate(ProcessDecisionRefreshCaptureReservation reservation) {
        ArgumentNullException.ThrowIfNull(Outcome);
        Outcome.Validate(reservation);
        if ((Payload is not null) != (Outcome.Kind == ProcessDecisionRefreshCaptureOutcomeKind.Captured && !reservation.InheritsMaterial) ||
                Payload is not null && (Payload.Scope != reservation.Scope || Payload.Reference != reservation.Material ||
                    Payload.ContentContract != reservation.ContentContract || ProcessDecisionRunScopeMaterial.From(Payload) != Outcome.Material)) {
            throw new InvalidDataException("Returned capture payload must match its original content owner and immutable material stamp.");
        }
    }
}

public sealed record ProcessDecisionRefreshCaptureState(ProcessDecisionRefreshCaptureReservation Reservation, long Revision,
    ProcessDecisionRefreshCapturePhase Phase, ProcessDecisionCaptureInvocationId? Invocation = null,
    ProcessDecisionRefreshCaptureOutcome? Outcome = null, ProcessDecisionRefreshCaptureDisposition? Disposition = null) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Reservation);
        Reservation.Validate();
        Invocation?.Validate();
        Outcome?.Validate(Reservation);
        var expectedRevision = Phase switch {
            ProcessDecisionRefreshCapturePhase.Reserved => 1,
            ProcessDecisionRefreshCapturePhase.Invoking => 2,
            ProcessDecisionRefreshCapturePhase.Returned => Invocation is null ? 2 : 3,
            ProcessDecisionRefreshCapturePhase.Settled => Invocation is null ? 3 : 4,
            _ => 0
        };
        if (expectedRevision == 0 || Revision != expectedRevision ||
                Phase == ProcessDecisionRefreshCapturePhase.Reserved && Invocation is not null ||
                Phase == ProcessDecisionRefreshCapturePhase.Invoking && Invocation is null ||
                (Phase is ProcessDecisionRefreshCapturePhase.Returned or ProcessDecisionRefreshCapturePhase.Settled) != (Outcome is not null) ||
                Outcome is not null && (Outcome.Kind == ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked) != (Invocation is null) ||
                (Phase == ProcessDecisionRefreshCapturePhase.Settled) != (Disposition is not null) ||
                Disposition is { } disposition && (!Enum.IsDefined(disposition) ||
                    disposition == ProcessDecisionRefreshCaptureDisposition.Advanced && Outcome?.Kind != ProcessDecisionRefreshCaptureOutcomeKind.Captured)) {
            throw new InvalidDataException("Refresh capture journal has an inconsistent original invocation, outcome or phase.");
        }
    }
}

public enum ProcessDecisionRefreshCaptureRefusal { InvalidPhase, OriginalReservationChanged, OriginalInvocationChanged, OriginalOutcomeChanged, OriginalSettlementChanged }

public sealed class ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal reason)
    : InvalidOperationException($"Refresh capture refused the transition: {reason}.") {
    public ProcessDecisionRefreshCaptureRefusal Reason { get; } = reason;
}

public sealed record ProcessDecisionRefreshCaptureChange(bool Changed, ProcessDecisionRefreshCaptureState State);

public static class ProcessDecisionRefreshCaptureJournal {
    public static ProcessDecisionRefreshCaptureChange Reserve(ProcessDecisionRefreshCaptureReservation reservation,
        ProcessDecisionRefreshCaptureState? existing = null) {
        ArgumentNullException.ThrowIfNull(reservation);
        reservation.Validate();
        if (existing is not null) {
            existing.Validate();
            if (existing.Reservation != reservation) {
                throw new ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal.OriginalReservationChanged);
            }
            return new(false, existing);
        }
        return new(true, new(reservation, 1, ProcessDecisionRefreshCapturePhase.Reserved));
    }

    public static ProcessDecisionRefreshCaptureChange Begin(ProcessDecisionRefreshCaptureState current, ProcessDecisionCaptureInvocationId invocation) {
        current.Validate();
        invocation.Validate();
        if (current.Invocation is { } original) {
            if (original != invocation) {
                throw new ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal.OriginalInvocationChanged);
            }
            return new(false, current);
        }
        RequirePhase(current, ProcessDecisionRefreshCapturePhase.Reserved);
        return Change(current with { Revision = 2, Phase = ProcessDecisionRefreshCapturePhase.Invoking, Invocation = invocation });
    }

    public static ProcessDecisionRefreshCaptureChange Return(ProcessDecisionRefreshCaptureState current, ProcessDecisionCaptureInvocationId invocation,
        ProcessDecisionRefreshCaptureReturned returned) {
        current.Validate();
        invocation.Validate();
        returned.Validate(current.Reservation);
        if (current.Invocation != invocation) {
            throw new ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal.OriginalInvocationChanged);
        }
        if (current.Outcome is { } original) {
            if (original != returned.Outcome) {
                throw new ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal.OriginalOutcomeChanged);
            }
            return new(false, current);
        }
        RequirePhase(current, ProcessDecisionRefreshCapturePhase.Invoking);
        return Change(current with { Revision = 3, Phase = ProcessDecisionRefreshCapturePhase.Returned, Outcome = returned.Outcome });
    }

    public static ProcessDecisionRefreshCaptureChange CloseNeverInvoked(ProcessDecisionRefreshCaptureState current) {
        current.Validate();
        if (current.Outcome?.Kind == ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked) {
            return new(false, current);
        }
        RequirePhase(current, ProcessDecisionRefreshCapturePhase.Reserved);
        return Change(current with { Revision = 2, Phase = ProcessDecisionRefreshCapturePhase.Returned,
            Outcome = new(ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked, new(1, new(0, 0))) });
    }

    public static ProcessDecisionRefreshCaptureChange Settle(ProcessDecisionRefreshCaptureState current, DecisionBudgetChange settlement,
        ProcessDecisionRefreshCaptureDisposition disposition) {
        current.Validate();
        ArgumentNullException.ThrowIfNull(settlement);
        settlement.Balance.Validate();
        settlement.Allocation.Validate();
        var original = current.Reservation.Budget;
        if (!Enum.IsDefined(disposition) || current.Outcome is null || settlement.Balance.BudgetId != original.Expectation.BudgetId ||
                settlement.Allocation.BudgetId != original.Expectation.BudgetId || settlement.Allocation.Id != original.AllocationId ||
                settlement.Allocation.Grant != original.Grant || settlement.Allocation.Settlement != current.Outcome.Work ||
                disposition == ProcessDecisionRefreshCaptureDisposition.Advanced &&
                    (current.Outcome.Kind != ProcessDecisionRefreshCaptureOutcomeKind.Captured || settlement.Balance.OverrunDetected) ||
                current.Disposition is not null && current.Disposition != disposition) {
            throw new ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal.OriginalSettlementChanged);
        }
        if (current.Phase == ProcessDecisionRefreshCapturePhase.Settled) {
            return new(false, current);
        }
        RequirePhase(current, ProcessDecisionRefreshCapturePhase.Returned);
        if (!settlement.Changed) {
            throw new ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal.OriginalSettlementChanged);
        }
        return Change(current with { Revision = checked(current.Revision + 1), Phase = ProcessDecisionRefreshCapturePhase.Settled, Disposition = disposition });
    }

    private static ProcessDecisionRefreshCaptureChange Change(ProcessDecisionRefreshCaptureState next) {
        next.Validate();
        return new(true, next);
    }

    private static void RequirePhase(ProcessDecisionRefreshCaptureState current, ProcessDecisionRefreshCapturePhase phase) {
        if (current.Phase != phase) {
            throw new ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal.InvalidPhase);
        }
    }
}
