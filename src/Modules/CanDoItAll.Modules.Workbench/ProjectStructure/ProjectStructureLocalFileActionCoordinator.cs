using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Modules.Workbench;

internal interface IProjectStructureCurrentNodeResolver
{
    ValueTask<ProjectStructureNode?> ResolveAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default);
}

internal sealed class ProjectStructureCurrentNodeResolver(
    ProjectWorkbenchService projectWorkbenchService) : IProjectStructureCurrentNodeResolver
{
    public async ValueTask<ProjectStructureNode?> ResolveAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        ProjectStructureLoadResult loadResult = await projectWorkbenchService.TryGetStructureAsync(
            projectId,
            cancellationToken);
        return loadResult.Surface?.Nodes.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
    }
}

internal sealed class ProjectStructureLocalFileActionCoordinator(
    IProjectStructureCurrentNodeResolver currentNodeResolver,
    IProjectStructureNodeFileScopeProvider scopeResolver,
    IFileToolsKnownFileActionService knownFileActionService,
    IProjectStructureLocalFileOpener localFileOpener)
{
    public async ValueTask<ProjectStructureLocalFileOpenResult> LaunchAsync(
        Guid projectId,
        string nodeId,
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        ProjectStructureNode? currentNode = await currentNodeResolver.ResolveAsync(
            projectId,
            nodeId,
            cancellationToken);
        if (currentNode is null)
        {
            return new ProjectStructureLocalFileOpenResult(
                false,
                "The project node no longer exists in the current structure.");
        }

        if (ProjectStructureNodeHelpers.HasManagedAttachment(currentNode))
        {
            FileToolsKnownFileScope current = await scopeResolver.ResolveKnownFileAsync(
                projectId,
                nodeId,
                cancellationToken);
            FileToolsBrowseItemActionResult result = await knownFileActionService.LaunchAsync(
                current.Scope,
                current.Occurrence,
                action,
                cancellationToken);
            return new ProjectStructureLocalFileOpenResult(result.IsSuccess, result.Message);
        }

        return action switch
        {
            FileToolsLocalFileAction.OpenInPreferredApplication =>
                await localFileOpener.OpenInPreferredApplicationAsync(currentNode, cancellationToken),
            FileToolsLocalFileAction.OpenContainingFolder =>
                await localFileOpener.OpenAsync(currentNode, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }
}
