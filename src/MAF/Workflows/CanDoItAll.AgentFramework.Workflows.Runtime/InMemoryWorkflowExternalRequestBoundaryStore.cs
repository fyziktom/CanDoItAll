using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record InMemoryWorkflowExternalRequestBoundaryUpsertPlan(
    WorkflowExternalRequestBoundaryRecord Boundary,
    WorkflowExternalRequestBoundarySaveResult Result,
    InMemoryWorkflowCheckpointRequestLinkPlan? CheckpointLinkPlan);

internal sealed record InMemoryWorkflowExternalRequestBoundaryPreparation(
    WorkflowExternalRequestBoundarySaveResult Result,
    InMemoryWorkflowExternalRequestBoundaryUpsertPlan? Plan)
{
    public bool Succeeded => Plan is not null;
}

public sealed class InMemoryWorkflowExternalRequestBoundaryStore : IWorkflowExternalRequestBoundaryStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<WorkflowExternalRequestId, WorkflowExternalRequestBoundaryRecord> boundaries = [];
    private readonly IWorkflowRunStore runStore;
    private readonly InMemoryWorkflowBackendCheckpointPayloadStore? checkpointPayloadStore;

    public InMemoryWorkflowExternalRequestBoundaryStore(IWorkflowRunStore runStore)
    {
        this.runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
        MutationCoordinator = new InMemoryWorkflowHitlMutationCoordinator();
    }

    public InMemoryWorkflowExternalRequestBoundaryStore(
        InMemoryWorkflowRunStore runStore,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointPayloadStore)
        : this(runStore, checkpointPayloadStore, allowCompatibilityComposition: false)
    {
    }

    internal InMemoryWorkflowExternalRequestBoundaryStore(
        IWorkflowRunStore runStore,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
        bool allowCompatibilityComposition)
    {
        _ = allowCompatibilityComposition;
        this.runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
        this.checkpointPayloadStore = checkpointPayloadStore ??
            throw new ArgumentNullException(nameof(checkpointPayloadStore));
        MutationCoordinator = checkpointPayloadStore.MutationCoordinator;
    }

    internal InMemoryWorkflowHitlMutationCoordinator MutationCoordinator { get; }

    internal IWorkflowRunStore RunStore => runStore;

    internal bool SupportsNativeCheckpointLink => checkpointPayloadStore is not null;

    public async Task<WorkflowExternalRequestBoundarySaveResult> UpsertAsync(
        WorkflowExternalRequestBoundaryRecord boundary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        await MutationCoordinator.EnterAsync(cancellationToken);
        try
        {
            var preparation = await PrepareUpsertAsync(boundary, cancellationToken);
            if (!preparation.Succeeded || preparation.Plan is null)
            {
                return preparation.Result;
            }

            ApplyUpsert(preparation.Plan);
            return preparation.Result;
        }
        finally
        {
            MutationCoordinator.Exit();
        }
    }

    internal async Task<InMemoryWorkflowExternalRequestBoundaryPreparation> PrepareUpsertAsync(
        WorkflowExternalRequestBoundaryRecord boundary,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        var request = await runStore.GetExternalRequestAsync(boundary.RequestId, cancellationToken);
        if (request is null)
        {
            return PreparationFailure(WorkflowExternalRequestBoundarySaveOutcome.RequestNotFound);
        }

        var run = await runStore.GetRunAsync(request.RunId, cancellationToken);
        return run is null
            ? PreparationFailure(WorkflowExternalRequestBoundarySaveOutcome.RequestNotFound)
            : PrepareUpsert(boundary, request, run);
    }

    internal InMemoryWorkflowExternalRequestBoundaryPreparation PrepareUpsert(
        WorkflowExternalRequestBoundaryRecord boundary,
        WorkflowExternalRequestRecord request,
        WorkflowRunSnapshot run)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(run);
        if (!WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var trustedBoundary) ||
            trustedBoundary is null ||
            !HasSameImmutableBoundary(trustedBoundary, boundary) ||
            request.RunId != run.RunId)
        {
            return PreparationFailure(WorkflowExternalRequestBoundarySaveOutcome.VersionConflict);
        }

        if (checkpointPayloadStore is null)
        {
            return PreparationFailure(
                WorkflowExternalRequestBoundarySaveOutcome.NativeCheckpointLinkUnavailable);
        }

        WorkflowExternalRequestBoundarySaveOutcome saveOutcome;
        lock (gate)
        {
            if (boundaries.TryGetValue(boundary.RequestId, out var existing))
            {
                if (existing.RequestVersion != boundary.RequestVersion ||
                    !HasSameImmutableBoundary(existing, boundary) ||
                    !CanTransition(existing.State, boundary.State))
                {
                    return new InMemoryWorkflowExternalRequestBoundaryPreparation(
                        new WorkflowExternalRequestBoundarySaveResult(
                            WorkflowExternalRequestBoundarySaveOutcome.VersionConflict,
                            existing),
                        Plan: null);
                }

                saveOutcome = WorkflowExternalRequestBoundarySaveOutcome.Updated;
            }
            else
            {
                if (boundary.State != trustedBoundary.State)
                {
                    return PreparationFailure(WorkflowExternalRequestBoundarySaveOutcome.VersionConflict);
                }

                saveOutcome = WorkflowExternalRequestBoundarySaveOutcome.Created;
            }
        }

        var linkOutcome = checkpointPayloadStore.TryPrepareExternalRequestLink(
            boundary,
            run,
            out var checkpointLinkPlan);
        if (linkOutcome is not (
            InMemoryWorkflowCheckpointRequestLinkOutcome.Linked or
            InMemoryWorkflowCheckpointRequestLinkOutcome.AlreadyLinked))
        {
            return PreparationFailure(WorkflowExternalRequestBoundarySaveOutcome.VersionConflict);
        }

        var result = new WorkflowExternalRequestBoundarySaveResult(saveOutcome, boundary);
        return new InMemoryWorkflowExternalRequestBoundaryPreparation(
            result,
            new InMemoryWorkflowExternalRequestBoundaryUpsertPlan(
                boundary,
                result,
                checkpointLinkPlan));
    }

    internal void ApplyUpsert(InMemoryWorkflowExternalRequestBoundaryUpsertPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.CheckpointLinkPlan is not null)
        {
            checkpointPayloadStore!.ApplyExternalRequestLink(plan.CheckpointLinkPlan);
        }

        lock (gate)
        {
            boundaries[plan.Boundary.RequestId] = plan.Boundary;
        }
    }

    public async Task<WorkflowExternalRequestBoundaryReadResult> ReadAsync(
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (boundaries.TryGetValue(requestId, out var boundary))
            {
                return new WorkflowExternalRequestBoundaryReadResult(
                    WorkflowExternalRequestBoundaryReadOutcome.Found,
                    boundary);
            }
        }

        var request = await runStore.GetExternalRequestAsync(requestId, cancellationToken);
        if (request is null)
        {
            return new WorkflowExternalRequestBoundaryReadResult(
                WorkflowExternalRequestBoundaryReadOutcome.RequestNotFound,
                Boundary: null);
        }

        if (!WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var reconstructed) ||
            reconstructed is null)
        {
            return new WorkflowExternalRequestBoundaryReadResult(
                WorkflowExternalRequestBoundaryReadOutcome.LegacyNonResumable,
                Boundary: null);
        }

        if (checkpointPayloadStore is null)
        {
            return new WorkflowExternalRequestBoundaryReadResult(
                WorkflowExternalRequestBoundaryReadOutcome.NativeCheckpointLinkUnavailable,
                Boundary: null);
        }

        lock (gate)
        {
            boundaries.TryAdd(requestId, reconstructed);
            return new WorkflowExternalRequestBoundaryReadResult(
                WorkflowExternalRequestBoundaryReadOutcome.Found,
                boundaries[requestId]);
        }
    }

    internal static bool HasSameImmutableBoundary(
        WorkflowExternalRequestBoundaryRecord existing,
        WorkflowExternalRequestBoundaryRecord requested)
        => existing with { State = requested.State } == requested;

    private static InMemoryWorkflowExternalRequestBoundaryPreparation PreparationFailure(
        WorkflowExternalRequestBoundarySaveOutcome outcome)
        => new(
            new WorkflowExternalRequestBoundarySaveResult(outcome, Boundary: null),
            Plan: null);

    private static bool CanTransition(
        WorkflowExternalRequestState current,
        WorkflowExternalRequestState next)
        => current == next || (current, next) switch
        {
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.ResponseClaimed) => true,
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.Responded) => true,
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.Denied) => true,
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.Superseded) => true,
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.Cancelled) => true,
            (WorkflowExternalRequestState.ResponseClaimed, WorkflowExternalRequestState.Responded) => true,
            (WorkflowExternalRequestState.ResponseClaimed, WorkflowExternalRequestState.Denied) => true,
            (WorkflowExternalRequestState.ResponseClaimed, WorkflowExternalRequestState.Cancelled) => true,
            _ => false
        };
}
