using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseContinuationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T12:00:00Z");

    [Fact]
    public async Task CompletedOperation_ReplaysWithoutRepeatingParticipatingEffect()
    {
        var fixture = await Fixture.CreateAsync(Now);

        var first = await fixture.ContinueAsync("host-a");
        var replay = await fixture.ContinueAsync("host-b");

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Completed, first.Outcome);
        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Replayed, replay.Outcome);
        Assert.Equal(1, fixture.Backend.SideEffectCount);
        Assert.Equal(WorkflowExternalResponseOperationState.Completed, replay.Operation?.State);
        Assert.Equal(WorkflowRunState.Completed, (await fixture.RunStore.GetRunAsync(fixture.Run.RunId))?.State);
    }

    [Fact]
    public async Task CrashAfterAcceptance_LeavesRecoverableOperationThatNewOwnerCompletes()
    {
        var fixture = await Fixture.CreateAsync(Now);
        var crashing = fixture.CreateContinuation(
            new FixedTimeProvider(Now),
            CrashAt(WorkflowExternalResponseRecoveryPoint.AcceptedBeforeClaim));

        await Assert.ThrowsAsync<InjectedRecoveryCrashException>(() =>
            crashing.ContinueAsync(
                new WorkflowExternalResponseContinuationRequest(
                    fixture.Operation.Id,
                    new WorkflowExternalResponseLeaseOwnerId("crashed-host"))));

        Assert.Equal(
            WorkflowExternalResponseOperationState.Accepted,
            (await fixture.OperationStore.GetAsync(fixture.Operation.Id))?.State);

        var recovered = await fixture.ContinueAsync("replacement-host", Now.AddMinutes(3));

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Completed, recovered.Outcome);
        Assert.Equal(1, fixture.Backend.SideEffectCount);
    }

    [Fact]
    public async Task CrashAfterClaim_RequiresLeaseExpiryBeforeTakeover()
    {
        var fixture = await Fixture.CreateAsync(Now);
        var crashing = fixture.CreateContinuation(
            new FixedTimeProvider(Now),
            CrashAt(WorkflowExternalResponseRecoveryPoint.ClaimedBeforeResponseDelivery));

        await Assert.ThrowsAsync<InjectedRecoveryCrashException>(() =>
            crashing.ContinueAsync(
                new WorkflowExternalResponseContinuationRequest(
                    fixture.Operation.Id,
                    new WorkflowExternalResponseLeaseOwnerId("crashed-host"))));

        var activeConflict = await fixture.ContinueAsync("early-host", Now.AddMinutes(1));
        var recovered = await fixture.ContinueAsync("replacement-host", Now.AddMinutes(3));

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.ClaimConflict, activeConflict.Outcome);
        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Completed, recovered.Outcome);
        Assert.Equal(1, fixture.Backend.SideEffectCount);
        Assert.Equal(2, recovered.Operation?.Attempt);
    }

    [Fact]
    public async Task CrashAfterResponseDelivery_ReplaysWithSameCausationKeyAndDoesNotRepeatEffect()
    {
        var fixture = await Fixture.CreateAsync(Now);
        var crashing = fixture.CreateContinuation(
            new FixedTimeProvider(Now),
            CrashAt(WorkflowExternalResponseRecoveryPoint.ResponseDeliveredBeforeCommit));

        await Assert.ThrowsAsync<InjectedRecoveryCrashException>(() =>
            crashing.ContinueAsync(
                new WorkflowExternalResponseContinuationRequest(
                    fixture.Operation.Id,
                    new WorkflowExternalResponseLeaseOwnerId("crashed-host"))));

        Assert.Equal(1, fixture.Backend.SideEffectCount);
        Assert.Equal(
            WorkflowExternalResponseOperationState.Resuming,
            (await fixture.OperationStore.GetAsync(fixture.Operation.Id))?.State);

        var recovered = await fixture.ContinueAsync("replacement-host", Now.AddMinutes(3));

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Completed, recovered.Outcome);
        Assert.Equal(2, fixture.Backend.ResumeCount);
        Assert.Equal(1, fixture.Backend.SideEffectCount);
    }

    [Fact]
    public async Task CancellationBeforeClaim_WinsWithoutCallingBackend()
    {
        var fixture = await Fixture.CreateAsync(Now);

        var cancelled = await fixture.ResumeBoundaryStore.TryCancelAsync(
            new WorkflowResumeBoundaryCancellationRequest(
                fixture.Run.RunId,
                fixture.Request.Id,
                fixture.Request.Version,
                Now.AddSeconds(1),
                "Cancelled by test."));

        Assert.Equal(WorkflowResumeBoundaryCancellationOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(WorkflowRunState.Cancelled, cancelled.Run?.State);
        Assert.Equal(WorkflowExternalRequestState.Cancelled, cancelled.Request?.State);
        Assert.Equal(WorkflowExternalResponseOperationState.Cancelled, cancelled.Operation?.State);
        Assert.Equal(0, fixture.Backend.ResumeCount);
    }

    [Fact]
    public async Task CallerCancellationAfterClaim_DoesNotCancelDurableContinuation()
    {
        var fixture = await Fixture.CreateAsync(Now);
        fixture.Backend.BlockOnResume = true;
        var activeRuns = new WorkflowActiveRunRegistry();
        var continuation = fixture.CreateContinuation(
            new FixedTimeProvider(Now),
            activeRuns: activeRuns);
        using var callerCancellation = new CancellationTokenSource();

        var continuationTask = continuation.ContinueAsync(
            new WorkflowExternalResponseContinuationRequest(
                fixture.Operation.Id,
                new WorkflowExternalResponseLeaseOwnerId("host-a")),
            callerCancellation.Token);
        await fixture.Backend.WaitUntilEnteredAsync();

        callerCancellation.Cancel();
        fixture.Backend.Release();
        var result = await continuationTask;

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Completed, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.Completed, result.Operation?.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.Completed, result.Operation?.OutcomeCode);
    }

    [Fact]
    public async Task ExplicitActiveRunCancellation_WinsAndPersistsCancelled()
    {
        var fixture = await Fixture.CreateAsync(Now);
        fixture.Backend.BlockOnResume = true;
        var activeRuns = new WorkflowActiveRunRegistry();
        var continuation = fixture.CreateContinuation(
            new FixedTimeProvider(Now),
            activeRuns: activeRuns);

        var continuationTask = continuation.ContinueAsync(
            new WorkflowExternalResponseContinuationRequest(
                fixture.Operation.Id,
                new WorkflowExternalResponseLeaseOwnerId("host-a")));
        await fixture.Backend.WaitUntilEnteredAsync();

        Assert.Equal(
            WorkflowActiveRunCancellationSignal.Signalled,
            activeRuns.TrySignalCancellation(fixture.Run.RunId));
        var result = await continuationTask;

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Cancelled, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.Cancelled, result.Operation?.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.Cancelled, result.Operation?.OutcomeCode);
        Assert.Equal(0, fixture.Backend.SideEffectCount);
    }

    [Fact]
    public async Task ExpiredLeaseTakeoverDuringPreparationPreventsOldOwnerFromCallingBackend()
    {
        var fixture = await Fixture.CreateAsync(Now);
        var clock = new ManualTimeProvider(Now);
        var replacementOwner = new WorkflowExternalResponseLeaseOwnerId("replacement-host");
        var operationStore = new TakeoverOnRenewalOperationStore(
            fixture.OperationStore,
            replacementOwner);
        WorkflowExternalResponseRecoveryHook delayedPreparation = async (point, _, _) =>
        {
            if (point != WorkflowExternalResponseRecoveryPoint.ClaimedBeforeResponseDelivery)
            {
                return;
            }

            clock.Advance(
                WorkflowExternalResponseContinuation.DefaultLeaseDuration +
                TimeSpan.FromSeconds(1));
            var takeover = await operationStore.WaitForTakeoverAsync();
            Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.Claimed, takeover.Outcome);
        };
        var continuation = fixture.CreateContinuation(
            clock,
            delayedPreparation,
            operationStore: operationStore);

        var result = await continuation.ContinueAsync(
            new WorkflowExternalResponseContinuationRequest(
                fixture.Operation.Id,
                new WorkflowExternalResponseLeaseOwnerId("old-host")));
        var current = await fixture.OperationStore.GetAsync(fixture.Operation.Id);

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.ClaimConflict, result.Outcome);
        Assert.Equal(0, fixture.Backend.ResumeCount);
        Assert.Equal(replacementOwner, current?.Lease?.OwnerId);
        Assert.Equal(WorkflowExternalResponseOperationState.Claimed, current?.State);
    }

    [Fact]
    public async Task ExpiredLeaseTakeoverAfterDeliveryPreventsStaleCommitWithoutRepeatingEffect()
    {
        var fixture = await Fixture.CreateAsync(Now);
        var clock = new ManualTimeProvider(Now);
        var replacementOwner = new WorkflowExternalResponseLeaseOwnerId("replacement-host");
        var operationStore = new TakeoverOnRenewalOperationStore(
            fixture.OperationStore,
            replacementOwner);
        WorkflowExternalResponseRecoveryHook delayedFinalization = async (point, _, _) =>
        {
            if (point != WorkflowExternalResponseRecoveryPoint.ResponseDeliveredBeforeCommit)
            {
                return;
            }

            clock.Advance(
                WorkflowExternalResponseContinuation.DefaultLeaseDuration +
                TimeSpan.FromSeconds(1));
            var takeover = await operationStore.WaitForTakeoverAsync();
            Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.Claimed, takeover.Outcome);
        };
        var continuation = fixture.CreateContinuation(
            clock,
            delayedFinalization,
            operationStore: operationStore);

        var result = await continuation.ContinueAsync(
            new WorkflowExternalResponseContinuationRequest(
                fixture.Operation.Id,
                new WorkflowExternalResponseLeaseOwnerId("old-host")));
        var current = await fixture.OperationStore.GetAsync(fixture.Operation.Id);
        var run = await fixture.RunStore.GetRunAsync(fixture.Run.RunId);

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.ClaimConflict, result.Outcome);
        Assert.Equal(1, fixture.Backend.ResumeCount);
        Assert.Equal(1, fixture.Backend.SideEffectCount);
        Assert.Equal(replacementOwner, current?.Lease?.OwnerId);
        Assert.Equal(WorkflowExternalResponseOperationState.Claimed, current?.State);
        Assert.Null(current?.FinalResult);
        Assert.Equal(WorkflowRunState.WaitingForInput, run?.State);
    }

    [Fact]
    public async Task InvalidResultBoundaryCommit_PersistsTerminalFailureInsteadOfLeavingOperationResuming()
    {
        var fixture = await Fixture.CreateAsync(Now);
        var boundaryStore = new CommitOutcomeBoundaryStore(
            fixture.ResumeBoundaryStore,
            WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary);
        var continuation = fixture.CreateContinuation(
            new FixedTimeProvider(Now),
            boundaryStore: boundaryStore);

        var result = await continuation.ContinueAsync(
            new WorkflowExternalResponseContinuationRequest(
                fixture.Operation.Id,
                new WorkflowExternalResponseLeaseOwnerId("host-a")));
        var stored = await fixture.OperationStore.GetAsync(fixture.Operation.Id);

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.FailedTerminal, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, result.Operation?.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.ResponseRejected, result.Operation?.OutcomeCode);
        Assert.Equal(result.Operation, stored);
        Assert.Null(stored?.Lease);
    }

    [Theory]
    [InlineData(WorkflowResumeBoundaryCommitOutcome.ConcurrencyConflict)]
    [InlineData(WorkflowResumeBoundaryCommitOutcome.LeaseConflict)]
    public async Task OwnershipConflictDuringCommit_ReturnsClaimConflictWithoutOverwritingOperation(
        WorkflowResumeBoundaryCommitOutcome commitOutcome)
    {
        var fixture = await Fixture.CreateAsync(Now);
        var boundaryStore = new CommitOutcomeBoundaryStore(
            fixture.ResumeBoundaryStore,
            commitOutcome);
        var continuation = fixture.CreateContinuation(
            new FixedTimeProvider(Now),
            boundaryStore: boundaryStore);

        var result = await continuation.ContinueAsync(
            new WorkflowExternalResponseContinuationRequest(
                fixture.Operation.Id,
                new WorkflowExternalResponseLeaseOwnerId("host-a")));
        var stored = await fixture.OperationStore.GetAsync(fixture.Operation.Id);

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.ClaimConflict, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.Resuming, result.Operation?.State);
        Assert.Equal(WorkflowExternalResponseOperationState.Resuming, stored?.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.None, stored?.OutcomeCode);
    }

    [Fact]
    public async Task CancellationWinningCommit_ReturnsCancelledEvenWhenCommitCarriesResumingSnapshot()
    {
        var fixture = await Fixture.CreateAsync(Now);
        var boundaryStore = new CommitOutcomeBoundaryStore(
            fixture.ResumeBoundaryStore,
            WorkflowResumeBoundaryCommitOutcome.CancellationWon);
        var continuation = fixture.CreateContinuation(
            new FixedTimeProvider(Now),
            boundaryStore: boundaryStore);

        var result = await continuation.ContinueAsync(
            new WorkflowExternalResponseContinuationRequest(
                fixture.Operation.Id,
                new WorkflowExternalResponseLeaseOwnerId("host-a")));

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Cancelled, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.Resuming, result.Operation?.State);
    }

    [Theory]
    [InlineData(WorkflowBackendResumeFailureKind.ExactWorkflowVersionMissing, WorkflowExternalResponseOperationOutcomeCode.WorkflowVersionMismatch)]
    [InlineData(WorkflowBackendResumeFailureKind.ExactWorkflowVersionMismatch, WorkflowExternalResponseOperationOutcomeCode.WorkflowVersionMismatch)]
    [InlineData(WorkflowBackendResumeFailureKind.CompilationFailed, WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible)]
    [InlineData(WorkflowBackendResumeFailureKind.CompilerContractMismatch, WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible)]
    [InlineData(WorkflowBackendResumeFailureKind.TopologyMismatch, WorkflowExternalResponseOperationOutcomeCode.TopologyMismatch)]
    [InlineData(WorkflowBackendResumeFailureKind.CheckpointMissing, WorkflowExternalResponseOperationOutcomeCode.CheckpointMissing)]
    [InlineData(WorkflowBackendResumeFailureKind.CheckpointCorrupt, WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt)]
    [InlineData(WorkflowBackendResumeFailureKind.CheckpointIncompatible, WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible)]
    [InlineData(WorkflowBackendResumeFailureKind.RequestMismatch, WorkflowExternalResponseOperationOutcomeCode.RequestMismatch)]
    [InlineData(WorkflowBackendResumeFailureKind.PortMismatch, WorkflowExternalResponseOperationOutcomeCode.RequestMismatch)]
    [InlineData(WorkflowBackendResumeFailureKind.ResponseMismatch, WorkflowExternalResponseOperationOutcomeCode.ResponseRejected)]
    public async Task TypedBackendResumeFailure_PersistsPreciseTerminalOutcome(
        WorkflowBackendResumeFailureKind failureKind,
        WorkflowExternalResponseOperationOutcomeCode expectedOutcomeCode)
    {
        var fixture = await Fixture.CreateAsync(Now);
        fixture.Backend.ResumeFailure = new WorkflowBackendResumeException(
            failureKind,
            "Typed terminal resume failure.");

        var result = await fixture.ContinueAsync("host-a");

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.FailedTerminal, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, result.Operation?.State);
        Assert.Equal(expectedOutcomeCode, result.Operation?.OutcomeCode);
        Assert.Equal(0, fixture.Backend.SideEffectCount);
    }

    [Fact]
    public async Task WaitingBackendResultWithoutPendingBoundary_PersistsTerminalResponseMismatch()
    {
        var fixture = await Fixture.CreateAsync(Now);
        fixture.Backend.ReturnWaitingWithoutBoundary = true;

        var result = await fixture.ContinueAsync("host-a");

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.FailedTerminal, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, result.Operation?.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.ResponseRejected, result.Operation?.OutcomeCode);
        Assert.Equal(1, fixture.Backend.SideEffectCount);
    }

    [Fact]
    public async Task RetryAttemptExhaustion_BecomesStableTerminalOutcome()
    {
        var fixture = await Fixture.CreateAsync(Now);
        fixture.Backend.ResumeFailure = new InvalidOperationException("Transient backend failure.");

        for (var attempt = 0; attempt < WorkflowExternalResponseContinuation.DefaultMaximumAttempts; attempt++)
        {
            var retryable = await fixture.ContinueAsync($"host-{attempt}");
            Assert.Equal(WorkflowExternalResponseContinuationOutcome.FailedRetryable, retryable.Outcome);
        }

        var terminal = await fixture.ContinueAsync("terminal-host");
        var replay = await fixture.ContinueAsync("replay-host");

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.FailedTerminal, terminal.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, terminal.Operation?.State);
        Assert.Equal(
            WorkflowExternalResponseOperationOutcomeCode.AttemptLimitReached,
            terminal.Operation?.OutcomeCode);
        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Replayed, replay.Outcome);
        Assert.Equal(WorkflowExternalResponseContinuation.DefaultMaximumAttempts, fixture.Backend.ResumeCount);
    }

    private static WorkflowExternalResponseRecoveryHook CrashAt(
        WorkflowExternalResponseRecoveryPoint expected)
        => (actual, _, _) => actual == expected
            ? ValueTask.FromException(new InjectedRecoveryCrashException())
            : ValueTask.CompletedTask;

    private sealed class Fixture(
        InMemoryWorkflowRunStore runStore,
        InMemoryWorkflowExternalResponseOperationStore operationStore,
        InMemoryWorkflowResumeBoundaryStore resumeBoundaryStore,
        ParticipatingBackend backend,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        WorkflowExternalResponseOperationRecord operation)
    {
        public InMemoryWorkflowRunStore RunStore { get; } = runStore;

        public InMemoryWorkflowExternalResponseOperationStore OperationStore { get; } = operationStore;

        public InMemoryWorkflowResumeBoundaryStore ResumeBoundaryStore { get; } = resumeBoundaryStore;

        public ParticipatingBackend Backend { get; } = backend;

        public WorkflowRunSnapshot Run { get; } = run;

        public WorkflowExternalRequestRecord Request { get; } = request;

        public WorkflowExternalResponseOperationRecord Operation { get; } = operation;

        public static async Task<Fixture> CreateAsync(DateTimeOffset now)
        {
            var runStore = new InMemoryWorkflowRunStore();
            var checkpointStore = new InMemoryWorkflowBackendCheckpointPayloadStore(
                new FixedTimeProvider(now));
            var requestBoundaryStore = new InMemoryWorkflowExternalRequestBoundaryStore(
                runStore,
                checkpointStore);
            var operationStore = new InMemoryWorkflowExternalResponseOperationStore(
                runStore,
                requestBoundaryStore);
            var resumeBoundaryStore = new InMemoryWorkflowResumeBoundaryStore(
                runStore,
                requestBoundaryStore,
                operationStore);
            var backend = new ParticipatingBackend(now);
            var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "approver-1");
            var scope = WorkspaceScopeDescriptor.Project("continuation-test-project");
            var run = new WorkflowRunSnapshot(
                WorkflowRunId.New(),
                WorkflowId.New(),
                WorkflowVersionId.New(),
                WorkflowRunState.WaitingForInput,
                WorkflowRuntimeBackendKind.InProcess,
                "native-session",
                "Waiting for input.",
                now,
                now)
            {
                Origin = new WorkflowLaunchOrigin.Api(
                    actor,
                    new WorkflowLaunchCorrelationId("continuation-launch"))
                {
                    AuthorizationScope = scope,
                    AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
                }
            };
            var request = await WorkflowHitlTestCheckpointFactory.AddCheckpointAsync(
                checkpointStore,
                run,
                CreateNativeRequest(run, actor, scope, now),
                "{}");
            await runStore.SaveRunAsync(run);
            await runStore.SaveExternalRequestAsync(request);
            Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
            Assert.NotNull(boundary);
            Assert.True((await requestBoundaryStore.UpsertAsync(boundary)).Succeeded);

            var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
                request.Id,
                request.Version,
                actor,
                scope,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                new WorkflowExternalResponseIdempotencyKey("response-1"),
                "{\"answer\":\"yes\"}");
            var created = await operationStore.CreateOrReplayAsync(
                new WorkflowExternalResponseOperationCreateRequest(
                    WorkflowExternalResponseOperationId.New(),
                    request.Id,
                    run.RunId,
                    request.Version,
                    fingerprint,
                    actor,
                    new WorkflowLaunchCorrelationId("continuation-test"),
                    now));
            Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Created, created.Outcome);
            Assert.NotNull(created.Operation);
            return new Fixture(
                runStore,
                operationStore,
                resumeBoundaryStore,
                backend,
                run,
                request,
                created.Operation);
        }

        public WorkflowExternalResponseContinuation CreateContinuation(
            TimeProvider timeProvider,
            WorkflowExternalResponseRecoveryHook? hook = null,
            IWorkflowActiveRunRegistry? activeRuns = null,
            IWorkflowExternalResponseOperationStore? operationStore = null,
            IWorkflowResumeBoundaryStore? boundaryStore = null)
            => new(
                [Backend],
                operationStore ?? OperationStore,
                boundaryStore ?? ResumeBoundaryStore,
                activeRuns ?? new WorkflowActiveRunRegistry(),
                new WorkflowExternalResponseValidator(),
                new NullWorkflowEventSink(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowExternalResponseContinuation>.Instance,
                timeProvider,
                hook);

        public Task<WorkflowExternalResponseContinuationResult> ContinueAsync(
            string owner,
            DateTimeOffset? now = null)
            => CreateContinuation(new FixedTimeProvider(now ?? Now))
                .ContinueAsync(
                    new WorkflowExternalResponseContinuationRequest(
                        Operation.Id,
                        new WorkflowExternalResponseLeaseOwnerId(owner)));

        private static WorkflowExternalRequestRecord CreateNativeRequest(
            WorkflowRunSnapshot run,
            WorkflowLaunchActor actor,
            WorkspaceScopeDescriptor scope,
            DateTimeOffset now)
        {
            var requestId = WorkflowExternalRequestId.New();
            var sessionId = new WorkflowBackendSessionId(run.BackendRunId);
            var checkpointId = new WorkflowBackendCheckpointId("checkpoint-1");
            var continuation = new WorkflowExternalRequestContinuation(
                new WorkflowBackendExternalRequestLink(
                    requestId,
                    new WorkflowBackendRequestId("native-request-1"),
                    new WorkflowBackendRequestPortId("human-input")),
                new WorkflowBackendCheckpointLink(sessionId, checkpointId),
                new WorkflowCompilerContractVersion(1),
                WorkflowTopologyFingerprint.Create("continuation-test-topology"),
                WorkflowBackendCheckpointPayloadHash.Compute("{}"));
            return new WorkflowExternalRequestRecord(
                requestId,
                run.RunId,
                WorkflowExternalRequestKind.HumanInput,
                new WorkflowNodeId("human"),
                "human-input",
                "{\"prompt\":\"Continue?\"}",
                string.Empty,
                now,
                RespondedAtUtc: null)
            {
                Version = WorkflowExternalRequestVersion.Initial,
                State = WorkflowExternalRequestState.Pending,
                ResponseContract = new WorkflowExternalResponseContract(
                    WorkflowExternalRequestKind.HumanInput,
                    "test.human-input",
                    1,
                    "{\"type\":\"object\",\"properties\":{\"answer\":{\"type\":\"string\"}},\"required\":[\"answer\"],\"additionalProperties\":false}",
                    4096),
                Continuation = continuation,
                AuthorizationPolicy = new WorkflowExternalRequestAuthorizationPolicySnapshot(
                    actor,
                    ExecutorId: null,
                    WorkflowExecutorCapabilityFlags.None,
                    WorkflowExecutorApprovalRequirement.NotRequired,
                    IntendedApproverSubjectId: string.Empty)
                {
                    AuthorizationScope = scope,
                    AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                    ResponseAuthorizationLifetimeSeconds = WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
                }
            };
        }
    }

    private sealed class ParticipatingBackend(DateTimeOffset completedAtUtc) :
        IWorkflowExecutionBackend,
        IWorkflowExternalResponseBackend
    {
        private readonly Dictionary<WorkflowExternalResponseOperationId, WorkflowBackendStartResult> completed = [];
        private readonly TaskCompletionSource entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
            WorkflowRuntimeBackendKind.InProcess,
            "Participating response backend",
            IsDurable: false,
            SupportsStreaming: true,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: true,
            OperationalNotes: "Deterministic response continuation test backend.")
        {
            SupportsExternalResponseResume = true,
            SupportsActiveCancellation = true
        };

        public int ResumeCount { get; private set; }

        public int SideEffectCount { get; private set; }

        public bool BlockOnResume { get; set; }

        public bool ReturnWaitingWithoutBoundary { get; set; }

        public Exception? ResumeFailure { get; set; }

        public Task WaitUntilEnteredAsync() => entered.Task;

        public void Release() => release.TrySetResult();

        public Task<WorkflowBackendStartResult> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowBackendStartResult> ResumeAsync(
            WorkflowRunSnapshot run,
            WorkflowExternalRequestRecord request,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The typed operation-bound response path is required.");

        public async Task<WorkflowBackendStartResult> ResumeAsync(
            WorkflowBackendResumeRequest request,
            CancellationToken cancellationToken = default)
        {
            ResumeCount++;
            entered.TrySetResult();
            if (BlockOnResume)
            {
                await release.Task.WaitAsync(cancellationToken);
            }

            if (ResumeFailure is { } failure)
            {
                throw failure;
            }

            var operationId = request.CausationOperationId
                ?? throw new InvalidOperationException("A causation operation id is required.");
            if (completed.TryGetValue(operationId, out var replay))
            {
                return replay;
            }

            SideEffectCount++;
            if (ReturnWaitingWithoutBoundary)
            {
                return new WorkflowBackendStartResult(
                    request.Run with
                    {
                        State = WorkflowRunState.WaitingForInput,
                        Summary = "Waiting again.",
                        UpdatedAtUtc = completedAtUtc
                    },
                    Events: [],
                    ExternalRequests: [],
                    Artifacts: []);
            }

            var completedRun = request.Run with
            {
                State = WorkflowRunState.Completed,
                Summary = "Completed.",
                UpdatedAtUtc = completedAtUtc,
                TerminalAtUtc = completedAtUtc
            };
            var result = new WorkflowBackendStartResult(
                completedRun,
                [
                    new WorkflowEventRecord(
                        Guid.NewGuid(),
                        request.Run.RunId,
                        WorkflowEventKind.Completed,
                        NodeId: null,
                        "Completed.",
                        "{}",
                        completedAtUtc)
                ],
                ExternalRequests: [],
                Artifacts: []);
            completed.Add(operationId, result);
            return result;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset current = utcNow;
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => current;

        public override long GetTimestamp() => timestamp;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            current += duration;
            timestamp += duration.Ticks;
            foreach (var timer in timers.ToArray())
            {
                timer.FireIfDue(timestamp);
            }

            timers.RemoveAll(timer => timer.IsDisposed);
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider owner;
            private readonly TimerCallback callback;
            private readonly object? state;
            private long dueTimestamp;
            private TimeSpan period;

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                this.owner = owner;
                this.callback = callback;
                this.state = state;
                Change(dueTime, period);
            }

            public bool IsDisposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (IsDisposed)
                {
                    return false;
                }

                this.period = period;
                dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : owner.timestamp + dueTime.Ticks;
                return true;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue(long currentTimestamp)
            {
                if (IsDisposed || currentTimestamp < dueTimestamp)
                {
                    return;
                }

                dueTimestamp = period == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : currentTimestamp + period.Ticks;
                callback(state);
            }
        }
    }

    private sealed class CommitOutcomeBoundaryStore(
        IWorkflowResumeBoundaryStore inner,
        WorkflowResumeBoundaryCommitOutcome commitOutcome) : IWorkflowResumeBoundaryStore
    {
        public Task<WorkflowResumeBoundaryLoadResult> LoadAsync(
            WorkflowResumeBoundaryLoadRequest request,
            CancellationToken cancellationToken = default)
            => inner.LoadAsync(request, cancellationToken);

        public async Task<WorkflowResumeBoundaryCommitResult> TryCommitAsync(
            WorkflowResumeBoundaryCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            var loaded = await inner.LoadAsync(
                new WorkflowResumeBoundaryLoadRequest(request.OperationId),
                cancellationToken);
            return new WorkflowResumeBoundaryCommitResult(
                commitOutcome,
                loaded.Context?.Operation,
                loaded.Context?.Run,
                NextRequest: null);
        }

        public Task<WorkflowResumeBoundaryCancellationResult> TryCancelAsync(
            WorkflowResumeBoundaryCancellationRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryCancelAsync(request, cancellationToken);
    }

    private sealed class TakeoverOnRenewalOperationStore(
        IWorkflowExternalResponseOperationStore inner,
        WorkflowExternalResponseLeaseOwnerId replacementOwner) :
        IWorkflowExternalResponseOperationStore
    {
        private readonly TaskCompletionSource<WorkflowExternalResponseOperationClaimResult> takeover =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int takeoverStarted;

        public Task<WorkflowExternalResponseOperationClaimResult> WaitForTakeoverAsync()
            => takeover.Task;

        public Task<WorkflowExternalResponseOperationCreateResult> CreateOrReplayAsync(
            WorkflowExternalResponseOperationCreateRequest request,
            CancellationToken cancellationToken = default)
            => inner.CreateOrReplayAsync(request, cancellationToken);

        public Task<WorkflowExternalResponseOperationRecord?> GetAsync(
            WorkflowExternalResponseOperationId operationId,
            CancellationToken cancellationToken = default)
            => inner.GetAsync(operationId, cancellationToken);

        public Task<IReadOnlyList<WorkflowExternalResponseOperationRecord>> ListRecoverableAsync(
            DateTimeOffset asOfUtc,
            int maximumCount,
            CancellationToken cancellationToken = default)
            => inner.ListRecoverableAsync(asOfUtc, maximumCount, cancellationToken);

        public Task<WorkflowExternalResponseOperationClaimResult> TryClaimAsync(
            WorkflowExternalResponseOperationClaimRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryClaimAsync(request, cancellationToken);

        public async Task<WorkflowExternalResponseOperationMutationResult> TryRenewLeaseAsync(
            WorkflowExternalResponseOperationLeaseRenewalRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref takeoverStarted, 1) == 0)
            {
                var current = await inner.GetAsync(request.OperationId, CancellationToken.None)
                    ?? throw new InvalidOperationException("The operation disappeared before lease takeover.");
                var claim = await inner.TryClaimAsync(
                    new WorkflowExternalResponseOperationClaimRequest(
                        current.Id,
                        current.ConcurrencyVersion,
                        replacementOwner,
                        request.RenewedAtUtc,
                        request.LeaseExpiresAtUtc,
                        WorkflowExternalResponseContinuation.DefaultMaximumAttempts),
                    CancellationToken.None);
                takeover.TrySetResult(claim);
            }

            return await inner.TryRenewLeaseAsync(request, cancellationToken);
        }

        public Task<WorkflowExternalResponseOperationMutationResult> TryMarkResumingAsync(
            WorkflowExternalResponseOperationMarkResumingRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryMarkResumingAsync(request, cancellationToken);

        public Task<WorkflowExternalResponseOperationMutationResult> TryCompleteAsync(
            WorkflowExternalResponseOperationCompletionRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryCompleteAsync(request, cancellationToken);

        public Task<WorkflowExternalResponseOperationMutationResult> TryFailAsync(
            WorkflowExternalResponseOperationFailureRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryFailAsync(request, cancellationToken);

        public Task<WorkflowExternalResponseOperationMutationResult> TryReleaseLeaseAsync(
            WorkflowExternalResponseOperationLeaseReleaseRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryReleaseLeaseAsync(request, cancellationToken);
    }

    private sealed class InjectedRecoveryCrashException : Exception
    {
    }
}
