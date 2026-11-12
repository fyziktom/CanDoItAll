using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectPendingOperationSessionTests {
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Prepare_failure_blocks_dispatch_and_keeps_the_original_submission() {
        var frozen = Frozen();
        var store = new Store(frozen) { PrepareFailure = new IOException("Quota") };
        using var session = new ProjectPendingOperationSession(store, frozen, true);
        await Assert.ThrowsAsync<IOException>(() => session.PrepareAsync());
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DispatchAsync(Context(frozen), Now, false,
            (_, _) => {
                calls++;
                return Task.FromResult(Rejected(frozen));
            }));
        Assert.Equal(0, calls);
        Assert.Same(frozen, session.Frozen);
        Assert.Null(session.Record);
    }

    [Theory]
    [InlineData(ProjectPendingStoreStatus.Unchanged)]
    [InlineData(ProjectPendingStoreStatus.Changed)]
    [InlineData(ProjectPendingStoreStatus.Missing)]
    public async Task Only_an_applied_dispatch_claim_authorizes_an_owner_call(ProjectPendingStoreStatus status) {
        var frozen = Frozen();
        var store = new Store(frozen) { ClaimStatus = status };
        using var session = new ProjectPendingOperationSession(store, frozen, false);
        await session.PrepareAsync();
        var calls = 0;
        await session.DispatchAsync(Context(frozen), Now, false, (_, _) => {
            calls++;
            return Task.FromResult(Rejected(frozen));
        });
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task An_applied_claim_without_the_exact_dispatch_state_cannot_authorize_the_owner() {
        var frozen = Frozen();
        var store = new Store(frozen) { ReturnPreparedClaim = true };
        using var session = new ProjectPendingOperationSession(store, frozen, false);
        await session.PrepareAsync();
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DispatchAsync(Context(frozen), Now, false, (_, _) => {
            calls++;
            return Task.FromResult(Rejected(frozen));
        }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Retiring_while_the_claim_completes_never_dispatches_or_clears_the_durable_record() {
        var frozen = Frozen();
        var held = new TaskCompletionSource<ProjectPendingStoreChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Store(frozen) { HeldClaim = held.Task };
        using var session = new ProjectPendingOperationSession(store, frozen, false);
        await session.PrepareAsync();
        var calls = 0;
        var action = session.DispatchAsync(Context(frozen), Now, false, (_, _) => {
            calls++;
            return Task.FromResult(Rejected(frozen));
        });
        session.Dispose();
        held.SetResult(new(ProjectPendingStoreStatus.Applied, store.Record with { Stage = ProjectPendingStage.Dispatching, AttemptId = Guid.NewGuid(), Revision = new(2) }));
        await action;
        Assert.Equal(0, calls);
        Assert.Equal(0, store.ClearCalls);
        Assert.Equal(ProjectPendingStage.Dispatching, session.Record!.Stage);
    }

    [Fact]
    public async Task Exact_acceptance_is_retained_before_storage_failure_and_cannot_be_dispatched_again() {
        var frozen = Frozen();
        var accepted = Accepted(frozen);
        var store = new Store(frozen) { AcceptedFailure = new IOException("Disconnected storage") };
        using var session = new ProjectPendingOperationSession(store, frozen, false);
        await session.PrepareAsync();
        await Assert.ThrowsAsync<IOException>(() => session.DispatchAsync(Context(frozen), Now, false,
            (_, _) => Task.FromResult<ProjectPendingDispatchOutcome>(new ProjectPendingDispatchOutcome.Full(new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted(accepted.Result, false)))));
        Assert.Same(accepted.Result, Assert.IsType<ProjectDefinitionAcceptedReceipt.Full>(session.KnownAccepted).Result);
        Assert.Equal(ProjectPendingProblem.StorageFailed, session.Problem);
        Assert.False(session.CanReconcile);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DispatchAsync(Context(frozen), Now, true, (_, _) => Task.FromResult(Rejected(frozen))));
    }

    [Fact]
    public async Task Late_acceptance_after_disposal_updates_its_evidence_without_reconciling_a_retired_editor() {
        var frozen = Frozen();
        var store = new Store(frozen);
        using var session = new ProjectPendingOperationSession(store, frozen, false);
        await session.PrepareAsync();
        var held = new TaskCompletionSource<ProjectPendingDispatchOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        ProjectDefinitionSubmission? sent = null;
        var action = session.DispatchAsync(Context(frozen), Now, false, (submission, _) => {
            sent = submission;
            return held.Task;
        });
        session.Dispose();
        held.SetResult(new ProjectPendingDispatchOutcome.Full(new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted(Accepted(frozen).Result, false)));
        await action;
        Assert.Same(frozen.Submission, sent);
        Assert.Equal(ProjectPendingStage.Accepted, store.Record.Stage);
        Assert.NotNull(session.KnownAccepted);
        Assert.False(session.CanReconcile);
        Assert.Equal(0, store.ClearCalls);
    }

    [Fact]
    public async Task Restored_local_acceptance_needs_an_authorized_lookup_and_replay_reconciles_only_once() {
        var frozen = Frozen();
        var accepted = Accepted(frozen);
        var store = new Store(frozen);
        store.Record = store.Record with { Stage = ProjectPendingStage.Accepted, AttemptId = Guid.NewGuid(), Accepted = new(accepted, "receipt"), Revision = new(3) };
        using var session = new ProjectPendingOperationSession(store, store.Record);
        Assert.Null(session.KnownAccepted);
        Assert.False(session.CanReconcile);
        await session.ObserveLookupAsync(new ProjectDefinitionLookup.FoundFull(accepted.Result));
        var calls = 0;
        session.Reconcile(_ => calls++);
        await session.ObserveLookupAsync(new ProjectDefinitionLookup.FoundFull(accepted.Result));
        Assert.Throws<InvalidOperationException>(() => session.Reconcile(_ => calls++));
        Assert.Equal(1, calls);
        await session.ClearReconciledAsync();
        Assert.Equal(1, store.ClearCalls);
    }

    [Fact]
    public async Task Rejected_retry_and_inconclusive_lookup_leave_earlier_uncertainty_blocked() {
        var frozen = Frozen();
        var store = new Store(frozen) { KeepUnknown = true };
        store.Record = store.Record with { Stage = ProjectPendingStage.Unconfirmed, AttemptId = Guid.NewGuid(), HadUnconfirmedDispatch = true };
        using var session = new ProjectPendingOperationSession(store, store.Record);
        await session.DispatchAsync(Context(frozen), Now, true, (_, _) => Task.FromResult(Rejected(frozen)));
        Assert.Equal(ProjectPendingStage.Unconfirmed, session.Record!.Stage);
        await session.ObserveLookupAsync(new ProjectDefinitionLookup.NotObserved(frozen.Submission.Operation, ProjectOperationObservationState.Open));
        Assert.Equal(ProjectPendingStage.Unconfirmed, session.Record.Stage);
        Assert.Equal(ProjectPendingProblem.OutcomeUnconfirmed, session.Problem);
        await session.ObserveLookupAsync(new ProjectDefinitionLookup.Expired(frozen.Submission.Operation));
        Assert.Equal(ProjectPendingStage.Unconfirmed, session.Record.Stage);
        Assert.Equal(ProjectPendingProblem.RecoveryUnavailable, session.Problem);
        Assert.Equal(0, store.ClearCalls);
    }

    [Fact]
    public async Task Reconciliation_failure_preserves_acceptance_and_cannot_repeat_after_lookup() {
        var frozen = Frozen();
        var store = new Store(frozen);
        using var session = new ProjectPendingOperationSession(store, frozen, false);
        await session.PrepareAsync();
        var accepted = Accepted(frozen);
        await session.DispatchAsync(Context(frozen), Now, false,
            (_, _) => Task.FromResult<ProjectPendingDispatchOutcome>(new ProjectPendingDispatchOutcome.Full(new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted(accepted.Result, false))));
        Assert.Throws<InvalidOperationException>(() => session.Reconcile(_ => throw new InvalidOperationException("Local mapping")));
        await session.ObserveLookupAsync(new ProjectDefinitionLookup.FoundFull(accepted.Result));
        Assert.Equal(ProjectPendingProblem.ReconciliationFailed, session.Problem);
        Assert.False(session.CanReconcile);
        Assert.NotNull(session.KnownAccepted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ClearReconciledAsync());
        Assert.Equal(0, store.ClearCalls);
    }

    [Fact]
    public async Task Different_caller_context_cannot_claim_or_rekey_the_original_operation() {
        var frozen = Frozen();
        var store = new Store(frozen);
        using var session = new ProjectPendingOperationSession(store, frozen, false);
        await session.PrepareAsync();
        var original = Context(frozen);
        var changed = new ProjectDefinitionWriteContext(original.Namespace, new("pc1:" + new string('b', 64)), original.Epoch,
            original.AdmissionDeadlineUtc, original.ReplayUntilUtc, original.RequestVersions, original.ResultVersions, original.Limits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DispatchAsync(changed, Now, false, (_, _) => Task.FromResult(Rejected(frozen))));
        Assert.Equal(0, store.ClaimCalls);
        Assert.Equal(ProjectPendingProblem.ContextChanged, session.Problem);
        Assert.Equal(frozen.Submission.Operation, session.Record!.Operation);
    }

    [Fact]
    public async Task Owner_failure_and_failed_uncertainty_persistence_preserve_both_errors() {
        var frozen = Frozen();
        var store = new Store(frozen) { AttemptFailure = new IOException("Storage unavailable") };
        using var session = new ProjectPendingOperationSession(store, frozen, false);
        await session.PrepareAsync();
        var failure = await Assert.ThrowsAsync<AggregateException>(() => session.DispatchAsync(Context(frozen), Now, false,
            (_, _) => throw new TimeoutException("Owner response lost")));
        Assert.Contains(failure.InnerExceptions, value => value is TimeoutException);
        Assert.Contains(failure.InnerExceptions, value => value is IOException);
        Assert.Equal(ProjectPendingStage.Dispatching, session.Record!.Stage);
        Assert.Equal(ProjectPendingProblem.StorageFailed, session.Problem);
    }

    private static ProjectDefinitionFrozenSubmission Frozen() {
        var operation = new ProjectDefinitionOperation(new(Guid.NewGuid()), new(1), Guid.NewGuid(), new("pc1:" + new string('a', 64)));
        var expected = new ProjectDefinitionExpectation(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());
        return new(new ProjectDefinitionSubmission.Replace(new(operation, ProjectDefinitionRequestVersion.VersionOne,
            new(expected, new("Submitted", "", "", ProjectStatus.Active, "", null), [], [], null))), "frozen submission");
    }

    private static ProjectDefinitionWriteContext Context(ProjectDefinitionFrozenSubmission frozen)
        => new(frozen.Submission.Operation.Namespace, frozen.Submission.Operation.CallerBinding, frozen.Submission.Operation.Epoch,
            Now.AddDays(1), Now.AddDays(7), [ProjectDefinitionRequestVersion.VersionOne], [ProjectDefinitionResultVersion.VersionOne],
            ProjectDefinitionLimits.VersionOneMaximum);

    private static ProjectDefinitionAcceptedReceipt.Full Accepted(ProjectDefinitionFrozenSubmission frozen) {
        var expected = Assert.IsType<ProjectDefinitionSubmission.Replace>(frozen.Submission).Request.Command.Expected;
        return new(new(frozen.Submission.Operation, ProjectDefinitionResultVersion.VersionOne, ProjectDefinitionCommandKind.Replace,
            Now, Now.AddDays(7), new(new(expected.Project, Guid.NewGuid()), "Submitted", "", "", ProjectStatus.Active, "", null, [], [], null)));
    }

    private static ProjectPendingDispatchOutcome Rejected(ProjectDefinitionFrozenSubmission frozen)
        => new ProjectPendingDispatchOutcome.Full(new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected(
            frozen.Submission.Operation, [new Error("test.rejected", "Rejected") ]));

    private sealed class Store(ProjectDefinitionFrozenSubmission frozen) : IProjectPendingSubmissionStore {
        public ProjectPendingSubmission Record { get; set; } = new(frozen, new(1), ProjectPendingStage.Prepared, null, false, false, null, null);
        public Exception? PrepareFailure { get; init; }
        public Exception? AcceptedFailure { get; init; }
        public Exception? AttemptFailure { get; init; }
        public bool KeepUnknown { get; init; }
        public bool ReturnPreparedClaim { get; init; }
        public int ClearCalls { get; private set; }
        public int ClaimCalls { get; private set; }
        public ProjectPendingStoreStatus ClaimStatus { get; init; } = ProjectPendingStoreStatus.Applied;
        public Task<ProjectPendingStoreChange>? HeldClaim { get; init; }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<ProjectPendingStoreChange> PrepareAsync(ProjectDefinitionFrozenSubmission submission, bool hadStarterPlan, CancellationToken cancellationToken = default)
            => PrepareFailure is null ? ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Applied, Record)) : ValueTask.FromException<ProjectPendingStoreChange>(PrepareFailure);

        public async ValueTask<ProjectPendingStoreChange> BeginDispatchAsync(ProjectPendingSubmission record, Guid attemptId, bool explicitRetry, CancellationToken cancellationToken = default) {
            ClaimCalls++;
            if (HeldClaim is not null) {
                return await HeldClaim;
            }
            if (ReturnPreparedClaim) {
                return new(ProjectPendingStoreStatus.Applied, Record);
            }
            Record = Record with { Stage = ProjectPendingStage.Dispatching, AttemptId = attemptId, Revision = new(Record.Revision.Value + 1) };
            return new(ClaimStatus, ClaimStatus == ProjectPendingStoreStatus.Missing ? null : Record);
        }

        public ValueTask<ProjectPendingStoreChange> ObserveAcceptedAsync(ProjectDefinitionFrozenSubmission submission, ProjectDefinitionAcceptedReceipt receipt, CancellationToken cancellationToken = default) {
            if (AcceptedFailure is not null) {
                return ValueTask.FromException<ProjectPendingStoreChange>(AcceptedFailure);
            }
            Record = Record with { Stage = ProjectPendingStage.Accepted, Accepted = new(receipt, "receipt"), Revision = new(Record.Revision.Value + 1) };
            return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Applied, Record));
        }

        public ValueTask<ProjectPendingStoreChange> ObserveAttemptAsync(ProjectDefinitionOperation operation, Guid attemptId, ProjectPendingAttemptOutcome outcome, CancellationToken cancellationToken = default) {
            if (AttemptFailure is not null) {
                return ValueTask.FromException<ProjectPendingStoreChange>(AttemptFailure);
            }
            Record = Record with { Stage = KeepUnknown || outcome == ProjectPendingAttemptOutcome.Unconfirmed ? ProjectPendingStage.Unconfirmed : ProjectPendingStage.Rejected,
                HadUnconfirmedDispatch = KeepUnknown || outcome == ProjectPendingAttemptOutcome.Unconfirmed, Revision = new(Record.Revision.Value + 1) };
            return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Applied, Record));
        }

        public ValueTask<ProjectPendingStoreChange> ClearResolvedAsync(ProjectPendingSubmission record, CancellationToken cancellationToken = default) {
            ClearCalls++;
            return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Applied, null));
        }

        public ValueTask<ProjectPendingStoreChange> ObserveClosedAbsentAsync(ProjectDefinitionFrozenSubmission submission, ProjectDefinitionLookup.ClosedAbsent absence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProjectPendingSubmission?> ReadAsync(ProjectDefinitionOperation operation, CancellationToken cancellationToken = default) => ValueTask.FromResult<ProjectPendingSubmission?>(Record);
        public ValueTask<ProjectPendingSubmissionPage> ListAsync(ProjectPendingScope scope, int take, ProjectDefinitionOperation? after = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProjectPendingStoreChange> CancelPreparedAsync(ProjectPendingSubmission record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
