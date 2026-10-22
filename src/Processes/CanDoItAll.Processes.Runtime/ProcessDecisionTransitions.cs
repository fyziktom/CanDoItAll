using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessDecisionTransitions {
    public static ProcessDecisionGeneration Create(ProcessDecisionId decisionId, ProcessDecisionMaterialId materialId, DecisionBudgetId budgetId) {
        var generation = new ProcessDecisionGeneration(new(decisionId, 1), 1, ProcessDecisionPhase.Active, materialId, budgetId, 0, 0);
        generation.Validate();
        return generation;
    }

    public static ProcessDecisionChildChange Enroll(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation,
        ProcessDecisionChild requested, ProcessDecisionChild? existing = null) {
        RequireExpected(current, expectation);
        RequireChild(current, requested);
        if (requested.Closure is not null) {
            throw new InvalidDataException("Enrollment cannot assert that a new child is already closed.");
        }
        if (existing is not null) {
            RequireChild(current, existing);
            if (requested != (existing with { Closure = null })) {
                throw new ProcessDecisionException(ProcessDecisionRefusal.OriginalChildChanged);
            }
            RequireRetainedChild(current, existing);
            return new(false, current, existing);
        }
        RequirePhase(current, ProcessDecisionPhase.Active);
        if (current.ChildCount == ProcessDecisionGeneration.MaximumChildren) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.ChildCapacity);
        }
        return new(true, current with { Revision = checked(current.Revision + 1),
            ChildCount = current.ChildCount + 1, UnclosedChildCount = current.UnclosedChildCount + 1 }, requested);
    }

    public static ProcessDecisionChildChange CloseChild(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation,
        ProcessDecisionChild child, ProcessDecisionChildClosure closure, ProcessDecisionCompletion? completion = null) {
        RequireExpected(current, expectation);
        RequireChild(current, child);
        RequireRetainedChild(current, child);
        ArgumentNullException.ThrowIfNull(closure);
        closure.Validate();
        var finalPhase = completion is { } target ? CompletionPhase(target) : current.Phase;
        if (child.Closure is not null && child.Closure != closure) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.OriginalClosureChanged);
        }
        if (child.Closure is not null && current.Phase == finalPhase) {
            return new(false, current, child);
        }
        if (current.Phase is not (ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview or ProcessDecisionPhase.Reconciling) ||
                current.Phase == ProcessDecisionPhase.Reconciling && finalPhase == ProcessDecisionPhase.WaitingForReview) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.InvalidPhase);
        }
        var remaining = current.UnclosedChildCount - (child.Closure is null ? 1 : 0);
        if (completion is ProcessDecisionCompletion.Complete or ProcessDecisionCompletion.Cancel && remaining != 0) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.UnclosedChildren);
        }
        return new(true, current with { Revision = checked(current.Revision + 1),
            UnclosedChildCount = remaining, Phase = finalPhase },
            child with { Closure = closure });
    }

    public static ProcessDecisionPhase CompletionPhase(ProcessDecisionCompletion completion) => completion switch {
        ProcessDecisionCompletion.Complete => ProcessDecisionPhase.Completed,
        ProcessDecisionCompletion.Cancel => ProcessDecisionPhase.Cancelled,
        ProcessDecisionCompletion.Review => ProcessDecisionPhase.WaitingForReview,
        ProcessDecisionCompletion.Reconcile => ProcessDecisionPhase.Reconciling,
        _ => throw new InvalidDataException("The Process child completion is unsupported.")
    };

    public static ProcessDecisionGeneration WaitForReview(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation) {
        RequireExpected(current, expectation);
        RequirePhase(current, ProcessDecisionPhase.Active);
        return ChangePhase(current, ProcessDecisionPhase.WaitingForReview);
    }

    public static ProcessDecisionGeneration BeginReconciliation(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation) {
        RequireExpected(current, expectation);
        if (current.Phase is not (ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview)) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.InvalidPhase);
        }
        return ChangePhase(current, ProcessDecisionPhase.Reconciling);
    }

    public static ProcessDecisionGeneration Complete(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation) {
        RequireExpected(current, expectation);
        if (current.Phase is not (ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview)) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.InvalidPhase);
        }
        RequireAllClosed(current);
        return ChangePhase(current, ProcessDecisionPhase.Completed);
    }

    public static ProcessDecisionGeneration Cancel(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation) {
        RequireExpected(current, expectation);
        RequirePhase(current, ProcessDecisionPhase.Reconciling);
        RequireAllClosed(current);
        return ChangePhase(current, ProcessDecisionPhase.Cancelled);
    }

    public static ProcessDecisionGeneration FinalizeTerminal(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation,
        ProcessRuntimeStepStatus stepStatus) {
        RequireExpected(current, expectation);
        var phase = TerminalPhase(stepStatus);
        if (current.Phase is not (ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview or ProcessDecisionPhase.Reconciling)) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.InvalidPhase);
        }
        RequireAllClosed(current);
        return ChangePhase(current, phase);
    }

    public static ProcessDecisionPhase TerminalPhase(ProcessRuntimeStepStatus stepStatus) => stepStatus switch {
        ProcessRuntimeStepStatus.Cancelled => ProcessDecisionPhase.Cancelled,
        ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Skipped => ProcessDecisionPhase.Completed,
        _ => throw new InvalidDataException("Decision finalization requires a terminal business step.")
    };

    public static ProcessDecisionAdvance Advance(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation,
        ProcessDecisionMaterialId materialId) {
        RequireExpected(current, expectation);
        RequirePhase(current, ProcessDecisionPhase.Reconciling);
        RequireAllClosed(current);
        if (current.Reference.Generation == ProcessDecisionGeneration.MaximumGenerations) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.GenerationCapacity);
        }
        if (materialId.Value == Guid.Empty) {
            throw new InvalidDataException("A successor requires its owner-issued material identity.");
        }
        var previous = ChangePhase(current, ProcessDecisionPhase.Superseded);
        var successor = new ProcessDecisionGeneration(current.Reference with { Generation = current.Reference.Generation + 1 },
            previous.Revision, ProcessDecisionPhase.Active, materialId, current.BudgetId, 0, 0);
        return new(previous, successor);
    }

    public static void RequirePublication(ProcessDecisionGeneration current, ProcessDecisionReference reference,
        ProcessDecisionChild child, ProcessDecisionPublicationKind kind) {
        ArgumentNullException.ThrowIfNull(current);
        current.Validate();
        ArgumentNullException.ThrowIfNull(reference);
        reference.Validate();
        if (current.Reference != reference) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.StaleExpectation);
        }
        RequireChild(current, child);
        RequireRetainedChild(current, child);
        if (child.Closure is not null) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.ChildNotOpen);
        }
        var phase = kind switch {
            ProcessDecisionPublicationKind.Dispatch => ProcessDecisionPhase.Active,
            ProcessDecisionPublicationKind.ManagerReview => ProcessDecisionPhase.WaitingForReview,
            _ => throw new InvalidDataException("The Process publication authority kind is unsupported.")
        };
        RequirePhase(current, phase);
    }

    private static void RequireExpected(ProcessDecisionGeneration current, ProcessDecisionExpectation expectation) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(expectation);
        current.Validate();
        expectation.Validate();
        if (current.Expectation != expectation) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.StaleExpectation);
        }
    }

    private static void RequireChild(ProcessDecisionGeneration current, ProcessDecisionChild child) {
        ArgumentNullException.ThrowIfNull(child);
        child.Validate();
        if (child.Reference != current.Reference) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.OriginalChildChanged);
        }
    }

    private static void RequireRetainedChild(ProcessDecisionGeneration current, ProcessDecisionChild child) {
        if (current.ChildCount == 0 || child.Closure is null && current.UnclosedChildCount == 0 ||
                child.Closure is not null && current.UnclosedChildCount == current.ChildCount) {
            throw new InvalidDataException("The retained child disagrees with its Process generation's child balance.");
        }
    }

    private static void RequireAllClosed(ProcessDecisionGeneration current) {
        if (current.UnclosedChildCount != 0) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.UnclosedChildren);
        }
    }

    private static void RequirePhase(ProcessDecisionGeneration current, ProcessDecisionPhase phase) {
        if (current.Phase != phase) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.InvalidPhase);
        }
    }

    private static ProcessDecisionGeneration ChangePhase(ProcessDecisionGeneration current, ProcessDecisionPhase phase)
        => current with { Revision = checked(current.Revision + 1), Phase = phase };
}
