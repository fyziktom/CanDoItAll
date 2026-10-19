using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public readonly record struct ProcessDecisionId(Guid Value);
public readonly record struct ProcessDecisionMaterialId(Guid Value);
public readonly record struct ProcessDecisionChildId(Guid Value);
public readonly record struct ProcessDecisionEffectReceiptId(Guid Value);

public sealed record ProcessDecisionReference(ProcessDecisionId DecisionId, int Generation) {
    public void Validate() {
        if (DecisionId.Value == Guid.Empty || Generation is < 1 or > ProcessDecisionGeneration.MaximumGenerations) {
            throw new InvalidDataException("A Process decision requires its original identity and bounded positive generation.");
        }
    }
}

public sealed record ProcessDecisionExpectation(ProcessDecisionReference Reference, long Revision) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Reference);
        Reference.Validate();
        if (Revision < 1) {
            throw new InvalidDataException("A Process decision expectation requires a positive revision.");
        }
    }
}

public enum ProcessDecisionPhase { Active, WaitingForReview, Reconciling, Completed, Cancelled, Superseded }
public enum ProcessDecisionChildOwner { Agent, Workflow }
public enum ProcessDecisionChildOutcome { Accepted, NoEffect }
public enum ProcessDecisionPublicationKind { Dispatch, ManagerReview }

public sealed record ProcessDecisionChildClosure(long OwnerRevision, ProcessDecisionChildOutcome Outcome,
    ProcessDecisionEffectReceiptId? AcceptedReceipt) {
    public void Validate() {
        if (OwnerRevision < 1 || !Enum.IsDefined(Outcome) ||
                (Outcome == ProcessDecisionChildOutcome.Accepted) != AcceptedReceipt.HasValue ||
                AcceptedReceipt is { Value: var value } && value == Guid.Empty) {
            throw new InvalidDataException("Child closure requires exact owner evidence and an accepted receipt only for an effect.");
        }
    }
}

public sealed record ProcessDecisionChild(ProcessDecisionReference Reference, ProcessDecisionChildId ChildId,
    ProcessDecisionChildOwner Owner, DecisionBudgetAllocationId AllocationId, ProcessDecisionChildClosure? Closure = null) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Reference);
        Reference.Validate();
        if (ChildId.Value == Guid.Empty || AllocationId.Value == Guid.Empty || !Enum.IsDefined(Owner)) {
            throw new InvalidDataException("A Process decision child requires its original occurrence, owner and allocation.");
        }
        Closure?.Validate();
    }
}

public sealed record ProcessDecisionGeneration(ProcessDecisionReference Reference, long Revision, ProcessDecisionPhase Phase,
    ProcessDecisionMaterialId MaterialId, DecisionBudgetId BudgetId, int ChildCount, int UnclosedChildCount) {
    public const int MaximumGenerations = 32;
    public const int MaximumChildren = 64;

    public ProcessDecisionExpectation Expectation => new(Reference, Revision);

    public void Validate() {
        Expectation.Validate();
        if (!Enum.IsDefined(Phase) || MaterialId.Value == Guid.Empty || BudgetId.Value == Guid.Empty ||
                ChildCount is < 0 or > MaximumChildren || UnclosedChildCount < 0 || UnclosedChildCount > ChildCount ||
                Revision < 2 * Reference.Generation - 1 || Revision < 1 + 2 * ChildCount - UnclosedChildCount ||
                Phase is ProcessDecisionPhase.Completed or ProcessDecisionPhase.Cancelled or ProcessDecisionPhase.Superseded && UnclosedChildCount != 0 ||
                Phase == ProcessDecisionPhase.Superseded && Reference.Generation == MaximumGenerations) {
            throw new InvalidDataException("The retained Process decision generation has an invalid identity, phase or child balance.");
        }
    }
}

public enum ProcessDecisionRefusal { StaleExpectation, InvalidPhase, ChildCapacity, GenerationCapacity, UnclosedChildren,
    OriginalChildChanged, OriginalClosureChanged, ChildNotOpen }

public sealed class ProcessDecisionException(ProcessDecisionRefusal reason)
    : InvalidOperationException($"The Process decision refused the transition: {reason}.") {
    public ProcessDecisionRefusal Reason { get; } = reason;
}

public sealed record ProcessDecisionChildChange(bool Changed, ProcessDecisionGeneration Generation, ProcessDecisionChild Child);
public sealed record ProcessDecisionAdvance(ProcessDecisionGeneration Previous, ProcessDecisionGeneration Current);
