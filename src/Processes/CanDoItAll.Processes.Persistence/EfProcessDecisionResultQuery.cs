using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionResultQuery(IDbContextFactory<ProcessPersistenceDbContext> factory) : IProcessDecisionResultQuery {
    public async Task<ProcessDecisionResultSelection?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        ProcessExecutionRunId? executionRunId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ReadCoreAsync(context, runId, stepId, executionRunId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<ProcessDecisionResultSelection?> ReadCoreAsync(ProcessPersistenceDbContext context,
        ProcessRunId runId, ProcessStepInstanceId stepId, ProcessExecutionRunId? executionRunId, CancellationToken cancellationToken) {
        var saved = await ReadStateAsync(context, runId, stepId, executionRunId, cancellationToken).ConfigureAwait(false);
        if (saved is null) {
            return null;
        }
        ProcessDecisionResultChild? child = null;
        if (saved.ChildGeneration is { } generation && executionRunId is { } execution && saved.Head is { } head) {
            ProcessDecisionChildAddress address = new(new(saved.State.RootRunId), runId, stepId,
                new(head.Reference.DecisionId, generation), new(execution.Value));
            var original = await EfProcessDecisionChildQuery.ReadCoreAsync(context, address, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The original Process result child disappeared.");
            var source = await EfProcessAcceptedSource.ReadAsync(context, saved.State, stepId, cancellationToken).ConfigureAwait(false);
            var policy = source.Assignment.DecisionDataPolicy
                ?? throw new InvalidDataException("The original Process result child has no accepted decision policy.");
            var contract = policy.ExecutionContract
                ?? throw new InvalidDataException("The original Process result child has no accepted execution contract.");
            var material = await (from item in context.DecisionMaterials.AsNoTracking()
                join scope in context.DecisionMaterialScopes.AsNoTracking() on item.ScopeId equals scope.ScopeId
                where item.MaterialId == original.Observation.Admission.MaterialId.Value
                select new { Scope = scope, item.Generation, item.Fingerprint })
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The original Process result material is unavailable.");
            var row = material.Scope;
            ProcessDecisionMaterialScope scopeValue = new(new(row.ScopeId), row.DatabaseProfileId, row.WorkspaceNamespaceId,
                new(row.RootRunId), new(row.OwnerRunId), row.StepInstanceId is { } scopeStep ? new(scopeStep) : null);
            ProcessDecisionChildMaterialBinding binding = new(original.Observation.Admission, scopeValue,
                new(new(scopeValue.Id, material.Generation, original.Observation.Admission.MaterialId), material.Fingerprint), contract);
            binding.Validate();
            if (policy.StepInstanceId != stepId || policy.WorkspaceNamespaceId != scopeValue.WorkspaceNamespaceId ||
                    policy.Policy.ReadBasisMode != scopeValue.ReadBasisMode || policy.MaterialProtocol != row.MaterialProtocol ||
                    scopeValue.OwnerRunId != (policy.SharedRunScopeId is null ? runId : new ProcessRunId(saved.State.RootRunId)) ||
                    policy.SharedRunScopeId is { } shared && shared != scopeValue.Id ||
                    row.ContractId != (policy.ContentContract?.SchemaId ?? contract.ContractId) ||
                    row.ContractVersion != (policy.ContentContract?.Version ?? contract.Version) ||
                    row.Executor != (policy.ContentContract is null ? (ProcessDecisionChildOwner?)contract.Executor : null) ||
                    saved.Budget.BudgetId != original.Balance.BudgetId) {
                throw new InvalidDataException("The Process result material differs from the original accepted policy.");
            }
            Guid? agentId = null;
            if (string.Equals(source.Assignment.ExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase)) {
                if (!Guid.TryParse(source.Assignment.ExecutorId, out var selectedAgent) || selectedAgent == Guid.Empty) {
                    throw new InvalidDataException("The original Process result has an invalid accepted Agent identity.");
                }
                agentId = selectedAgent;
            }
            child = new(binding, original.Observation) {
                OriginalAgentId = agentId, OriginalProject = source.Header.ProjectAdmission
            };
            saved = saved with { Budget = original.Balance };
        }
        return new(new(saved.State.RootRunId), runId, stepId, saved.Budget, saved.Head, child,
            saved.OtherWorkPending, saved.SharedScopePending) { SharedScope = saved.SharedScope };
    }

    internal static async Task<ProcessDecisionResultState?> ReadStateAsync(ProcessPersistenceDbContext context,
        ProcessRunId runId, ProcessStepInstanceId stepId, ProcessExecutionRunId? executionRunId, CancellationToken cancellationToken) {
        var actualId = executionRunId?.Value;
        var row = await (from state in context.RuntimeStates.AsNoTracking()
            join step in context.RuntimeSteps.AsNoTracking() on state.RunId equals step.RunId
            join assignment in context.RuntimeStepAssignments.AsNoTracking()
                on new { state.RunId, step.StepInstanceId } equals new { assignment.RunId, assignment.StepInstanceId } into assignments
            from assignment in assignments.DefaultIfEmpty()
            join budget in context.DecisionBudgets.AsNoTracking()
                on new { state.RunId, StepInstanceId = (Guid?)step.StepInstanceId } equals new { budget.RunId, budget.StepInstanceId } into budgets
            from budget in budgets.DefaultIfEmpty()
            join head in context.DecisionHeads.AsNoTracking()
                on new { state.RunId, step.StepInstanceId } equals new { head.RunId, head.StepInstanceId } into heads
            from head in heads.DefaultIfEmpty()
            join generation in context.DecisionGenerations.AsNoTracking()
                on new { DecisionId = head == null ? (Guid?)null : head.DecisionId,
                    Generation = head == null ? (int?)null : head.CurrentGeneration }
                equals new { DecisionId = (Guid?)generation.DecisionId, Generation = (int?)generation.Generation } into generations
            from generation in generations.DefaultIfEmpty()
            join scope in context.DecisionRunScopes.AsNoTracking()
                on (budget == null ? null : budget.ParentBudgetId) equals (Guid?)scope.BudgetId into scopes
            from scope in scopes.DefaultIfEmpty()
            where state.RunId == runId.Value && step.StepInstanceId == stepId.Value
            select new {
                State = new ProcessRuntimeStateEntity {
                    RunId = state.RunId, RootRunId = state.RootRunId, PlanId = state.PlanId, PlanHash = state.PlanHash,
                    LaunchAdmissionId = state.LaunchAdmissionId, ProjectAdmissionDatabaseProfileId = state.ProjectAdmissionDatabaseProfileId,
                    ProjectAdmissionProjectId = state.ProjectAdmissionProjectId, ProjectAdmissionLifetimeId = state.ProjectAdmissionLifetimeId
                },
                HasPolicy = assignment != null && assignment.DecisionDataPolicyJson != null, Budget = budget, Head = head, Generation = generation,
                ScopeId = scope == null ? (Guid?)null : scope.ScopeId, ScopeRevision = scope == null ? (long?)null : scope.Revision,
                ChildGeneration = head == null || actualId == null ? null :
                    context.DecisionChildren.Where(child => child.DecisionId == head.DecisionId && child.ChildId == actualId)
                    .Select(child => (int?)child.Generation).SingleOrDefault(),
                OtherWorkPending = budget != null && context.DecisionBudgetAllocations.Any(allocation => allocation.BudgetId == budget.BudgetId &&
                    allocation.OwnerWorkRevision == null && (head == null || !context.DecisionChildren.Any(child => child.DecisionId == head.DecisionId &&
                        child.ChildId == actualId && child.BudgetId == allocation.BudgetId && child.AllocationId == allocation.AllocationId))),
                SharedScopePending = scope != null && (scope.CaptureAllocationId != null ||
                    head != null && (generation == null || scope.Phase != ProcessDecisionRunScopePhase.Active ||
                        scope.CurrentMaterialId != generation.MaterialId))
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The Process result has no persisted run and step assignment.");
        if (row.Budget is null) {
            if (row.HasPolicy || row.Head is not null) {
                throw new InvalidDataException("The governed Process result has no original local budget.");
            }
            return null;
        }
        if ((row.Head is null) != (row.Generation is null) || row.Budget.RootRunId != row.State.RootRunId ||
                row.Head is not null && row.Head.BudgetId != row.Budget.BudgetId) {
            throw new InvalidDataException("The Process result head, generation and budget are inconsistent.");
        }
        return new(row.State, ProcessDecisionRows.ReadBalance(row.Budget),
            row.Head is null ? null : ProcessDecisionRows.RequireHead(row.Head, row.Generation!),
            row.ChildGeneration, row.OtherWorkPending, row.SharedScopePending) {
            SharedScope = row.ScopeId is { } scopeId ? new(new(scopeId), row.ScopeRevision!.Value) : null
        };
    }
}

internal sealed record ProcessDecisionResultState(ProcessRuntimeStateEntity State, CanDoItAll.SharedKernel.DecisionBudgetBalance Budget,
    ProcessDecisionGeneration? Head, int? ChildGeneration, bool OtherWorkPending, bool SharedScopePending) {
    internal ProcessDecisionRunScopeExpectation? SharedScope { get; init; }
}
