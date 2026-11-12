using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Processes.Persistence.ProcessDecisionRows;

namespace CanDoItAll.Processes.Persistence;

internal sealed class EfProcessDecisionFundingStore(ProcessPersistenceDbContext context, ICanonicalRuntimeDatabase? runtimeDatabase) {
    internal async Task<ProcessDecisionRunAdmission?> StageInitialAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
        var state = request.Mutation.State;
        var assignments = request.InitialAssignments!;
        var admission = request.InitialRunDecisionAdmission;
        if (admission is not null) {
            admission.RequireRoot(state, request.InitialPlan!.Definition.VersionId);
            RequireProfile(admission);
            var head = NewBudget(admission.BudgetId, state.RootRunId.Value, state.RunId.Value, null, admission.Policy.Budget);
            head.AdmissionCommandId = request.CommandId.Value;
            head.RunAdmissionJson = ProcessDecisionRunAdmissionCodec.Serialize(admission);
            context.DecisionBudgets.Add(head);
        } else if (state.RunId != state.RootRunId) {
            var root = await context.DecisionBudgets.AsNoTracking().SingleOrDefaultAsync(item =>
                item.RunId == state.RootRunId.Value && item.StepInstanceId == null, cancellationToken).ConfigureAwait(false);
            if (root is not null) {
                admission = ReadRoot(root, state);
            }
        }
        foreach (var assignment in assignments) {
            if (admission is not null) {
                admission.RequireChild(assignment.DecisionDataPolicy
                    ?? throw new InvalidDataException("Every assignment under a governed root requires its original funded policy."));
            } else if (assignment.DecisionDataPolicy?.Funding is not null) {
                throw new InvalidDataException("The declared Process funding has no original admitted root budget.");
            }
        }
        return admission;
    }

    internal async Task RequireOriginalInitialAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
        var retained = await context.DecisionBudgets.AsNoTracking().Where(item => item.RunId == request.Mutation.State.RunId.Value &&
                item.StepInstanceId == null && item.AdmissionCommandId == request.CommandId.Value)
            .Select(item => item.RunAdmissionJson).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var requested = request.InitialRunDecisionAdmission is { } admission ? ProcessDecisionRunAdmissionCodec.Serialize(admission) : null;
        if (retained != requested) {
            throw new InvalidOperationException("The original runtime command has different root budget admission.");
        }
    }

    internal async Task RequireDirectRootAsync(ProcessDecisionBudgetEntity head, ProcessRuntimeStateSnapshot state,
        ProcessDecisionBudgetCommand command, CancellationToken cancellationToken) {
        _ = ReadRoot(head, state);
        if (state.RunId != state.RootRunId || await context.DecisionBudgetAllocations.AnyAsync(item =>
                item.FundingBudgetId == head.BudgetId && item.FundingAllocationId == command.AllocationId.Value, cancellationToken).ConfigureAwait(false)) {
            throw new InvalidOperationException("Root accounting cannot replace a funded child's original reservation or settlement.");
        }
    }

    internal async Task<ProcessDecisionBudgetFundingReceipt?> StageAsync(ProcessRuntimeStateSnapshot state, ProcessDecisionBudgetEntity local,
        ProcessDecisionBudgetAllocationEntity? allocation, ProcessDecisionBudgetCommand command, CancellationToken cancellationToken) {
        if (local.ParentBudgetId is not { } parentId) {
            if (allocation?.FundingBudgetId is not null || allocation?.FundingAllocationId is not null) {
                throw new InvalidDataException("The local allocation has funding outside its original budget.");
            }
            return null;
        }
        var parent = await context.DecisionBudgets.SingleOrDefaultAsync(item => item.BudgetId == parentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original root funding budget is missing.");
        var admission = ReadRoot(parent, state);
        if (local.StepInstanceId is null || local.RootRunId != parent.RootRunId || local.BudgetId == parent.BudgetId) {
            throw new InvalidDataException("Child funding requires a distinct local step budget under the original root.");
        }
        if (command is ProcessDecisionBudgetReservation) {
            var rootStatus = await context.RuntimeStates.AsNoTracking().Where(item => item.RunId == parent.RunId)
                .Select(item => (ProcessRuntimeStatus?)item.Status).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (rootStatus is null or ProcessRuntimeStatus.CancelRequested || ProcessRuntimeTerminalStates.IsRunTerminal(rootStatus.Value)) {
                throw new InvalidOperationException("A stopped root Process cannot fund new descendant work.");
            }
        }
        if (admission.Policy.ReadBasisMode == ReadBasisMode.RunSnapshot &&
                (command is ProcessDecisionBudgetReservation { Grant.Replans: > 0 } ||
                 command is ProcessDecisionBudgetSettlement { Settlement.Consumed.Replans: > 0 })) {
            throw new InvalidOperationException("Shared run material replanning must be charged once by its root owner.");
        }
        var id = ProcessDecisionFundingIdentity.Derive(new(parent.BudgetId), new(local.BudgetId), command.AllocationId);
        var retained = await context.DecisionBudgetAllocations.SingleOrDefaultAsync(item =>
            item.BudgetId == parent.BudgetId && item.AllocationId == id.Value, cancellationToken).ConfigureAwait(false);
        if (allocation is null ? retained is not null : retained is null || allocation.FundingBudgetId != parent.BudgetId || allocation.FundingAllocationId != id.Value) {
            throw new InvalidDataException("The original child and parent allocation bindings disagree.");
        }
        if (retained is not null && (retained.FundingBudgetId is not null || retained.FundingAllocationId is not null ||
                ReadAllocation(retained).Grant != ReadAllocation(allocation!).Grant ||
                ReadAllocation(retained).Settlement != ReadAllocation(allocation!).Settlement)) {
            throw new InvalidDataException("The original parent funding has different grant or closed usage evidence.");
        }
        ProcessDecisionBudgetExpectation expected = new(null, new(parent.BudgetId), parent.Revision);
        ProcessDecisionBudgetCommand parentCommand = command switch {
            ProcessDecisionBudgetReservation reservation => new ProcessDecisionBudgetReservation(expected, id, reservation.Grant),
            ProcessDecisionBudgetSettlement settlement => new ProcessDecisionBudgetSettlement(expected, id, settlement.Settlement),
            _ => throw new InvalidDataException("The child funding command is unsupported.")
        };
        var change = ApplyAccounting(parent, retained, parentCommand);
        if (change.Changed) {
            WriteBalance(parent, change.Balance);
            if (retained is null) {
                retained = new() { BudgetId = parent.BudgetId, AllocationId = id.Value };
                context.DecisionBudgetAllocations.Add(retained);
            }
            WriteAllocation(retained, change.Allocation);
        }
        return new(new(parent.RunId), new(parentCommand, change));
    }

    private ProcessDecisionRunAdmission ReadRoot(ProcessDecisionBudgetEntity head, ProcessRuntimeStateSnapshot state) {
        var admission = ProcessDecisionRunAdmissionCodec.Deserialize(head.RunAdmissionJson
            ?? throw new InvalidDataException("The root budget has no original policy admission."));
        RequireProfile(admission);
        if (head.StepInstanceId is not null || head.ParentBudgetId is not null || head.AdmissionCommandId is null ||
            head.RunId != state.RootRunId.Value || head.RootRunId != head.RunId || admission.RunId.Value != head.RunId ||
            admission.BudgetId.Value != head.BudgetId || admission.Policy.Budget != ReadBalance(head).Ceiling ||
            state.ProjectAdmission is { } project && project.DatabaseProfileId != admission.DatabaseProfileId) {
            throw new InvalidDataException("The retained root budget differs from its original owner or policy.");
        }
        return admission;
    }

    private void RequireProfile(ProcessDecisionRunAdmission admission) {
        var current = runtimeDatabase ?? throw new InvalidOperationException("Root Process accounting requires the selected canonical database profile.");
        if (current.Profile.Profile.Id != admission.DatabaseProfileId) {
            throw new InvalidDataException("The original root budget belongs to another database profile.");
        }
    }
}
