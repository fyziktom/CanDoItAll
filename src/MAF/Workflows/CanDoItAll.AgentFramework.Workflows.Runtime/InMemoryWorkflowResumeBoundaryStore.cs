using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowResumeBoundaryStore : IWorkflowResumeBoundaryStore
{
    private readonly IWorkflowRunStore runStore;
    private readonly IWorkflowExternalRequestBoundaryStore requestBoundaryStore;
    private readonly InMemoryWorkflowExternalResponseOperationStore operationStore;
    private readonly IWorkflowUsageObservationStore? usageStore;
    private readonly IWorkflowRedactedExternalResponseAcceptanceStore? redactedAcceptanceStore;
    private readonly InMemoryWorkflowRunStore? stagedRunStore;
    private readonly InMemoryWorkflowExternalRequestBoundaryStore? stagedBoundaryStore;
    private readonly InMemoryWorkflowUsageObservationStore? stagedUsageStore;
    private readonly InMemoryWorkflowResumeCommitter? stagedCommitter;

    public InMemoryWorkflowResumeBoundaryStore(
        InMemoryWorkflowRunStore runStore,
        InMemoryWorkflowExternalRequestBoundaryStore requestBoundaryStore,
        InMemoryWorkflowExternalResponseOperationStore operationStore,
        InMemoryWorkflowUsageObservationStore? usageStore = null)
        : this(runStore, requestBoundaryStore, operationStore, usageStore, requireStagedComposition: true)
    {
    }

    internal InMemoryWorkflowResumeBoundaryStore(
        IWorkflowRunStore runStore,
        IWorkflowExternalRequestBoundaryStore requestBoundaryStore,
        InMemoryWorkflowExternalResponseOperationStore operationStore,
        IWorkflowUsageObservationStore? usageStore = null)
        : this(runStore, requestBoundaryStore, operationStore, usageStore, requireStagedComposition: false)
    {
    }

    private InMemoryWorkflowResumeBoundaryStore(
        IWorkflowRunStore runStore,
        IWorkflowExternalRequestBoundaryStore requestBoundaryStore,
        InMemoryWorkflowExternalResponseOperationStore operationStore,
        IWorkflowUsageObservationStore? usageStore,
        bool requireStagedComposition)
    {
        this.runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
        this.requestBoundaryStore = requestBoundaryStore ??
            throw new ArgumentNullException(nameof(requestBoundaryStore));
        this.operationStore = operationStore ?? throw new ArgumentNullException(nameof(operationStore));
        this.usageStore = usageStore;
        redactedAcceptanceStore = runStore as IWorkflowRedactedExternalResponseAcceptanceStore;

        stagedRunStore = runStore as InMemoryWorkflowRunStore;
        stagedBoundaryStore = requestBoundaryStore as InMemoryWorkflowExternalRequestBoundaryStore;
        stagedUsageStore = usageStore as InMemoryWorkflowUsageObservationStore;
        var stagedComposition = stagedRunStore is not null &&
            stagedBoundaryStore is not null &&
            (usageStore is null || stagedUsageStore is not null) &&
            stagedBoundaryStore.SupportsNativeCheckpointLink &&
            ReferenceEquals(stagedBoundaryStore.RunStore, stagedRunStore) &&
            ReferenceEquals(operationStore.RunStore, stagedRunStore) &&
            ReferenceEquals(operationStore.BoundaryStore, stagedBoundaryStore) &&
            ReferenceEquals(operationStore.MutationCoordinator, stagedBoundaryStore.MutationCoordinator);
        if (requireStagedComposition && !stagedComposition)
        {
            throw new ArgumentException(
                "In-memory workflow resume composition must share one concrete run store, boundary store, and mutation coordinator.",
                nameof(operationStore));
        }

        if (!stagedComposition &&
            stagedBoundaryStore?.SupportsNativeCheckpointLink == true &&
            redactedAcceptanceStore is null)
        {
            throw new ArgumentException(
                "Native in-memory workflow resume compatibility requires metadata-only external-response acceptance.",
                nameof(runStore));
        }

        if (!stagedComposition)
        {
            stagedRunStore = null;
            stagedBoundaryStore = null;
            stagedUsageStore = null;
            return;
        }

        stagedCommitter = new InMemoryWorkflowResumeCommitter(
            stagedRunStore!,
            stagedBoundaryStore!,
            operationStore,
            stagedUsageStore);
    }

    private InMemoryWorkflowHitlMutationCoordinator MutationCoordinator => operationStore.MutationCoordinator;

    public async Task<WorkflowResumeBoundaryLoadResult> LoadAsync(
        WorkflowResumeBoundaryLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operation = await operationStore.GetAsync(request.OperationId, cancellationToken);
        if (operation is null)
        {
            return LoadFailure(WorkflowResumeBoundaryLoadOutcome.OperationNotFound);
        }

        var run = await runStore.GetRunAsync(operation.RunId, cancellationToken);
        if (run is null)
        {
            return LoadFailure(WorkflowResumeBoundaryLoadOutcome.RunNotFound);
        }

        var externalRequest = await runStore.GetExternalRequestAsync(operation.RequestId, cancellationToken);
        if (externalRequest is null)
        {
            return LoadFailure(WorkflowResumeBoundaryLoadOutcome.RequestNotFound);
        }

        var boundary = await requestBoundaryStore.ReadAsync(operation.RequestId, cancellationToken);
        if (boundary.Outcome == WorkflowExternalRequestBoundaryReadOutcome.LegacyNonResumable)
        {
            return LoadFailure(WorkflowResumeBoundaryLoadOutcome.LegacyNonResumable);
        }

        if (!boundary.Succeeded || boundary.Boundary is null)
        {
            return LoadFailure(WorkflowResumeBoundaryLoadOutcome.RequestNotFound);
        }

        var actualRequestPayloadHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(externalRequest.RequestJson)));
        var hasTrustedRequestBoundary = WorkflowExternalRequestBoundaryRecord.TryCreate(
            externalRequest,
            out var trustedBoundary) &&
            trustedBoundary is not null &&
            InMemoryWorkflowExternalRequestBoundaryStore.HasSameImmutableBoundary(
                trustedBoundary,
                boundary.Boundary);
        if (run.RunId != operation.RunId ||
            externalRequest.RunId != operation.RunId ||
            externalRequest.Id != operation.RequestId ||
            boundary.Boundary.RequestId != operation.RequestId ||
            operation.State != WorkflowExternalResponseOperationState.Resuming ||
            operation.Lease is null ||
            operation.ExpectedRequestVersion != externalRequest.Version ||
            operation.ExpectedRequestVersion != boundary.Boundary.RequestVersion ||
            boundary.Boundary.Continuation.Request.ExternalRequestId != operation.RequestId ||
            !string.Equals(
                boundary.Boundary.RequestPayloadHash.Value,
                actualRequestPayloadHash,
                StringComparison.Ordinal) ||
            !hasTrustedRequestBoundary)
        {
            return LoadFailure(WorkflowResumeBoundaryLoadOutcome.LinkageMismatch);
        }

        if (externalRequest.EffectiveState != WorkflowExternalRequestState.Pending ||
            boundary.Boundary.State != WorkflowExternalRequestState.ResponseClaimed ||
            externalRequest.RespondedAtUtc.HasValue)
        {
            return LoadFailure(WorkflowResumeBoundaryLoadOutcome.RequestNotPending);
        }

        if (run.State != WorkflowRunState.WaitingForInput)
        {
            return LoadFailure(WorkflowResumeBoundaryLoadOutcome.RunNotWaiting);
        }

        return new WorkflowResumeBoundaryLoadResult(
            WorkflowResumeBoundaryLoadOutcome.Found,
            new WorkflowResumableExternalRequestContext(
                operation,
                run,
                externalRequest,
                boundary.Boundary));
    }

    public async Task<WorkflowResumeBoundaryCommitResult> TryCommitAsync(
        WorkflowResumeBoundaryCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await MutationCoordinator.EnterAsync(cancellationToken);
        try
        {
            var loaded = await LoadAsync(
                new WorkflowResumeBoundaryLoadRequest(request.OperationId),
                cancellationToken);
            if (!loaded.Succeeded || loaded.Context is null)
            {
                return CommitFailure(MapLoadToCommit(loaded.Outcome));
            }

            var context = loaded.Context;
            if (context.Operation.ConcurrencyVersion != request.ExpectedOperationVersion)
            {
                return CommitFailure(
                    WorkflowResumeBoundaryCommitOutcome.ConcurrencyConflict,
                    context.Operation,
                    context.Run);
            }

            if (context.Operation.Lease is not { } lease ||
                lease.OwnerId != request.LeaseOwnerId ||
                lease.Epoch != request.LeaseEpoch ||
                lease.IsExpired(request.CommittedAtUtc))
            {
                return CommitFailure(
                    WorkflowResumeBoundaryCommitOutcome.LeaseConflict,
                    context.Operation,
                    context.Run);
            }

            if (context.Boundary.RequestVersion != request.ExpectedRequestVersion)
            {
                return CommitFailure(
                    WorkflowResumeBoundaryCommitOutcome.RequestVersionConflict,
                    context.Operation,
                    context.Run);
            }

            if (!IsValidResultBoundary(context, request))
            {
                return CommitFailure(
                    WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary,
                    context.Operation,
                    context.Run);
            }

            return stagedCommitter is not null
                ? stagedCommitter.TryCommit(context, request)
                : await TryCommitCompatibilityAsync(context, request, cancellationToken);
        }
        finally
        {
            MutationCoordinator.Exit();
        }
    }

    private async Task<WorkflowResumeBoundaryCommitResult> TryCommitCompatibilityAsync(
        WorkflowResumableExternalRequestContext context,
        WorkflowResumeBoundaryCommitRequest request,
        CancellationToken cancellationToken)
    {
        if (redactedAcceptanceStore is null)
        {
            return CommitFailure(
                WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary,
                context.Operation,
                context.Run);
        }

        var accepted = await redactedAcceptanceStore.TryAcceptRedactedExternalResponseAsync(
            context.Request.Id,
            request.CommittedAtUtc,
            cancellationToken);
        if (accepted.Outcome != WorkflowExternalResponseAcceptanceOutcome.Accepted || accepted.Request is null)
        {
            return CommitFailure(
                WorkflowResumeBoundaryCommitOutcome.CancellationWon,
                context.Operation,
                context.Run);
        }

        var requestState = request.FinalResult.State switch
        {
            WorkflowExternalResponseOperationState.Denied => WorkflowExternalRequestState.Denied,
            WorkflowExternalResponseOperationState.Cancelled => WorkflowExternalRequestState.Cancelled,
            _ => WorkflowExternalRequestState.Responded
        };
        var respondedRequest = accepted.Request with { State = requestState };
        var persistedRun = request.BackendResult.Run with
        {
            CreatedAtUtc = context.Run.CreatedAtUtc,
            UpdatedAtUtc = request.CommittedAtUtc,
            TerminalAtUtc = WorkflowRuntimeTransitionRules.IsTerminal(request.BackendResult.Run.State)
                ? request.CommittedAtUtc
                : null
        };
        await runStore.SaveExternalRequestAsync(respondedRequest, cancellationToken);
        var sourceBoundary = await UpsertCompatibilityBoundaryAsync(
            context.Boundary with { State = requestState },
            respondedRequest,
            persistedRun,
            cancellationToken);
        if (!sourceBoundary.Succeeded)
        {
            return CommitFailure(
                WorkflowResumeBoundaryCommitOutcome.RequestVersionConflict,
                context.Operation,
                context.Run);
        }

        foreach (var externalRequest in request.BackendResult.ExternalRequests)
        {
            await runStore.SaveExternalRequestAsync(externalRequest, cancellationToken);
            if (WorkflowExternalRequestBoundaryRecord.TryCreate(externalRequest, out var nextBoundary) &&
                nextBoundary is not null)
            {
                var saved = await UpsertCompatibilityBoundaryAsync(
                    nextBoundary,
                    externalRequest,
                    persistedRun,
                    cancellationToken);
                if (!saved.Succeeded)
                {
                    return CommitFailure(
                        WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary,
                        context.Operation,
                        context.Run);
                }
            }
        }

        foreach (var checkpoint in request.BackendResult.Checkpoints)
        {
            await runStore.SaveCheckpointAsync(checkpoint, cancellationToken);
        }

        foreach (var artifact in request.BackendResult.Artifacts)
        {
            await runStore.SaveArtifactAsync(artifact, cancellationToken);
        }

        if (usageStore is not null && request.BackendResult.UsageObservations.Count > 0)
        {
            await usageStore.AppendRangeAsync(request.BackendResult.UsageObservations, cancellationToken);
        }

        var transitionEvent = FindTransitionEvent(request.BackendResult);
        foreach (var workflowEvent in request.BackendResult.Events)
        {
            if (transitionEvent is null || workflowEvent.Id != transitionEvent.Id)
            {
                await runStore.SaveEventAsync(workflowEvent, cancellationToken);
            }
        }

        var transition = await runStore.TryTransitionRunAsync(
            context.Run.RunId,
            [WorkflowRunState.WaitingForInput],
            persistedRun,
            transitionEvent,
            cancellationToken);
        if (!transition.Transitioned || transition.Run is null)
        {
            return CommitFailure(
                WorkflowResumeBoundaryCommitOutcome.CancellationWon,
                context.Operation,
                transition.Run ?? context.Run);
        }

        var completion = operationStore.TryPrepareCompletion(
            new WorkflowExternalResponseOperationCompletionRequest(
                context.Operation.Id,
                context.Operation.ConcurrencyVersion,
                request.LeaseOwnerId,
                request.LeaseEpoch,
                request.FinalResult,
                request.CommittedAtUtc),
            out var completionPlan);
        if (!completion.Succeeded || completion.Operation is null || completionPlan is null)
        {
            return CommitFailure(
                completion.Outcome is (
                    WorkflowExternalResponseOperationMutationOutcome.LeaseConflict or
                    WorkflowExternalResponseOperationMutationOutcome.LeaseExpired)
                    ? WorkflowResumeBoundaryCommitOutcome.LeaseConflict
                    : WorkflowResumeBoundaryCommitOutcome.ConcurrencyConflict,
                completion.Operation ?? context.Operation,
                transition.Run);
        }

        operationStore.ApplyCompletion(completionPlan);
        return new WorkflowResumeBoundaryCommitResult(
            WorkflowResumeBoundaryCommitOutcome.Committed,
            completionPlan.Operation,
            transition.Run,
            request.BackendResult.ExternalRequests.SingleOrDefault());
    }

    private async Task<WorkflowExternalRequestBoundarySaveResult> UpsertCompatibilityBoundaryAsync(
        WorkflowExternalRequestBoundaryRecord boundary,
        WorkflowExternalRequestRecord externalRequest,
        WorkflowRunSnapshot run,
        CancellationToken cancellationToken)
    {
        if (requestBoundaryStore is not InMemoryWorkflowExternalRequestBoundaryStore inMemoryBoundaryStore)
        {
            return await requestBoundaryStore.UpsertAsync(boundary, cancellationToken);
        }

        var preparation = inMemoryBoundaryStore.PrepareUpsert(boundary, externalRequest, run);
        if (preparation.Succeeded && preparation.Plan is not null)
        {
            inMemoryBoundaryStore.ApplyUpsert(preparation.Plan);
        }

        return preparation.Result;
    }

    public async Task<WorkflowResumeBoundaryCancellationResult> TryCancelAsync(
        WorkflowResumeBoundaryCancellationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await MutationCoordinator.EnterAsync(cancellationToken);
        try
        {
            var run = await runStore.GetRunAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return CancellationFailure(WorkflowResumeBoundaryCancellationOutcome.RunNotFound);
            }

            if (WorkflowRuntimeTransitionRules.IsTerminal(run.State))
            {
                return CancellationFailure(
                    WorkflowResumeBoundaryCancellationOutcome.AlreadyTerminal,
                    run);
            }

            var externalRequest = await runStore.GetExternalRequestAsync(request.RequestId, cancellationToken);
            if (externalRequest is null)
            {
                return CancellationFailure(
                    WorkflowResumeBoundaryCancellationOutcome.RequestNotFound,
                    run);
            }

            if (externalRequest.RunId != request.RunId)
            {
                return CancellationFailure(
                    WorkflowResumeBoundaryCancellationOutcome.RunNotFound,
                    run,
                    externalRequest);
            }

            var boundary = await requestBoundaryStore.ReadAsync(request.RequestId, cancellationToken);
            if (boundary.Boundary is null || boundary.Boundary.RequestVersion != request.ExpectedRequestVersion)
            {
                return CancellationFailure(
                    WorkflowResumeBoundaryCancellationOutcome.RequestVersionConflict,
                    run,
                    externalRequest);
            }

            var operationCancellation = operationStore.TryPrepareCancellationForBoundary(
                request.RequestId,
                request.CancelledAtUtc,
                request.SafeReason,
                out var operationCancellationPlan);
            if (operationCancellation.Outcome == InMemoryWorkflowBoundaryCancellationOutcome.ActiveResume)
            {
                return CancellationFailure(
                    WorkflowResumeBoundaryCancellationOutcome.ActiveResume,
                    run,
                    externalRequest,
                    operationCancellation.Operation);
            }

            if (operationCancellation.Outcome == InMemoryWorkflowBoundaryCancellationOutcome.AlreadyTerminal)
            {
                return CancellationFailure(
                    WorkflowResumeBoundaryCancellationOutcome.AlreadyTerminal,
                    run,
                    externalRequest,
                    operationCancellation.Operation);
            }

            var cancelledRequest = externalRequest with
            {
                State = WorkflowExternalRequestState.Cancelled,
                RespondedAtUtc = request.CancelledAtUtc
            };
            var cancelledRun = run with
            {
                State = WorkflowRunState.Cancelled,
                Summary = request.SafeReason,
                UpdatedAtUtc = request.CancelledAtUtc,
                TerminalAtUtc = request.CancelledAtUtc
            };
            if (stagedRunStore is not null &&
                stagedBoundaryStore is not null &&
                operationCancellationPlan is not null)
            {
                var boundaryPreparation = stagedBoundaryStore.PrepareUpsert(
                    boundary.Boundary with { State = WorkflowExternalRequestState.Cancelled },
                    cancelledRequest,
                    cancelledRun);
                if (!boundaryPreparation.Succeeded || boundaryPreparation.Plan is null)
                {
                    return CancellationFailure(
                        WorkflowResumeBoundaryCancellationOutcome.RequestVersionConflict,
                        run,
                        externalRequest,
                        operationCancellation.Operation);
                }

                var runPlan = new InMemoryWorkflowRunCommitPlan(
                    run,
                    externalRequest,
                    cancelledRequest,
                    cancelledRun,
                    CreateTransitionEvent(cancelledRun, request.CancelledAtUtc),
                    Events: [],
                    NextRequests: [],
                    Checkpoints: [],
                    Artifacts: []);
                if (!stagedRunStore.TryApplyResumeCommit(runPlan, out var committedRun) || committedRun is null)
                {
                    return CancellationFailure(
                        WorkflowResumeBoundaryCancellationOutcome.ActiveResume,
                        committedRun ?? run,
                        externalRequest,
                        operationCancellation.Operation);
                }

                stagedBoundaryStore.ApplyUpsert(boundaryPreparation.Plan);
                operationStore.ApplyBoundaryCancellation(operationCancellationPlan);
                return new WorkflowResumeBoundaryCancellationResult(
                    WorkflowResumeBoundaryCancellationOutcome.Cancelled,
                    committedRun,
                    cancelledRequest,
                    operationCancellation.Operation);
            }

            operationStore.ApplyBoundaryCancellation(operationCancellationPlan!);
            await runStore.SaveExternalRequestAsync(cancelledRequest, cancellationToken);
            var savedBoundary = await UpsertCompatibilityBoundaryAsync(
                boundary.Boundary with { State = WorkflowExternalRequestState.Cancelled },
                cancelledRequest,
                cancelledRun,
                cancellationToken);
            if (!savedBoundary.Succeeded)
            {
                return CancellationFailure(
                    WorkflowResumeBoundaryCancellationOutcome.RequestVersionConflict,
                    run,
                    externalRequest,
                    operationCancellation.Operation);
            }

            var transition = await runStore.TryTransitionRunAsync(
                run.RunId,
                [WorkflowRunState.WaitingForInput],
                cancelledRun,
                CreateTransitionEvent(cancelledRun, request.CancelledAtUtc),
                cancellationToken);
            return transition.Transitioned
                ? new WorkflowResumeBoundaryCancellationResult(
                    WorkflowResumeBoundaryCancellationOutcome.Cancelled,
                    transition.Run,
                    cancelledRequest,
                    operationCancellation.Operation)
                : CancellationFailure(
                    WorkflowResumeBoundaryCancellationOutcome.ActiveResume,
                    transition.Run ?? run,
                    externalRequest,
                    operationCancellation.Operation);
        }
        finally
        {
            MutationCoordinator.Exit();
        }
    }

    private static WorkflowResumeBoundaryLoadResult LoadFailure(WorkflowResumeBoundaryLoadOutcome outcome)
        => new(outcome, Context: null);

    private static WorkflowResumeBoundaryCommitResult CommitFailure(
        WorkflowResumeBoundaryCommitOutcome outcome,
        WorkflowExternalResponseOperationRecord? operation = null,
        WorkflowRunSnapshot? run = null)
        => new(outcome, operation, run, NextRequest: null);

    private static WorkflowResumeBoundaryCancellationResult CancellationFailure(
        WorkflowResumeBoundaryCancellationOutcome outcome,
        WorkflowRunSnapshot? run = null,
        WorkflowExternalRequestRecord? request = null,
        WorkflowExternalResponseOperationRecord? operation = null)
        => new(outcome, run, request, operation);

    private static bool IsValidResultBoundary(
        WorkflowResumableExternalRequestContext context,
        WorkflowResumeBoundaryCommitRequest request)
    {
        var result = request.BackendResult;
        if (result.Run.RunId != context.Run.RunId ||
            result.Run.WorkflowId != context.Run.WorkflowId ||
            result.Run.VersionId != context.Run.VersionId ||
            request.FinalResult.ResultRunState != result.Run.State)
        {
            return false;
        }

        if (result.Events.Any(workflowEvent => workflowEvent.RunId != context.Run.RunId) ||
            result.ExternalRequests.Any(externalRequest =>
                externalRequest.RunId != context.Run.RunId ||
                externalRequest.Id == context.Request.Id ||
                externalRequest.State != WorkflowExternalRequestState.Pending ||
                !WorkflowExternalRequestBoundaryRecord.TryCreate(externalRequest, out _)) ||
            result.Checkpoints.Any(checkpoint =>
                checkpoint.RunId != context.Run.RunId ||
                checkpoint.WorkflowId != context.Run.WorkflowId ||
                checkpoint.VersionId != context.Run.VersionId) ||
            result.Artifacts.Any(artifact => artifact.RunId != context.Run.RunId) ||
            result.UsageObservations.Any(observation =>
                observation.RunId != context.Run.RunId ||
                observation.WorkflowId != context.Run.WorkflowId ||
                observation.VersionId != context.Run.VersionId))
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
        return checkpoints.Any(checkpoint =>
            checkpoint.Id == resultCheckpointId &&
            checkpoint.ExternalRequestId == externalRequest.Id &&
            string.Equals(
                checkpoint.BackendCheckpointId,
                continuation.Checkpoint.CheckpointId.Value,
                StringComparison.Ordinal) &&
            string.Equals(
                checkpoint.PayloadHash,
                continuation.CheckpointPayloadHash.Value,
                StringComparison.Ordinal));
    }

    private static WorkflowResumeBoundaryCommitOutcome MapLoadToCommit(WorkflowResumeBoundaryLoadOutcome outcome)
        => outcome switch
        {
            WorkflowResumeBoundaryLoadOutcome.OperationNotFound => WorkflowResumeBoundaryCommitOutcome.OperationNotFound,
            WorkflowResumeBoundaryLoadOutcome.RunNotFound => WorkflowResumeBoundaryCommitOutcome.RunNotFound,
            WorkflowResumeBoundaryLoadOutcome.RequestNotFound => WorkflowResumeBoundaryCommitOutcome.RequestNotFound,
            _ => WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary
        };

    private static WorkflowEventRecord? FindTransitionEvent(WorkflowBackendStartResult result)
    {
        var kind = result.Run.State switch
        {
            WorkflowRunState.WaitingForInput => WorkflowEventKind.WaitingForInput,
            WorkflowRunState.Completed => WorkflowEventKind.Completed,
            WorkflowRunState.Failed => WorkflowEventKind.Error,
            WorkflowRunState.Cancelled => WorkflowEventKind.Cancelled,
            _ => (WorkflowEventKind?)null
        };
        return kind.HasValue
            ? result.Events.LastOrDefault(workflowEvent => workflowEvent.Kind == kind.Value)
            : null;
    }

    private static WorkflowEventRecord CreateTransitionEvent(
        WorkflowRunSnapshot run,
        DateTimeOffset timestamp)
        => new(
            Guid.NewGuid(),
            run.RunId,
            WorkflowEventKind.Cancelled,
            NodeId: null,
            run.Summary,
            "{}",
            timestamp);
}
