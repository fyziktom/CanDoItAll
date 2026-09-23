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
        CancellationToken cancellationToken = default,
        ProjectStructureAgentContext? mutationOwner = null,
        ProjectMutationAuthorization? authorization = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeId);
        var normalizedSourceNodeId = sourceNodeId.Trim();
        return ExecuteAsync(
            sourceProjectId,
            targetProjectId,
            targetProject,
            (owner, cancellationToken) => operations.MoveDescendantsAsync(
                sourceProjectId,
                normalizedSourceNodeId,
                targetProjectId,
                cancellationToken, owner),
            ProjectStructureTransferRejectionReason.DescendantsUnavailable,
            "The descendants could not be moved to the new subproject.",
            cancellationToken, mutationOwner: mutationOwner, authorization: authorization);
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

    public Task<ProjectStructureCreatedSubprojectTransferResult> MoveNodesToNewSubprojectAsync(Guid sourceProjectId, Guid targetProjectId,
        ProjectEditorModel targetProject, IReadOnlyCollection<string> sourceNodeIds, bool includeDescendants,
        CancellationToken cancellationToken = default, ProjectStructureAgentContext? mutationOwner = null, ProjectMutationAuthorization? authorization = null)
        => MoveNodesToNewSubprojectCoreAsync(sourceProjectId, targetProjectId, null, targetProject, sourceNodeIds, includeDescendants, cancellationToken, mutationOwner, authorization);

    public Task<ProjectStructureCreatedSubprojectTransferResult> MoveNodesToNewSubprojectAsync(Guid sourceProjectId, ProjectCreationReservation reservation,
        ProjectEditorModel targetProject, IReadOnlyCollection<string> sourceNodeIds, bool includeDescendants,
        CancellationToken cancellationToken = default, ProjectStructureAgentContext? mutationOwner = null, ProjectMutationAuthorization? authorization = null) {
        ArgumentNullException.ThrowIfNull(reservation);
        return MoveNodesToNewSubprojectCoreAsync(sourceProjectId, reservation.ProjectId, reservation, targetProject, sourceNodeIds, includeDescendants, cancellationToken, mutationOwner, authorization);
    }

    private Task<ProjectStructureCreatedSubprojectTransferResult> MoveNodesToNewSubprojectCoreAsync(Guid sourceProjectId, Guid targetProjectId,
        ProjectCreationReservation? reservation, ProjectEditorModel targetProject, IReadOnlyCollection<string> sourceNodeIds,
        bool includeDescendants, CancellationToken cancellationToken, ProjectStructureAgentContext? mutationOwner,
        ProjectMutationAuthorization? authorization) {
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
            (owner, cancellationToken) => operations.MoveNodesAsync(
                sourceProjectId,
                normalizedSourceNodeIds,
                targetProjectId,
                includeDescendants,
                cancellationToken, owner),
            ProjectStructureTransferRejectionReason.SelectedNodesUnavailable,
            "The selected nodes could not be moved to the new subproject.",
            cancellationToken,
            reservation, mutationOwner, authorization);
    }

    private async Task<ProjectStructureCreatedSubprojectTransferResult> ExecuteAsync(
        Guid sourceProjectId,
        Guid targetProjectId,
        ProjectEditorModel targetProject,
        Func<ProjectStructureAgentContext?, CancellationToken, Task<ProjectStructureSubprojectTransferResult?>> transferAsync,
        ProjectStructureTransferRejectionReason unavailableReason,
        string unavailableMessage,
        CancellationToken cancellationToken,
        ProjectCreationReservation? reservation = null, ProjectStructureAgentContext? mutationOwner = null,
        ProjectMutationAuthorization? authorization = null)
    {
        ValidateProjectIds(sourceProjectId, targetProjectId);
        ArgumentNullException.ThrowIfNull(targetProject);
        ArgumentNullException.ThrowIfNull(transferAsync);

        ProjectCreationReceipt? creationReceipt = null;
        try
        {
            var createResult = await operations.CreateSubprojectAsync(sourceProjectId, targetProjectId,
                reservation, targetProject, cancellationToken, authorization);
            if (createResult.IsFailure) {
                ProjectStructureProjectCreationResult.ThrowIfRejected(Result<Guid>.Failure(createResult.Errors), "The subproject could not be created.");
            }
            creationReceipt = createResult.Value ?? throw new InvalidOperationException("The project owner did not return its original creation receipt.");
            if (creationReceipt.Project.ProjectId != targetProjectId) {
                throw new InvalidOperationException($"Subproject creation returned '{creationReceipt.Project.ProjectId:D}' instead of reserved id '{targetProjectId:D}'.");
            }
            if (mutationOwner is not null && (mutationOwner.ExpectedProjectAdmission is not null ||
                    mutationOwner.AgentMutationAdmission is not null || mutationOwner.ProcessMutationAdmission is not null)) {
                var sourceAdmission = ProjectAssignmentAdmission.Require(sourceProjectId, mutationOwner.ExpectedProjectAdmission);
                mutationOwner = mutationOwner with {
                    ExpectedProjectAdmissions = [sourceAdmission, creationReceipt.Project],
                    AgentMutationAdmission = reservation is null ? mutationOwner.AgentMutationAdmission :
                        mutationOwner.AgentMutationAdmission?.WithCreatedProject(reservation),
                    ProcessMutationAdmission = reservation is null ? mutationOwner.ProcessMutationAdmission :
                        mutationOwner.ProcessMutationAdmission?.WithCreatedProject(reservation)
                };
            }
            var transfer = await transferAsync(mutationOwner, cancellationToken);
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
            if (creationReceipt is null) {
                if (ProjectStructureExceptionGraph.TryFind(transferFailure,
                        (ProjectStructureProjectCreationRejectedException _) => true, out _)) {
                    throw;
                }
                throw ProjectCreationPartialCompletionFailure.Create(targetProjectId, false, transferFailure);
            }

            var emptyChildRemoved = await CompensateEmptyCreatedSubprojectAsync(
                creationReceipt,
                transferFailure);
            if (emptyChildRemoved)
            {
                throw new ProjectStructureCompensatedSubprojectTransferException(
                    targetProjectId,
                    transferFailure);
            }

            throw ProjectCreationPartialCompletionFailure.Create(targetProjectId, true, transferFailure);
        }
    }

    private Guid CreateTargetProjectId()
    {
        var targetProjectId = projectIdFactory();
        return targetProjectId == Guid.Empty
            ? throw new InvalidOperationException("The project id factory returned an empty id.")
            : targetProjectId;
    }

    private async Task<bool> CompensateEmptyCreatedSubprojectAsync(ProjectCreationReceipt receipt, Exception transferFailure) {
        try {
            return await operations.TryCompensateCreationAsync(receipt, CancellationToken.None);
        } catch (Exception compensationFailure) {
            throw ProjectCreationPartialCompletionFailure.Create(receipt.Project.ProjectId, true, new AggregateException(
                $"Node transfer failed and the original creation of subproject '{receipt.Project.ProjectId:D}' could not be compensated.",
                transferFailure, compensationFailure));
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
    Func<Guid, Guid, ProjectCreationReservation?, ProjectEditorModel, CancellationToken, ProjectMutationAuthorization?, Task<Result<ProjectCreationReceipt>>> CreateSubprojectAsync,
    Func<Guid, string, Guid, CancellationToken, ProjectStructureAgentContext?, Task<ProjectStructureSubprojectTransferResult?>> MoveDescendantsAsync,
    Func<Guid, IReadOnlyCollection<string>, Guid, bool, CancellationToken, ProjectStructureAgentContext?, Task<ProjectStructureSubprojectTransferResult?>> MoveNodesAsync,
    Func<ProjectCreationReceipt, CancellationToken, Task<bool>> TryCompensateCreationAsync) {
    public static ProjectStructureSubprojectTransferOperations Create(ProjectsService projects, ProjectWorkbenchService workbench) {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(workbench);
        return new(
            (parentId, projectId, reservation, editor, cancellationToken, authorization) => reservation is null
                ? projects.CreateWithReceiptAsync(projectId, editor, parentId, cancellationToken, authorization)
                : projects.CreateWithReceiptAsync(reservation, editor, cancellationToken, authorization),
            workbench.MoveDescendantsToProjectAsync, workbench.MoveNodesToProjectAsync, projects.TryCompensateCreationAsync);
    }
}
