using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationRecoveryQueueTests {
    [Fact]
    public async Task Malformed_record_reports_its_root_and_defers_without_rewriting_evidence_or_starving_the_next_root() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var first = await RootAsync(context);
        var second = await RootAsync(context);
        const string malformed = "{malformed-position";
        var originalIntent = ProcessCancellationRecoveryCodec.WriteIntent(first.RootRunId, new ProcessCancellationCancelRun(first.RunId,
            ProcessCancellationRecoveryIntent.CreateContext(first.RootRunId, DateTimeOffset.UtcNow)));
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE process_cancellation_recovery SET "PositionJson" = {malformed}, "IntentJson" = {originalIntent} WHERE "RootRunId" = {first.RootRunId.Value}
            """);
        var queue = Queue(app.Services);
        var failure = await Assert.ThrowsAsync<ProcessCancellationRecoveryCorruptRecordException>(() => queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease));
        Assert.Equal(first.RootRunId, failure.RootRunId);
        var retained = await context.CancellationRecoveries.AsNoTracking().SingleAsync(item => item.RootRunId == first.RootRunId.Value);
        Assert.Equal(malformed, retained.PositionJson);
        Assert.Equal(originalIntent, retained.IntentJson);
        Assert.Null(retained.LeaseToken);
        Assert.Equal(ProcessCancellationRecoveryPending.InvalidEvidence, retained.Pending);
        Assert.True(retained.DueAtUtc > retained.UpdatedAtUtc);
        Assert.Equal(second.RootRunId, (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!.RootRunId);
    }

    [Fact]
    public async Task Independent_owners_claim_distinct_roots_and_leave_no_transaction_locks_after_return() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var roots = new[] { await RootAsync(context), await RootAsync(context) };
        var claims = await Task.WhenAll(Queue(app.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease),
            Queue(app.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease));
        Assert.All(claims, claim => Assert.NotNull(claim));
        Assert.Equal(roots.Select(item => item.RunId.Value).Order(), claims.Select(item => item!.RootRunId.Value).Order());
        Assert.Null(await Queue(app.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease));
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync("SELECT 1 FROM process_cancellation_recovery FOR UPDATE NOWAIT");
        await context.Database.ExecuteSqlRawAsync("SELECT 1 FROM process_runtime_states FOR UPDATE NOWAIT");
    }

    [Fact]
    public async Task Expired_takeover_preserves_original_intent_and_refuses_every_stale_lease_identity() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await RootAsync(context);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, TimeSpan.FromSeconds(1)))!;
        var intent = Intent(claim);
        claim = (await queue.CheckpointAsync(claim, new(claim.Position with { CycleHadPending = true }, intent))).Claim!;
        var wire = (await context.CancellationRecoveries.AsNoTracking().SingleAsync()).IntentJson;
        await WaitExpiredAsync(app.Services, claim.LeaseExpiresAtUtc);
        var takeover = (await Queue(app.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(intent, takeover.Intent);
        Assert.True(takeover.Position.CycleHadPending);
        Assert.Equal(claim.ProgressRevision, takeover.ProgressRevision);
        Assert.Equal(claim.LeaseGeneration + 1, takeover.LeaseGeneration);
        Assert.NotEqual(claim.LeaseToken, takeover.LeaseToken);
        Assert.Equal(wire, (await context.CancellationRecoveries.AsNoTracking().SingleAsync()).IntentJson);
        foreach (var stale in new[] { claim, takeover with { LeaseToken = claim.LeaseToken }, takeover with { LeaseGeneration = claim.LeaseGeneration } }) {
            Assert.Equal(ProcessCancellationCheckpointStatus.NotOwned, (await queue.CheckpointAsync(stale, new(stale.Position, intent))).Status);
        }
    }

    [Fact]
    public async Task Checkpoint_compare_exchange_uses_stored_intent_and_retains_unknown_outcomes_after_lost_acknowledgement() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await RootAsync(context);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        var intent = Intent(claim);
        var results = await Task.WhenAll(queue.CheckpointAsync(claim, new(claim.Position, intent)),
            Queue(app.Services).CheckpointAsync(claim, new(claim.Position, intent)));
        claim = Assert.Single(results, item => item.Status == ProcessCancellationCheckpointStatus.Applied).Claim!;
        Assert.Single(results, item => item.Status == ProcessCancellationCheckpointStatus.NotOwned);
        var retained = await RowsAsync(context);
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(claim.Position).Validate(claim));
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.CheckpointAsync(claim with { Intent = null }, new(claim.Position)));
        Assert.Equal(retained, await RowsAsync(context));
        var failure = new IOException("Injected recovery checkpoint acknowledgement loss.");
        await Assert.ThrowsAsync<IOException>(() => Queue(app.Services, new LostAcknowledgement(failure)).CheckpointAsync(claim,
            new(claim.Position, intent, ProcessCancellationRecoveryPending.UnresolvedEffects, RetryAfter: TimeSpan.Zero)));
        var recovered = (await Queue(app.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(intent, recovered.Intent);
        Assert.Equal(ProcessCancellationRecoveryPending.UnresolvedEffects, recovered.Pending);
        Assert.Equal(ProcessCancellationCheckpointStatus.NotOwned, (await queue.CheckpointAsync(claim, new(claim.Position, intent))).Status);
    }

    [Fact]
    public async Task Selected_run_agent_cursor_and_pending_cycle_survive_release_and_a_new_owner() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        var selected = claim.Position with { CurrentRun = root.RunId, ExecutionAfter = new(Guid.NewGuid()), CycleHadPending = true };
        Assert.Equal(ProcessCancellationCheckpointStatus.Released, (await queue.CheckpointAsync(claim,
            new(selected, Pending: ProcessCancellationRecoveryPending.UnresolvedEffects, RetryAfter: TimeSpan.Zero))).Status);
        var retained = (await context.CancellationRecoveries.AsNoTracking().SingleAsync()).PositionJson;
        var next = (await Queue(app.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(selected, next.Position);
        Assert.Equal(claim.LeaseGeneration + 1, next.LeaseGeneration);
        Assert.NotEqual(claim.LeaseToken, next.LeaseToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.CheckpointAsync(next,
            new(next.Position with { CycleHadPending = false })));
        Assert.Equal(retained, (await context.CancellationRecoveries.AsNoTracking().SingleAsync()).PositionJson);
        Assert.Equal(ProcessCancellationCheckpointStatus.NotOwned, (await queue.CheckpointAsync(claim,
            new(claim.Position))).Status);
    }

    [Fact]
    public async Task Due_order_active_lease_and_origin_filters_allow_other_roots_to_progress() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var first = await RootAsync(context);
        var second = await RootAsync(context);
        var legacy = await RootAsync(context, legacy: true);
        var queue = Queue(app.Services);
        await queue.SeedLegacyAsync();
        var held = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(first.RunId, held.RootRunId);
        Assert.Equal(second.RunId, (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!.RootRunId);
        var seeded = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LegacySeed, Lease))!;
        Assert.Equal(legacy.RunId, seeded.RootRunId);
        Assert.Null(seeded.BarrierCommandId);
        await queue.CheckpointAsync(held, new(held.Position, Pending: ProcessCancellationRecoveryPending.ActiveWork, RetryAfter: TimeSpan.FromSeconds(1)));
        Assert.Null(await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease));
        var due = (await context.CancellationRecoveries.AsNoTracking().SingleAsync(item => item.RootRunId == first.RunId.Value)).DueAtUtc;
        await WaitExpiredAsync(app.Services, due);
        Assert.Equal(first.RunId, (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!.RootRunId);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, TimeSpan.Zero));
    }

    [Fact]
    public async Task Busy_rows_are_skipped_and_a_checkpoint_delayed_until_after_expiry_cannot_write() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await RootAsync(context);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, TimeSpan.FromSeconds(1)))!;
        var retained = await RowsAsync(context);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new ReaderGate("UPDATE process_cancellation_recovery SET \"PositionJson\"", async () => {
            entered.TrySetResult();
            await resume.Task.WaitAsync(TimeSpan.FromSeconds(10));
        });
        var checkpoint = Queue(app.Services, gate).CheckpointAsync(claim, new(claim.Position, Intent(claim)));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitExpiredAsync(app.Services, claim.LeaseExpiresAtUtc);
            Assert.Null(await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease).WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(ProcessCancellationCheckpointStatus.NotOwned,
                (await queue.CheckpointAsync(claim, new(claim.Position)).WaitAsync(TimeSpan.FromSeconds(2))).Status);
        } finally {
            resume.TrySetResult();
        }
        Assert.Equal(ProcessCancellationCheckpointStatus.NotOwned, (await checkpoint).Status);
        Assert.Equal(retained, await RowsAsync(context));
        Assert.NotNull(await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease));
    }

    [Fact]
    public async Task Verified_root_completion_retains_a_closed_marker_without_another_claim() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var state = await RootAsync(context);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        foreach (var stage in new[] { ProcessCancellationRecoveryStage.Decisions, ProcessCancellationRecoveryStage.SharedScope, ProcessCancellationRecoveryStage.Quiescence }) {
            claim = (await queue.CheckpointAsync(claim, new(new(stage)))).Claim!;
        }
        var intent = new ProcessCancellationFinalizeRoot(state.RunId, ProcessCancellationRecoveryIntent.CreateContext(state.RootRunId, DateTimeOffset.UtcNow));
        claim = (await queue.CheckpointAsync(claim, new(claim.Position, intent))).Claim!;
        var completed = await new ProcessRuntimeEngine(new EfProcessRuntimeUnitOfWork(context)).FinalizeRootCancellationAsync(state, intent.Context);
        Assert.True(completed.CommittedNow);
        Assert.Equal(ProcessCancellationCheckpointStatus.Released, (await queue.CheckpointAsync(claim,
            new(new(ProcessCancellationRecoveryStage.Completed), Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed)))).Status);
        var row = await context.CancellationRecoveries.AsNoTracking().SingleAsync();
        Assert.True(row.Completed);
        Assert.Null(row.IntentJson);
        Assert.Null(row.LeaseToken);
        Assert.Null(await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease));
        Assert.Equal(ProcessRuntimeStatus.Cancelled, (await new EfProcessRuntimeUnitOfWork(context).LoadAsync(state.RunId))!.Status);
    }
}
