using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCancellationQuiescenceRecoveryStepTests {
    [Fact]
    public async Task An_empty_guard_retains_the_final_intent_before_mutation() {
        var fixture = new Fixture();
        var checkpoint = await fixture.AdvanceAsync();
        var intent = Assert.IsType<ProcessCancellationFinalizeRoot>(checkpoint.Intent);
        Assert.Equal(fixture.State.RootRunId, intent.RunId);
        Assert.Equal(1, fixture.Reads);
        Assert.Equal(0, fixture.Loads);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_final_acknowledgement_replays_before_new_reads_or_mutation(bool legacy) {
        var fixture = new Fixture();
        if (legacy) {
            fixture.State = fixture.State with { Status = ProcessRuntimeStatus.Cancelled };
        }
        await fixture.AdvanceAsync();
        var retained = fixture.Claim;
        fixture.LoseAcknowledgement = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.AdvanceAsync());
        Assert.Equal(retained, fixture.Claim);
        Assert.Equal(legacy, fixture.LastCommit!.ConfirmRootCancellation);
        if (legacy) {
            Assert.Empty(fixture.LastCommit.Mutation.Events);
            Assert.Empty(fixture.LastCommit.Mutation.OutboxMessages);
            Assert.Equal(fixture.LastCommit.OriginalState, fixture.State with { UpdatedAtUtc = fixture.LastCommit.OriginalState.UpdatedAtUtc });
        }
        fixture.Claim = retained with { Pending = ProcessCancellationRecoveryPending.UnresolvedEffects };
        fixture.Pending = ProcessCancellationRecoveryPending.InvalidEvidence;
        var result = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryStage.Completed, result.Position.Stage);
        Assert.Equal(new(retained.Intent!.Context.CommandId, ProcessCancellationIntentOutcome.Committed), result.Resolution);
        Assert.Equal(1, fixture.Loads);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.Reads);
    }

    [Theory]
    [InlineData(ProcessCancellationRecoveryPending.RuntimeWork)]
    [InlineData(ProcessCancellationRecoveryPending.DecisionWork)]
    [InlineData(ProcessCancellationRecoveryPending.BudgetWork)]
    [InlineData(ProcessCancellationRecoveryPending.ScopeWork)]
    [InlineData(ProcessCancellationRecoveryPending.InvalidEvidence)]
    public async Task Persisted_work_restarts_a_bounded_cycle_without_finalizing(ProcessCancellationRecoveryPending pending) {
        var fixture = new Fixture { Pending = pending };
        var result = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryPosition.Initial, result.Position);
        Assert.Equal(pending, result.Pending);
        Assert.Equal(TimeSpan.FromSeconds(2), result.RetryAfter);
        Assert.Null(result.Intent);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Earlier_pending_work_cannot_be_erased_by_a_clear_persisted_guard() {
        var fixture = new Fixture();
        fixture.Claim = fixture.Claim with { Position = fixture.Claim.Position with { CycleHadPending = true }, Pending = ProcessCancellationRecoveryPending.MissingJournal };
        var result = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryPosition.Initial, result.Position);
        Assert.Equal(ProcessCancellationRecoveryPending.MissingJournal, result.Pending);
        Assert.Equal(0, fixture.Reads);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Retained_pending_evidence_survives_even_a_committed_final_receipt() {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        fixture.LoseAcknowledgement = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.AdvanceAsync());
        fixture.Claim = fixture.Claim with { Position = fixture.Claim.Position with { CycleHadPending = true }, Pending = ProcessCancellationRecoveryPending.ActiveWork };
        var result = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryPosition.Initial, result.Position);
        Assert.Equal(ProcessCancellationIntentOutcome.Committed, result.Resolution!.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(2), result.RetryAfter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_refusal_or_conflict_clears_only_its_original_intent_and_restarts(bool conflict) {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var action = fixture.Claim.Intent!.Context.CommandId;
        fixture.Refuse = !conflict;
        fixture.Failure = conflict ? new ProcessRuntimeOptimisticConcurrencyException(fixture.State.RunId, fixture.State.UpdatedAtUtc) : null;
        var result = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryPosition.Initial, result.Position);
        Assert.Equal(new(action, ProcessCancellationIntentOutcome.RefusedWithoutCommit), result.Resolution);
        Assert.Empty(fixture.Saved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task A_rebound_or_effectful_receipt_cannot_close_recovery(int change) {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var retained = fixture.Claim;
        await fixture.AdvanceAsync();
        var action = retained.Intent!.Context.CommandId;
        var receipt = fixture.Saved[action];
        fixture.Saved[action] = change switch {
            0 => receipt with { State = receipt.State with { RootRunId = ProcessRunId.New() } },
            1 => receipt with { State = receipt.State with { RunId = ProcessRunId.New() } },
            2 => receipt with { State = receipt.State with { Status = ProcessRuntimeStatus.Completed } },
            _ => receipt with { Diagnostics = [new("corrupt", "Unresolved original evidence.")] }
        };
        fixture.Claim = retained;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.Equal(retained, fixture.Claim);
        Assert.Equal(1, fixture.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Finalization_keeps_monotonic_microsecond_precision_after_intent_was_saved(bool legacy) {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var occurred = fixture.Claim.Intent!.Context.OccurredAtUtc;
        fixture.State = fixture.State with { Status = legacy ? ProcessRuntimeStatus.Cancelled : ProcessRuntimeStatus.CancelRequested,
            UpdatedAtUtc = new(occurred.UtcTicks - occurred.UtcTicks % 10 + TimeSpan.TicksPerMinute, TimeSpan.Zero) };
        var before = fixture.State.UpdatedAtUtc;
        await fixture.AdvanceAsync();
        Assert.True(fixture.State.UpdatedAtUtc > before);
        Assert.Equal(0, fixture.State.UpdatedAtUtc.UtcTicks % 10);
        Assert.Equal(legacy, fixture.LastCommit!.ConfirmRootCancellation);
    }

    [Fact]
    public async Task Missing_barrier_refuses_before_reads_or_mutation() {
        var fixture = new Fixture { Stopped = false };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.Equal(0, fixture.Reads);
        Assert.Equal(0, fixture.Loads);
        Assert.Equal(0, fixture.Commits);
    }

    private sealed class Fixture : IProcessCancellationQuiescenceReader, IProcessRootCancellationBarrierReader,
        IProcessRuntimeStateStore, IProcessIdempotencyStore, IProcessRuntimeUnitOfWork {
        internal Fixture() {
            Claim = RecoveryContractFixture.Claim() with { Position = new(ProcessCancellationRecoveryStage.Quiescence) };
            State = new(Claim.RootRunId, Claim.RootRunId, new(Guid.NewGuid()), new string('a', 64), ProcessRuntimeStatus.CancelRequested,
                [], [], [], new HashSet<ArtifactSlotId>(), RecoveryContractFixture.Now);
        }
        internal ProcessCancellationRecoveryClaim Claim { get; set; }
        internal ProcessRuntimeStateSnapshot State { get; set; }
        internal ProcessCancellationRecoveryPending Pending { get; set; }
        internal bool Stopped { get; set; } = true;
        internal bool LoseAcknowledgement { get; set; }
        internal bool Refuse { get; set; }
        internal Exception? Failure { get; set; }
        internal int Reads { get; private set; }
        internal int Loads { get; private set; }
        internal int Commits { get; private set; }
        internal ProcessRuntimeCommitRequest? LastCommit { get; private set; }
        internal Dictionary<RuntimeCommandId, ProcessRuntimeCommitResult> Saved { get; } = [];
        internal async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync() {
            var result = await new ProcessCancellationQuiescenceRecoveryStep(this, this, this, this, this, TimeProvider.System).AdvanceAsync(Claim, default);
            result.Validate(Claim);
            Claim = Claim with { Position = result.Position, Pending = result.Pending, Intent = result.Intent };
            return result;
        }
        public Task<bool> IsStoppedAsync(ProcessRunId rootRunId, CancellationToken cancellationToken) => Task.FromResult(Stopped);
        public Task<ProcessCancellationRecoveryPending> ReadAsync(ProcessRunId rootRunId, CancellationToken cancellationToken) {
            Assert.Equal(Claim.RootRunId, rootRunId);
            Reads++;
            return Task.FromResult(Pending);
        }
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            Assert.Equal(Claim.RootRunId, runId);
            Loads++;
            return Task.FromResult<ProcessRuntimeStateSnapshot?>(State);
        }
        public Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(RuntimeCommandId commandId, CancellationToken cancellationToken) {
            Assert.Equal(Claim.Intent!.Context.CommandId, commandId);
            var result = Saved.GetValueOrDefault(commandId);
            return Task.FromResult(result is null ? null : result with { CommittedNow = false, Events = [], OutboxMessages = [] });
        }
        public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            Commits++;
            LastCommit = request;
            if (Failure is not null) {
                throw Failure;
            }
            if (Refuse) {
                return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(ProcessRuntimeMutation.Rejected(State, "test-refused", "Original work remains.")));
            }
            State = request.Mutation.State;
            var result = ProcessRuntimeCommitResult.FromMutation(request.Mutation) with { CommittedNow = true };
            Saved.Add(request.CommandId, result);
            if (LoseAcknowledgement) {
                throw new IOException("The original finalization committed before acknowledgement.");
            }
            return Task.FromResult(result);
        }
    }
}
