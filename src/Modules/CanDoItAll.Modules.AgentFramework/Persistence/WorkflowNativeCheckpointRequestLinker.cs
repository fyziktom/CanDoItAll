using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

internal enum WorkflowNativeCheckpointRequestLinkOutcome
{
    Linked,
    AlreadyLinked,
    CheckpointNotFound,
    SessionMismatch,
    PayloadHashMismatch,
    LinkConflict
}

internal static class WorkflowNativeCheckpointRequestLinker
{
    public static async Task<WorkflowNativeCheckpointRequestLinkOutcome> LinkAsync(
        AppDbContext dbContext,
        WorkflowExternalRequestBoundaryRecord boundary,
        WorkflowRunId runId,
        WorkflowId workflowId,
        WorkflowVersionId workflowVersionId,
        WorkflowRuntimeBackendKind backend,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(boundary);
        var continuation = boundary.Continuation;
        if (continuation.Request.ExternalRequestId != boundary.RequestId)
        {
            return WorkflowNativeCheckpointRequestLinkOutcome.LinkConflict;
        }

        var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM "AgentFramework_WorkflowBackendCheckpointPayloads"
                WHERE "Id" = {continuation.Checkpoint.CheckpointId.Value}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (checkpoint is null)
        {
            return WorkflowNativeCheckpointRequestLinkOutcome.CheckpointNotFound;
        }

        var session = await dbContext.Set<WorkflowBackendCheckpointSessionEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == continuation.Checkpoint.SessionId.Value,
                cancellationToken);
        if (session is null ||
            !string.Equals(checkpoint.SessionId, session.Id, StringComparison.Ordinal) ||
            session.RunId != runId.Value ||
            session.WorkflowId != workflowId.Value ||
            session.WorkflowVersionId != workflowVersionId.Value ||
            session.Backend != (int)backend ||
            session.CompilerContractVersion != continuation.CompilerContractVersion.Value ||
            !string.Equals(
                session.TopologyFingerprint,
                continuation.TopologyFingerprint.Value,
                StringComparison.Ordinal))
        {
            return WorkflowNativeCheckpointRequestLinkOutcome.SessionMismatch;
        }

        if (!string.Equals(
            checkpoint.PayloadHash,
            continuation.CheckpointPayloadHash.Value,
            StringComparison.Ordinal))
        {
            return WorkflowNativeCheckpointRequestLinkOutcome.PayloadHashMismatch;
        }

        var hasConflictingRequestLink = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.Id != checkpoint.Id &&
                    (item.ExternalRequestId == boundary.RequestId.Value ||
                     (item.SessionId == checkpoint.SessionId &&
                      item.BackendRequestId == continuation.Request.BackendRequestId.Value &&
                      item.BackendRequestPortId == continuation.Request.BackendRequestPortId.Value)),
                cancellationToken);
        if (hasConflictingRequestLink)
        {
            return WorkflowNativeCheckpointRequestLinkOutcome.LinkConflict;
        }

        var hasNoLink = checkpoint.ExternalRequestId is null &&
            checkpoint.BackendRequestId is null &&
            checkpoint.BackendRequestPortId is null;
        if (hasNoLink)
        {
            checkpoint.ExternalRequestId = boundary.RequestId.Value;
            checkpoint.BackendRequestId = continuation.Request.BackendRequestId.Value;
            checkpoint.BackendRequestPortId = continuation.Request.BackendRequestPortId.Value;
            await dbContext.SaveChangesAsync(cancellationToken);
            return WorkflowNativeCheckpointRequestLinkOutcome.Linked;
        }

        var hasExactLink = checkpoint.ExternalRequestId == boundary.RequestId.Value &&
            string.Equals(
                checkpoint.BackendRequestId,
                continuation.Request.BackendRequestId.Value,
                StringComparison.Ordinal) &&
            string.Equals(
                checkpoint.BackendRequestPortId,
                continuation.Request.BackendRequestPortId.Value,
                StringComparison.Ordinal);
        return hasExactLink
            ? WorkflowNativeCheckpointRequestLinkOutcome.AlreadyLinked
            : WorkflowNativeCheckpointRequestLinkOutcome.LinkConflict;
    }
}
