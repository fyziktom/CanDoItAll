using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed record WorkflowBackendCheckpointCreateRequest(
    WorkflowBackendCheckpointSession Session,
    WorkflowBackendCheckpointLink? Parent,
    WorkflowBackendCheckpointPayload Payload)
{
    public WorkflowBackendExternalRequestLink? ExternalRequestLink { get; init; }
}

public enum WorkflowBackendCheckpointCreateOutcome
{
    Created,
    SessionMetadataMismatch,
    ParentSessionMismatch,
    ParentNotFound,
    PayloadCorrupt
}

public sealed record WorkflowBackendCheckpointCreateResult(
    WorkflowBackendCheckpointCreateOutcome Outcome,
    WorkflowBackendCheckpointPayloadRecord? Checkpoint)
{
    public bool Succeeded => Outcome == WorkflowBackendCheckpointCreateOutcome.Created;
}

public enum WorkflowBackendCheckpointListOutcome
{
    Found,
    SessionNotFound
}

public sealed record WorkflowBackendCheckpointListResult(
    WorkflowBackendCheckpointListOutcome Outcome,
    IReadOnlyList<WorkflowBackendCheckpointIndexEntry> Checkpoints);

public enum WorkflowBackendCheckpointReadOutcome
{
    Found,
    NotFound,
    SessionMismatch,
    PayloadCorrupt
}

public sealed record WorkflowBackendCheckpointReadResult(
    WorkflowBackendCheckpointReadOutcome Outcome,
    WorkflowBackendCheckpointPayloadRecord? Checkpoint)
{
    public bool Succeeded => Outcome == WorkflowBackendCheckpointReadOutcome.Found;
}

public interface IWorkflowBackendCheckpointPayloadStore
{
    Task<WorkflowBackendCheckpointCreateResult> CreateAsync(
        WorkflowBackendCheckpointCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowBackendCheckpointListResult> ListIndexAsync(
        WorkflowBackendSessionId sessionId,
        CancellationToken cancellationToken = default);

    Task<WorkflowBackendCheckpointReadResult> ReadAsync(
        WorkflowBackendCheckpointLink link,
        CancellationToken cancellationToken = default);
}
