using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowInMemoryHitlStoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T12:00:00Z");

    [Fact]
    public async Task CancellationWithoutResponseOperation_CancelsWaitingBoundary()
    {
        var fixture = await StoreFixture.CreateAsync(Now);

        var result = await fixture.ResumeBoundaryStore.TryCancelAsync(
            new WorkflowResumeBoundaryCancellationRequest(
                fixture.Run.RunId,
                fixture.InitialRequest.Id,
                fixture.InitialRequest.Version,
                Now.AddSeconds(1),
                "Cancelled before a response was submitted."));

        Assert.Equal(WorkflowResumeBoundaryCancellationOutcome.Cancelled, result.Outcome);
        Assert.Null(result.Operation);
        Assert.Equal(WorkflowRunState.Cancelled, result.Run?.State);
        Assert.Equal(WorkflowExternalRequestState.Cancelled, result.Request?.State);
        var boundary = await fixture.BoundaryStore.ReadAsync(fixture.InitialRequest.Id);
        Assert.Equal(WorkflowExternalRequestState.Cancelled, boundary.Boundary?.State);
    }

    [Fact]
    public async Task CancellationWithMismatchedRun_DoesNotMutateEitherBoundary()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var unrelatedRun = fixture.Run with
        {
            RunId = WorkflowRunId.New(),
            BackendRunId = "unrelated-session"
        };
        await fixture.RunStore.SaveRunAsync(unrelatedRun);

        var result = await fixture.ResumeBoundaryStore.TryCancelAsync(
            new WorkflowResumeBoundaryCancellationRequest(
                unrelatedRun.RunId,
                fixture.InitialRequest.Id,
                fixture.InitialRequest.Version,
                Now.AddSeconds(1),
                "This request belongs to another run."));

        Assert.Equal(WorkflowResumeBoundaryCancellationOutcome.RunNotFound, result.Outcome);
        Assert.Equal(
            WorkflowRunState.WaitingForInput,
            (await fixture.RunStore.GetRunAsync(unrelatedRun.RunId))?.State);
        Assert.Equal(
            WorkflowExternalRequestState.Pending,
            (await fixture.RunStore.GetExternalRequestAsync(fixture.InitialRequest.Id))?.State);
        Assert.Equal(
            WorkflowExternalRequestState.Pending,
            (await fixture.BoundaryStore.ReadAsync(fixture.InitialRequest.Id)).Boundary?.State);
    }

    [Fact]
    public async Task CreateAndCancel_ShareOneMutationBoundary()
    {
        BlockingBoundaryStore? blockingStore = null;
        var fixture = await StoreFixture.CreateAsync(
            Now,
            inner => blockingStore = new BlockingBoundaryStore(inner));
        Assert.NotNull(blockingStore);

        var createTask = fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "shared-gate",
            "{\"answer\":\"yes\"}",
            Now.AddSeconds(1));
        await blockingStore.ReadEntered.WaitAsync(TimeSpan.FromSeconds(5));
        var cancelTask = Task.Run(() => fixture.ResumeBoundaryStore.TryCancelAsync(
            new WorkflowResumeBoundaryCancellationRequest(
                fixture.Run.RunId,
                fixture.InitialRequest.Id,
                fixture.InitialRequest.Version,
                Now.AddSeconds(2),
                "Cancellation raced operation creation.")));

        await Task.Delay(50);
        Assert.False(cancelTask.IsCompleted);
        blockingStore.Release();

        var created = await createTask;
        var cancelled = await cancelTask;
        Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Created, created.Outcome);
        Assert.Equal(WorkflowResumeBoundaryCancellationOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(
            WorkflowExternalResponseOperationState.Cancelled,
            (await fixture.OperationStore.GetAsync(created.Operation!.Id))?.State);
        Assert.Equal(
            WorkflowRunState.Cancelled,
            (await fixture.RunStore.GetRunAsync(fixture.Run.RunId))?.State);
    }

    [Fact]
    public async Task CreateOperation_ClaimsBoundary_AndTerminalReplayPrecedesRunStateChecks()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var createRequest = fixture.CreateOperationRequest(
            fixture.InitialRequest,
            "stable-replay",
            "{\"answer\":\"yes\"}",
            Now.AddSeconds(1));
        var created = await fixture.OperationStore.CreateOrReplayAsync(createRequest);
        Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Created, created.Outcome);
        var operation = Assert.IsType<WorkflowExternalResponseOperationRecord>(created.Operation);
        Assert.Equal(
            WorkflowExternalRequestState.ResponseClaimed,
            (await fixture.BoundaryStore.ReadAsync(fixture.InitialRequest.Id)).Boundary?.State);

        var claimed = await ClaimAsync(
            fixture.OperationStore,
            operation,
            "replay-owner",
            Now.AddSeconds(2),
            Now.AddMinutes(1));
        var resuming = await MarkResumingAsync(
            fixture.OperationStore,
            claimed,
            "replay-owner",
            Now.AddSeconds(3));
        var completedRun = fixture.Run with
        {
            State = WorkflowRunState.Completed,
            Summary = "Completed.",
            UpdatedAtUtc = Now.AddSeconds(4),
            TerminalAtUtc = Now.AddSeconds(4)
        };
        var completedEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            fixture.Run.RunId,
            WorkflowEventKind.Completed,
            NodeId: null,
            "Completed.",
            "{}",
            Now.AddSeconds(4));
        var backendResult = new WorkflowBackendStartResult(
            completedRun,
            [completedEvent],
            ExternalRequests: [],
            Artifacts: []);
        var finalResult = new WorkflowExternalResponseOperationFinalResult(
            WorkflowExternalResponseOperationState.Completed,
            WorkflowExternalResponseOperationOutcomeCode.Completed,
            "The response was accepted and the workflow completed.",
            WorkflowRunState.Completed);
        var committed = await fixture.ResumeBoundaryStore.TryCommitAsync(
            new WorkflowResumeBoundaryCommitRequest(
                operation.Id,
                resuming.Operation!.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("replay-owner"),
                claimed.Claim!.Lease.Epoch,
                fixture.InitialRequest.Version,
                backendResult,
                finalResult,
                Now.AddSeconds(4)));
        Assert.Equal(WorkflowResumeBoundaryCommitOutcome.Committed, committed.Outcome);
        Assert.Equal("{\"answer\":\"yes\"}", committed.Operation?.ResponsePayload.Json);
        Assert.Equal(
            string.Empty,
            (await fixture.RunStore.GetExternalRequestAsync(fixture.InitialRequest.Id))?.ResponseJson);

        var replayed = await fixture.OperationStore.CreateOrReplayAsync(createRequest with
        {
            OperationId = WorkflowExternalResponseOperationId.New(),
            AcceptedAtUtc = Now.AddSeconds(5)
        });

        Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Replayed, replayed.Outcome);
        Assert.Equal(operation.Id, replayed.Operation?.Id);
        Assert.Equal(WorkflowExternalResponseOperationState.Completed, replayed.Operation?.State);
    }

    [Fact]
    public async Task BoundaryStore_RejectsImmutableChangesAndReverseTransitions()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var pending = (await fixture.BoundaryStore.ReadAsync(fixture.InitialRequest.Id)).Boundary!;
        var tampered = pending with
        {
            Continuation = pending.Continuation with
            {
                TopologyFingerprint = WorkflowTopologyFingerprint.Create("tampered-topology")
            }
        };

        var immutableConflict = await fixture.BoundaryStore.UpsertAsync(tampered);
        Assert.Equal(WorkflowExternalRequestBoundarySaveOutcome.VersionConflict, immutableConflict.Outcome);

        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "boundary-transition",
            "{\"answer\":\"yes\"}",
            Now.AddSeconds(1));
        Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Created, created.Outcome);
        var claimedBoundary = (await fixture.BoundaryStore.ReadAsync(fixture.InitialRequest.Id)).Boundary!;

        var reverseConflict = await fixture.BoundaryStore.UpsertAsync(
            claimedBoundary with { State = WorkflowExternalRequestState.Pending });

        Assert.Equal(WorkflowExternalRequestBoundarySaveOutcome.VersionConflict, reverseConflict.Outcome);
        Assert.Equal(
            WorkflowExternalRequestState.ResponseClaimed,
            (await fixture.BoundaryStore.ReadAsync(fixture.InitialRequest.Id)).Boundary?.State);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("payload")]
    public async Task LoadBoundary_RequestVersionOrPayloadDrift_FailsClosed(string mismatch)
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            $"load-{mismatch}",
            "{\"answer\":\"yes\"}",
            Now.AddSeconds(1));
        var operation = Assert.IsType<WorkflowExternalResponseOperationRecord>(created.Operation);
        var claimed = await ClaimAsync(
            fixture.OperationStore,
            operation,
            "load-owner",
            Now.AddSeconds(2),
            Now.AddMinutes(1));
        await MarkResumingAsync(
            fixture.OperationStore,
            claimed,
            "load-owner",
            Now.AddSeconds(3));
        var changedRequest = mismatch == "version"
            ? fixture.InitialRequest with { Version = new WorkflowExternalRequestVersion(2) }
            : fixture.InitialRequest with { RequestJson = "{\"prompt\":\"changed\"}" };
        await fixture.RunStore.SaveExternalRequestAsync(changedRequest);

        var loaded = await fixture.ResumeBoundaryStore.LoadAsync(
            new WorkflowResumeBoundaryLoadRequest(operation.Id));

        Assert.Equal(WorkflowResumeBoundaryLoadOutcome.LinkageMismatch, loaded.Outcome);
        Assert.Null(loaded.Context);
    }

    [Fact]
    public async Task ExpiredResumingLease_RecordsLegalRecoveryPath()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "expired-resuming",
            "{\"answer\":\"yes\"}",
            Now);
        var operation = Assert.IsType<WorkflowExternalResponseOperationRecord>(created.Operation);
        var claimed = await ClaimAsync(
            fixture.OperationStore,
            operation,
            "first-owner",
            Now.AddSeconds(1),
            Now.AddSeconds(10));
        var resuming = await MarkResumingAsync(
            fixture.OperationStore,
            claimed,
            "first-owner",
            Now.AddSeconds(2));

        var takeover = await fixture.OperationStore.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                operation.Id,
                resuming.Operation!.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("replacement-owner"),
                Now.AddSeconds(10),
                Now.AddSeconds(20),
                MaximumAttempts: 3));

        Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.Claimed, takeover.Outcome);
        Assert.Equal(2, takeover.Operation?.Attempt);
        Assert.Equal(2, takeover.Claim?.Lease.Epoch.Value);
        Assert.Equal(WorkflowExternalResponseOperationState.Resuming, takeover.Claim?.Recovery?.PriorState);
        Assert.Equal(
            [
                WorkflowExternalResponseOperationState.FailedRetryable,
                WorkflowExternalResponseOperationState.Claimed
            ],
            takeover.Claim?.Recovery?.TransitionPath);
    }

    [Fact]
    public async Task LeaseRenewal_RejectsExpiryAtOrBeforeRenewalTime()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "invalid-renewal",
            "{\"answer\":\"yes\"}",
            Now);
        var claimed = await ClaimAsync(
            fixture.OperationStore,
            created.Operation!,
            "renewal-owner",
            Now.AddSeconds(1),
            Now.AddMinutes(1));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fixture.OperationStore.TryRenewLeaseAsync(
                new WorkflowExternalResponseOperationLeaseRenewalRequest(
                    claimed.Operation!.Id,
                    claimed.Operation.ConcurrencyVersion,
                    new WorkflowExternalResponseLeaseOwnerId("renewal-owner"),
                    claimed.Claim!.Lease.Epoch,
                    Now.AddSeconds(2),
                    Now.AddSeconds(2))));
    }

    [Fact]
    public async Task AttemptLimitReached_BecomesStableTerminalOutcomeAndIsNotRecoverable()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "attempt-limit",
            "{\"answer\":\"yes\"}",
            Now);
        var operation = Assert.IsType<WorkflowExternalResponseOperationRecord>(created.Operation);
        var claimed = await ClaimAsync(
            fixture.OperationStore,
            operation,
            "first-owner",
            Now.AddSeconds(1),
            Now.AddSeconds(10),
            maximumAttempts: 1);
        var resuming = await MarkResumingAsync(
            fixture.OperationStore,
            claimed,
            "first-owner",
            Now.AddSeconds(2));

        var exhausted = await fixture.OperationStore.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                operation.Id,
                resuming.Operation!.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("replacement-owner"),
                Now.AddSeconds(10),
                Now.AddSeconds(20),
                MaximumAttempts: 1));
        var recoverable = await fixture.OperationStore.ListRecoverableAsync(
            Now.AddMinutes(1),
            maximumCount: 10);

        Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.AttemptLimitReached, exhausted.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, exhausted.Operation?.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.AttemptLimitReached, exhausted.Operation?.OutcomeCode);
        Assert.Equal(
            WorkflowExternalResponseOperationOutcomeCode.AttemptLimitReached,
            exhausted.Operation?.FinalResult?.OutcomeCode);
        Assert.DoesNotContain(recoverable, candidate => candidate.Id == operation.Id);
    }

    [Fact]
    public async Task ConcurrentClaimsForDifferentRequestsOnSameRun_AllowOneActiveLease()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var secondRequest = await fixture.AddRequestAsync("second", Now.AddMilliseconds(1));
        var firstOperation = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "first-request",
            "{\"answer\":1}",
            Now.AddSeconds(1));
        var secondOperation = await fixture.CreateOperationAsync(
            secondRequest,
            "second-request",
            "{\"answer\":2}",
            Now.AddSeconds(1));

        var claims = await Task.WhenAll(
            Task.Run(() => ClaimAsync(
                fixture.OperationStore,
                firstOperation.Operation!,
                "owner-a",
                Now.AddSeconds(2),
                Now.AddMinutes(1))),
            Task.Run(() => ClaimAsync(
                fixture.OperationStore,
                secondOperation.Operation!,
                "owner-b",
                Now.AddSeconds(2),
                Now.AddMinutes(1))));

        Assert.Single(claims, claim => claim.Outcome == WorkflowExternalResponseOperationClaimOutcome.Claimed);
        Assert.Single(claims, claim => claim.Outcome == WorkflowExternalResponseOperationClaimOutcome.ActiveLease);
    }

    [Fact]
    public async Task FailedTerminalOperation_DoesNotStrandWaitingRunCancellation()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "terminal-failure",
            "{\"answer\":\"yes\"}",
            Now);
        var operation = Assert.IsType<WorkflowExternalResponseOperationRecord>(created.Operation);
        var claimed = await ClaimAsync(
            fixture.OperationStore,
            operation,
            "failure-owner",
            Now.AddSeconds(1),
            Now.AddMinutes(1));
        var resuming = await MarkResumingAsync(
            fixture.OperationStore,
            claimed,
            "failure-owner",
            Now.AddSeconds(2));
        var failure = await fixture.OperationStore.TryFailAsync(
            new WorkflowExternalResponseOperationFailureRequest(
                operation.Id,
                resuming.Operation!.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("failure-owner"),
                claimed.Claim!.Lease.Epoch,
                WorkflowExternalResponseOperationState.FailedTerminal,
                WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt,
                "The checkpoint is corrupt.",
                Now.AddSeconds(3)));
        Assert.Equal(WorkflowExternalResponseOperationMutationOutcome.Updated, failure.Outcome);

        var cancelled = await fixture.ResumeBoundaryStore.TryCancelAsync(
            new WorkflowResumeBoundaryCancellationRequest(
                fixture.Run.RunId,
                fixture.InitialRequest.Id,
                fixture.InitialRequest.Version,
                Now.AddSeconds(4),
                "Cancelled after terminal response failure."));

        Assert.Equal(WorkflowResumeBoundaryCancellationOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(WorkflowRunState.Cancelled, cancelled.Run?.State);
        Assert.Equal(WorkflowExternalRequestState.Cancelled, cancelled.Request?.State);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, cancelled.Operation?.State);
        Assert.Equal(
            failure.Operation?.ConcurrencyVersion,
            (await fixture.OperationStore.GetAsync(operation.Id))?.ConcurrencyVersion);
    }

    [Fact]
    public async Task SuccessfulTerminalOperation_PreventsContradictoryWaitingRunCancellation()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "successful-terminal",
            "{\"answer\":\"yes\"}",
            Now);
        var operation = Assert.IsType<WorkflowExternalResponseOperationRecord>(created.Operation);
        var claimed = await ClaimAsync(
            fixture.OperationStore,
            operation,
            "completion-owner",
            Now.AddSeconds(1),
            Now.AddMinutes(1));
        var resuming = await MarkResumingAsync(
            fixture.OperationStore,
            claimed,
            "completion-owner",
            Now.AddSeconds(2));
        var completion = await fixture.OperationStore.TryCompleteAsync(
            new WorkflowExternalResponseOperationCompletionRequest(
                operation.Id,
                resuming.Operation!.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("completion-owner"),
                claimed.Claim!.Lease.Epoch,
                new WorkflowExternalResponseOperationFinalResult(
                    WorkflowExternalResponseOperationState.Completed,
                    WorkflowExternalResponseOperationOutcomeCode.Completed,
                    "The response completed.",
                    WorkflowRunState.Completed),
                Now.AddSeconds(3)));
        Assert.Equal(WorkflowExternalResponseOperationMutationOutcome.Updated, completion.Outcome);

        var cancelled = await fixture.ResumeBoundaryStore.TryCancelAsync(
            new WorkflowResumeBoundaryCancellationRequest(
                fixture.Run.RunId,
                fixture.InitialRequest.Id,
                fixture.InitialRequest.Version,
                Now.AddSeconds(4),
                "Cancellation must not contradict a successful terminal response."));

        Assert.Equal(WorkflowResumeBoundaryCancellationOutcome.AlreadyTerminal, cancelled.Outcome);
        Assert.Equal(WorkflowRunState.WaitingForInput, cancelled.Run?.State);
        Assert.Equal(WorkflowExternalRequestState.Pending, cancelled.Request?.State);
        Assert.Equal(WorkflowExternalResponseOperationState.Completed, cancelled.Operation?.State);
    }

    [Fact]
    public async Task StaleCompletionPlan_DoesNotPartiallyCommitRunRequestBoundaryOrEvents()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "stale-completion",
            "{\"answer\":\"yes\"}",
            Now.AddSeconds(1));
        var claimed = await ClaimAsync(
            fixture.OperationStore,
            created.Operation!,
            "stale-owner",
            Now.AddSeconds(2),
            Now.AddMinutes(1));
        var resuming = await MarkResumingAsync(
            fixture.OperationStore,
            claimed,
            "stale-owner",
            Now.AddSeconds(3));
        var loaded = await fixture.ResumeBoundaryStore.LoadAsync(
            new WorkflowResumeBoundaryLoadRequest(resuming.Operation!.Id));
        var context = Assert.IsType<WorkflowResumableExternalRequestContext>(loaded.Context);
        var renewed = await fixture.OperationStore.TryRenewLeaseAsync(
            new WorkflowExternalResponseOperationLeaseRenewalRequest(
                resuming.Operation.Id,
                resuming.Operation.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("stale-owner"),
                claimed.Claim!.Lease.Epoch,
                Now.AddSeconds(4),
                Now.AddMinutes(2)));
        Assert.Equal(WorkflowExternalResponseOperationMutationOutcome.Updated, renewed.Outcome);

        var completedRun = fixture.Run with
        {
            State = WorkflowRunState.Completed,
            Summary = "Completed.",
            UpdatedAtUtc = Now.AddSeconds(5),
            TerminalAtUtc = Now.AddSeconds(5)
        };
        var completionEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            fixture.Run.RunId,
            WorkflowEventKind.Completed,
            NodeId: null,
            "Completed.",
            "{}",
            Now.AddSeconds(5));
        var commitRequest = new WorkflowResumeBoundaryCommitRequest(
            context.Operation.Id,
            context.Operation.ConcurrencyVersion,
            new WorkflowExternalResponseLeaseOwnerId("stale-owner"),
            claimed.Claim.Lease.Epoch,
            fixture.InitialRequest.Version,
            new WorkflowBackendStartResult(completedRun, [completionEvent], [], []),
            new WorkflowExternalResponseOperationFinalResult(
                WorkflowExternalResponseOperationState.Completed,
                WorkflowExternalResponseOperationOutcomeCode.Completed,
                "Completed.",
                WorkflowRunState.Completed),
            Now.AddSeconds(5));

        var result = new InMemoryWorkflowResumeCommitter(
            fixture.RunStore,
            fixture.NativeBoundaryStore,
            fixture.OperationStore,
            usageStore: null).TryCommit(context, commitRequest);

        Assert.Equal(WorkflowResumeBoundaryCommitOutcome.ConcurrencyConflict, result.Outcome);
        Assert.Equal(fixture.Run, await fixture.RunStore.GetRunAsync(fixture.Run.RunId));
        Assert.Equal(
            fixture.InitialRequest,
            await fixture.RunStore.GetExternalRequestAsync(fixture.InitialRequest.Id));
        Assert.Equal(
            WorkflowExternalRequestState.ResponseClaimed,
            (await fixture.NativeBoundaryStore.ReadAsync(fixture.InitialRequest.Id)).Boundary?.State);
        Assert.Equal(renewed.Operation, await fixture.OperationStore.GetAsync(context.Operation.Id));
        Assert.Empty(await fixture.RunStore.ListEventsAsync(fixture.Run.RunId));
    }

    [Fact]
    public async Task MissingNativeCheckpointForNextBoundary_DoesNotPartiallyCommitPreparedState()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var created = await fixture.CreateOperationAsync(
            fixture.InitialRequest,
            "invalid-next-boundary",
            "{\"answer\":\"yes\"}",
            Now.AddSeconds(1));
        var claimed = await ClaimAsync(
            fixture.OperationStore,
            created.Operation!,
            "next-owner",
            Now.AddSeconds(2),
            Now.AddMinutes(1));
        var resuming = await MarkResumingAsync(
            fixture.OperationStore,
            claimed,
            "next-owner",
            Now.AddSeconds(3));
        var nextRequest = StoreFixture.CreateRequest(fixture.Run, "missing-native", Now.AddSeconds(4));
        var continuation = Assert.IsType<WorkflowExternalRequestContinuation>(nextRequest.Continuation);
        var checkpoint = new WorkflowCheckpointRecord(
            WorkflowCheckpointId.New(),
            fixture.Run.RunId,
            fixture.Run.WorkflowId,
            fixture.Run.VersionId,
            fixture.Run.Backend,
            WorkflowCheckpointKind.WaitingForInput,
            WorkflowCheckpointTrustBoundary.TrustedRuntimeState,
            WorkflowResumeAvailability.Available,
            nextRequest.NodeId,
            nextRequest.Id,
            continuation.Checkpoint.CheckpointId.Value,
            "runtime://missing-native",
            continuation.CheckpointPayloadHash.Value,
            "Waiting for more input.",
            string.Empty,
            Now.AddSeconds(4),
            ResumedAtUtc: null);
        var waitingEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            fixture.Run.RunId,
            WorkflowEventKind.WaitingForInput,
            nextRequest.NodeId,
            "Waiting for more input.",
            "{}",
            Now.AddSeconds(4));
        var backendResult = new WorkflowBackendStartResult(
            fixture.Run with { Summary = "Waiting for more input." },
            [waitingEvent],
            [nextRequest],
            [])
        {
            Checkpoints = [checkpoint]
        };
        var finalResult = new WorkflowExternalResponseOperationFinalResult(
            WorkflowExternalResponseOperationState.WaitingAgain,
            WorkflowExternalResponseOperationOutcomeCode.WaitingAgain,
            "Waiting for more input.",
            WorkflowRunState.WaitingForInput)
        {
            ResultCheckpointId = checkpoint.Id,
            NextExternalRequestId = nextRequest.Id
        };

        var result = await fixture.ResumeBoundaryStore.TryCommitAsync(
            new WorkflowResumeBoundaryCommitRequest(
                resuming.Operation!.Id,
                resuming.Operation.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("next-owner"),
                claimed.Claim!.Lease.Epoch,
                fixture.InitialRequest.Version,
                backendResult,
                finalResult,
                Now.AddSeconds(4)));

        Assert.Equal(WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary, result.Outcome);
        Assert.Equal(fixture.Run, await fixture.RunStore.GetRunAsync(fixture.Run.RunId));
        Assert.Equal(
            fixture.InitialRequest,
            await fixture.RunStore.GetExternalRequestAsync(fixture.InitialRequest.Id));
        Assert.Null(await fixture.RunStore.GetExternalRequestAsync(nextRequest.Id));
        Assert.Empty(await fixture.RunStore.ListEventsAsync(fixture.Run.RunId));
        Assert.Empty(await fixture.RunStore.ListCheckpointsAsync(fixture.Run.RunId));
        Assert.Equal(
            WorkflowExternalRequestState.ResponseClaimed,
            (await fixture.NativeBoundaryStore.ReadAsync(fixture.InitialRequest.Id)).Boundary?.State);
        Assert.Equal(resuming.Operation, await fixture.OperationStore.GetAsync(resuming.Operation.Id));
    }

    [Fact]
    public async Task ConcurrentNativeBoundaryLinks_CommitExactlyOneCheckpointRequestLink()
    {
        var fixture = await StoreFixture.CreateAsync(Now);
        var first = await WorkflowHitlTestCheckpointFactory.AddCheckpointAsync(
            fixture.CheckpointStore,
            fixture.Run,
            StoreFixture.CreateRequest(fixture.Run, "link-a", Now.AddSeconds(1)),
            "{\"checkpoint\":\"link-a\"}");
        var secondSeed = StoreFixture.CreateRequest(fixture.Run, "link-b", Now.AddSeconds(2));
        var firstContinuation = Assert.IsType<WorkflowExternalRequestContinuation>(first.Continuation);
        var secondContinuation = Assert.IsType<WorkflowExternalRequestContinuation>(secondSeed.Continuation);
        var second = secondSeed with
        {
            Continuation = secondContinuation with
            {
                Checkpoint = firstContinuation.Checkpoint,
                CheckpointPayloadHash = firstContinuation.CheckpointPayloadHash
            }
        };
        await fixture.RunStore.SaveExternalRequestAsync(first);
        await fixture.RunStore.SaveExternalRequestAsync(second);
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(first, out var firstBoundary));
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(second, out var secondBoundary));

        var results = await Task.WhenAll(
            Task.Run(() => fixture.NativeBoundaryStore.UpsertAsync(firstBoundary!)),
            Task.Run(() => fixture.NativeBoundaryStore.UpsertAsync(secondBoundary!)));
        var reloaded = await fixture.CheckpointStore.ReadAsync(firstContinuation.Checkpoint);

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => !result.Succeeded);
        Assert.Equal(
            results.Single(result => result.Succeeded).Boundary?.RequestId,
            reloaded.Checkpoint?.ExternalRequestLink?.ExternalRequestId);
    }

    [Fact]
    public void PublicNativeInMemoryComposition_RequiresConcreteStagedStores()
    {
        var resumeConstructor = Assert.Single(
            typeof(InMemoryWorkflowResumeBoundaryStore).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Equal(typeof(InMemoryWorkflowRunStore), resumeConstructor.GetParameters()[0].ParameterType);
        Assert.Equal(
            typeof(InMemoryWorkflowExternalRequestBoundaryStore),
            resumeConstructor.GetParameters()[1].ParameterType);

        var nativeBoundaryConstructor = Assert.Single(
            typeof(InMemoryWorkflowExternalRequestBoundaryStore)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public),
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(InMemoryWorkflowBackendCheckpointPayloadStore)));
        Assert.Equal(typeof(InMemoryWorkflowRunStore), nativeBoundaryConstructor.GetParameters()[0].ParameterType);

        var nativeFactoryMethods = typeof(WorkflowRuntimeManager)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(WorkflowRuntimeManager.CreateInMemory))
            .Where(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(InMemoryWorkflowBackendCheckpointPayloadStore)))
            .ToArray();
        Assert.NotEmpty(nativeFactoryMethods);
        Assert.All(nativeFactoryMethods, method =>
            Assert.Equal(typeof(InMemoryWorkflowRunStore), method.GetParameters()[1].ParameterType));
    }

    private static async Task<WorkflowExternalResponseOperationClaimResult> ClaimAsync(
        InMemoryWorkflowExternalResponseOperationStore operationStore,
        WorkflowExternalResponseOperationRecord operation,
        string owner,
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        int maximumAttempts = 3)
        => await operationStore.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                operation.Id,
                operation.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId(owner),
                claimedAtUtc,
                leaseExpiresAtUtc,
                maximumAttempts));

    private static async Task<WorkflowExternalResponseOperationMutationResult> MarkResumingAsync(
        InMemoryWorkflowExternalResponseOperationStore operationStore,
        WorkflowExternalResponseOperationClaimResult claim,
        string owner,
        DateTimeOffset startedAtUtc)
        => await operationStore.TryMarkResumingAsync(
            new WorkflowExternalResponseOperationMarkResumingRequest(
                claim.Operation!.Id,
                claim.Operation.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId(owner),
                claim.Claim!.Lease.Epoch,
                startedAtUtc));

    private sealed class StoreFixture
    {
        private readonly InMemoryWorkflowExternalRequestBoundaryStore seedBoundaryStore;

        private StoreFixture(
            InMemoryWorkflowRunStore runStore,
            InMemoryWorkflowBackendCheckpointPayloadStore checkpointStore,
            InMemoryWorkflowExternalRequestBoundaryStore seedBoundaryStore,
            IWorkflowExternalRequestBoundaryStore boundaryStore,
            InMemoryWorkflowExternalResponseOperationStore operationStore,
            InMemoryWorkflowResumeBoundaryStore resumeBoundaryStore,
            WorkflowRunSnapshot run,
            WorkflowExternalRequestRecord initialRequest)
        {
            RunStore = runStore;
            CheckpointStore = checkpointStore;
            this.seedBoundaryStore = seedBoundaryStore;
            BoundaryStore = boundaryStore;
            OperationStore = operationStore;
            ResumeBoundaryStore = resumeBoundaryStore;
            Run = run;
            InitialRequest = initialRequest;
        }

        public InMemoryWorkflowRunStore RunStore { get; }

        public InMemoryWorkflowBackendCheckpointPayloadStore CheckpointStore { get; }

        public IWorkflowExternalRequestBoundaryStore BoundaryStore { get; }

        public InMemoryWorkflowExternalRequestBoundaryStore NativeBoundaryStore => seedBoundaryStore;

        public InMemoryWorkflowExternalResponseOperationStore OperationStore { get; }

        public InMemoryWorkflowResumeBoundaryStore ResumeBoundaryStore { get; }

        public WorkflowRunSnapshot Run { get; }

        public WorkflowExternalRequestRecord InitialRequest { get; }

        public static async Task<StoreFixture> CreateAsync(
            DateTimeOffset now,
            Func<IWorkflowExternalRequestBoundaryStore, IWorkflowExternalRequestBoundaryStore>? decorateBoundaryStore = null)
        {
            var runStore = new InMemoryWorkflowRunStore();
            var checkpointStore = new InMemoryWorkflowBackendCheckpointPayloadStore(
                new FixedTimeProvider(now));
            var seedBoundaryStore = new InMemoryWorkflowExternalRequestBoundaryStore(
                runStore,
                checkpointStore);
            var run = new WorkflowRunSnapshot(
                WorkflowRunId.New(),
                WorkflowId.New(),
                WorkflowVersionId.New(),
                WorkflowRunState.WaitingForInput,
                WorkflowRuntimeBackendKind.InProcess,
                "native-session",
                "Waiting for input.",
                now,
                now);
            await runStore.SaveRunAsync(run);
            var initialRequest = await WorkflowHitlTestCheckpointFactory.AddCheckpointAsync(
                checkpointStore,
                run,
                CreateRequest(run, "initial", now),
                "{\"checkpoint\":\"initial\"}");
            await runStore.SaveExternalRequestAsync(initialRequest);
            Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(initialRequest, out var boundary));
            Assert.NotNull(boundary);
            Assert.True((await seedBoundaryStore.UpsertAsync(boundary)).Succeeded);
            var boundaryStore = decorateBoundaryStore?.Invoke(seedBoundaryStore) ?? seedBoundaryStore;
            var operationStore = new InMemoryWorkflowExternalResponseOperationStore(runStore, boundaryStore);
            var resumeBoundaryStore = decorateBoundaryStore is null
                ? new InMemoryWorkflowResumeBoundaryStore(runStore, seedBoundaryStore, operationStore)
                : new InMemoryWorkflowResumeBoundaryStore(runStore, boundaryStore, operationStore);
            return new StoreFixture(
                runStore,
                checkpointStore,
                seedBoundaryStore,
                boundaryStore,
                operationStore,
                resumeBoundaryStore,
                run,
                initialRequest);
        }

        public async Task<WorkflowExternalRequestRecord> AddRequestAsync(
            string suffix,
            DateTimeOffset createdAtUtc)
        {
            var request = await WorkflowHitlTestCheckpointFactory.AddCheckpointAsync(
                CheckpointStore,
                Run,
                CreateRequest(Run, suffix, createdAtUtc),
                $"{{\"checkpoint\":\"{suffix}\"}}");
            await RunStore.SaveExternalRequestAsync(request);
            Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
            Assert.NotNull(boundary);
            Assert.True((await seedBoundaryStore.UpsertAsync(boundary)).Succeeded);
            return request;
        }

        public WorkflowExternalResponseOperationCreateRequest CreateOperationRequest(
            WorkflowExternalRequestRecord request,
            string idempotencyKey,
            string responseJson,
            DateTimeOffset acceptedAtUtc)
        {
            var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "store-test-actor");
            return new WorkflowExternalResponseOperationCreateRequest(
                WorkflowExternalResponseOperationId.New(),
                request.Id,
                Run.RunId,
                request.Version,
                WorkflowExternalResponseFingerprintFactory.Create(
                    request.Id,
                    request.Version,
                    actor,
                    WorkspaceScopeDescriptor.Project("store-test-project"),
                    WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                    new WorkflowExternalResponseIdempotencyKey(idempotencyKey),
                    responseJson),
                actor,
                new WorkflowLaunchCorrelationId($"store-test-{idempotencyKey}"),
                acceptedAtUtc);
        }

        public Task<WorkflowExternalResponseOperationCreateResult> CreateOperationAsync(
            WorkflowExternalRequestRecord request,
            string idempotencyKey,
            string responseJson,
            DateTimeOffset acceptedAtUtc)
            => OperationStore.CreateOrReplayAsync(
                CreateOperationRequest(request, idempotencyKey, responseJson, acceptedAtUtc));

        internal static WorkflowExternalRequestRecord CreateRequest(
            WorkflowRunSnapshot run,
            string suffix,
            DateTimeOffset createdAtUtc)
        {
            var requestId = WorkflowExternalRequestId.New();
            var checkpointPayloadHash = WorkflowBackendCheckpointPayloadHash.Compute(
                $"{{\"checkpoint\":\"{suffix}\"}}");
            return new WorkflowExternalRequestRecord(
                requestId,
                run.RunId,
                WorkflowExternalRequestKind.HumanInput,
                new WorkflowNodeId($"human-{suffix}"),
                "human-input",
                $"{{\"prompt\":\"{suffix}\"}}",
                string.Empty,
                createdAtUtc,
                RespondedAtUtc: null)
            {
                Version = WorkflowExternalRequestVersion.Initial,
                State = WorkflowExternalRequestState.Pending,
                ResponseContract = new WorkflowExternalResponseContract(
                    WorkflowExternalRequestKind.HumanInput,
                    "test.human-input",
                    1,
                    "{}",
                    4096),
                Continuation = new WorkflowExternalRequestContinuation(
                    new WorkflowBackendExternalRequestLink(
                        requestId,
                        new WorkflowBackendRequestId($"native-request-{suffix}"),
                        new WorkflowBackendRequestPortId($"native-port-{suffix}")),
                    new WorkflowBackendCheckpointLink(
                        new WorkflowBackendSessionId(run.BackendRunId),
                        new WorkflowBackendCheckpointId($"checkpoint-{suffix}")),
                    new WorkflowCompilerContractVersion(1),
                    WorkflowTopologyFingerprint.Create("in-memory-hitl-test-topology"),
                    checkpointPayloadHash)
            };
        }
    }

    private sealed class BlockingBoundaryStore(IWorkflowExternalRequestBoundaryStore inner) :
        IWorkflowExternalRequestBoundaryStore
    {
        private readonly TaskCompletionSource<bool> readEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int blockNextRead = 1;

        public Task ReadEntered => readEntered.Task;

        public void Release()
            => release.TrySetResult(true);

        public Task<WorkflowExternalRequestBoundarySaveResult> UpsertAsync(
            WorkflowExternalRequestBoundaryRecord boundary,
            CancellationToken cancellationToken = default)
            => inner.UpsertAsync(boundary, cancellationToken);

        public async Task<WorkflowExternalRequestBoundaryReadResult> ReadAsync(
            WorkflowExternalRequestId requestId,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref blockNextRead, 0) == 1)
            {
                readEntered.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }

            return await inner.ReadAsync(requestId, cancellationToken);
        }
    }
}
