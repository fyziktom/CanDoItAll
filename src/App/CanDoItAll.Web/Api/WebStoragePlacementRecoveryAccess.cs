using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal sealed partial class WebStoragePlacementRecoveryAccess(WebCurrentPrincipalResolver principals,
    ProjectStoragePlacementRecoveryQuery owners, ProjectWriteAdmissionService projects, ICanonicalRuntimeDatabase canonical,
    AgentAssetCheckpointQuery agentCheckpoints, WorkflowAssetCheckpointQuery workflowCheckpoints)
    : IStoragePlacementRecoveryAccess {
    public async Task<StoragePlacementRecoveryAuthorization> AuthorizeAsync(StoragePlacementRecoveryOperation operation,
        CancellationToken cancellationToken) {
        try {
            var principal = await principals.ResolveAsync(cancellationToken);
            await principals.RequireScopeAsync(principal, ApiAuthorizationPolicies.ReadStoragePlacementRecovery,
                ApiAccessScopeNames.ReadStoragePlacementRecovery, cancellationToken);
            await principals.RequireScopeAsync(principal, ApiAuthorizationPolicies.GeneralApi, ApiAccessScopeNames.Api, cancellationToken);
            var canWriteProject = ApiAuthorizationPolicies.HasScope(principal.Principal, ApiAccessScopeNames.WriteProjectStructure) &&
                await principals.HasScopeAsync(principal, ApiAuthorizationPolicies.WriteProjectStructure,
                ApiAccessScopeNames.WriteProjectStructure, cancellationToken);
            var canReconcile = canWriteProject && await principals.HasScopeAsync(principal,
                ApiAuthorizationPolicies.ReconcileStoragePlacement, ApiAccessScopeNames.ReconcileStoragePlacement, cancellationToken);
            var canVerify = canReconcile && await principals.HasScopeAsync(principal,
                ApiAuthorizationPolicies.VerifyStorageExternalTermination, ApiAccessScopeNames.VerifyStorageExternalTermination, cancellationToken);
            if (!Enum.IsDefined(operation) || operation == StoragePlacementRecoveryOperation.Reconcile && !canReconcile ||
                operation == StoragePlacementRecoveryOperation.VerifyExternalTermination && !canVerify) {
                throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Denied);
            }
            return new(principal.Stamp, canReconcile, canVerify);
        } catch (WebCurrentPrincipalDeniedException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Denied);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, StoragePlacementRecoveryOwnerObservation>> InspectOwnersAsync(
        StoragePlacementRecoveryAuthorization authorization, IReadOnlyCollection<StoragePlacementRecoveryIdentity> identities,
        CancellationToken cancellationToken) {
        await EnsureCurrentAsync(authorization, cancellationToken);
        var facts = await owners.ObserveAsync(identities.Select(item => item.IntentId).ToArray(), cancellationToken);
        var projectIds = facts.Values.Where(item => item.OriginalAdmission is not null)
            .Select(item => item.OriginalProjectId).Distinct().ToArray();
        var current = await projects.ListAccessBindingFactsAsync(projectIds, cancellationToken);
        var lifetimes = current.Projects.ToDictionary(item => item.ProjectId, item => item.LifetimeId);
        return identities.ToDictionary(item => item.IntentId.Value, item => {
            var fact = facts.GetValueOrDefault(item.IntentId.Value);
            if (fact is null) {
                return new StoragePlacementRecoveryOwnerObservation(StoragePlacementRecoveryOwner.Unknown, false,
                    StoragePlacementRecoveryBlock.MissingOwnerAssociation);
            }
            var block = OriginalAssociationBlock(item, fact);
            if (block == StoragePlacementRecoveryBlock.None &&
                (fact.OriginalAdmission is not { } admission || !lifetimes.TryGetValue(admission.ProjectId, out var lifetime) ||
                 lifetime != admission.LifetimeId)) {
                block = StoragePlacementRecoveryBlock.OriginalProjectUnavailable;
            }
            return new StoragePlacementRecoveryOwnerObservation(fact.Owner, fact.NativeReceiptPresent, block);
        });
    }

    public async Task RequireOriginalOwnerForMutationAsync(StoragePlacementRecoveryAuthorization authorization,
        StoragePlacementRecoveryIdentity identity, CancellationToken cancellationToken) {
        await EnsureCurrentAsync(authorization, cancellationToken);
        if (!authorization.CanReconcile) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Denied);
        }
        var fact = await owners.ObserveForMutationAsync(identity.IntentId, cancellationToken);
        if (fact is null || OriginalAssociationBlock(identity, fact) != StoragePlacementRecoveryBlock.None ||
            fact.OriginalAdmission is not { } admission) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        }
        try {
            await projects.RequireForMutationAsync(admission, cancellationToken);
        } catch (ProjectWriteAdmissionRejectedException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        }
        await EnsureCurrentAsync(authorization, cancellationToken);
    }

    public async Task EnsureCurrentAsync(StoragePlacementRecoveryAuthorization authorization, CancellationToken cancellationToken) {
        var current = await AuthorizeAsync(StoragePlacementRecoveryOperation.Read, cancellationToken);
        if (current != authorization) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Denied);
        }
    }

    private StoragePlacementRecoveryBlock OriginalAssociationBlock(StoragePlacementRecoveryIdentity identity,
        ProjectStoragePlacementOwnerFact fact) {
        if (fact.StorageIntentId != identity.IntentId || identity.OriginalProjectId != fact.OriginalProjectId ||
            fact.OriginalProjectId == Guid.Empty) {
            return StoragePlacementRecoveryBlock.InvalidOwnerAssociation;
        }
        if (fact.Block != StoragePlacementRecoveryBlock.None) {
            return fact.Block;
        }
        return fact.OriginalAdmission?.DatabaseProfileId == canonical.Profile.Profile.Id
            ? StoragePlacementRecoveryBlock.None : StoragePlacementRecoveryBlock.OriginalProjectUnavailable;
    }
}
