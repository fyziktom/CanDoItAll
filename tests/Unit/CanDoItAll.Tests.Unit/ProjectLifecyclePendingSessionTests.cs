using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectLifecyclePendingSessionTests {
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
    private static readonly ProjectLifecycleRecoveryCodec Codec = new();

    [Fact]
    public void Retained_closed_absence_is_exact_original_evidence_even_after_its_minimum_retention_promise() {
        var frozen = Frozen();
        var absence = new ProjectLifecycleLookup.ClosedAbsent(frozen.Submission.Operation, Now, Now.AddDays(8), Now.AddDays(7));
        var encoded = Codec.EncodeClosedAbsent(frozen.Submission, absence);
        Assert.Equal(absence, Codec.ReadClosedAbsent(frozen.Submission, encoded.Payload).Absence);
        Assert.Throws<InvalidDataException>(() => Codec.ReadClosedAbsent(Frozen().Submission, encoded.Payload));
        Assert.Throws<InvalidDataException>(() => Codec.ReadClosedAbsent(frozen.Submission, encoded.Payload + " "));
        Assert.Throws<InvalidDataException>(() => Codec.EncodeClosedAbsent(frozen.Submission, absence with { ClosedAtUtc = Now.AddDays(9) }));
    }

    [Fact]
    public async Task Durable_original_claim_and_matching_scope_are_required_before_any_owner_call() {
        var frozen = Frozen();
        var store = new Store(frozen) { PreparationFailure = new IOException("Storage unavailable") };
        using var session = new ProjectLifecyclePendingSession(store, Codec, frozen);
        await session.PrepareAsync();
        var calls = 0;
        Task<ProjectLifecycleOutcome> Dispatch(ProjectLifecycleSubmission original, CancellationToken token) {
            calls++;
            return Task.FromResult<ProjectLifecycleOutcome>(Refused(original));
        }
        await session.DispatchAsync(Context(frozen), Now, false, Dispatch);
        Assert.True(session.NeedsStorageRepair);
        Assert.Equal(0, calls);
        store.PreparationFailure = null;
        await session.PrepareAsync();
        await session.DispatchAsync(Context(frozen) with { DatabaseProfileId = Guid.NewGuid() }, Now, false, Dispatch);
        Assert.Equal(0, store.Claims);
        Assert.Equal(ProjectPendingProblem.ContextChanged, session.Problem);
        store.ClaimStatus = ProjectPendingStoreStatus.Unchanged;
        await session.DispatchAsync(Context(frozen), Now, false, Dispatch);
        Assert.Equal(0, calls);
        Assert.Equal(frozen.Payload, session.Frozen.Payload);
        Assert.Equal(frozen.Submission.Operation, session.Record!.Operation);
    }

    [Fact]
    public async Task Exact_deletion_acceptance_survives_storage_failure_and_missing_reads_until_acknowledged_repair() {
        var frozen = Frozen();
        var accepted = Accepted(frozen);
        var store = new Store(frozen) { AcceptanceFailure = new IOException("Quota") };
        using var session = new ProjectLifecyclePendingSession(store, Codec, frozen);
        await session.PrepareAsync();
        var result = await session.DispatchAsync(Context(frozen), Now, false,
            (_, _) => Task.FromResult<ProjectLifecycleOutcome>(new ProjectLifecycleOutcome.Accepted(accepted, false)));
        Assert.Equal(ProjectLifecyclePendingResultKind.Accepted, result.Kind);
        Assert.Same(accepted, session.KnownAccepted);
        Assert.True(session.NeedsStorageRepair);
        Assert.False(session.CanReconcile);
        var prior = store.Record;
        store.Record = null;
        await session.RestoreAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PrepareAsync());
        await session.LookupAsync((operation, _) => Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.ContextChanged(operation, ProjectLifecycleContextChange.Dataset)));
        Assert.True(session.NeedsStorageRepair);
        Assert.Same(accepted, session.KnownAccepted);
        var calls = 0;
        await session.DispatchAsync(Context(frozen), Now, true, (original, _) => {
            calls++;
            return Task.FromResult<ProjectLifecycleOutcome>(Refused(original));
        });
        Assert.Equal(0, calls);
        store.Record = prior;
        store.AcceptanceFailure = null;
        await session.LookupAsync((_, _) => Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.Found(accepted)));
        Assert.False(session.NeedsStorageRepair);
        Assert.True(session.CanReconcile);
        Assert.NotSame(accepted, store.Record!.Accepted!.Accepted);
        Assert.Equal(Codec.EncodeAccepted(frozen.Submission, accepted).Payload, store.Record.Accepted.Payload);
        var reconciled = 0;
        session.Reconcile(receipt => {
            Assert.Equal(frozen.Submission, receipt.Original);
            Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(receipt.Acknowledgement);
            reconciled++;
        });
        Assert.Equal(1, reconciled);
        Assert.False(session.CanReconcile);
        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task A_late_first_attempt_refusal_cannot_erase_a_newer_tabs_original_address_retry() {
        var frozen = Frozen();
        var store = new Store(frozen);
        using var first = new ProjectLifecyclePendingSession(store, Codec, frozen);
        using var second = new ProjectLifecyclePendingSession(store, Codec, frozen);
        await first.PrepareAsync();
        var held = new TaskCompletionSource<ProjectLifecycleOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = first.DispatchAsync(Context(frozen), Now, false, (_, _) => held.Task);
        Exception? primary = null;
        try {
            Assert.Equal(ProjectPendingStage.Dispatching, store.Record!.Stage);
            await second.RestoreAsync();
            await second.LookupAsync((operation, _) => Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.NotObserved(operation, ProjectOperationObservationState.Open)));
            var retryCalls = 0;
            await second.DispatchAsync(Context(frozen) with { Epoch = new(long.MaxValue) }, Now, true, (original, _) => {
                retryCalls++;
                Assert.Same(frozen.Submission, original);
                return Task.FromResult<ProjectLifecycleOutcome>(new ProjectLifecycleOutcome.Unconfirmed(original, ProjectLifecycleRecoveryReason.CommitUnconfirmed));
            });
            held.TrySetResult(Refused(frozen.Submission));
            var late = await pending;
            Assert.Equal(1, retryCalls);
            Assert.Equal(2, store.Claims);
            Assert.Equal(ProjectLifecyclePendingResultKind.Unconfirmed, late.Kind);
            Assert.True(first.Record!.HadUnconfirmedDispatch);
            Assert.Equal(second.Record!.AttemptId, first.Record.AttemptId);
            Assert.Equal(frozen.Submission.Operation, first.Record.Operation);
            Assert.Equal(0, store.Clears);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            held.TrySetResult(Refused(frozen.Submission));
            try {
                await pending;
            } catch (Exception terminal) when (primary is not null && !ReferenceEquals(primary, terminal)) {
                throw new AggregateException(primary, terminal);
            }
        }
    }

    [Fact]
    public async Task First_local_refusal_and_open_absence_cannot_resolve_a_concurrent_original_address_acceptance() {
        var frozen = Frozen();
        var accepted = Accepted(frozen);
        var store = new Store(frozen);
        using var session = new ProjectLifecyclePendingSession(store, Codec, frozen);
        await session.PrepareAsync();
        var externalCommit = new TaskCompletionSource<ProjectLifecycleAccepted>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var refused = await session.DispatchAsync(Context(frozen), Now, false, (original, _) => {
            calls++;
            Assert.Same(frozen.Submission, original);
            Assert.False(externalCommit.Task.IsCompleted);
            return Task.FromResult<ProjectLifecycleOutcome>(Refused(original));
        });
        Assert.Equal(ProjectLifecyclePendingResultKind.Unconfirmed, refused.Kind);
        Assert.IsType<ProjectLifecycleOutcome.Refused>(session.LastOutcome);
        Assert.Equal(ProjectPendingStage.Unconfirmed, session.Record!.Stage);
        Assert.True(session.Record.HadUnconfirmedDispatch);
        await session.LookupAsync((operation, _) => Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.NotObserved(operation, ProjectOperationObservationState.Open)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ClearResolvedAsync(session.Record).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CancelPreparedAsync(session.Record).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PrepareAsync());
        Assert.Equal(1, calls);
        Assert.Equal(1, store.Claims);
        Assert.Equal(0, store.Clears);
        Assert.False(externalCommit.Task.IsCompleted);
        Assert.Equal(frozen.Payload, session.Record.Frozen.Payload);

        externalCommit.SetResult(accepted);
        var found = await session.LookupAsync(async (operation, _) => {
            Assert.Equal(frozen.Submission.Operation, operation);
            return new ProjectLifecycleLookup.Found(await externalCommit.Task);
        });
        Assert.Equal(ProjectLifecyclePendingResultKind.Accepted, found.Kind);
        Assert.Same(accepted, found.Accepted);
        Assert.Equal(Codec.EncodeAccepted(frozen.Submission, accepted).Payload, session.Record!.Accepted!.Payload);
        Assert.Equal(1, calls);
        Assert.Equal(1, store.Claims);
        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task Cross_tab_acceptance_wins_over_stale_claim_and_closed_absence_but_local_restore_needs_owner_lookup() {
        var frozen = Frozen();
        var accepted = Accepted(frozen);
        var store = new Store(frozen);
        using var session = new ProjectLifecyclePendingSession(store, Codec, frozen);
        await session.PrepareAsync();
        await store.ObserveAcceptedAsync(frozen, accepted);
        var calls = 0;
        var stale = await session.DispatchAsync(Context(frozen), Now, false, (original, _) => {
            calls++;
            return Task.FromResult<ProjectLifecycleOutcome>(Refused(original));
        });
        Assert.Equal(0, calls);
        Assert.Equal(ProjectLifecyclePendingResultKind.Accepted, stale.Kind);
        Assert.False(session.HasOwnerAcknowledgement);
        Assert.False(session.CanReconcile);
        var absence = new ProjectLifecycleLookup.ClosedAbsent(frozen.Submission.Operation, Now, Now.AddMinutes(1), Now.AddDays(1));
        var result = await session.LookupAsync((_, _) => Task.FromResult<ProjectLifecycleLookup>(absence));
        Assert.Equal(ProjectLifecyclePendingResultKind.Accepted, result.Kind);
        Assert.Equal(ProjectPendingStage.Accepted, store.Record!.Stage);
        Assert.False(session.CanReconcile);
        await session.LookupAsync((_, _) => Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.Found(accepted)));
        Assert.True(session.CanReconcile);
        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task Retirement_during_claim_stops_dispatch_and_late_owner_acceptance_only_updates_retained_evidence() {
        var frozen = Frozen();
        var store = new Store(frozen);
        using var beforeEntry = new ProjectLifecyclePendingSession(store, Codec, frozen);
        await beforeEntry.PrepareAsync();
        var claim = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.HeldClaim = claim.Task;
        var calls = 0;
        var stopped = beforeEntry.DispatchAsync(Context(frozen), Now, false, (original, _) => {
            calls++;
            return Task.FromResult<ProjectLifecycleOutcome>(Refused(original));
        });
        beforeEntry.Dispose();
        claim.TrySetResult(true);
        await stopped;
        Assert.Equal(0, calls);
        Assert.Equal(ProjectPendingStage.Dispatching, store.Record!.Stage);
        store.HeldClaim = null;
        using var afterEntry = new ProjectLifecyclePendingSession(store, Codec, frozen);
        await afterEntry.RestoreAsync();
        await afterEntry.LookupAsync((operation, _) => Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.NotObserved(operation, ProjectOperationObservationState.Open)));
        var held = new TaskCompletionSource<ProjectLifecycleOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = afterEntry.DispatchAsync(Context(frozen), Now, true, (_, _) => held.Task);
        afterEntry.Dispose();
        held.TrySetResult(new ProjectLifecycleOutcome.Accepted(Accepted(frozen), false));
        await pending;
        Assert.True(afterEntry.HasOwnerAcknowledgement);
        Assert.NotNull(afterEntry.KnownAccepted);
        Assert.False(afterEntry.CanReconcile);
        Assert.Equal(ProjectPendingStage.Accepted, store.Record!.Stage);
        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task Same_revision_state_conflicts_and_unavailable_lookup_cannot_clear_storage_repair_or_authorize_retry() {
        var frozen = Frozen();
        var store = new Store(frozen);
        using var session = new ProjectLifecyclePendingSession(store, Codec, frozen);
        await session.PrepareAsync();
        store.Record = store.Record! with { Stage = ProjectPendingStage.Unconfirmed, AttemptId = Guid.NewGuid(), HadUnconfirmedDispatch = true };
        await session.RestoreAsync();
        Assert.True(session.NeedsStorageRepair);
        Assert.Equal(ProjectPendingStage.Prepared, session.Record!.Stage);
        await session.LookupAsync((operation, _) => Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.NotObserved(operation, ProjectOperationObservationState.Open)));
        var calls = 0;
        await session.DispatchAsync(Context(frozen), Now, true, (original, _) => {
            calls++;
            return Task.FromResult<ProjectLifecycleOutcome>(Refused(original));
        });
        Assert.Equal(0, calls);
        Assert.True(session.NeedsStorageRepair);
        Assert.IsType<InvalidDataException>(session.Failure);
    }

    private static ProjectLifecycleFrozenSubmission Frozen() {
        var profile = Guid.NewGuid();
        var operation = new ProjectLifecycleOperation(profile, new(Guid.NewGuid()), new(9_007_199_254_740_993), new("pc1:" + new string('a', 64)), Guid.NewGuid());
        return Codec.Freeze(new(operation, ProjectLifecycleInterpretation.VersionOne,
            new ProjectLifecycleCommand.Delete(new(new(profile, Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()))));
    }

    private static ProjectLifecycleWriteContext Context(ProjectLifecycleFrozenSubmission frozen)
        => new(frozen.Submission.Operation.DatabaseProfileId, frozen.Submission.Operation.Namespace, frozen.Submission.Operation.CallerBinding,
            frozen.Submission.Operation.Epoch, Now.AddHours(1), Now.AddDays(7));

    private static ProjectLifecycleAccepted Accepted(ProjectLifecycleFrozenSubmission frozen) {
        var command = Assert.IsType<ProjectLifecycleCommand.Delete>(frozen.Submission.Command);
        return new(frozen.Submission, 1, Now, Now.AddDays(7), new ProjectLifecycleAcknowledgement.DeletionCommitted(new(
            new(new(frozen.Submission.Operation.DatabaseProfileId, frozen.Submission.Operation.Namespace, new(Guid.NewGuid())), command.Expected.Project),
            [new(new("cleanup"), Guid.NewGuid())])));
    }

    private static ProjectLifecycleOutcome.Refused Refused(ProjectLifecycleSubmission original) => new(original, ProjectLifecycleRefusal.TargetChanged);

    private sealed class Store(ProjectLifecycleFrozenSubmission frozen) : IProjectLifecyclePendingStore {
        public ProjectLifecyclePendingRecord? Record { get; set; }
        public Exception? PreparationFailure { get; set; }
        public Exception? AcceptanceFailure { get; set; }
        public ProjectPendingStoreStatus ClaimStatus { get; set; } = ProjectPendingStoreStatus.Applied;
        public Task? HeldClaim { get; set; }
        public int Claims { get; private set; }
        public int Clears { get; private set; }
        private long revision;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<ProjectLifecyclePendingChange> PrepareAsync(ProjectLifecycleFrozenSubmission original, CancellationToken cancellationToken = default) {
            Assert.Equal(frozen.Payload, original.Payload);
            if (PreparationFailure is not null) {
                return ValueTask.FromException<ProjectLifecyclePendingChange>(PreparationFailure);
            }
            if (Record is not null) {
                return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Unchanged, Record));
            }
            Record = new(frozen, new(++revision), ProjectPendingStage.Prepared, null, false, null, null);
            return Applied();
        }

        public async ValueTask<ProjectLifecyclePendingChange> BeginDispatchAsync(ProjectLifecyclePendingRecord original, Guid attemptId, bool explicitRetry, CancellationToken cancellationToken = default) {
            Claims++;
            if (Record is null) {
                return new(ProjectPendingStoreStatus.Missing, null);
            }
            if (Record.Revision != original.Revision) {
                return new(ProjectPendingStoreStatus.Changed, Record);
            }
            if (ClaimStatus != ProjectPendingStoreStatus.Applied) {
                return new(ClaimStatus, Record);
            }
            Assert.True(Record.Stage == ProjectPendingStage.Prepared || explicitRetry && Record.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed);
            Record = Record with { Revision = new(++revision), Stage = ProjectPendingStage.Dispatching, AttemptId = attemptId,
                HadUnconfirmedDispatch = Record.HadUnconfirmedDispatch || Record.Stage != ProjectPendingStage.Prepared };
            if (HeldClaim is not null) {
                await HeldClaim;
            }
            return new(ProjectPendingStoreStatus.Applied, Record);
        }

        public ValueTask<ProjectLifecyclePendingChange> ObserveAcceptedAsync(ProjectLifecycleFrozenSubmission original, ProjectLifecycleAccepted accepted, CancellationToken cancellationToken = default) {
            Assert.Equal(frozen.Payload, original.Payload);
            if (AcceptanceFailure is not null) {
                return ValueTask.FromException<ProjectLifecyclePendingChange>(AcceptanceFailure);
            }
            if (Record is null) {
                return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Missing, null));
            }
            var encoded = Codec.EncodeAccepted(original.Submission, accepted);
            if (Record.Accepted is { } previous) {
                Assert.Equal(previous.Payload, encoded.Payload);
                return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Unchanged, Record));
            }
            Assert.NotEqual(ProjectPendingStage.ClosedAbsent, Record.Stage);
            Record = Record with { Revision = new(++revision), Stage = ProjectPendingStage.Accepted, Accepted = Codec.ReadAccepted(original.Submission, encoded.Payload) };
            return Applied();
        }

        public ValueTask<ProjectLifecyclePendingChange> ObserveAttemptAsync(ProjectLifecycleOperation original, Guid attemptId, ProjectPendingAttemptOutcome outcome, CancellationToken cancellationToken = default) {
            Assert.Equal(frozen.Submission.Operation, original);
            if (Record is null) {
                return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Missing, null));
            }
            if (Record.Stage != ProjectPendingStage.Dispatching || Record.AttemptId != attemptId) {
                return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Unchanged, Record));
            }
            Record = Record with { Revision = new(++revision), Stage = ProjectPendingStage.Unconfirmed, HadUnconfirmedDispatch = true };
            return Applied();
        }

        public ValueTask<ProjectLifecyclePendingChange> ObserveClosedAbsentAsync(ProjectLifecycleFrozenSubmission original, ProjectLifecycleLookup.ClosedAbsent absence, CancellationToken cancellationToken = default) {
            var encoded = Codec.EncodeClosedAbsent(original.Submission, absence);
            if (Record is null) {
                return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Missing, null));
            }
            if (Record.Stage is ProjectPendingStage.Accepted or ProjectPendingStage.ClosedAbsent) {
                return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Unchanged, Record));
            }
            Record = Record with { Revision = new(++revision), Stage = ProjectPendingStage.ClosedAbsent, Absence = encoded };
            return Applied();
        }

        public ValueTask<ProjectLifecyclePendingRecord?> ReadAsync(ProjectLifecycleOperation original, CancellationToken cancellationToken = default) {
            Assert.Equal(frozen.Submission.Operation, original);
            return ValueTask.FromResult(Record);
        }

        public ValueTask<ProjectLifecyclePendingPage> ListAsync(ProjectLifecyclePendingScope scope, int take, ProjectLifecycleOperation? after = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public ValueTask<ProjectLifecyclePendingChange> CancelPreparedAsync(ProjectLifecyclePendingRecord original, CancellationToken cancellationToken = default)
            => Clear(original, ProjectPendingStage.Prepared);
        public ValueTask<ProjectLifecyclePendingChange> ClearResolvedAsync(ProjectLifecyclePendingRecord original, CancellationToken cancellationToken = default)
            => Clear(original, ProjectPendingStage.Accepted, ProjectPendingStage.ClosedAbsent);
        private ValueTask<ProjectLifecyclePendingChange> Applied() => ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Applied, Record));
        private ValueTask<ProjectLifecyclePendingChange> Clear(ProjectLifecyclePendingRecord original, params ProjectPendingStage[] allowed) {
            if (Record != original || !allowed.Contains(original.Stage)) {
                throw new InvalidOperationException("Unresolved original lifecycle evidence cannot be cleared.");
            }
            Clears++;
            throw new InvalidOperationException("These lifecycle session actions must not clear retained evidence.");
        }
    }
}
