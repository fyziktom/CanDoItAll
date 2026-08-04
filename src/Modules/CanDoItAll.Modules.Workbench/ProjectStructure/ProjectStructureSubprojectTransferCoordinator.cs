using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureCreatedSubprojectTransferResult(
    Guid SourceProjectId,
    ProjectStructureSubprojectTransferResult Transfer)
{
    public Guid TargetProjectId => Transfer.TargetProjectId;
}

public sealed class ProjectStructureSubprojectTransferCoordinator
{
    private readonly ProjectStructureSubprojectTransferOperations operations;
    private readonly Func<Guid> projectIdFactory;

    public ProjectStructureSubprojectTransferCoordinator(
        ProjectsService projectsService,
        ProjectWorkbenchService projectWorkbenchService)
        : this(
            ProjectStructureSubprojectTransferOperations.Create(
                projectsService,
                projectWorkbenchService),
            Guid.NewGuid)
    {
    }

    internal ProjectStructureSubprojectTransferCoordinator(
        ProjectStructureSubprojectTransferOperations operations,
        Func<Guid>? projectIdFactory = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        this.operations = operations;
        this.projectIdFactory = projectIdFactory ?? Guid.NewGuid;
    }

    public Task<ProjectStructureCreatedSubprojectTransferResult> MoveDescendantsToNewSubprojectAsync(
        Guid sourceProjectId,
        ProjectEditorModel targetProject,
        string sourceNodeId,
        CancellationToken cancellationToken = default)
    {
        return MoveDescendantsToNewSubprojectAsync(
            sourceProjectId,
            CreateTargetProjectId(),
            targetProject,
            sourceNodeId,
            cancellationToken);
    }

    public Task<ProjectStructureCreatedSubprojectTransferResult> MoveDescendantsToNewSubprojectAsync(
        Guid sourceProjectId,
        Guid targetProjectId,
        ProjectEditorModel targetProject,
        string sourceNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeId);
        var normalizedSourceNodeId = sourceNodeId.Trim();
        return ExecuteAsync(
            sourceProjectId,
            targetProjectId,
            targetProject,
            cancellationToken => operations.MoveDescendantsAsync(
                sourceProjectId,
                normalizedSourceNodeId,
                targetProjectId,
                cancellationToken),
            ProjectStructureTransferRejectionReason.DescendantsUnavailable,
            "The descendants could not be moved to the new subproject.",
            cancellationToken);
    }

    public Task<ProjectStructureCreatedSubprojectTransferResult> MoveNodesToNewSubprojectAsync(
        Guid sourceProjectId,
        ProjectEditorModel targetProject,
        IReadOnlyCollection<string> sourceNodeIds,
        bool includeDescendants,
        CancellationToken cancellationToken = default)
    {
        return MoveNodesToNewSubprojectAsync(
            sourceProjectId,
            CreateTargetProjectId(),
            targetProject,
            sourceNodeIds,
            includeDescendants,
            cancellationToken);
    }

    public Task<ProjectStructureCreatedSubprojectTransferResult> MoveNodesToNewSubprojectAsync(
        Guid sourceProjectId,
        Guid targetProjectId,
        ProjectEditorModel targetProject,
        IReadOnlyCollection<string> sourceNodeIds,
        bool includeDescendants,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceNodeIds);
        var normalizedSourceNodeIds = sourceNodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedSourceNodeIds.Length == 0)
        {
            throw new ProjectStructureTransferRejectedException(
                ProjectStructureTransferRejectionReason.SelectedNodesRequired,
                "At least one selected project-structure node id is required.",
                sourceProjectId,
                targetProjectId);
        }

        return ExecuteAsync(
            sourceProjectId,
            targetProjectId,
            targetProject,
            cancellationToken => operations.MoveNodesAsync(
                sourceProjectId,
                normalizedSourceNodeIds,
                targetProjectId,
                includeDescendants,
                cancellationToken),
            ProjectStructureTransferRejectionReason.SelectedNodesUnavailable,
            "The selected nodes could not be moved to the new subproject.",
            cancellationToken);
    }

    private async Task<ProjectStructureCreatedSubprojectTransferResult> ExecuteAsync(
        Guid sourceProjectId,
        Guid targetProjectId,
        ProjectEditorModel targetProject,
        Func<CancellationToken, Task<ProjectStructureSubprojectTransferResult?>> transferAsync,
        ProjectStructureTransferRejectionReason unavailableReason,
        string unavailableMessage,
        CancellationToken cancellationToken)
    {
        ValidateProjectIds(sourceProjectId, targetProjectId);
        ArgumentNullException.ThrowIfNull(targetProject);
        ArgumentNullException.ThrowIfNull(transferAsync);

        var targetProjectCreated = false;
        try
        {
            var createResult = await operations.CreateSubprojectAsync(
                sourceProjectId,
                targetProjectId,
                targetProject,
                cancellationToken);
            ProjectStructureProjectCreationResult.ThrowIfRejected(
                createResult,
                "The subproject could not be created.");
            if (createResult.Value != targetProjectId)
            {
                throw new InvalidOperationException(
                    $"Subproject creation returned '{createResult.Value:D}' instead of reserved id '{targetProjectId:D}'.");
            }

            targetProjectCreated = true;
            var transfer = await transferAsync(cancellationToken);
            if (transfer is null || transfer.MovedNodeCount == 0)
            {
                throw new ProjectStructureTransferRejectedException(
                    unavailableReason,
                    unavailableMessage,
                    sourceProjectId,
                    targetProjectId);
            }

            if (transfer.TargetProjectId != targetProjectId)
            {
                throw new ProjectStructureTransferRejectedException(
                    ProjectStructureTransferRejectionReason.TargetProjectMismatch,
                    $"The node transfer returned target '{transfer.TargetProjectId:D}' instead of reserved target '{targetProjectId:D}'.",
                    sourceProjectId,
                    targetProjectId,
                    transfer.TargetProjectId);
            }

            return new ProjectStructureCreatedSubprojectTransferResult(
                sourceProjectId,
                transfer);
        }
        catch (ProjectStructureTransferPartialCommitException)
        {
            throw;
        }
        catch (Exception transferFailure)
        {
            if (!targetProjectCreated)
            {
                throw;
            }

            var emptyChildRemoved = await CompensateEmptyCreatedSubprojectAsync(
                targetProjectId,
                transferFailure);
            if (emptyChildRemoved)
            {
                throw new ProjectStructureCompensatedSubprojectTransferException(
                    targetProjectId,
                    transferFailure);
            }

            throw;
        }
    }

    private Guid CreateTargetProjectId()
    {
        var targetProjectId = projectIdFactory();
        return targetProjectId == Guid.Empty
            ? throw new InvalidOperationException("The project id factory returned an empty id.")
            : targetProjectId;
    }

    private async Task<bool> CompensateEmptyCreatedSubprojectAsync(
        Guid targetProjectId,
        Exception transferFailure)
    {
        try
        {
            var targetSurface = await operations.GetStructureAsync(
                targetProjectId,
                CancellationToken.None);
            var containsEditableNode = targetSurface.Nodes.Any(node =>
                !node.IsSystemManaged &&
                node.ObjectType != ProjectObjectType.ProjectRoot);
            if (containsEditableNode)
            {
                return false;
            }

            await operations.DeleteProjectAsync(targetProjectId, CancellationToken.None);
            if (await operations.ProjectExistsAsync(targetProjectId, CancellationToken.None))
            {
                throw new InvalidOperationException(
                    $"Compensation did not remove empty subproject '{targetProjectId:D}'.");
            }

            return true;
        }
        catch (Exception compensationFailure)
        {
            throw new AggregateException(
                $"Node transfer failed and empty subproject '{targetProjectId:D}' could not be compensated.",
                transferFailure,
                compensationFailure);
        }
    }

    private static void ValidateProjectIds(Guid sourceProjectId, Guid targetProjectId)
    {
        if (sourceProjectId == Guid.Empty)
        {
            throw new ProjectStructureTransferRejectedException(
                ProjectStructureTransferRejectionReason.SourceProjectRequired,
                "A source project id is required.",
                sourceProjectId,
                targetProjectId);
        }

        if (targetProjectId == Guid.Empty)
        {
            throw new ProjectStructureTransferRejectedException(
                ProjectStructureTransferRejectionReason.TargetProjectRequired,
                "A reserved subproject id is required.",
                sourceProjectId,
                targetProjectId);
        }

        if (sourceProjectId == targetProjectId)
        {
            throw new ProjectStructureTransferRejectedException(
                ProjectStructureTransferRejectionReason.TargetProjectMustDiffer,
                "The target project must differ from the source project.",
                sourceProjectId,
                targetProjectId);
        }
    }
}

internal sealed record ProjectStructureSubprojectTransferOperations(
    Func<Guid, Guid, ProjectEditorModel, CancellationToken, Task<Result<Guid>>> CreateSubprojectAsync,
    Func<Guid, string, Guid, CancellationToken, Task<ProjectStructureSubprojectTransferResult?>> MoveDescendantsAsync,
    Func<Guid, IReadOnlyCollection<string>, Guid, bool, CancellationToken, Task<ProjectStructureSubprojectTransferResult?>> MoveNodesAsync,
    Func<Guid, CancellationToken, Task<ProjectStructureSurface>> GetStructureAsync,
    Func<Guid, CancellationToken, Task> DeleteProjectAsync,
    Func<Guid, CancellationToken, Task<bool>> ProjectExistsAsync)
{
    public static ProjectStructureSubprojectTransferOperations Create(
        ProjectsService projectsService,
        ProjectWorkbenchService projectWorkbenchService)
    {
        ArgumentNullException.ThrowIfNull(projectsService);
        ArgumentNullException.ThrowIfNull(projectWorkbenchService);

        return new ProjectStructureSubprojectTransferOperations(
            projectsService.CreateSubprojectAsync,
            projectWorkbenchService.MoveDescendantsToProjectAsync,
            projectWorkbenchService.MoveNodesToProjectAsync,
            projectWorkbenchService.GetStructureAsync,
            projectsService.DeleteAsync,
            async (projectId, cancellationToken) =>
                (await projectsService.ListAsync(cancellationToken))
                .Any(project => project.Id == projectId));
    }
}
