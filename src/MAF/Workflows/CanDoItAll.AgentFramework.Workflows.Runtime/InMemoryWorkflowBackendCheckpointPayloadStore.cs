using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal enum InMemoryWorkflowCheckpointRequestLinkOutcome
{
    Linked,
    AlreadyLinked,
    CheckpointNotFound,
    SessionMismatch,
    PayloadHashMismatch,
    LinkConflict
}

internal sealed record InMemoryWorkflowCheckpointRequestLinkPlan(
    WorkflowBackendCheckpointPayloadRecord? LinkedCheckpoint);

public sealed class InMemoryWorkflowBackendCheckpointPayloadStore(
    TimeProvider timeProvider) : IWorkflowBackendCheckpointPayloadStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<WorkflowBackendCheckpointId, WorkflowBackendCheckpointPayloadRecord> checkpoints = [];
    private readonly Dictionary<WorkflowBackendSessionId, SessionState> sessions = [];
    private readonly TimeProvider clock = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    internal InMemoryWorkflowHitlMutationCoordinator MutationCoordinator { get; } = new();

    public async Task<WorkflowBackendCheckpointCreateResult> CreateAsync(
        WorkflowBackendCheckpointCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Session);
        ArgumentNullException.ThrowIfNull(request.Payload);
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.Payload.HasValidHash)
        {
            return new WorkflowBackendCheckpointCreateResult(
                WorkflowBackendCheckpointCreateOutcome.PayloadCorrupt,
                Checkpoint: null);
        }

        if (request.Parent is { } parent && parent.SessionId != request.Session.Id)
        {
            return new WorkflowBackendCheckpointCreateResult(
                WorkflowBackendCheckpointCreateOutcome.ParentSessionMismatch,
                Checkpoint: null);
        }

        await MutationCoordinator.EnterAsync(cancellationToken);
        try
        {
            lock (gate)
            {
                if (sessions.TryGetValue(request.Session.Id, out var existingSession) &&
                    existingSession.Session != request.Session)
                {
                    return new WorkflowBackendCheckpointCreateResult(
                        WorkflowBackendCheckpointCreateOutcome.SessionMetadataMismatch,
                        Checkpoint: null);
                }

                if (request.Parent is { } parentLink &&
                    (!checkpoints.TryGetValue(parentLink.CheckpointId, out var parentCheckpoint) ||
                     parentCheckpoint.Index.Link != parentLink))
                {
                    return new WorkflowBackendCheckpointCreateResult(
                        WorkflowBackendCheckpointCreateOutcome.ParentNotFound,
                        Checkpoint: null);
                }

                var session = existingSession ?? new SessionState(request.Session);
                var checkpointId = AllocateCheckpointId();
                var commitOrdinal = session.AllocateOrdinal();
                var index = new WorkflowBackendCheckpointIndexEntry(
                    new WorkflowBackendCheckpointLink(request.Session.Id, checkpointId),
                    request.Parent,
                    commitOrdinal,
                    clock.GetUtcNow());
                var checkpoint = new WorkflowBackendCheckpointPayloadRecord(
                    request.Session,
                    index,
                    request.Payload,
                    request.ExternalRequestLink);

                session.Add(checkpoint);
                sessions[request.Session.Id] = session;
                checkpoints.Add(checkpointId, checkpoint);

                return new WorkflowBackendCheckpointCreateResult(
                    WorkflowBackendCheckpointCreateOutcome.Created,
                    checkpoint);
            }
        }
        finally
        {
            MutationCoordinator.Exit();
        }
    }

    public Task<WorkflowBackendCheckpointListResult> ListIndexAsync(
        WorkflowBackendSessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(new WorkflowBackendCheckpointListResult(
                    WorkflowBackendCheckpointListOutcome.SessionNotFound,
                    []));
            }

            var index = session.Checkpoints
                .Select(checkpoint => checkpoint.Index)
                .OrderBy(checkpoint => checkpoint.CommitOrdinal.Value)
                .ToArray();
            return Task.FromResult(new WorkflowBackendCheckpointListResult(
                WorkflowBackendCheckpointListOutcome.Found,
                index));
        }
    }

    public Task<WorkflowBackendCheckpointReadResult> ReadAsync(
        WorkflowBackendCheckpointLink link,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!checkpoints.TryGetValue(link.CheckpointId, out var checkpoint))
            {
                return Task.FromResult(new WorkflowBackendCheckpointReadResult(
                    WorkflowBackendCheckpointReadOutcome.NotFound,
                    Checkpoint: null));
            }

            if (checkpoint.Session.Id != link.SessionId)
            {
                return Task.FromResult(new WorkflowBackendCheckpointReadResult(
                    WorkflowBackendCheckpointReadOutcome.SessionMismatch,
                    Checkpoint: null));
            }

            if (!checkpoint.Payload.HasValidHash)
            {
                return Task.FromResult(new WorkflowBackendCheckpointReadResult(
                    WorkflowBackendCheckpointReadOutcome.PayloadCorrupt,
                    Checkpoint: null));
            }

            return Task.FromResult(new WorkflowBackendCheckpointReadResult(
                WorkflowBackendCheckpointReadOutcome.Found,
                checkpoint));
        }
    }

    internal InMemoryWorkflowCheckpointRequestLinkOutcome TryLinkExternalRequest(
        WorkflowExternalRequestBoundaryRecord boundary,
        WorkflowRunSnapshot run)
    {
        MutationCoordinator.Enter();
        try
        {
            var outcome = TryPrepareExternalRequestLink(boundary, run, out var plan);
            if (outcome is InMemoryWorkflowCheckpointRequestLinkOutcome.Linked && plan is not null)
            {
                ApplyExternalRequestLink(plan);
            }

            return outcome;
        }
        finally
        {
            MutationCoordinator.Exit();
        }
    }

    internal InMemoryWorkflowCheckpointRequestLinkOutcome TryPrepareExternalRequestLink(
        WorkflowExternalRequestBoundaryRecord boundary,
        WorkflowRunSnapshot run,
        out InMemoryWorkflowCheckpointRequestLinkPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(run);
        plan = null;

        var continuation = boundary.Continuation;
        if (continuation.Request.ExternalRequestId != boundary.RequestId)
        {
            return InMemoryWorkflowCheckpointRequestLinkOutcome.LinkConflict;
        }

        lock (gate)
        {
            if (!checkpoints.TryGetValue(
                    continuation.Checkpoint.CheckpointId,
                    out var checkpoint))
            {
                return InMemoryWorkflowCheckpointRequestLinkOutcome.CheckpointNotFound;
            }

            if (checkpoint.Index.Link != continuation.Checkpoint ||
                checkpoint.Session.RunId != run.RunId ||
                checkpoint.Session.WorkflowId != run.WorkflowId ||
                checkpoint.Session.WorkflowVersionId != run.VersionId ||
                checkpoint.Session.Backend != run.Backend ||
                checkpoint.Session.CompilerContractVersion != continuation.CompilerContractVersion ||
                checkpoint.Session.TopologyFingerprint != continuation.TopologyFingerprint)
            {
                return InMemoryWorkflowCheckpointRequestLinkOutcome.SessionMismatch;
            }

            if (checkpoint.Payload.Sha256 != continuation.CheckpointPayloadHash)
            {
                return InMemoryWorkflowCheckpointRequestLinkOutcome.PayloadHashMismatch;
            }

            var requestLink = continuation.Request;
            var conflictingLink = checkpoints.Values.Any(candidate =>
                candidate.Index.Link != checkpoint.Index.Link &&
                candidate.ExternalRequestLink is { } candidateLink &&
                (candidateLink.ExternalRequestId == requestLink.ExternalRequestId ||
                 (candidate.Session.Id == checkpoint.Session.Id &&
                  candidateLink.BackendRequestId == requestLink.BackendRequestId &&
                  candidateLink.BackendRequestPortId == requestLink.BackendRequestPortId)));
            if (conflictingLink)
            {
                return InMemoryWorkflowCheckpointRequestLinkOutcome.LinkConflict;
            }

            if (checkpoint.ExternalRequestLink is null)
            {
                var linked = checkpoint with { ExternalRequestLink = requestLink };
                plan = new InMemoryWorkflowCheckpointRequestLinkPlan(linked);
                return InMemoryWorkflowCheckpointRequestLinkOutcome.Linked;
            }

            return checkpoint.ExternalRequestLink == requestLink
                ? InMemoryWorkflowCheckpointRequestLinkOutcome.AlreadyLinked
                : InMemoryWorkflowCheckpointRequestLinkOutcome.LinkConflict;
        }
    }

    internal void ApplyExternalRequestLink(InMemoryWorkflowCheckpointRequestLinkPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.LinkedCheckpoint is not { } linked)
        {
            return;
        }

        lock (gate)
        {
            checkpoints[linked.Index.Link.CheckpointId] = linked;
            sessions[linked.Session.Id].Replace(linked);
        }
    }

    private WorkflowBackendCheckpointId AllocateCheckpointId()
    {
        while (true)
        {
            var candidate = WorkflowBackendCheckpointId.New();
            if (!checkpoints.ContainsKey(candidate))
            {
                return candidate;
            }
        }
    }

    private sealed class SessionState(WorkflowBackendCheckpointSession session)
    {
        private readonly List<WorkflowBackendCheckpointPayloadRecord> checkpoints = [];

        public WorkflowBackendCheckpointSession Session { get; } = session;

        public IReadOnlyList<WorkflowBackendCheckpointPayloadRecord> Checkpoints => checkpoints;

        private long NextOrdinal { get; set; }

        public WorkflowCheckpointCommitOrdinal AllocateOrdinal()
        {
            var ordinal = new WorkflowCheckpointCommitOrdinal(NextOrdinal);
            NextOrdinal = checked(NextOrdinal + 1);
            return ordinal;
        }

        public void Add(WorkflowBackendCheckpointPayloadRecord checkpoint)
        {
            checkpoints.Add(checkpoint);
        }

        public void Replace(WorkflowBackendCheckpointPayloadRecord checkpoint)
        {
            var index = checkpoints.FindIndex(candidate => candidate.Index.Link == checkpoint.Index.Link);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Workflow checkpoint '{checkpoint.Index.Link.CheckpointId}' is not part of session '{Session.Id}'.");
            }

            checkpoints[index] = checkpoint;
        }
    }
}
