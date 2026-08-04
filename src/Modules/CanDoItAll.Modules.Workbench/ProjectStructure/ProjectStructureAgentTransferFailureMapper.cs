using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureAgentTransferFailureMapper
{
    public static bool TryMap(
        Exception exception,
        out ProjectStructureAgentException mappedException)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (ProjectStructureExceptionGraph.TryFind<ProjectStructureDeletionBatchRejectedException>(
                exception,
                static _ => true,
                out var batchDeletionRejected))
        {
            mappedException = MapBatchDeletionRejection(
                batchDeletionRejected,
                exception);
            return true;
        }

        if (ProjectStructureExceptionGraph.TryFind<ProjectStructureDeletionBatchPartialCommitException>(
                exception,
                static _ => true,
                out var batchDeletionPartialCommit))
        {
            mappedException = ProjectStructureAgentException.CreateMapped(
                409,
                "ProjectStructureDeletionBatchPartialCommit",
                batchDeletionPartialCommit.Message,
                batchDeletionPartialCommit.Recovery,
                isSafeToExpose: true,
                canRetryWithCorrectedInput: false,
                exception);
            return true;
        }

        if (ProjectStructureExceptionGraph.TryFind<ProjectStructureDeletionPartialCommitException>(
                exception,
                static _ => true,
                out var deletionPartialCommit))
        {
            mappedException = ProjectStructureAgentException.CreateMapped(
                409,
                "ProjectStructureDeletionPartialCommit",
                deletionPartialCommit.Message,
                deletionPartialCommit.Recovery,
                isSafeToExpose: true,
                canRetryWithCorrectedInput: false,
                exception);
            return true;
        }

        if (ProjectStructureExceptionGraph.TryFind<ProjectStructureTransferPartialCommitException>(
                exception,
                static _ => true,
                out var partialCommit))
        {
            mappedException = ProjectStructureAgentException.CreateMapped(
                409,
                "ProjectStructureTransferPartialCommit",
                partialCommit.Message,
                partialCommit.Recovery,
                isSafeToExpose: true,
                canRetryWithCorrectedInput: false,
                exception);
            return true;
        }

        if (ProjectStructureExceptionGraph.TryFind<ProjectStructureCompensatedSubprojectTransferException>(
                exception,
                static _ => true,
                out var compensatedTransfer))
        {
            mappedException = MapCompensatedTransfer(
                compensatedTransfer,
                exception);
            return true;
        }

        if (ProjectStructureExceptionGraph.TryFind<ProjectStructureProjectCreationRejectedException>(
                exception,
                static _ => true,
                out var creationRejected))
        {
            var projectNoLongerExists = creationRejected.Errors.Any(
                static error => string.Equals(
                    error.Code,
                    ProjectErrorCodes.NotFound,
                    StringComparison.Ordinal));
            mappedException = ProjectStructureAgentException.CreateMapped(
                projectNoLongerExists ? 404 : 400,
                projectNoLongerExists ? "ProjectNotFound" : "ProjectCreationRejected",
                creationRejected.Message,
                creationRejected.Errors,
                isSafeToExpose: false,
                canRetryWithCorrectedInput: false,
                exception);
            return true;
        }

        if (ProjectStructureExceptionGraph.TryFind<ProjectStructureTransferRejectedException>(
                exception,
                static _ => true,
                out var transferRejected))
        {
            mappedException = MapTransferRejection(
                transferRejected,
                exception);
            return true;
        }

        mappedException = null!;
        return false;
    }

    private static ProjectStructureAgentException MapBatchDeletionRejection(
        ProjectStructureDeletionBatchRejectedException rejection,
        Exception outerException)
    {
        var (statusCode, errorCode) = rejection.Reason switch
        {
            ProjectStructureDeletionBatchRejectionReason.SelectedNodesRequired =>
                (400, "SelectedNodesRequired"),
            ProjectStructureDeletionBatchRejectionReason.SelectedNodesNotFound =>
                (404, "SelectedNodesNotFound"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(rejection),
                rejection.Reason,
                "Unsupported project-structure batch-deletion rejection reason.")
        };
        var details = rejection.Reason == ProjectStructureDeletionBatchRejectionReason.SelectedNodesNotFound
            ? new { requestedNodeIds = rejection.RequestedNodeIds }
            : null;

        return ProjectStructureAgentException.CreateMapped(
            statusCode,
            errorCode,
            rejection.Message,
            details,
            isSafeToExpose: false,
            canRetryWithCorrectedInput: false,
            outerException);
    }

    private static ProjectStructureAgentException MapCompensatedTransfer(
        ProjectStructureCompensatedSubprojectTransferException compensatedTransfer,
        Exception outerException)
    {
        if (compensatedTransfer.TransferFailure is ProjectStructureAgentException agentFailure)
        {
            return CopyAgentFailure(agentFailure, outerException);
        }

        if (TryMap(compensatedTransfer.TransferFailure, out var mappedFailure))
        {
            return CopyAgentFailure(mappedFailure, outerException);
        }

        return ProjectStructureAgentException.CreateMapped(
            500,
            "SubprojectTransferFailed",
            "The subproject transfer failed after child creation; the empty child was removed.",
            new { removedProjectId = compensatedTransfer.RemovedProjectId },
            isSafeToExpose: false,
            canRetryWithCorrectedInput: false,
            outerException);
    }

    private static ProjectStructureAgentException MapTransferRejection(
        ProjectStructureTransferRejectedException rejection,
        Exception outerException)
    {
        var (statusCode, errorCode) = rejection.Reason switch
        {
            ProjectStructureTransferRejectionReason.SourceProjectRequired =>
                (400, "ProjectIdRequired"),
            ProjectStructureTransferRejectionReason.TargetProjectRequired =>
                (400, "SubprojectIdRequired"),
            ProjectStructureTransferRejectionReason.TargetProjectMustDiffer =>
                (400, "TargetProjectMustDiffer"),
            ProjectStructureTransferRejectionReason.SelectedNodesRequired =>
                (400, "SelectedNodesRequired"),
            ProjectStructureTransferRejectionReason.DescendantsUnavailable =>
                (400, "DescendantsTransferUnavailable"),
            ProjectStructureTransferRejectionReason.SelectedNodesUnavailable =>
                (400, "SelectedNodesTransferUnavailable"),
            ProjectStructureTransferRejectionReason.TargetProjectMismatch =>
                (500, "SubprojectTransferTargetMismatch"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(rejection),
                rejection.Reason,
                "Unsupported project-structure transfer rejection reason.")
        };
        object? details = rejection.Reason switch
        {
            ProjectStructureTransferRejectionReason.DescendantsUnavailable or
                ProjectStructureTransferRejectionReason.SelectedNodesUnavailable => new
                {
                    rejection.SourceProjectId,
                    rejection.TargetProjectId
                },
            ProjectStructureTransferRejectionReason.TargetProjectMismatch => new
                {
                    rejection.SourceProjectId,
                    expectedTargetProjectId = rejection.TargetProjectId,
                    rejection.ActualTargetProjectId
                },
            _ => null
        };

        return ProjectStructureAgentException.CreateMapped(
            statusCode,
            errorCode,
            rejection.Message,
            details,
            isSafeToExpose: false,
            canRetryWithCorrectedInput: false,
            outerException);
    }

    private static ProjectStructureAgentException CopyAgentFailure(
        ProjectStructureAgentException failure,
        Exception outerException)
    {
        return ProjectStructureAgentException.CreateMapped(
            failure.StatusCode,
            failure.ErrorCode,
            failure.Message,
            failure.Details,
            failure.IsSafeToExpose,
            failure.CanRetryWithCorrectedInput,
            outerException);
    }
}
