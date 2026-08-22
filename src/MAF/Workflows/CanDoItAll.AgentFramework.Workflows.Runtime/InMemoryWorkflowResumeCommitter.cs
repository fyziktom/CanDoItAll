using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class InMemoryWorkflowResumeCommitter(
    InMemoryWorkflowRunStore runStore,
    InMemoryWorkflowExternalRequestBoundaryStore boundaryStore,
    InMemoryWorkflowExternalResponseOperationStore operationStore,
    InMemoryWorkflowUsageObservationStore? usageStore)
{
    private readonly InMemoryWorkflowRunStore runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
    private readonly InMemoryWorkflowExternalRequestBoundaryStore boundaryStore = boundaryStore ?? throw new ArgumentNullException(nameof(boundaryStore));
    private readonly InMemoryWorkflowExternalResponseOperationStore operationStore = operationStore ?? throw new ArgumentNullException(nameof(operationStore));
    private readonly InMemoryWorkflowUsageObservationStore? usageStore = usageStore;

    public WorkflowResumeBoundaryCommitResult TryCommit(
        WorkflowResumableExternalRequestContext context,
        WorkflowResumeBoundaryCommitRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var completionRequest = new WorkflowExternalResponseOperationCompletionRequest(
            context.Operation.Id,
            context.Operation.ConcurrencyVersion,
            request.LeaseOwnerId,
            request.LeaseEpoch,
            request.FinalResult,
            request.CommittedAtUtc);
        var completion = operationStore.TryPrepareCompletion(completionRequest, out var completionPlan);
        if (!completion.Succeeded || completion.Operation is null || completionPlan is null)
        {
            return CommitFailure(
                completion.Outcome is (
                    WorkflowExternalResponseOperationMutationOutcome.LeaseConflict or
                    WorkflowExternalResponseOperationMutationOutcome.LeaseExpired)
                    ? WorkflowResumeBoundaryCommitOutcome.LeaseConflict
                    : WorkflowResumeBoundaryCommitOutcome.ConcurrencyConflict,
                completion.Operation ?? context.Operation,
                context.Run);
        }

        var requestState = request.FinalResult.State switch
        {
            WorkflowExternalResponseOperationState.Denied => WorkflowExternalRequestState.Denied,
            WorkflowExternalResponseOperationState.Cancelled => WorkflowExternalRequestState.Cancelled,
            _ => WorkflowExternalRequestState.Responded
        };
        var respondedRequest = context.Request with
        {
            ResponseJson = string.Empty,
            RespondedAtUtc = request.CommittedAtUtc,
            State = requestState
        };
        var persistedRun = request.BackendResult.Run with
        {
            CreatedAtUtc = context.Run.CreatedAtUtc,
            UpdatedAtUtc = request.CommittedAtUtc,
            TerminalAtUtc = WorkflowRuntimeTransitionRules.IsTerminal(request.BackendResult.Run.State)
                ? request.CommittedAtUtc
                : null
        };
        var boundaryPlans = new List<InMemoryWorkflowExternalRequestBoundaryUpsertPlan>();
        var sourceBoundary = boundaryStore.PrepareUpsert(
            context.Boundary with { State = requestState },
            respondedRequest,
            persistedRun);
        if (!TryAddBoundaryPlan(sourceBoundary, boundaryPlans))
        {
            return CommitFailure(
                WorkflowResumeBoundaryCommitOutcome.RequestVersionConflict,
                context.Operation,
                context.Run);
        }

        foreach (var nextRequest in request.BackendResult.ExternalRequests)
        {
            if (!WorkflowExternalRequestBoundaryRecord.TryCreate(nextRequest, out var nextBoundary) ||
                nextBoundary is null ||
                !TryAddBoundaryPlan(
                    boundaryStore.PrepareUpsert(nextBoundary, nextRequest, persistedRun),
                    boundaryPlans))
            {
                return CommitFailure(
                    WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary,
                    context.Operation,
                    context.Run);
            }
        }

        var transitionEvent = FindTransitionEvent(request.BackendResult);
        var runPlan = new InMemoryWorkflowRunCommitPlan(
            context.Run,
            context.Request,
            respondedRequest,
            persistedRun,
            transitionEvent,
            request.BackendResult.Events
                .Where(workflowEvent => transitionEvent is null || workflowEvent.Id != transitionEvent.Id)
                .ToArray(),
            request.BackendResult.ExternalRequests.ToArray(),
            request.BackendResult.Checkpoints.ToArray(),
            request.BackendResult.Artifacts.ToArray());
        var usagePlan = usageStore is not null && request.BackendResult.UsageObservations.Count > 0
            ? usageStore.PrepareAppend(request.BackendResult.UsageObservations)
            : null;
        WorkflowRunSnapshot? committedRun = null;
        bool CommitPreparedState()
        {
            if (!runStore.TryApplyResumeCommit(runPlan, out committedRun))
            {
                return false;
            }

            foreach (var boundaryPlan in boundaryPlans)
            {
                boundaryStore.ApplyUpsert(boundaryPlan);
            }

            operationStore.ApplyCompletion(completionPlan);
            return true;
        }

        var committed = usagePlan is null
            ? CommitPreparedState()
            : usageStore!.TryCommitPrepared(usagePlan, CommitPreparedState);
        if (!committed || committedRun is null)
        {
            return CommitFailure(
                WorkflowResumeBoundaryCommitOutcome.CancellationWon,
                context.Operation,
                committedRun ?? context.Run);
        }

        return new WorkflowResumeBoundaryCommitResult(
            WorkflowResumeBoundaryCommitOutcome.Committed,
            completionPlan.Operation,
            committedRun,
            request.BackendResult.ExternalRequests.SingleOrDefault());
    }

    private static bool TryAddBoundaryPlan(
        InMemoryWorkflowExternalRequestBoundaryPreparation preparation,
        ICollection<InMemoryWorkflowExternalRequestBoundaryUpsertPlan> plans)
    {
        if (!preparation.Succeeded || preparation.Plan is null)
        {
            return false;
        }

        plans.Add(preparation.Plan);
        return true;
    }

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

    private static WorkflowResumeBoundaryCommitResult CommitFailure(
        WorkflowResumeBoundaryCommitOutcome outcome,
        WorkflowExternalResponseOperationRecord? operation,
        WorkflowRunSnapshot? run)
        => new(outcome, operation, run, NextRequest: null);
}
