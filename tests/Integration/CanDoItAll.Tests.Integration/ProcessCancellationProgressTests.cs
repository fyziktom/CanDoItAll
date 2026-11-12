using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationProgressTests(ITestOutputHelper output) {
    [Fact]
    public async Task Missing_active_child_and_legacy_reads_never_seed_or_observe_another_runs_queue() {
        await using var app = await TestApplication.CreateAsync(ProcessExecutionAuthorityPersistenceTests.Harness());
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var root = await CommitAsync(owner, ProcessProjectAdmissionFixture.Initial());
        root = await CommitAsync(owner, ProcessDecisionRunScopeFixture.Claim(root));
        var child = await CommitAsync(owner, Descendant(root));
        var reader = Reader(app.Services);
        Assert.Null(await reader.ReadAsync(new(Guid.NewGuid())));
        Assert.Equal(ProcessCancellationTracking.NotRequested, (await reader.ReadAsync(root.RunId))!.Tracking);
        root = await StopAsync(owner, root);
        var childProgress = (await reader.ReadAsync(child.RunId))!;
        Assert.Equal(root.RunId, childProgress.RootRunId);
        Assert.Equal(ProcessCancellationTracking.ChildRun, childProgress.Tracking);
        Assert.Null(childProgress.Recovery);
        var legacy = await RootAsync(context, legacy: true);
        var before = await RetainedAsync(context, legacy);
        var legacyProgress = (await Reader(app.Services).ReadAsync(legacy.RunId))!;
        Assert.Equal(ProcessRuntimeStatus.Cancelled, legacyProgress.RuntimeStatus);
        Assert.Equal(ProcessCancellationTracking.LegacyUntracked, legacyProgress.Tracking);
        Assert.Null(legacyProgress.Recovery);
        Assert.Equal(before, await RetainedAsync(context, legacy));
        Assert.Single(await RowsAsync(context));
    }

    [Fact]
    public async Task Original_pending_barrier_and_verified_completion_are_observed_without_mutating_any_receipt() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        var pending = (await Reader(app.Services).ReadAsync(root.RunId))!;
        Assert.Equal(ProcessCancellationTracking.Pending, pending.Tracking);
        Assert.Equal(claim.BarrierCommandId, pending.Recovery!.BarrierCommandId);
        Assert.Equal(claim.ProgressRevision, pending.Recovery.Revision);
        foreach (var stage in new[] { ProcessCancellationRecoveryStage.Decisions, ProcessCancellationRecoveryStage.SharedScope, ProcessCancellationRecoveryStage.Quiescence }) {
            claim = (await queue.CheckpointAsync(claim, new(new(stage)))).Claim!;
        }
        var intent = new ProcessCancellationFinalizeRoot(root.RunId, ProcessCancellationRecoveryIntent.CreateContext(root.RootRunId, DateTimeOffset.UtcNow));
        claim = (await queue.CheckpointAsync(claim, new(claim.Position, intent))).Claim!;
        Assert.True((await new ProcessRuntimeEngine(new EfProcessRuntimeUnitOfWork(context)).FinalizeRootCancellationAsync(root, intent.Context)).CommittedNow);
        var beforeCheckpoint = (await Reader(app.Services).ReadAsync(root.RunId))!;
        Assert.Equal(ProcessRuntimeStatus.Cancelled, beforeCheckpoint.RuntimeStatus);
        Assert.Equal(ProcessCancellationTracking.Pending, beforeCheckpoint.Tracking);
        Assert.Equal(ProcessCancellationCheckpointStatus.Released, (await queue.CheckpointAsync(claim,
            new(new(ProcessCancellationRecoveryStage.Completed), Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed)))).Status);
        var before = await RetainedAsync(context, root);
        var completed = (await Reader(app.Services).ReadAsync(root.RunId))!;
        Assert.Equal(ProcessCancellationTracking.Completed, completed.Tracking);
        Assert.Equal(ProcessCancellationRecoveryStage.Completed, completed.Recovery!.Stage);
        Assert.Equal(claim.BarrierCommandId, completed.Recovery.BarrierCommandId);
        Assert.True(completed.Recovery.Revision > pending.Recovery.Revision);
        Assert.Equal(completed, await Reader(app.Services).ReadAsync(root.RunId));
        Assert.Equal(before, await RetainedAsync(context, root));
    }

    [Theory]
    [InlineData(Corruption.MalformedPosition)]
    [InlineData(Corruption.NoncanonicalPosition)]
    [InlineData(Corruption.PositionWithoutMarker)]
    [InlineData(Corruption.MarkerWithoutPosition)]
    [InlineData(Corruption.MarkerBeforeRuntimeStop)]
    [InlineData(Corruption.InvalidEvidence)]
    [InlineData(Corruption.ActiveRuntime)]
    [InlineData(Corruption.OversizedPosition)]
    public async Task Invalid_progress_is_explicit_reconciliation_and_preserves_the_original_bytes(Corruption corruption) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        var position = ProcessCancellationRecoveryCodec.WritePosition(ProcessCancellationRecoveryPosition.Initial);
        var completed = false;
        var pending = ProcessCancellationRecoveryPending.None;
        switch (corruption) {
            case Corruption.MalformedPosition:
                position = "{retained-invalid-progress";
                break;
            case Corruption.NoncanonicalPosition:
                position = " " + position;
                break;
            case Corruption.PositionWithoutMarker:
                position = ProcessCancellationRecoveryCodec.WritePosition(new(ProcessCancellationRecoveryStage.Completed));
                break;
            case Corruption.MarkerWithoutPosition:
                completed = true;
                break;
            case Corruption.MarkerBeforeRuntimeStop:
                position = ProcessCancellationRecoveryCodec.WritePosition(new(ProcessCancellationRecoveryStage.Completed));
                completed = true;
                break;
            case Corruption.InvalidEvidence:
                pending = ProcessCancellationRecoveryPending.InvalidEvidence;
                break;
            case Corruption.ActiveRuntime:
                await context.Database.ExecuteSqlInterpolatedAsync($"""UPDATE process_runtime_states SET "Status" = 'Active' WHERE "RunId" = {root.RunId.Value}""");
                break;
            case Corruption.OversizedPosition:
                await context.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE process_cancellation_recovery DROP CONSTRAINT "CK_process_cancellation_recovery_payload"
                    """);
                position = new string('界', 800);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE process_cancellation_recovery SET "PositionJson" = {position}, "Completed" = {completed}, "Pending" = {(int)pending}, "ProgressRevision" = 1
            WHERE "RootRunId" = {root.RunId.Value}
            """);
        var before = await RetainedAsync(context, root);
        var progress = (await Reader(app.Services).ReadAsync(root.RunId))!;
        Assert.Equal(ProcessCancellationTracking.ReconciliationRequired, progress.Tracking);
        Assert.DoesNotContain(position, JsonSerializer.Serialize(progress), StringComparison.Ordinal);
        Assert.Equal(before, await RetainedAsync(context, root));
    }

    [Fact]
    public async Task Independent_canonical_profiles_cannot_observe_each_others_run_or_barrier() {
        await using var first = await TestApplication.CreateAsync();
        await using var second = await TestApplication.CreateAsync();
        await using var context = Context(first.Services);
        var root = await RootAsync(context);
        Assert.NotNull(await Reader(first.Services).ReadAsync(root.RunId));
        Assert.Null(await Reader(second.Services).ReadAsync(root.RunId));
        await using var other = Context(second.Services);
        Assert.Empty(await RowsAsync(other));
    }

    [Fact]
    public async Task Caller_cancellation_interrupts_a_real_blocked_SQL_read_without_claiming_or_repairing() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        var before = await RowsAsync(context);
        var capture = new QueryCapture();
        var reader = Reader(app.Services, capture);
        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(root.RunId, alreadyCancelled.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(default));
        Assert.Empty(capture.Commands);
        await using (var transaction = await context.Database.BeginTransactionAsync()) {
            await context.Database.ExecuteSqlRawAsync("LOCK TABLE process_cancellation_recovery IN ACCESS EXCLUSIVE MODE");
            using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            var started = Stopwatch.GetTimestamp();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(root.RunId, deadline.Token));
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(8));
            Assert.Single(capture.Commands);
            await transaction.RollbackAsync();
        }
        Assert.Equal(before, await RowsAsync(context));
        Assert.Equal(ProcessCancellationTracking.Pending, (await reader.ReadAsync(root.RunId))!.Tracking);
    }

    [Fact]
    public async Task Selected_observation_uses_two_primary_keys_and_one_bounded_read_regardless_of_history() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO process_runtime_states ("RunId", "RootRunId", "PlanId", "PlanHash", "Status", "UpdatedAtUtc", "ConcurrencyToken", "BlockedRecoveryActionsJson")
            SELECT id, id, source."PlanId", source."PlanHash", 'CancelRequested', source."UpdatedAtUtc", gen_random_uuid(), '[]'
            FROM process_runtime_states source CROSS JOIN (SELECT gen_random_uuid() AS id FROM generate_series(1, 2000)) history
            WHERE source."RunId" = {root.RunId.Value};
            INSERT INTO process_cancellation_recovery ("RootRunId", "Origin", "BarrierCommandId", "LeaseGeneration", "ProgressRevision", "Cycle", "PositionJson", "Pending", "Completed")
            SELECT state."RunId", 0, gen_random_uuid(), 0, 0, 0, recovery."PositionJson", 0, FALSE
            FROM process_runtime_states state CROSS JOIN process_cancellation_recovery recovery
            WHERE recovery."RootRunId" = {root.RunId.Value} AND state."RunId" <> {root.RunId.Value};
            ANALYZE process_runtime_states;
            ANALYZE process_cancellation_recovery;
            """);
        var before = await RowsAsync(context);
        var capture = new QueryCapture();
        var started = Stopwatch.GetTimestamp();
        Assert.Equal(ProcessCancellationTracking.Pending, (await Reader(app.Services, capture).ReadAsync(root.RunId))!.Tracking);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var selected = Assert.Single(capture.Commands);
        Assert.Equal(5, selected.Timeout);
        Assert.Contains("octet_length", selected.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("FOR UPDATE", selected.Sql, StringComparison.Ordinal);
        await context.Database.OpenConnectionAsync();
        await using var explain = context.Database.GetDbConnection().CreateCommand();
        explain.CommandText = "EXPLAIN (ANALYZE, FORMAT JSON) " + selected.Sql;
        explain.CommandTimeout = 5;
        foreach (var parameter in selected.Parameters) {
            explain.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Value));
        }
        var plan = (string)(await explain.ExecuteScalarAsync())!;
        Assert.Contains("PK_process_runtime_states", plan, StringComparison.Ordinal);
        Assert.Contains("PK_process_cancellation_recovery", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Seq Scan", plan, StringComparison.Ordinal);
        Assert.Equal(before, await RowsAsync(context));
        output.WriteLine(JsonSerializer.Serialize(new { historicalRoots = 2000, statements = capture.Commands.Count,
            elapsedMilliseconds = elapsed, plan = JsonDocument.Parse(plan).RootElement }));
    }

    private static EfProcessCancellationProgressReader Reader(IServiceProvider services, params IInterceptor[] interceptors)
        => new(new Factory(Options(services, interceptors)));

    private static async Task<ProcessRuntimeStateSnapshot> CommitAsync(EfProcessRuntimeUnitOfWork owner, ProcessRuntimeCommitRequest request) {
        var result = await owner.CommitAsync(request);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        return result.State;
    }

    public enum Corruption { MalformedPosition, NoncanonicalPosition, PositionWithoutMarker, MarkerWithoutPosition,
        MarkerBeforeRuntimeStop, InvalidEvidence, ActiveRuntime, OversizedPosition }

    private sealed record CapturedQuery(string Sql, int Timeout, (string Name, object Value)[] Parameters);

    private sealed class QueryCapture : DbCommandInterceptor {
        internal List<CapturedQuery> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(new(command.CommandText, command.CommandTimeout, command.Parameters.Cast<DbParameter>()
                .Select(parameter => (parameter.ParameterName, parameter.Value!)).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
