using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Processes.Persistence.ProcessDecisionRows;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionStore(ProcessPersistenceDbContext dbContext, ICanonicalRuntimeDatabase? runtimeDatabase = null) {
    private readonly EfProcessDecisionMaterialStore materials = new(dbContext, runtimeDatabase);
    private readonly EfProcessDecisionRunScopeStore scopes = new(dbContext, runtimeDatabase);

    internal Task RequireOriginalMaterialAsync(ProcessDecisionMaterial material, CancellationToken cancellationToken)
        => materials.RequireOriginalAsync(material, cancellationToken);

    public async Task<ProcessDecisionGeneration?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        CancellationToken cancellationToken = default) {
        var row = await (from head in dbContext.DecisionHeads.AsNoTracking()
            join generation in dbContext.DecisionGenerations.AsNoTracking()
                on new { head.DecisionId, Generation = head.CurrentGeneration } equals new { generation.DecisionId, generation.Generation }
                into generations
            from generation in generations.DefaultIfEmpty()
            where head.RunId == runId.Value && head.StepInstanceId == stepId.Value
            select new { Head = head, Generation = generation }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null) {
            return null;
        }
        return RequireHead(row.Head, row.Generation ?? throw new InvalidDataException("The current Process decision generation is missing."));
    }

    public async Task<ProcessDecisionGeneration?> ReadGenerationAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        ProcessDecisionReference reference, CancellationToken cancellationToken = default) {
        reference.Validate();
        var row = await (from head in dbContext.DecisionHeads.AsNoTracking()
            join generation in dbContext.DecisionGenerations.AsNoTracking() on head.DecisionId equals generation.DecisionId
            where head.RunId == runId.Value && head.StepInstanceId == stepId.Value && head.DecisionId == reference.DecisionId.Value && generation.Generation == reference.Generation
            select generation).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : ReadGeneration(row);
    }

    public async Task<ProcessDecisionReconciliation> ReadReconciliationAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        ProcessDecisionExpectation expected, CancellationToken cancellationToken = default) {
        expected.Validate();
        var before = await ReadAsync(runId, stepId, cancellationToken).ConfigureAwait(false);
        RequireExpected(before, expected);
        if (before!.Phase != ProcessDecisionPhase.Reconciling) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.InvalidPhase);
        }
        var rows = await ReadBoundedChildrenAsync(expected.Reference, cancellationToken).ConfigureAwait(false);
        var after = await ReadAsync(runId, stepId, cancellationToken).ConfigureAwait(false);
        RequireExpected(after, expected);
        return new(before, RequireCompleteChildren(before, rows));
    }

    internal async Task<ProcessDecisionReceipt?> StageAsync(ProcessRuntimeStateSnapshot state, ProcessRuntimeStateSnapshot proposed, ProcessDecisionCommand? command,
        ProcessDecisionBudgetReceipt? accounting, ProcessDecisionMaterial? material, CancellationToken cancellationToken) {
        if (command is null) {
            if (accounting?.Command is ProcessDecisionBudgetSettlement && await dbContext.DecisionChildren.AnyAsync(item =>
                    item.BudgetId == accounting.Change.Balance.BudgetId.Value && item.AllocationId == accounting.Command.AllocationId.Value, cancellationToken).ConfigureAwait(false)) {
                throw new InvalidOperationException("An enrolled child's work must settle atomically with its exact decision closure.");
            }
            return null;
        }
        command.Validate();
        if (dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Process decisions require the runtime owner's fenced transaction.");
        }
        var step = state.Steps.SingleOrDefault(item => item.StepInstanceId == command.StepInstanceId)
            ?? throw new InvalidOperationException("The decision step is not part of the original Process state.");
        var nextStep = proposed.Steps.SingleOrDefault(item => item.StepInstanceId == command.StepInstanceId)
            ?? throw new InvalidOperationException("The decision step is missing from the proposed Process state.");
        if (command is ProcessDecisionStart or ProcessDecisionEnrollment or ProcessDecisionRefresh or ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.WaitForReview } &&
                (ProcessRuntimeTerminalStates.IsStepTerminal(step.Status) || ProcessRuntimeTerminalStates.IsStepTerminal(nextStep.Status) ||
                ProcessRuntimeTerminalStates.IsRunTerminal(state.Status) || ProcessRuntimeTerminalStates.IsRunTerminal(proposed.Status) ||
                state.Status == ProcessRuntimeStatus.CancelRequested || proposed.Status == ProcessRuntimeStatus.CancelRequested)) {
            throw new InvalidOperationException("A stopped Process cannot begin new decision work or review.");
        }
        RequireRuntimeTransition(proposed, nextStep, command);
        if (command is ProcessDecisionStart start) {
            return await StageStartAsync(state, start, accounting, material, cancellationToken).ConfigureAwait(false);
        }
        var expected = command.Expected!;
        var head = await dbContext.DecisionHeads.SingleOrDefaultAsync(item => item.DecisionId == expected.Reference.DecisionId.Value,
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The original Process decision was not found.");
        if (head.RootRunId != state.RootRunId.Value || head.RunId != state.RunId.Value || head.StepInstanceId != command.StepInstanceId.Value) {
            throw new InvalidOperationException("The Process decision belongs to another runtime scope.");
        }
        var entity = await dbContext.DecisionGenerations.SingleAsync(item => item.DecisionId == head.DecisionId && item.Generation == head.CurrentGeneration,
            cancellationToken).ConfigureAwait(false);
        var current = RequireHead(head, entity);
        RequireExpected(current, expected);
        if (command is ProcessDecisionEnrollment or ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.WaitForReview }) {
            await scopes.RequireActiveDecisionAsync(head, current, cancellationToken).ConfigureAwait(false);
        }
        ProcessDecisionReceipt receipt;
        switch (command) {
            case ProcessDecisionEnrollment enrollment:
                receipt = await StageEnrollmentAsync(current, enrollment, accounting, cancellationToken).ConfigureAwait(false);
                break;
            case ProcessDecisionClosure closure:
                receipt = await StageClosureAsync(current, closure, accounting, cancellationToken).ConfigureAwait(false);
                break;
            case ProcessDecisionRefresh refresh:
                receipt = await StageRefreshAsync(state, current, refresh, accounting, material, cancellationToken).ConfigureAwait(false);
                break;
            case ProcessDecisionChangePhase phase when accounting is null:
                var changed = phase.Change switch {
                    ProcessDecisionPhaseChange.WaitForReview => ProcessDecisionTransitions.WaitForReview(current, expected),
                    ProcessDecisionPhaseChange.BeginReconciliation => ProcessDecisionTransitions.BeginReconciliation(current, expected),
                    ProcessDecisionPhaseChange.Complete => ProcessDecisionTransitions.Complete(current, expected),
                    ProcessDecisionPhaseChange.Cancel => ProcessDecisionTransitions.Cancel(current, expected),
                    _ => throw new InvalidDataException("The Process phase change is unsupported.")
                };
                if (phase.Change is ProcessDecisionPhaseChange.Complete or ProcessDecisionPhaseChange.Cancel) {
                    _ = RequireCompleteChildren(current, await ReadBoundedChildrenAsync(current.Reference, cancellationToken).ConfigureAwait(false));
                }
                receipt = new(command, true, changed);
                break;
            default:
                throw new InvalidOperationException("The Process decision transition has incompatible accounting.");
        }
        if (receipt.Changed) {
            WriteGeneration(entity, receipt.Previous ?? receipt.Current);
            head.CurrentGeneration = receipt.Current.Reference.Generation;
            head.Revision = receipt.Current.Revision;
        }
        return receipt;
    }

    private async Task<ProcessDecisionReceipt> StageStartAsync(ProcessRuntimeStateSnapshot state, ProcessDecisionStart command,
        ProcessDecisionBudgetReceipt? accounting, ProcessDecisionMaterial? material, CancellationToken cancellationToken) {
        var admission = await ReadAdmissionAsync(state, command.StepInstanceId, cancellationToken).ConfigureAwait(false);
        var policy = admission.Policy;
        if (policy.Budget.MaximumReplans >= ProcessDecisionGeneration.MaximumGenerations) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.GenerationCapacity);
        }
        var capture = RequireSettlement(command.StepInstanceId, accounting, 0);
        var budget = accounting!.Change.Balance;
        if (budget.Ceiling != policy.Budget || await dbContext.DecisionHeads.AnyAsync(item => item.RunId == state.RunId.Value &&
                item.StepInstanceId == command.StepInstanceId.Value, cancellationToken).ConfigureAwait(false)) {
            throw new InvalidOperationException("The original decision budget changed or this step already has a decision.");
        }
        await RequireUnusedCaptureAsync(budget.BudgetId, capture.Id, cancellationToken).ConfigureAwait(false);
        var current = ProcessDecisionTransitions.Create(command.DecisionId, command.MaterialId, budget.BudgetId);
        await StageMaterialAsync(state, admission, command, current, material, null, cancellationToken).ConfigureAwait(false);
        dbContext.DecisionHeads.Add(new() { DecisionId = command.DecisionId.Value, RootRunId = state.RootRunId.Value, RunId = state.RunId.Value,
            StepInstanceId = command.StepInstanceId.Value, BudgetId = budget.BudgetId.Value, CurrentGeneration = 1, Revision = 1,
            SharedRunScopeId = admission.SharedRunScopeId?.Value });
        AddGeneration(current, capture.Id, command.Material?.Reference.MaterialId.Value);
        return new(command, true, current);
    }

    private async Task<ProcessDecisionReceipt> StageEnrollmentAsync(ProcessDecisionGeneration current, ProcessDecisionEnrollment command,
        ProcessDecisionBudgetReceipt? accounting, CancellationToken cancellationToken) {
        RequireAccounting(command.StepInstanceId, current.BudgetId, accounting);
        if (accounting!.Command is not ProcessDecisionBudgetReservation || accounting.Command.AllocationId != command.Child.AllocationId) {
            throw new InvalidOperationException("Child enrollment requires its own original work reservation in this commit.");
        }
        var entity = await FindChildAsync(command.Expectation.Reference, command.Child.ChildId, cancellationToken).ConfigureAwait(false);
        var retained = entity is null ? null : ReadChild(entity);
        if (entity is null) {
            if (accounting.Change.Allocation.Settlement is not null || await dbContext.DecisionGenerations.AnyAsync(item =>
                    item.BudgetId == current.BudgetId.Value && item.CaptureAllocationId == command.Child.AllocationId.Value, cancellationToken).ConfigureAwait(false)) {
                throw new InvalidOperationException("A settled or capture allocation cannot enroll a new child.");
            }
        } else if (entity.BudgetId != current.BudgetId.Value ||
                (retained!.Closure is null) != (accounting.Change.Allocation.Settlement is null)) {
            throw new InvalidDataException("The retained child and its accounting disagree.");
        }
        var change = ProcessDecisionTransitions.Enroll(current, command.Expectation, command.Child, retained);
        if (change.Changed) {
            dbContext.DecisionChildren.Add(new() { DecisionId = current.Reference.DecisionId.Value, Generation = current.Reference.Generation,
                ChildId = command.Child.ChildId.Value, Owner = command.Child.Owner, BudgetId = current.BudgetId.Value, AllocationId = command.Child.AllocationId.Value });
        }
        return new(command, change.Changed, change.Generation, Child: change.Child);
    }

    private async Task<ProcessDecisionReceipt> StageClosureAsync(ProcessDecisionGeneration current, ProcessDecisionClosure command,
        ProcessDecisionBudgetReceipt? accounting, CancellationToken cancellationToken) {
        RequireAccounting(command.StepInstanceId, current.BudgetId, accounting);
        var entity = await FindChildAsync(command.Expectation.Reference, command.ChildId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The original enrolled child was not found.");
        if (entity.BudgetId != current.BudgetId.Value || accounting!.Command is not ProcessDecisionBudgetSettlement settlement ||
                accounting.Command.AllocationId.Value != entity.AllocationId || settlement.Settlement.OwnerWorkRevision != command.Closure.OwnerRevision) {
            throw new InvalidOperationException("Child closure requires settlement of its exact owner revision and original allocation.");
        }
        var change = ProcessDecisionTransitions.CloseChild(current, command.Expectation, ReadChild(entity), command.Closure);
        if (change.Changed) {
            entity.OwnerRevision = command.Closure.OwnerRevision;
            entity.Outcome = command.Closure.Outcome;
            entity.AcceptedReceiptId = command.Closure.AcceptedReceipt?.Value;
        }
        return new(command, change.Changed, change.Generation, Child: change.Child);
    }

    private async Task<ProcessDecisionReceipt> StageRefreshAsync(ProcessRuntimeStateSnapshot state, ProcessDecisionGeneration current,
        ProcessDecisionRefresh command, ProcessDecisionBudgetReceipt? accounting, ProcessDecisionMaterial? material, CancellationToken cancellationToken) {
        RequireAccounting(command.StepInstanceId, current.BudgetId, accounting);
        var admission = await ReadAdmissionAsync(state, command.StepInstanceId, cancellationToken).ConfigureAwait(false);
        var policy = admission.Policy;
        if (policy.Budget != accounting!.Change.Balance.Ceiling || policy.OnBasisChange == OnBasisChange.Fail ||
                command.Kind == ProcessDecisionRefreshKind.AutomaticReplan && policy.OnBasisChange != OnBasisChange.Replan ||
                (command.Kind == ProcessDecisionRefreshKind.ParentRefresh) != (admission.SharedRunScopeId is not null)) {
            throw new InvalidOperationException("The original Process policy does not admit this refresh.");
        }
        var capture = RequireSettlement(command.StepInstanceId, accounting, command.Kind == ProcessDecisionRefreshKind.AutomaticReplan ? 1 : 0);
        await RequireUnusedCaptureAsync(current.BudgetId, capture.Id, cancellationToken).ConfigureAwait(false);
        var change = ProcessDecisionTransitions.Advance(current, command.Expectation, command.MaterialId);
        _ = RequireCompleteChildren(current, await ReadBoundedChildrenAsync(current.Reference, cancellationToken).ConfigureAwait(false));
        await StageMaterialAsync(state, admission, command, change.Current, material, current.MaterialId, cancellationToken).ConfigureAwait(false);
        AddGeneration(change.Current, capture.Id, command.Material?.Reference.MaterialId.Value);
        return new(command, true, change.Current, change.Previous);
    }

    private async Task StageMaterialAsync(ProcessRuntimeStateSnapshot state, ProcessDecisionDataPolicyAdmission admission,
        ProcessDecisionCommand command, ProcessDecisionGeneration generation, ProcessDecisionMaterial? material,
        ProcessDecisionMaterialId? previous, CancellationToken cancellationToken) {
        if (admission.SharedRunScopeId is not null) {
            if (material is not null) {
                throw new InvalidDataException("Inherited material must be referenced without another payload or capture.");
            }
            await scopes.StageInheritanceAsync(state, admission, command, generation, previous, cancellationToken).ConfigureAwait(false);
            return;
        }
        await materials.StageAsync(state, admission, command, material, previous, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessDecisionDataPolicyAdmission> ReadAdmissionAsync(ProcessRuntimeStateSnapshot state, ProcessStepInstanceId stepId, CancellationToken cancellationToken) {
        var row = await (from assignment in dbContext.RuntimeStepAssignments.AsNoTracking()
            join plan in dbContext.InstancePlans.AsNoTracking() on assignment.PlanId equals plan.PlanId
            where assignment.RunId == state.RunId.Value && assignment.StepInstanceId == stepId.Value
            select new { assignment.PlanId, plan.DefinitionVersionId, assignment.DecisionDataPolicyJson }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row?.DecisionDataPolicyJson is not { } json || row.PlanId != state.PlanId.Value) {
            throw new InvalidOperationException("This Process assignment has no original admitted decision policy.");
        }
        var admission = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json);
        admission.RequireBinding(new(row.DefinitionVersionId), stepId);
        return admission;
    }

    private async Task RequireUnusedCaptureAsync(DecisionBudgetId budgetId, DecisionBudgetAllocationId allocationId, CancellationToken cancellationToken) {
        if (await dbContext.DecisionGenerations.AnyAsync(item => item.BudgetId == budgetId.Value && item.CaptureAllocationId == allocationId.Value,
                cancellationToken).ConfigureAwait(false) ||
                await dbContext.DecisionChildren.AnyAsync(item => item.BudgetId == budgetId.Value && item.AllocationId == allocationId.Value,
                    cancellationToken).ConfigureAwait(false)) {
            throw new InvalidOperationException("A capture allocation cannot be reused by another generation or child.");
        }
    }

    private Task<ProcessDecisionChildEntity?> FindChildAsync(ProcessDecisionReference reference, ProcessDecisionChildId childId, CancellationToken cancellationToken)
        => dbContext.DecisionChildren.SingleOrDefaultAsync(item => item.DecisionId == reference.DecisionId.Value &&
            item.Generation == reference.Generation && item.ChildId == childId.Value, cancellationToken);

    private Task<ProcessDecisionChildEntity[]> ReadBoundedChildrenAsync(ProcessDecisionReference reference, CancellationToken cancellationToken)
        => dbContext.DecisionChildren.AsNoTracking().Where(item => item.DecisionId == reference.DecisionId.Value && item.Generation == reference.Generation)
            .OrderBy(item => item.ChildId).Take(ProcessDecisionGeneration.MaximumChildren + 1).ToArrayAsync(cancellationToken);

    private static ProcessDecisionChild[] RequireCompleteChildren(ProcessDecisionGeneration current, ProcessDecisionChildEntity[] rows) {
        var children = rows.Select(ReadChild).ToArray();
        if (children.Length != current.ChildCount || children.Count(child => child.Closure is null) != current.UnclosedChildCount ||
                children.Select(child => child.ChildId).Distinct().Count() != children.Length || rows.Any(child => child.BudgetId != current.BudgetId.Value)) {
            throw new InvalidDataException("The complete retained child set disagrees with the Process generation.");
        }
        return children;
    }

    private static void RequireAccounting(ProcessStepInstanceId stepId, DecisionBudgetId budgetId, ProcessDecisionBudgetReceipt? accounting) {
        if (accounting is null || accounting.Command.Expectation.StepInstanceId != stepId || accounting.Change.Balance.BudgetId != budgetId) {
            throw new InvalidOperationException("The Process decision requires its exact step and budget accounting in this commit.");
        }
    }

    private static DecisionBudgetAllocation RequireSettlement(ProcessStepInstanceId stepId, ProcessDecisionBudgetReceipt? accounting, int replans) {
        if (accounting is null || accounting.Command.Expectation.StepInstanceId != stepId || accounting.Command is not ProcessDecisionBudgetSettlement ||
                accounting.Change.Allocation.Settlement is not { } closed || closed.Consumed.Replans != replans) {
            throw new InvalidOperationException("Material capture requires its closed work and exact replan accounting in this commit.");
        }
        return accounting.Change.Allocation;
    }

    private static void RequireExpected(ProcessDecisionGeneration? current, ProcessDecisionExpectation expected) {
        if (current?.Expectation != expected) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.StaleExpectation);
        }
    }

    private static void RequireRuntimeTransition(ProcessRuntimeStateSnapshot proposed, ProcessRuntimeStepState step, ProcessDecisionCommand command) {
        var valid = command switch {
            ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.WaitForReview or ProcessDecisionPhaseChange.BeginReconciliation }
                => step.Status is ProcessRuntimeStepStatus.Waiting or ProcessRuntimeStepStatus.WaitingApproval or ProcessRuntimeStepStatus.Blocked,
            ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.Complete } => step.Status == ProcessRuntimeStepStatus.Completed,
            ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.Cancel } => step.Status == ProcessRuntimeStepStatus.Cancelled,
            ProcessDecisionRefresh => step.Status == ProcessRuntimeStepStatus.Ready,
            _ => true
        };
        if (!valid || command is ProcessDecisionChangePhase or ProcessDecisionRefresh &&
                (step.ActiveClaimToken is not null || proposed.Claims.Any(claim => claim.StepInstanceId == step.StepInstanceId &&
                    claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed))) {
            throw new InvalidOperationException("The decision transition must atomically update its runtime step and release its dispatch claim.");
        }
    }

    private void AddGeneration(ProcessDecisionGeneration generation, DecisionBudgetAllocationId capture, Guid? canonicalMaterialId) {
        var entity = new ProcessDecisionGenerationEntity { DecisionId = generation.Reference.DecisionId.Value, Generation = generation.Reference.Generation,
            MaterialId = generation.MaterialId.Value, CanonicalMaterialId = canonicalMaterialId,
            BudgetId = generation.BudgetId.Value, CaptureAllocationId = capture.Value };
        WriteGeneration(entity, generation);
        dbContext.DecisionGenerations.Add(entity);
    }

    private static void WriteGeneration(ProcessDecisionGenerationEntity entity, ProcessDecisionGeneration generation) {
        entity.Revision = generation.Revision;
        entity.Phase = generation.Phase;
        entity.ChildCount = generation.ChildCount;
        entity.UnclosedChildCount = generation.UnclosedChildCount;
    }
}
