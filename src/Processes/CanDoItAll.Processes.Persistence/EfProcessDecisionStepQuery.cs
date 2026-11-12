using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionStepQuery(ProcessPersistenceDbContext context, ICanonicalRuntimeDatabase database,
    TimeProvider clock) : IProcessDecisionStepQuery {
    public async Task<ProcessDecisionStepSelection> ReadAsync(ProcessRuntimeStateSnapshot state,
        ProcessRuntimeDispatchPrecondition claim, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(claim);
        claim.Validate();
        var row = await (from runtime in context.RuntimeStates.AsNoTracking()
            join root in context.RuntimeStates.AsNoTracking() on runtime.RootRunId equals root.RunId
            join budget in context.DecisionBudgets.AsNoTracking().Where(item => item.StepInstanceId == null)
                on root.RunId equals budget.RunId
            join step in context.RuntimeSteps.AsNoTracking() on runtime.RunId equals step.RunId
            join dispatch in context.DispatchClaims.AsNoTracking()
                on new { step.RunId, step.StepInstanceId } equals new { dispatch.RunId, dispatch.StepInstanceId }
            join assignment in ProcessAcceptedAssignmentGuard.Query(context)
                on new { step.RunId, step.StepInstanceId } equals new { assignment.RunId, assignment.StepInstanceId }
            where runtime.RunId == state.RunId.Value && step.StepInstanceId == claim.StepInstanceId.Value &&
                dispatch.ClaimToken == claim.ClaimToken.Value
            select new {
                Runtime = new ProcessRuntimeStateEntity {
                    RunId = runtime.RunId, RootRunId = runtime.RootRunId, PlanId = runtime.PlanId, PlanHash = runtime.PlanHash,
                    Status = runtime.Status, LaunchAdmissionId = runtime.LaunchAdmissionId,
                    ProjectAdmissionDatabaseProfileId = runtime.ProjectAdmissionDatabaseProfileId,
                    ProjectAdmissionProjectId = runtime.ProjectAdmissionProjectId, ProjectAdmissionLifetimeId = runtime.ProjectAdmissionLifetimeId
                },
                RootStatus = root.Status, Budget = budget,
                StepStatus = step.Status, step.ActiveClaimToken, step.AttemptNumber,
                Claim = dispatch, Assignment = assignment
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw Refuse(state, claim, ProcessRuntimeDispatchPreconditionRefusal.ClaimChanged);
        if (row.Runtime.Status != ProcessRuntimeStatus.Active || row.RootStatus != ProcessRuntimeStatus.Active) {
            throw Refuse(state, claim, ProcessRuntimeDispatchPreconditionRefusal.RuntimeInactive);
        }
        if (row.Runtime.RootRunId != state.RootRunId.Value || row.Runtime.PlanId != state.PlanId.Value ||
                row.Runtime.PlanHash != state.PlanHash || row.Runtime.LaunchAdmissionId != state.LaunchAdmissionId?.Value ||
                ProcessPersistenceMappers.ReadProjectAdmission(row.Runtime) != state.ProjectAdmission ||
                row.StepStatus is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running) ||
                row.ActiveClaimToken != claim.ClaimToken.Value || row.Claim.OwnerId != claim.OwnerId.Value ||
                row.Claim.AttemptNumber != row.AttemptNumber ||
                row.Claim.Status is not (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed)) {
            throw Refuse(state, claim, ProcessRuntimeDispatchPreconditionRefusal.ClaimChanged);
        }
        var source = await EfProcessAcceptedSource.ReadProjectedAsync(context, row.Runtime, claim.StepInstanceId, cancellationToken).ConfigureAwait(false);
        var original = source.Assignment;
        var current = ProcessAssignmentSource.From(EfProcessRuntimeStepAssignmentStore.ToAssignment(row.Assignment));
        if (!ProcessAcceptedAssignmentGuard.Same(original, current)) {
            throw new ProcessExecutionAuthorityMismatchException("Selected preparation cannot replace the original accepted assignment.");
        }
        var policy = original.DecisionDataPolicy ?? throw new InvalidDataException("Selected preparation has no original decision policy.");
        policy.RequireBinding(source.Header.DefinitionVersionId, claim.StepInstanceId);
        var rootAdmission = ProcessDecisionRunAdmissionCodec.Deserialize(row.Budget.RunAdmissionJson
            ?? throw new InvalidDataException("Selected preparation has no original root funding admission."));
        rootAdmission.RequireChild(policy);
        if (source.Header.Authority is not { ProjectAdmission: not null } authority ||
                authority.DatabaseProfileId != database.Profile.Profile.Id || rootAdmission.DatabaseProfileId != authority.DatabaseProfileId ||
                rootAdmission.RunId != state.RootRunId || rootAdmission.BudgetId.Value != row.Budget.BudgetId ||
                policy.Funding != rootAdmission.Funding || policy.WorkspaceNamespaceId != rootAdmission.WorkspaceNamespaceId ||
                !Guid.TryParse(original.ExecutorId, out var executorId) || executorId == Guid.Empty ||
                policy.ExecutionContract is null || string.IsNullOrWhiteSpace(original.ReadinessHash)) {
            throw new InvalidDataException("Selected preparation differs from its original source, profile, funding or executor.");
        }
        var selected = await EfProcessDecisionResultQuery.ReadStateAsync(context, state.RunId, claim.StepInstanceId, null, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("Selected preparation has no original local budget.");
        if (selected.Budget.Ceiling != policy.Policy.Budget) {
            throw new InvalidDataException("Selected preparation differs from its original local budget.");
        }
        ProcessDecisionRunScopeState? shared = null;
        if (policy.SharedRunScopeId is { } scopeId) {
            var loaded = await new EfProcessDecisionRunScopeStore(context, database).ReadPreparationAsync(scopeId, cancellationToken).ConfigureAwait(false);
            shared = loaded.Scope;
            if (shared.Admission != rootAdmission.MaterialScope || shared.Admission.ContentContract != policy.ContentContract) {
                throw new InvalidDataException("Selected preparation differs from its original shared material scope.");
            }
        }
        var material = selected.Head is { } head ? await ReadMaterialAsync(head, policy, state, cancellationToken).ConfigureAwait(false) : null;
        if (row.Claim.ExpiresAtUtc <= clock.GetUtcNow()) {
            throw Refuse(state, claim, ProcessRuntimeDispatchPreconditionRefusal.LeaseExpired);
        }
        return new(authority, policy, executorId, original.StepKey, selected.Budget,
            ProcessDecisionRows.ReadBalance(row.Budget), selected.Head, shared, selected.OtherWorkPending, material);
    }

    private async Task<ProcessDecisionStepMaterial> ReadMaterialAsync(ProcessDecisionGeneration head,
        ProcessDecisionDataPolicyAdmission policy, ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken) {
        var row = await (from material in context.DecisionMaterials.AsNoTracking()
            join scope in context.DecisionMaterialScopes.AsNoTracking() on material.ScopeId equals scope.ScopeId
            where material.MaterialId == head.MaterialId.Value
            select new { Scope = scope, material.Generation, material.Fingerprint, material.CapturedAtUtcTicks })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The selected original Process material is missing.");
        var saved = row.Scope;
        ProcessDecisionMaterialScope scopeValue = new(new(saved.ScopeId), saved.DatabaseProfileId, saved.WorkspaceNamespaceId,
            new(saved.RootRunId), new(saved.OwnerRunId), saved.StepInstanceId is { } stepId ? new(stepId) : null);
        scopeValue.Validate();
        if (scopeValue.DatabaseProfileId != database.Profile.Profile.Id || scopeValue.RootRunId != state.RootRunId ||
                scopeValue.WorkspaceNamespaceId != policy.WorkspaceNamespaceId || scopeValue.ReadBasisMode != policy.Policy.ReadBasisMode ||
                scopeValue.OwnerRunId != (policy.SharedRunScopeId is null ? state.RunId : state.RootRunId) ||
                policy.SharedRunScopeId is { } shared && scopeValue.Id != shared ||
                policy.SharedRunScopeId is null && scopeValue.StepInstanceId != policy.StepInstanceId ||
                saved.MaterialProtocol != policy.MaterialProtocol || saved.ContractId != policy.ContentContract?.SchemaId ||
                saved.ContractVersion != policy.ContentContract?.Version || saved.Executor is not null) {
            throw new InvalidDataException("The selected original Process material no longer matches its admitted scope or content.");
        }
        ProcessDecisionMaterialStamp stamp = new(new(scopeValue.Id, row.Generation, head.MaterialId), row.Fingerprint);
        stamp.Validate();
        return new(scopeValue, stamp, new(row.CapturedAtUtcTicks, TimeSpan.Zero));
    }

    private static ProcessRuntimeDispatchPreconditionException Refuse(ProcessRuntimeStateSnapshot state,
        ProcessRuntimeDispatchPrecondition claim, ProcessRuntimeDispatchPreconditionRefusal reason) => new(state.RunId, claim.StepInstanceId, reason);
}
