using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionRefreshAuthorityReader(IDbContextFactory<ProcessPersistenceDbContext> factory)
    : IProcessDecisionRefreshAuthorityReader {
    public async Task RequireCurrentAsync(ProcessDecisionChildMaterialBinding original, RuntimeCommandId admissionId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        ArgumentOutOfRangeException.ThrowIfEqual(admissionId.Value, Guid.Empty);
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var address = original.Child.Address;
        var receipt = await EfProcessDecisionRefreshReceiptReader.ReadCoreAsync(context, address.RunId, admissionId, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Stopping for refresh requires its durable original admission.");
        var intent = ((ProcessDecisionBeginRefresh)receipt.Decision.Command).Intent;
        if (intent.Request.Child != address || intent.OriginalMaterialId != original.Child.MaterialId ||
                intent.ChildFingerprint != ProcessDecisionChildMaterialBindingCodec.Fingerprint(original)) {
            throw new InvalidDataException("The refresh admission does not authorize this original child and material.");
        }
        var current = await EfProcessDecisionChildQuery.ReadCoreAsync(context, address, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Observation.Admission != original.Child ||
                current.Observation.Generation is not { Phase: ProcessDecisionPhase.Reconciling } generation ||
                generation.RefreshCommandId != admissionId || generation.Reference != address.Reference ||
                !AllowsStop(current.RootStatus) || !AllowsStop(current.RunStatus) ||
                current.StepStatus is not (ProcessRuntimeStepStatus.Waiting or ProcessRuntimeStepStatus.WaitingApproval or ProcessRuntimeStepStatus.Blocked) ||
                current.ActiveClaimToken is not null || current.HasLiveClaim) {
            throw new InvalidOperationException("The original refresh generation, runtime or child no longer permits stopping.");
        }
        var owner = await (from head in context.DecisionHeads.AsNoTracking()
            join scope in context.DecisionRunScopes.AsNoTracking() on head.SharedRunScopeId equals scope.ScopeId into scopes
            from scope in scopes.DefaultIfEmpty()
            where head.DecisionId == address.Reference.DecisionId.Value && head.RootRunId == address.RootRunId.Value &&
                head.RunId == address.RunId.Value && head.StepInstanceId == address.StepInstanceId.Value
            select new { head.CurrentGeneration, head.SharedRunScopeId,
                ScopePhase = scope == null ? null : (ProcessDecisionRunScopePhase?)scope.Phase,
                ScopeRevision = scope == null ? null : (long?)scope.Revision,
                ScopeMaterial = scope == null ? null : scope.CurrentMaterialId })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (owner is null || owner.CurrentGeneration != address.Reference.Generation ||
                owner.SharedRunScopeId != intent.Request.SharedScope?.ScopeId.Value ||
                receipt.SharedScope is { } shared && (owner.ScopePhase != ProcessDecisionRunScopePhase.Reconciling ||
                    owner.ScopeRevision != shared.State.Revision || owner.ScopeMaterial != original.Child.MaterialId.Value)) {
            throw new InvalidOperationException("Stopping for refresh requires its current original local and shared material owner.");
        }
    }

    private static bool AllowsStop(ProcessRuntimeStatus status)
        => status is ProcessRuntimeStatus.Active or ProcessRuntimeStatus.Waiting or ProcessRuntimeStatus.Blocked or ProcessRuntimeStatus.WaitingForUser;
}
