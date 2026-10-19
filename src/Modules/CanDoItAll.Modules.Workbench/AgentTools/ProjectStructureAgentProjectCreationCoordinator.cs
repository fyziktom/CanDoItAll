using System.Diagnostics;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureAgentProjectCreationCoordinator(
    ProjectStructureAgentAuthorizationService authorizationService,
    ProjectWriteAdmissionService writeAdmissionService,
    ProjectReservationCancellationOwner cancellationOwner,
    ProjectCreationCompensationReader compensationReader,
    ProjectDeletionCompletionProjection completionProjection,
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
        Action<ProjectCreationReservation>? retainLifetimeAccessForSession = null,
        ProjectMutationAuthorization? authorization = null)
    {
        var reservedProjectId = projectIdFactory();
        if (reservedProjectId == Guid.Empty)
        {
            throw new InvalidOperationException("The project id factory returned an empty id.");
        }

        var reservation = await writeAdmissionService.ReserveCreationAsync(
            reservedProjectId, agent.Id, Guid.NewGuid(), parentProjectId, cancellationToken, authorization);
        var cancellation = await writeAdmissionService.CaptureReservationCancellationAsync(reservation, cancellationToken);
        try {
            await writeAdmissionService.RequireCreationGrantAsync(reservation, cancellationToken);
            await authorizationService.GrantCreatedProjectAccessAsync(agent.Id, reservation, cancellationToken);
        } catch (Exception grantFailure) {
            await CancelReservedAccessOrThrowAsync(cancellation, authorization, grantFailure,
                "The original reservation grant could not be acknowledged and its cancellation needs observation.");
            throw;
        }

        try
        {
            ProjectStructureResultEvidenceScope.RecordReservation(reservation);
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
            var receipt = compensatedTransfer.CreationReceipt
                ?? throw new InvalidOperationException("Compensated creation requires its exact original receipt.", exception);
            var original = ProjectCreationCompensationRequest.FromReceipt(receipt);
            var observed = await compensationReader.ObserveAsync(original, CancellationToken.None);
            if (observed is not ProjectCreationCompensationObservation.Committed committed) {
                throw new ProjectCreationCompensationRequiresObservationException(
                    new ProjectCreationCompensationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.ReadUnavailable));
            }
            if (await completionProjection.ObserveAsync(committed.Completion, CancellationToken.None)
                    is not ProjectDeletionCompletionProjectionResult.Completed) {
                throw new ProjectCreationCompensationRequiresObservationException(
                    new ProjectCreationCompensationOutcome.Committed(original, committed.Completion, true));
            }
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
            await CancelReservedAccessOrThrowAsync(
                cancellation,
                authorization,
                exception,
                "Project creation was rejected and its reserved access grant could not be revoked.");

            throw;
        }
        catch (Exception original) {
            ProjectCreationReservationState? observed;
            try {
                observed = await writeAdmissionService.ReadCreationStateAsync(reservation, CancellationToken.None);
            } catch (Exception observationFailure) {
                throw ProjectCreationPartialCompletionFailure.Create(reservedProjectId, false,
                    new AggregateException("Creation failed and its original reservation could not be observed.", original, observationFailure));
            }
            if (observed == ProjectCreationReservationState.Reserved) {
                throw;
            }
            throw ProjectCreationPartialCompletionFailure.Create(reservedProjectId, observed == ProjectCreationReservationState.Consumed, original);
        }
    }

    private async Task CancelReservedAccessOrThrowAsync(ProjectReservationCancellationRequest original,
        ProjectMutationAuthorization? authorization, Exception originalFailure, string failureMessage) {
        try {
            var outcome = await cancellationOwner.CancelAsync(original, CancellationToken.None, authorization);
            if (outcome is not ProjectReservationCancellationOutcome.Committed committed ||
                    await completionProjection.ObserveAsync(committed.Completion, CancellationToken.None)
                        is not ProjectDeletionCompletionProjectionResult.Completed) {
                throw new ProjectReservationCancellationRequiresObservationException(outcome);
            }
        } catch (Exception cleanupFailure) {
            throw new AggregateException(failureMessage, originalFailure, cleanupFailure);
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
