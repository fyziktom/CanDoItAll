using System.Diagnostics;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureAgentProjectCreationCoordinator(
    ProjectStructureAgentAuthorizationService authorizationService,
    ProjectWriteAdmissionService writeAdmissionService,
    Func<Guid>? projectIdFactory = null)
{
    private readonly Func<Guid> projectIdFactory = projectIdFactory ?? Guid.NewGuid;

    public async Task<T> CreateAsync<T>(
        AgentDefinition agent,
        Func<ProjectCreationReservation, CancellationToken, Task<T>> create,
        Func<T, Guid> projectIdSelector,
        CancellationToken cancellationToken,
        Action<Guid>? retainProjectAccessForSession = null,
        Guid? parentProjectId = null,
        Action<ProjectCreationReservation>? retainLifetimeAccessForSession = null)
    {
        var reservedProjectId = projectIdFactory();
        if (reservedProjectId == Guid.Empty)
        {
            throw new InvalidOperationException("The project id factory returned an empty id.");
        }

        var reservation = await writeAdmissionService.ReserveCreationAsync(
            reservedProjectId, agent.Id, Guid.NewGuid(), parentProjectId, cancellationToken);
        await writeAdmissionService.RequireCreationGrantAsync(reservation, cancellationToken);
        await authorizationService.GrantCreatedProjectAccessAsync(
            agent.Id,
            reservation,
            cancellationToken);

        try
        {
            var result = await create(reservation, cancellationToken);
            var createdProjectId = projectIdSelector(result);
            if (createdProjectId != reservedProjectId)
            {
                throw new InvalidOperationException(
                    $"The project creation operation returned '{createdProjectId:D}' instead of reserved id '{reservedProjectId:D}'.");
            }

            retainLifetimeAccessForSession?.Invoke(reservation);
            return result;
        }
        catch (Exception exception) when (ProjectStructureExceptionGraph.TryFind(
            exception,
            (ProjectStructureCompensatedSubprojectTransferException candidate) =>
                candidate.RemovedProjectId == reservedProjectId,
            out var compensatedTransfer))
        {
            var failureToSurface = ReferenceEquals(exception, compensatedTransfer)
                ? compensatedTransfer.TransferFailure
                : exception;
            await RevokeReservedAccessOrThrowAsync(
                agent.Id,
                reservation,
                failureToSurface,
                "The empty subproject was removed after transfer failure, but its reserved access grant could not be revoked.");
            ExceptionDispatchInfo.Capture(failureToSurface).Throw();
            throw new UnreachableException();
        }
        catch (Exception exception) when (ProjectStructureExceptionGraph.TryFind(
            exception,
            (ProjectStructureTransferPartialCommitException candidate) =>
                candidate.Recovery.TargetProjectId == reservedProjectId &&
                candidate.Recovery.CommitState == ProjectStructureTransferCommitState.WorkbenchCommitted,
            out _))
        {
            try
            {
                retainProjectAccessForSession?.Invoke(reservedProjectId);
                retainLifetimeAccessForSession?.Invoke(reservation);
            }
            catch (Exception sessionAccessFailure)
            {
                throw new AggregateException(
                    "The subproject transfer committed, but current-session access to the retained project could not be granted.",
                    exception,
                    sessionAccessFailure);
            }

            throw;
        }
        catch (Exception exception) when (ProjectStructureExceptionGraph.TryFind(
            exception,
            (ProjectStructureProjectCreationRejectedException _) => true,
            out _))
        {
            await RevokeReservedAccessOrThrowAsync(
                agent.Id,
                reservation,
                exception,
                "Project creation was rejected and its reserved access grant could not be revoked.");

            throw;
        }
    }

    private async Task RevokeReservedAccessOrThrowAsync(
        Guid agentId,
        ProjectCreationReservation reservation,
        Exception originalFailure,
        string compensationFailureMessage)
    {
        try
        {
            await writeAdmissionService.CancelCreationAsync(reservation, CancellationToken.None);
            await authorizationService.RevokeCreatedProjectAccessAsync(
                agentId,
                reservation,
                CancellationToken.None);
        }
        catch (Exception compensationException)
        {
            throw new AggregateException(
                compensationFailureMessage,
                originalFailure,
                compensationException);
        }
    }
}

internal static class ProjectStructureAgentCreationValidation
{
    public static void EnsureProjectRequest(ProjectStructureProjectSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProjectStructureAgentException(400, "ProjectNameRequired", "Project name is required.");
        }
    }

    public static void EnsureSubprojectRequest(
        Guid parentProjectId,
        ProjectStructureProjectSaveRequest request)
    {
        if (parentProjectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ParentProjectRequired", "A parent project id is required.");
        }

        EnsureProjectRequest(request);
    }

    public static void EnsureNodesToSubprojectRequest(
        Guid sourceProjectId,
        ProjectStructureNodesToSubprojectInput request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (sourceProjectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProjectIdRequired", "A source project id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProjectStructureAgentException(400, "SubprojectNameRequired", "A subproject name is required.");
        }

        if (!request.NodeIds.Any(nodeId => !string.IsNullOrWhiteSpace(nodeId)))
        {
            throw new ProjectStructureAgentException(400, "SelectedNodesRequired", "At least one selected project-structure node id is required.");
        }
    }
}
