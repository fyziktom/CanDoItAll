using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Processes;

using DispatchArtifactInput = ProcessRunAutomationDispatchService.DispatchArtifactInput;

internal sealed class ProcessDispatchCandidateArtifactInputPreparationService(
    IWorkspacePathResolver workspacePathResolver,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor)
{
    public IReadOnlyList<DispatchArtifactInput> Prepare(
        ProcessDispatchCandidateHydrationSnapshot snapshot,
        ProcessStepRun stepRun)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(stepRun);

        snapshot.ArtifactInputsByStepDefinitionId.TryGetValue(
            stepRun.StepDefinitionId,
            out var configuredArtifactInputs);
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));

        return ProcessDispatchManagedArtifactPromptPathPreparer.PrepareArtifactInputsForPrompt(
            ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs(
                configuredArtifactInputs ?? [],
                snapshot.ArtifactExpectationsById,
                snapshot.SourceStepsById,
                snapshot.StepRunsByDefinitionId,
                snapshot.ExistingArtifacts),
            workspaceRoot,
            workspaceScope);
    }
}
