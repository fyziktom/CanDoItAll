using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationCoverageUpgradeTests {
    [Fact]
    public async Task Claim_restarts_old_partial_coverage_without_replacing_the_original_barrier() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        var original = await context.CancellationRecoveries.AsNoTracking().SingleAsync();
        var old = new ProcessCancellationRecoveryPosition(ProcessCancellationRecoveryStage.Runtime,
            CurrentRun: root.RunId, ExecutionAfter: new(Guid.NewGuid()), CycleHadPending: true, CoverageVersion: 1);
        await StorePositionAsync(context, old);
        var claim = (await Queue(app.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(ProcessCancellationRecoveryPosition.Initial, claim.Position);
        Assert.Equal(original.BarrierCommandId, claim.BarrierCommandId!.Value.Value);
        Assert.Equal(root.RunId, claim.RootRunId);
        Assert.Equal(original.ProgressRevision + 1, claim.ProgressRevision);
        Assert.Equal(original.Cycle + 1, claim.Cycle);
        Assert.Equal(ProcessCancellationRecoveryPending.None, claim.Pending);
    }

    [Theory]
    [InlineData(ProcessCancellationIntentOutcome.Committed)]
    [InlineData(ProcessCancellationIntentOutcome.RefusedWithoutCommit)]
    public async Task Retained_old_intent_survives_unknown_outcome_and_restarts_only_after_exact_resolution(ProcessCancellationIntentOutcome outcome) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        var old = new ProcessCancellationRecoveryPosition(ProcessCancellationRecoveryStage.Quiescence, CoverageVersion: 1);
        var intent = new ProcessCancellationFinalizeRoot(root.RunId,
            ProcessCancellationRecoveryIntent.CreateContext(root.RootRunId, DateTimeOffset.UtcNow));
        await StorePositionAsync(context, old, ProcessCancellationRecoveryCodec.WriteIntent(root.RootRunId, intent));
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(old, claim.Position);
        Assert.Equal(intent, claim.Intent);
        await queue.CheckpointAsync(claim, new(old, intent, ProcessCancellationRecoveryPending.UnresolvedEffects, RetryAfter: TimeSpan.Zero));
        var retained = (await Queue(app.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(old, retained.Position);
        Assert.Equal(intent, retained.Intent);
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.CheckpointAsync(retained,
            new(old, Resolution: new(new(Guid.NewGuid()), outcome))));
        var resolved = outcome == ProcessCancellationIntentOutcome.Committed
            ? new ProcessCancellationRecoveryCheckpoint(new(ProcessCancellationRecoveryStage.Completed),
                Resolution: new(intent.Context.CommandId, outcome))
            : new(ProcessCancellationRecoveryPosition.Initial, Pending: ProcessCancellationRecoveryPending.ActiveWork,
                Resolution: new(intent.Context.CommandId, outcome), RetryAfter: TimeSpan.FromSeconds(2));
        Assert.Equal(ProcessCancellationCheckpointStatus.Released, (await queue.CheckpointAsync(retained, resolved)).Status);
        var upgraded = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(ProcessCancellationRecoveryPosition.Initial, upgraded.Position);
        Assert.Null(upgraded.Intent);
        Assert.Equal(claim.BarrierCommandId, upgraded.BarrierCommandId);
        Assert.Equal(claim.Cycle + 1, upgraded.Cycle);
        Assert.False((await context.CancellationRecoveries.AsNoTracking().SingleAsync()).Completed);
    }

    [Theory]
    [InlineData(1, ProcessCancellationTracking.ReconciliationRequired, 1)]
    [InlineData(2, ProcessCancellationTracking.Completed, 0)]
    public async Task Bounded_seed_reopens_only_old_completed_coverage_and_preserves_runtime_and_barrier(int version,
        ProcessCancellationTracking expected, int reopened) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        await ProcessRootCancellationFixture.LegacyCancelledAsync(context, root);
        await StorePositionAsync(context, new(ProcessCancellationRecoveryStage.Completed, CoverageVersion: version), completed: true);
        var saved = await context.CancellationRecoveries.AsNoTracking().SingleAsync();
        var runtime = await context.Database.SqlQueryRaw<string>("""SELECT to_jsonb(r)::text AS "Value" FROM process_runtime_states r""").SingleAsync();
        var events = await context.RuntimeEvents.CountAsync();
        var reader = new EfProcessCancellationProgressReader(new Factory(Options(app.Services)));
        Assert.Equal(expected, (await reader.ReadAsync(root.RunId))!.Tracking);
        var result = await Queue(app.Services).SeedLegacyAsync(1);
        Assert.Equal(new(ProcessCancellationSeedStatus.Advanced, 1, 0, reopened), result);
        var after = await context.CancellationRecoveries.AsNoTracking().SingleAsync();
        Assert.Equal(saved.BarrierCommandId, after.BarrierCommandId);
        Assert.Equal(saved.Origin, after.Origin);
        Assert.Equal(saved.ProgressRevision + reopened, after.ProgressRevision);
        Assert.Equal(saved.Cycle + reopened, after.Cycle);
        Assert.Equal(reopened == 0, after.Completed);
        Assert.Equal(runtime, await context.Database.SqlQueryRaw<string>("""SELECT to_jsonb(r)::text AS "Value" FROM process_runtime_states r""").SingleAsync());
        Assert.Equal(events, await context.RuntimeEvents.CountAsync());
        Assert.Equal(reopened == 0 ? expected : ProcessCancellationTracking.Pending, (await reader.ReadAsync(root.RunId))!.Tracking);
    }

    [Fact]
    public async Task Corrupt_completed_coverage_is_not_rewritten_and_does_not_advance_the_seed() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        const string malformed = """{"version":1,"stage":4,"unexpected":true}""";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE process_cancellation_recovery SET "PositionJson" = {malformed}, "Completed" = TRUE
            """);
        var retained = await RowsAsync(context);
        var failure = await Assert.ThrowsAsync<ProcessCancellationRecoveryCorruptRecordException>(() => Queue(app.Services).SeedLegacyAsync(1));
        Assert.Equal(root.RunId, failure.RootRunId);
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Equal(retained, await RowsAsync(context));
        Assert.Equal(0, (await context.CancellationRecoverySeeds.AsNoTracking().SingleAsync()).Revision);
    }

    [Fact]
    public async Task Locked_old_completion_cancels_its_page_without_skipping_the_root_then_reopens_after_release() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await RootAsync(context);
        await StorePositionAsync(context, new(ProcessCancellationRecoveryStage.Completed, CoverageVersion: 1), completed: true);
        await using (var transaction = await context.Database.BeginTransactionAsync()) {
            await context.Database.ExecuteSqlRawAsync("SELECT 1 FROM process_cancellation_recovery FOR UPDATE");
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Queue(app.Services).SeedLegacyAsync(1, timeout.Token));
            Assert.Equal(0, (await context.CancellationRecoverySeeds.AsNoTracking().SingleAsync()).Revision);
            Assert.True((await context.CancellationRecoveries.AsNoTracking().SingleAsync()).Completed);
        }
        Assert.Equal(1, (await Queue(app.Services).SeedLegacyAsync(1)).Reopened);
    }

    private static Task StorePositionAsync(ProcessPersistenceDbContext context, ProcessCancellationRecoveryPosition position,
        string? intent = null, bool completed = false) {
        var json = ProcessCancellationRecoveryCodec.WritePosition(position);
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE process_cancellation_recovery SET "PositionJson" = {json}, "IntentJson" = {intent}, "Completed" = {completed},
                "ProgressRevision" = CASE WHEN {completed} THEN 1 ELSE "ProgressRevision" END
            """);
    }
}
