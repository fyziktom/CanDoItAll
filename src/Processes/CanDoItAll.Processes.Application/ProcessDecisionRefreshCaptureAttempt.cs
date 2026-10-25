using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

internal sealed record ProcessDecisionRefreshCaptureAttempt(ProcessDecisionRefreshCaptureIdentity Identity,
    ProcessDecisionRefreshCaptureSelection Original, bool IsRoot, DecisionBudgetAmount Grant,
    ProcessDecisionRefreshCaptureReservation? RetainedReservation = null) {
    internal ProcessDecisionRefreshIntent Intent => ((ProcessDecisionBeginRefresh)Original.Admission.Decision.Command).Intent;
    internal DecisionBudgetBalance Budget => IsRoot ? Original.RootBudget : Original.Original.Budget;
    internal DecisionBudgetAllocationId Allocation => IsRoot ? Identity.RootAllocation : Identity.LocalAllocation;
    internal RuntimeCommandId Reserve => IsRoot ? Identity.RootReserve : Identity.LocalReserve;
    internal RuntimeCommandId Complete => IsRoot ? Identity.RootComplete : Identity.Successor;
    internal RuntimeCommandId Abort => IsRoot ? Identity.RootAbort : Identity.LocalAbort;
    internal ProcessDecisionRunScopeCapture RootCapture => new(Identity.RootAllocation, Identity.Material, ProcessDecisionRunCaptureKind.ManagerRefresh);
    internal ProcessDecisionMaterialCaptureRequest CaptureRequest => new(Original.Input.Source,
        Original.Original.Child!.Original.Scope, Original.Input.Assignment.DecisionDataPolicy!.ContentContract!,
        new(Original.Original.Child.Original.Scope.Id, checked(Original.Original.Child.Original.Material.Reference.Generation + 1), Identity.Material));
    internal ProcessDecisionRefreshCaptureReservation JournalReservation => RetainedReservation ?? Describe(Budget.Revision);

    private ProcessDecisionRefreshCaptureReservation Describe(long revision) => new(Original.Original.RunId, Identity.AdmissionId, Reserve,
        new(new(IsRoot ? null : Original.Original.StepInstanceId, Budget.BudgetId, revision), Allocation, Grant),
        CaptureRequest.Scope, CaptureRequest.ContentContract, CaptureRequest.Material, Original.Input.InputFingerprint);

    internal static ProcessDecisionRefreshCaptureAttempt Restore(ProcessDecisionRefreshCaptureSelection selected,
        ProcessDecisionRefreshCaptureReservation reservation) {
        reservation.Validate();
        var attempt = new ProcessDecisionRefreshCaptureAttempt(new(reservation.AdmissionId), selected, reservation.IsRoot,
            reservation.Budget.Grant, reservation);
        if (attempt.Describe(reservation.Budget.Expectation.Revision) != reservation) {
            throw new InvalidDataException("Refresh recovery changed the original reservation, source, contract or material.");
        }
        return attempt;
    }

    internal static ProcessDecisionRefreshCaptureAttempt Create(ProcessDecisionRefreshCaptureIdentity identity,
        ProcessDecisionRefreshCaptureSelection selected, bool root) {
        var intent = ((ProcessDecisionBeginRefresh)selected.Admission.Decision.Command).Intent;
        if (intent.Request.CommandId != identity.AdmissionId || intent.Request.Kind != ProcessDecisionRefreshKind.ManagerRefresh ||
                root && (selected.SharedScope is not { Phase: ProcessDecisionRunScopePhase.Reconciling } ||
                    selected.Original.RunId != selected.Root.RunId) ||
                selected.Input.Assignment.DecisionDataPolicy is not {
                    MaterialProtocol: ProcessDecisionMaterialProtocol.SharedContentV1, ContentContract: not null
                }) {
            throw new InvalidDataException("Refresh capture requires its original admitted manual action and content owner.");
        }
        var local = selected.Original.Budget;
        var funding = selected.RootBudget;
        if (selected.Original.Head!.Reference.Generation == ProcessDecisionGeneration.MaximumGenerations ||
                selected.Original.Child!.Original.Material.Reference.Generation == ProcessDecisionGeneration.MaximumGenerations) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.GenerationCapacity);
        }
        if (selected.SharedScope is { TotalMemberships: ProcessDecisionRunScopeState.MaximumMemberships }) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.MembershipCapacity);
        }
        if (local.AllocationCount > DecisionBudgetBalance.MaximumAllocations - 2 ||
                funding.AllocationCount > DecisionBudgetBalance.MaximumAllocations - (root ? 3 : 2)) {
            throw new DecisionBudgetException(DecisionBudgetRefusal.AllocationCapacity);
        }
        var remaining = Math.Min(ProcessDecisionStepIdentity.Remaining(local), ProcessDecisionStepIdentity.Remaining(funding));
        if (remaining <= 0) {
            throw new DecisionBudgetException(DecisionBudgetRefusal.WorkExhausted);
        }
        return new(identity, selected, root, new(Math.Min(remaining, TimeSpan.FromSeconds(10).Ticks), 0));
    }

    internal ProcessRuntimeCommitRequest Reservation(ProcessRuntimeStateSnapshot state, DateTimeOffset now)
        => TimestampOnly(state, Reserve, now) with {
            DecisionBudget = JournalReservation.Budget,
            RefreshCapture = new(JournalReservation),
            DecisionRunScope = IsRoot ? new(Original.SharedScope!.Expectation, ProcessDecisionRunScopeCommandKind.BeginCapture, RootCapture) : null
        };

    internal void RequireReceipt(ProcessRuntimeCommitResult result, RuntimeCommandId command, DecisionBudgetSettlement? work = null,
        ProcessDecisionMaterialStamp? material = null) {
        if (result.State.RunId != Original.Original.RunId || result.State.RootRunId != Original.Original.RootRunId) {
            throw new InvalidDataException("Refresh capture returned another runtime owner's receipt.");
        }
        RequireReceipt(new ProcessDecisionRefreshCaptureReceipt(result.Outcome, result.Decision, result.DecisionBudget, result.DecisionRunScope), command, work, material);
    }

    internal void RequireReceipt(ProcessDecisionRefreshCaptureReceipt receipt, RuntimeCommandId command,
        DecisionBudgetSettlement? work = null, ProcessDecisionMaterialStamp? material = null) {
        ProcessDecisionRefreshCaptureReceipts.Require(receipt, Original.Original.RootRunId, Identity, command);
        var budget = receipt.Budget!;
        if (command != Reserve && command != Complete && command != Abort || budget.Command.Expectation.BudgetId != Budget.BudgetId ||
                budget.Command.Expectation.StepInstanceId != (IsRoot ? null : Original.Original.StepInstanceId) ||
                budget.Change.Allocation.Grant != Grant || budget.Change.Allocation.Settlement != work ||
                command == Reserve && budget.Command.Expectation.Revision != JournalReservation.Budget.Expectation.Revision ||
                command == Complete && (IsRoot ? receipt.Scope?.State.Material?.Stamp != material : receipt.Decision?.Command.Material != material)) {
            throw new InvalidDataException("Refresh capture changed its original reservation, material or closed work.");
        }
    }

    internal static ProcessRuntimeCommitRequest TimestampOnly(ProcessRuntimeStateSnapshot state, RuntimeCommandId command, DateTimeOffset now) {
        var ticks = Math.Max(now.UtcTicks / 10 * 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
        return new(command, state, ProcessRuntimeMutation.Applied(state with { UpdatedAtUtc = new(ticks, TimeSpan.Zero) }, [], []));
    }
}
