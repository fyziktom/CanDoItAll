using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessDecisionRunScopeTransitions {
    public static ProcessDecisionRunScopeState Create(ProcessDecisionRunScopeAdmission admission) {
        var state = new ProcessDecisionRunScopeState(admission, 1, ProcessDecisionRunScopePhase.AwaitingCapture, null, 0, 0);
        state.Validate();
        return state;
    }

    public static ProcessDecisionRunScopeState BeginReconciliation(ProcessDecisionRunScopeState current,
        ProcessDecisionRunScopeExpectation expected) {
        RequireExpected(current, expected);
        RequirePhase(current, ProcessDecisionRunScopePhase.Active);
        return Next(current, ProcessDecisionRunScopePhase.Reconciling);
    }

    public static ProcessDecisionRunScopeState BeginCapture(ProcessDecisionRunScopeState current,
        ProcessDecisionRunScopeExpectation expected, ProcessDecisionRunScopeCapture capture, ProcessDecisionRunAdmission original,
        DecisionBudgetChange reservation, IReadOnlyList<DecisionBudgetAllocation> allocations,
        IReadOnlyList<ProcessDecisionRunScopeMemberObservation> members) {
        RequireExpected(current, expected);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(original);
        capture.Validate();
        original.Validate();
        RequirePhase(current, capture.Kind == ProcessDecisionRunCaptureKind.Initial
            ? ProcessDecisionRunScopePhase.AwaitingCapture : ProcessDecisionRunScopePhase.Reconciling);
        if (current.Material?.Stamp.Reference.Generation == ProcessDecisionGeneration.MaximumGenerations) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.GenerationCapacity);
        }
        if (capture.MaterialId == current.Material?.Stamp.Reference.MaterialId) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.OriginalCaptureChanged);
        }
        if (original.MaterialScope != current.Admission || original.Funding != current.Admission.Funding || original.DatabaseProfileId != current.Admission.DatabaseProfileId ||
                original.WorkspaceNamespaceId != current.Admission.WorkspaceNamespaceId || original.Policy.ReadBasisMode != ReadBasisMode.RunSnapshot ||
                capture.Kind != ProcessDecisionRunCaptureKind.Initial && original.Policy.OnBasisChange == OnBasisChange.Fail ||
                capture.Kind == ProcessDecisionRunCaptureKind.AutomaticReplan && original.Policy.OnBasisChange != OnBasisChange.Replan) {
            throw new InvalidDataException("The original root admission does not authorize this shared capture.");
        }
        RequireAccounting(current, capture, reservation);
        if (!reservation.Changed || reservation.Allocation.Settlement is not null || reservation.Balance.OverrunDetected ||
                reservation.Balance.Ceiling != original.Policy.Budget ||
                reservation.Allocation.Grant.Replans != (capture.Kind == ProcessDecisionRunCaptureKind.AutomaticReplan ? 1 : 0)) {
            throw new InvalidDataException("A shared capture requires one new original root grant with its exact replan charge.");
        }
        RequireQuiescence(current, members, reservation.Balance, allocations, capture.AllocationId);
        return Next(current, ProcessDecisionRunScopePhase.Capturing) with { Capture = capture };
    }

    public static ProcessDecisionRunScopeState CompleteCapture(ProcessDecisionRunScopeState current,
        ProcessDecisionRunScopeExpectation expected, ProcessDecisionMaterial material, DecisionBudgetChange settlement) {
        RequireExpected(current, expected);
        RequirePhase(current, ProcessDecisionRunScopePhase.Capturing);
        ArgumentNullException.ThrowIfNull(material);
        RequireClosedCapture(current, settlement);
        if (settlement.Balance.OverrunDetected) {
            throw new DecisionBudgetException(DecisionBudgetRefusal.Overrun);
        }
        if (material.Scope != current.Admission.MaterialScope || material.ContentContract != current.Admission.ContentContract ||
                material.Reference.MaterialId != current.Capture!.MaterialId ||
                material.Reference.Generation != (current.Material?.Stamp.Reference.Generation ?? 0) + 1 ||
                current.Material is { } previous && material.CapturedAtUtc < previous.CapturedAtUtc ||
                settlement.Allocation.Settlement!.Consumed.Replans != (current.Capture.Kind == ProcessDecisionRunCaptureKind.AutomaticReplan ? 1 : 0)) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.OriginalCaptureChanged);
        }
        return Next(current, ProcessDecisionRunScopePhase.Active) with {
            Material = ProcessDecisionRunScopeMaterial.From(material), Capture = null, CurrentMemberships = 0
        };
    }

    public static ProcessDecisionRunScopeState AbortCapture(ProcessDecisionRunScopeState current,
        ProcessDecisionRunScopeExpectation expected, DecisionBudgetChange settlement) {
        RequireExpected(current, expected);
        RequirePhase(current, ProcessDecisionRunScopePhase.Capturing);
        RequireClosedCapture(current, settlement);
        return Next(current, current.Material is null ? ProcessDecisionRunScopePhase.AwaitingCapture : ProcessDecisionRunScopePhase.Reconciling)
            with { Capture = null };
    }

    public static ProcessDecisionRunScopeJoin Join(ProcessDecisionRunScopeState current, ProcessDecisionRunScopeExpectation expected,
        ProcessDecisionRunScopeMembership membership, ProcessDecisionRunScopeMembership? retained = null) {
        RequireExpected(current, expected);
        ArgumentNullException.ThrowIfNull(membership);
        membership.Validate();
        if (retained is not null) {
            retained.Validate();
            if (membership != retained) {
                throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.OriginalMembershipChanged);
            }
            if (current.TotalMemberships == 0 || retained.Material.ScopeId != current.Admission.ScopeId ||
                    retained.Material.Generation > current.Material?.Stamp.Reference.Generation) {
                throw new InvalidDataException("The retained membership disagrees with its original shared material head.");
            }
            return new(false, current, retained);
        }
        RequirePublication(current, membership);
        if (current.TotalMemberships == ProcessDecisionRunScopeState.MaximumMemberships) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.MembershipCapacity);
        }
        return new(true, Next(current, current.Phase) with {
            TotalMemberships = current.TotalMemberships + 1, CurrentMemberships = current.CurrentMemberships + 1
        }, membership);
    }

    public static ProcessDecisionRunScopeState Finish(ProcessDecisionRunScopeState current, ProcessDecisionRunScopeExpectation expected,
        bool cancelled, IReadOnlyList<ProcessDecisionRunScopeMemberObservation> members, DecisionBudgetBalance balance,
        IReadOnlyList<DecisionBudgetAllocation> allocations) {
        RequireExpected(current, expected);
        if (cancelled ? current.Phase is not (ProcessDecisionRunScopePhase.Reconciling or ProcessDecisionRunScopePhase.AwaitingCapture)
                : current.Phase != ProcessDecisionRunScopePhase.Active) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.InvalidPhase);
        }
        RequireQuiescence(current, members, balance, allocations);
        return Next(current, cancelled ? ProcessDecisionRunScopePhase.Cancelled : ProcessDecisionRunScopePhase.Completed);
    }

    public static void RequirePublication(ProcessDecisionRunScopeState current, ProcessDecisionRunScopeMembership membership) {
        current.Validate();
        membership.Validate();
        RequirePhase(current, ProcessDecisionRunScopePhase.Active);
        if (membership.Material != current.Material!.Stamp.Reference) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.OriginalMembershipChanged);
        }
    }

    public static ProcessDecisionRunScopeState FinishResult(ProcessDecisionRunScopeState current,
        ProcessDecisionRunScopeExpectation expected, bool cancelled, IReadOnlyList<ProcessDecisionRunScopeMemberObservation> members,
        DecisionBudgetBalance balance, IReadOnlyList<DecisionBudgetAllocation> allocations) {
        RequireExpected(current, expected);
        if (cancelled ? current.Phase is not (ProcessDecisionRunScopePhase.Active or ProcessDecisionRunScopePhase.Reconciling or ProcessDecisionRunScopePhase.AwaitingCapture)
                : current.Phase != ProcessDecisionRunScopePhase.Active) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.InvalidPhase);
        }
        RequireQuiescence(current, members, balance, allocations);
        return Next(current, cancelled ? ProcessDecisionRunScopePhase.Cancelled : ProcessDecisionRunScopePhase.Completed);
    }

    public static ProcessDecisionRunScopeState FinishRefreshResult(ProcessDecisionRunScopeState current,
        ProcessDecisionRunScopeExpectation expected, bool cancelled, IReadOnlyList<ProcessDecisionRunScopeMemberObservation> members,
        DecisionBudgetBalance balance, IReadOnlyList<DecisionBudgetAllocation> allocations, ProcessDecisionRefreshIntent intent) {
        RequireExpected(current, expected);
        intent.Validate();
        var original = intent.Request;
        if (original.SharedScope is null || current.Phase != ProcessDecisionRunScopePhase.Reconciling ||
                current.Expectation != original.SharedScope with { Revision = checked(original.SharedScope.Revision + 1) } ||
                current.Admission.Funding.RootRunId != original.Child.RootRunId || original.Child.RunId != original.Child.RootRunId ||
                current.Material?.Stamp.Reference.MaterialId != intent.OriginalMaterialId || members.Count != 1 ||
                members[0].Membership.RunId != original.Child.RunId || members[0].Membership.StepInstanceId != original.Child.StepInstanceId ||
                members[0].Decision.Reference != original.Child.Reference || members[0].Decision.RefreshCommandId != original.CommandId ||
                members[0].Decision.Phase is not (ProcessDecisionPhase.Completed or ProcessDecisionPhase.Cancelled) ||
                !cancelled && members[0].Decision.Phase != ProcessDecisionPhase.Completed) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.OriginalMembershipChanged);
        }
        RequireQuiescence(current, members, balance, allocations);
        return Next(current, cancelled ? ProcessDecisionRunScopePhase.Cancelled : ProcessDecisionRunScopePhase.Completed);
    }

    private static void RequireQuiescence(ProcessDecisionRunScopeState current, IReadOnlyList<ProcessDecisionRunScopeMemberObservation> members,
        DecisionBudgetBalance balance, IReadOnlyList<DecisionBudgetAllocation> allocations, DecisionBudgetAllocationId? pending = null) {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(allocations);
        balance.Validate();
        if (members.Count != current.CurrentMemberships || members.Count > ProcessDecisionRunScopeState.MaximumMemberships ||
                members.Select(member => member.Membership.Decision).Distinct().Count() != members.Count) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.IncompleteMembership);
        }
        foreach (var member in members) {
            member.Validate();
            if (member.Membership.Material != current.Material?.Stamp.Reference) {
                throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.OriginalMembershipChanged);
            }
            if (member.HasActiveClaim || member.Decision.UnclosedChildCount != 0 ||
                    member.Decision.Phase is ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview) {
                throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.UnclosedWork);
            }
        }
        if (balance.BudgetId != current.Admission.Funding.BudgetId || allocations.Count != balance.AllocationCount ||
                allocations.Select(allocation => allocation.Id).Distinct().Count() != allocations.Count) {
            throw new InvalidDataException("Shared reconciliation requires the complete original root allocation set.");
        }
        foreach (var allocation in allocations) {
            allocation.Validate();
            if (allocation.BudgetId != balance.BudgetId) {
                throw new InvalidDataException("Shared reconciliation observed another root's allocation.");
            }
            if (allocation.Settlement is null && allocation.Id != pending) {
                throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.UnclosedWork);
            }
        }
        if (pending is { } id && allocations.Count(allocation => allocation.Id == id && allocation.Settlement is null) != 1 ||
                allocations.Sum(allocation => allocation.Settlement?.Consumed.WorkTicks ?? 0) != balance.ConsumedWorkTicks ||
                allocations.Sum(allocation => (long)(allocation.Settlement?.Consumed.Replans ?? 0)) != balance.ConsumedReplans ||
                allocations.Sum(allocation => allocation.Settlement is null ? allocation.Grant.WorkTicks : 0) != balance.ReservedWorkTicks ||
                allocations.Sum(allocation => allocation.Settlement is null ? (long)allocation.Grant.Replans : 0) != balance.ReservedReplans) {
            throw new InvalidDataException("The complete root allocations disagree with their retained balance.");
        }
    }

    private static void RequireClosedCapture(ProcessDecisionRunScopeState current, DecisionBudgetChange settlement) {
        RequireAccounting(current, current.Capture!, settlement);
        if (!settlement.Changed || settlement.Allocation.Settlement is null) {
            throw new InvalidDataException("Shared capture completion requires its exact newly closed root allocation.");
        }
    }

    private static void RequireAccounting(ProcessDecisionRunScopeState current, ProcessDecisionRunScopeCapture capture, DecisionBudgetChange change) {
        ArgumentNullException.ThrowIfNull(change);
        change.Balance.Validate();
        change.Allocation.Validate();
        if (change.Balance.BudgetId != current.Admission.Funding.BudgetId || change.Allocation.BudgetId != change.Balance.BudgetId ||
                change.Allocation.Id != capture.AllocationId) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.OriginalCaptureChanged);
        }
    }

    private static void RequireExpected(ProcessDecisionRunScopeState current, ProcessDecisionRunScopeExpectation expected) {
        current.Validate();
        expected.Validate();
        if (current.Expectation != expected) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.StaleExpectation);
        }
    }

    private static void RequirePhase(ProcessDecisionRunScopeState current, ProcessDecisionRunScopePhase phase) {
        if (current.Phase != phase) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.InvalidPhase);
        }
    }

    private static ProcessDecisionRunScopeState Next(ProcessDecisionRunScopeState current, ProcessDecisionRunScopePhase phase)
        => current with { Revision = checked(current.Revision + 1), Phase = phase };
}
