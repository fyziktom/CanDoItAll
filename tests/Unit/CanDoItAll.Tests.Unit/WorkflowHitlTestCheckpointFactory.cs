using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal static class WorkflowHitlTestCheckpointFactory
{
    public static async Task<WorkflowExternalRequestRecord> AddCheckpointAsync(
        InMemoryWorkflowBackendCheckpointPayloadStore store,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        var continuation = request.Continuation ??
            throw new ArgumentException("A native continuation is required.", nameof(request));
        var session = new WorkflowBackendCheckpointSession(
            continuation.Checkpoint.SessionId,
            run.RunId,
            run.WorkflowId,
            run.VersionId,
            run.Backend,
            new WorkflowBackendCheckpointFormat("test-json"),
            new WorkflowBackendCheckpointFormatVersion(1),
            continuation.CompilerContractVersion,
            continuation.TopologyFingerprint);
        var created = await store.CreateAsync(new WorkflowBackendCheckpointCreateRequest(
            session,
            Parent: null,
            WorkflowBackendCheckpointPayload.Create(payloadJson)));
        if (!created.Succeeded || created.Checkpoint is null)
        {
            throw new InvalidOperationException(
                $"Test checkpoint prerequisite could not be created: {created.Outcome}.");
        }

        return request with
        {
            Continuation = continuation with
            {
                Checkpoint = created.Checkpoint.Index.Link,
                CheckpointPayloadHash = created.Checkpoint.Payload.Sha256
            }
        };
    }
}
