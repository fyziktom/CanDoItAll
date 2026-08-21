using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

internal static class WorkflowResumeBoundaryPersistenceValidator
{
    public static bool IsValid(
        WorkflowExternalResponseOperationEntity operation,
        WorkflowRunRecordEntity sourceRun,
        WorkflowExternalRequestRecordEntity sourceRequest,
        WorkflowResumeBoundaryCommitRequest request)
    {
        var result = request.BackendResult;
        if (operation.RunId != sourceRun.RunId ||
            operation.RequestId != sourceRequest.Id ||
            sourceRequest.RunId != sourceRun.RunId ||
            result.Run.RunId.Value != sourceRun.RunId ||
            result.Run.WorkflowId.Value != sourceRun.WorkflowId ||
            result.Run.VersionId.Value != sourceRun.VersionId ||
            request.FinalResult.ResultRunState != result.Run.State)
        {
            return false;
        }

        if (result.Events.Any(workflowEvent => workflowEvent.RunId.Value != sourceRun.RunId) ||
            result.ExternalRequests.Any(externalRequest =>
                externalRequest.RunId.Value != sourceRun.RunId ||
                externalRequest.Id.Value == sourceRequest.Id ||
                externalRequest.State != WorkflowExternalRequestState.Pending ||
                !WorkflowExternalRequestBoundaryRecord.TryCreate(externalRequest, out _)) ||
            result.Checkpoints.Any(checkpoint =>
                checkpoint.RunId.Value != sourceRun.RunId ||
                checkpoint.WorkflowId.Value != sourceRun.WorkflowId ||
                checkpoint.VersionId.Value != sourceRun.VersionId) ||
            result.Artifacts.Any(artifact => artifact.RunId.Value != sourceRun.RunId) ||
            result.UsageObservations.Any(observation =>
                observation.RunId?.Value != sourceRun.RunId ||
                observation.WorkflowId.Value != sourceRun.WorkflowId ||
                observation.VersionId.Value != sourceRun.VersionId))
        {
            return false;
        }

        if ((request.FinalResult.ResultCheckpointId is { } resultCheckpointId &&
             !result.Checkpoints.Any(checkpoint => checkpoint.Id == resultCheckpointId)) ||
            result.Checkpoints.Any(checkpoint =>
                checkpoint.ResumeAvailability == WorkflowResumeAvailability.Available &&
                !result.ExternalRequests.Any(externalRequest =>
                    externalRequest.Id == checkpoint.ExternalRequestId &&
                    externalRequest.Continuation is { } continuation &&
                    string.Equals(
                        continuation.Checkpoint.CheckpointId.Value,
                        checkpoint.BackendCheckpointId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        continuation.CheckpointPayloadHash.Value,
                        checkpoint.PayloadHash,
                        StringComparison.Ordinal))))
        {
            return false;
        }

        return request.FinalResult.State switch
        {
            WorkflowExternalResponseOperationState.WaitingAgain =>
                result.Run.State == WorkflowRunState.WaitingForInput &&
                result.ExternalRequests.Count == 1 &&
                request.FinalResult.NextExternalRequestId == result.ExternalRequests[0].Id &&
                HasLinkedCheckpoint(
                    result.ExternalRequests[0],
                    request.FinalResult.ResultCheckpointId,
                    result.Checkpoints),
            WorkflowExternalResponseOperationState.Completed =>
                result.Run.State == WorkflowRunState.Completed &&
                result.ExternalRequests.Count == 0 &&
                request.FinalResult.NextExternalRequestId is null,
            WorkflowExternalResponseOperationState.Denied =>
                result.Run.State is WorkflowRunState.Completed or WorkflowRunState.Cancelled &&
                result.ExternalRequests.Count == 0 &&
                request.FinalResult.NextExternalRequestId is null,
            WorkflowExternalResponseOperationState.FailedTerminal =>
                result.Run.State == WorkflowRunState.Failed &&
                result.ExternalRequests.Count == 0 &&
                request.FinalResult.NextExternalRequestId is null,
            WorkflowExternalResponseOperationState.Cancelled =>
                result.Run.State == WorkflowRunState.Cancelled &&
                result.ExternalRequests.Count == 0 &&
                request.FinalResult.NextExternalRequestId is null,
            _ => false
        };
    }

    private static bool HasLinkedCheckpoint(
        WorkflowExternalRequestRecord externalRequest,
        WorkflowCheckpointId? resultCheckpointId,
        IReadOnlyList<WorkflowCheckpointRecord> checkpoints)
    {
        var continuation = externalRequest.Continuation!;
        return checkpoints.Count(checkpoint =>
            checkpoint.Id == resultCheckpointId &&
            checkpoint.ResumeAvailability == WorkflowResumeAvailability.Available &&
            checkpoint.ExternalRequestId == externalRequest.Id &&
            string.Equals(
                checkpoint.BackendCheckpointId,
                continuation.Checkpoint.CheckpointId.Value,
                StringComparison.Ordinal) &&
            string.Equals(
                checkpoint.PayloadHash,
                continuation.CheckpointPayloadHash.Value,
                StringComparison.Ordinal)) == 1 &&
            checkpoints.Count(checkpoint =>
                checkpoint.ResumeAvailability == WorkflowResumeAvailability.Available &&
                checkpoint.ExternalRequestId == externalRequest.Id) == 1;
    }
}
