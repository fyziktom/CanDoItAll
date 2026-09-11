namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureAgentService {
    private async Task<ProjectProcessAssetResult> CreateProcessAssetAsync(Guid projectId, ProjectStructureAgentContext agent,
        string? leaseToken, CancellationToken cancellationToken) {
        var invocation = agent.ProcessAssetInvocation ?? throw new InvalidOperationException("The original asset proposal is required.");
        var input = new ProjectProcessAssetProposalCodec().Read(invocation.Admitted.Payload);
        if (input.ProjectId != projectId) {
            throw new ProjectStructureAgentException(409, "ProcessAssetIntentConflict", "The asset proposal cannot change its original project.");
        }
        var prior = await projectWorkbenchService.FindProcessAssetAsync(invocation.Admitted.IntentId, cancellationToken);
        if (prior?.Receipt is not null) {
            return await projectWorkbenchService.CommitProcessAssetAsync(
                await projectWorkbenchService.PrepareProcessAssetAsync(invocation, cancellationToken), cancellationToken);
        }
        return await leaseService.RunWithProjectMutationLeaseAsync(projectId, leaseToken, agent,
            input.Revision is null ? "create-structure-node" : "create-asset-revision", async token => {
                await EnsureParentAuthorityAllowedAsync(projectId, invocation.ParentNodeKey, token);
                var prepared = await projectWorkbenchService.PrepareProcessAssetAsync(invocation, token);
                if (prepared.MaterializedRequest is null) {
                    await projectWorkbenchService.RequireProcessAssetMediaReadAsync(prepared, token);
                    var media = input.Create is { } create
                        ? await ResolveAssetCreateMediaAsync(projectId, create.ToServiceRequest(), token)
                        : input.Revision!.Media;
                    EnsureValidMediaPayload(media);
                    ProjectStructureSvgAssetValidator.Validate(media);
                    prepared = await projectWorkbenchService.MaterializeProcessAssetAsync(prepared, media, token);
                }
                var result = await projectWorkbenchService.CommitProcessAssetAsync(prepared, token);
                return result.StorageObservationException is null ? result : result with {
                    Observation = result.Observation with {
                        StorageObservationWarning = "Storage placement and the native asset receipt committed; a later Storage observer failed."
                    }
                };
            }, cancellationToken);
    }
}
