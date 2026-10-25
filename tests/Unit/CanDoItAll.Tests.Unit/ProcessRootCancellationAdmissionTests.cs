using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRootCancellationAdmissionTests {
    [Fact]
    public async Task Disabled_recovery_refuses_before_reading_or_mutating_runtime() {
        var owner = new Harness();
        var result = await owner.Service(enabled: false).AdmitAsync(owner.RunId, owner.Requester);
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Unavailable, result.Status);
        Assert.Equal(1, owner.AccessChecks);
        Assert.Equal(0, owner.Reads);
        Assert.Equal(0, owner.Loads);
        Assert.Empty(owner.Requests);
    }

    [Fact]
    public async Task A_child_is_never_resolved_into_a_parent_or_sibling_cancellation() {
        var owner = new Harness();
        owner.Progress = owner.Progress! with { RootRunId = ProcessRunId.New(), Tracking = ProcessCancellationTracking.ChildRun };
        var result = await owner.Service().AdmitAsync(owner.RunId, owner.Requester);
        Assert.Equal(ProcessRootCancellationAdmissionStatus.NotRoot, result.Status);
        Assert.Equal(owner.RunId, result.RunId);
        Assert.Equal(0, owner.Loads);
        Assert.Empty(owner.Requests);
    }

    [Theory]
    [InlineData(ProcessCancellationTracking.Pending, ProcessRootCancellationAdmissionStatus.Accepted)]
    [InlineData(ProcessCancellationTracking.Completed, ProcessRootCancellationAdmissionStatus.Completed)]
    [InlineData(ProcessCancellationTracking.LegacyUntracked, ProcessRootCancellationAdmissionStatus.ReconciliationRequired)]
    [InlineData(ProcessCancellationTracking.ReconciliationRequired, ProcessRootCancellationAdmissionStatus.ReconciliationRequired)]
    public async Task Existing_progress_preserves_its_original_barrier_without_another_mutation(ProcessCancellationTracking tracking,
        ProcessRootCancellationAdmissionStatus expected) {
        var owner = new Harness();
        var barrier = RuntimeCommandId.New();
        owner.Progress = owner.Progress! with { Tracking = tracking, RuntimeStatus = ProcessRuntimeStatus.Cancelled,
            Recovery = new(ProcessCancellationRecoveryOrigin.LiveBarrier, barrier, 19, owner.Clock.GetUtcNow(),
                tracking == ProcessCancellationTracking.Completed ? ProcessCancellationRecoveryStage.Completed : ProcessCancellationRecoveryStage.Runtime,
                ProcessCancellationRecoveryPending.None) };
        var result = await owner.Service().AdmitAsync(owner.RunId, owner.Requester);
        Assert.Equal(expected, result.Status);
        Assert.Equal(barrier, result.BarrierCommandId);
        Assert.Equal(owner.Progress, result.Progress);
        Assert.Equal(0, owner.Loads);
        Assert.Empty(owner.Requests);
    }

    [Fact]
    public async Task Missing_and_terminal_roots_are_explicit_without_a_commit() {
        var owner = new Harness();
        var active = owner.Progress;
        owner.Progress = null;
        Assert.Equal(ProcessRootCancellationAdmissionStatus.NotFound, (await owner.Service().AdmitAsync(owner.RunId, owner.Requester)).Status);
        owner.Progress = active! with { RuntimeStatus = ProcessRuntimeStatus.Completed };
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Terminal, (await owner.Service().AdmitAsync(owner.RunId, owner.Requester)).Status);
        Assert.Empty(owner.Requests);
    }

    [Fact]
    public async Task Conflict_retries_keep_original_command_actor_time_and_correlation_while_runtime_time_advances() {
        var owner = new Harness();
        owner.BeforeCommit = (request, _) => {
            if (owner.Requests.Count == 1) {
                owner.State = owner.State with { UpdatedAtUtc = owner.Clock.GetUtcNow().AddSeconds(1) };
                throw new ProcessRuntimeOptimisticConcurrencyException(owner.RunId, request.OriginalState.UpdatedAtUtc);
            }
            return Task.CompletedTask;
        };
        var result = await owner.Service().AdmitAsync(owner.RunId, owner.Requester);
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Accepted, result.Status);
        Assert.Null(result.Progress);
        Assert.Equal(2, owner.Requests.Count);
        var first = owner.Requests[0];
        var second = owner.Requests[1];
        Assert.Equal(first.CommandId, second.CommandId);
        Assert.Equal(first.CommandId, result.BarrierCommandId);
        var originalEvent = Assert.Single(first.Mutation.Events);
        var retriedEvent = Assert.Single(second.Mutation.Events);
        Assert.Equal(owner.Requester.Actor, retriedEvent.Actor);
        Assert.Equal(originalEvent.OccurredAtUtc, retriedEvent.OccurredAtUtc);
        Assert.Equal(originalEvent.CorrelationId, retriedEvent.CorrelationId);
        Assert.True(second.Mutation.State.UpdatedAtUtc > second.OriginalState.UpdatedAtUtc);
        Assert.Equal(0, second.Mutation.State.UpdatedAtUtc.UtcTicks % 10);
        Assert.Equal(2, owner.Reads);
    }

    [Fact]
    public async Task Repeated_concurrency_has_exactly_three_attempts_and_no_unbounded_cascade() {
        var owner = new Harness();
        owner.BeforeCommit = (request, _) => throw new ProcessRuntimeOptimisticConcurrencyException(owner.RunId, request.OriginalState.UpdatedAtUtc);
        var result = await owner.Service().AdmitAsync(owner.RunId, owner.Requester);
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Contended, result.Status);
        Assert.Equal(3, owner.Requests.Count);
        Assert.Single(owner.Requests.Select(request => request.CommandId).Distinct());
        Assert.Equal(ProcessRuntimeStatus.Created, owner.State.Status);
    }

    [Fact]
    public async Task Lost_acknowledgement_is_not_retried_and_next_observation_recovers_original_admission() {
        var owner = new Harness();
        var failure = new IOException("Injected committed admission acknowledgement loss.");
        owner.BeforeCommit = (request, _) => {
            owner.Save(request);
            throw failure;
        };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => owner.Service().AdmitAsync(owner.RunId, owner.Requester)));
        var original = Assert.Single(owner.Requests).CommandId;
        var recovered = await owner.Service().AdmitAsync(owner.RunId, owner.Requester);
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Accepted, recovered.Status);
        Assert.Equal(original, recovered.BarrierCommandId);
        Assert.Single(owner.Requests);
    }

    [Fact]
    public async Task Authority_changed_during_the_read_refuses_before_mutation() {
        var owner = new Harness();
        owner.BeforeLoad = _ => {
            owner.Authorized = false;
            return Task.CompletedTask;
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owner.Service().AdmitAsync(owner.RunId, owner.Requester));
        Assert.Equal(2, owner.AccessChecks);
        Assert.Empty(owner.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task One_deadline_awaits_the_current_owner_and_distinguishes_submission_uncertainty(bool duringCommit) {
        var owner = new Harness();
        var exited = false;
        async Task ExpireAsync(CancellationToken token) {
            try {
                owner.Clock.Expire();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            } finally {
                exited = true;
            }
        }
        if (duringCommit) {
            owner.BeforeCommit = (_, token) => ExpireAsync(token);
        } else {
            owner.BeforeRead = ExpireAsync;
        }
        var result = await owner.Service().AdmitAsync(owner.RunId, owner.Requester);
        Assert.Equal(duringCommit ? ProcessRootCancellationAdmissionStatus.Unconfirmed : ProcessRootCancellationAdmissionStatus.TimedOut, result.Status);
        Assert.True(exited);
        Assert.Equal(duringCommit ? 1 : 0, owner.Requests.Count);
        Assert.True(owner.Clock.TimerDisposed);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_after_awaiting_the_owner_without_retry() {
        var owner = new Harness();
        using var caller = new CancellationTokenSource();
        var exited = false;
        owner.BeforeCommit = async (_, token) => {
            try {
                caller.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            } finally {
                exited = true;
            }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.Service().AdmitAsync(owner.RunId, owner.Requester, caller.Token));
        Assert.True(exited);
        Assert.Single(owner.Requests);
    }

    [Fact]
    public async Task Known_commit_is_not_relabelled_when_caller_cancellation_arrives_with_acknowledgement() {
        var owner = new Harness();
        using var caller = new CancellationTokenSource();
        owner.BeforeCommit = (_, _) => {
            caller.Cancel();
            return Task.CompletedTask;
        };
        var result = await owner.Service().AdmitAsync(owner.RunId, owner.Requester, caller.Token);
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Accepted, result.Status);
        Assert.Equal(Assert.Single(owner.Requests).CommandId, result.BarrierCommandId);
    }

    private sealed class Harness : IProcessRuntimeStateStore, IProcessRuntimeUnitOfWork, IProcessCancellationProgressReader {
        internal ProcessRuntimeStateSnapshot State { get; set; } = ProcessProjectAdmissionFixture.Initial().OriginalState;
        internal ProcessRunId RunId => State.RunId;
        internal ProcessCancellationProgress? Progress { get; set; }
        internal DeadlineClock Clock { get; } = new();
        internal List<ProcessRuntimeCommitRequest> Requests { get; } = [];
        internal ProcessRootCancellationRequester Requester { get; }
        internal bool Authorized { get; set; } = true;
        internal int AccessChecks { get; private set; }
        internal int Reads { get; private set; }
        internal int Loads { get; private set; }
        internal Func<CancellationToken, Task>? BeforeRead { get; set; }
        internal Func<CancellationToken, Task>? BeforeLoad { get; set; }
        internal Func<ProcessRuntimeCommitRequest, CancellationToken, Task>? BeforeCommit { get; set; }

        internal Harness() {
            Progress = new(RunId, RunId, State.Status, State.UpdatedAtUtc, ProcessCancellationTracking.NotRequested);
            Requester = new(new(ProcessEventActorKind.User, new("admission-test-operator")), token => {
                token.ThrowIfCancellationRequested();
                AccessChecks++;
                return Authorized ? ValueTask.CompletedTask : throw new UnauthorizedAccessException();
            });
        }

        internal ProcessRootCancellationAdmissionService Service(bool enabled = true) => new(this, this, this, new(enabled), Clock);

        public async Task<ProcessCancellationProgress?> ReadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            Assert.Equal(RunId, runId);
            Reads++;
            if (BeforeRead is not null) {
                await BeforeRead(cancellationToken);
            }
            return Progress;
        }

        public async Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            Assert.Equal(RunId, runId);
            Loads++;
            if (BeforeLoad is not null) {
                await BeforeLoad(cancellationToken);
            }
            return State;
        }

        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            Requests.Add(request);
            if (BeforeCommit is not null) {
                await BeforeCommit(request, cancellationToken);
            }
            Save(request);
            return ProcessRuntimeCommitResult.FromMutation(request.Mutation) with { CommittedNow = true };
        }

        internal void Save(ProcessRuntimeCommitRequest request) {
            State = request.Mutation.State;
            Progress = new(RunId, RunId, State.Status, State.UpdatedAtUtc, ProcessCancellationTracking.Pending,
                new(ProcessCancellationRecoveryOrigin.LiveBarrier, request.CommandId, 0, Clock.GetUtcNow(),
                    ProcessCancellationRecoveryStage.Runtime, ProcessCancellationRecoveryPending.None));
        }
    }

    private sealed class DeadlineClock : TimeProvider {
        private DeadlineTimer? timer;
        internal bool TimerDisposed => timer!.Disposed;
        public override DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now.AddSeconds(1);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            Assert.Equal(ProcessRootCancellationAdmissionService.MaximumDuration, dueTime);
            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            Assert.True(timer is null || timer.Disposed);
            return timer = new(callback, state);
        }
        internal void Expire() => timer!.Fire();

        private sealed class DeadlineTimer(TimerCallback callback, object? state) : ITimer {
            internal bool Disposed { get; private set; }
            internal void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new InvalidOperationException();
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
