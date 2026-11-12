using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshDeliveryQueueTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_admission_replay_preserves_the_original_leased_delivery(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var admission = await ProcessDecisionRefreshCaptureFixture.AdmitAsync(fixture);
        var queue = fixture.Services.GetRequiredService<IProcessDecisionRefreshDeliveryQueue>();
        var claim = (await queue.ClaimAsync(Lease))!;
        Assert.Equal(fixture.State.RunId, claim.RunId);
        Assert.Equal(fixture.State.RootRunId, claim.RootRunId);
        Assert.Equal(admission, claim.AdmissionId);
        await using var context = Context(fixture.Services);
        var before = await RowsAsync(context);
        var receipt = (await fixture.Services.GetRequiredService<IProcessDecisionRefreshReceiptReader>().ReadAsync(claim.RunId, admission))!;
        var intent = Assert.IsType<ProcessDecisionBeginRefresh>(receipt.Decision.Command).Intent;
        Assert.False((await fixture.Services.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(fixture.State, fixture.CommandContext, intent.Request)).CommittedNow);
        Assert.Equal(before, await RowsAsync(context));
        Assert.Null(await queue.ClaimAsync(Lease));
    }

    [Fact]
    public async Task Independent_claims_skip_busy_rows_and_do_not_acquire_root_locks() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var first = await SeedAsync(context);
        var second = await SeedAsync(context);
        var third = await SeedAsync(context);
        await using var locked = Context(app.Services);
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM process_decision_refresh_delivery WHERE "RunId" = {first.RunId} FOR UPDATE""");
        var key = ProcessRootAdvisoryKey.From(second.RootRunId);
        await locked.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
        var claims = await Task.WhenAll(Queue(app.Services).ClaimAsync(Lease), Queue(app.Services).ClaimAsync(Lease)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { second.RunId, third.RunId }.Order(), claims.Select(item => item!.RunId.Value).Order());
        Assert.Null(await Queue(app.Services).ClaimAsync(Lease));
        await transaction.RollbackAsync();
        Assert.Equal(first.RunId, (await Queue(app.Services).ClaimAsync(Lease))!.RunId.Value);
        await context.Database.ExecuteSqlRawAsync("SELECT 1 FROM process_decision_refresh_delivery FOR UPDATE NOWAIT");
    }

    [Fact]
    public async Task Expiry_reclaims_original_identity_and_every_stale_or_spoofed_checkpoint_is_refused() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await SeedAsync(context);
        var queue = Queue(app.Services);
        var original = (await queue.ClaimAsync(TimeSpan.FromSeconds(1)))!;
        await WaitExpiredAsync(app.Services, original.LeaseExpiresAtUtc);
        Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.NotOwned, await queue.CheckpointAsync(original,
            new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation)));
        var next = (await Queue(app.Services).ClaimAsync(Lease))!;
        Assert.Equal(original.LeaseGeneration + 1, next.LeaseGeneration);
        Assert.NotEqual(original.LeaseToken, next.LeaseToken);
        var saved = await RowsAsync(context);
        foreach (var stale in new[] { original, next with { LeaseToken = original.LeaseToken }, next with { LeaseGeneration = original.LeaseGeneration },
                next with { ProgressRevision = next.ProgressRevision + 1 }, next with { WakeRevision = next.WakeRevision + 1 },
                next with { RootRunId = new(Guid.NewGuid()) }, next with { RunId = new(Guid.NewGuid()) }, next with { AdmissionId = RuntimeCommandId.New() } }) {
            Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.NotOwned, await queue.CheckpointAsync(stale,
                new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation)));
        }
        Assert.Equal(saved, await RowsAsync(context));
        var results = await Task.WhenAll(queue.CheckpointAsync(next, new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation)),
            Queue(app.Services).CheckpointAsync(next, new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation)));
        Assert.Single(results, item => item == ProcessDecisionRefreshDeliveryCheckpointStatus.Released);
        Assert.Single(results, item => item == ProcessDecisionRefreshDeliveryCheckpointStatus.NotOwned);
        Assert.Null(await queue.ClaimAsync(Lease));
        var parked = await context.RefreshDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(ProcessDecisionRefreshDeliveryDisposition.AwaitingCapture, parked.Disposition);
        Assert.Null(parked.LeaseToken);
    }

    [Fact]
    public async Task Lost_checkpoint_acknowledgement_preserves_pending_delay_and_allows_other_work() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var first = await SeedAsync(context);
        var second = await SeedAsync(context);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(Lease))!;
        Assert.Equal(first.RunId, claim.RunId.Value);
        var fault = new IOException("Injected refresh delivery checkpoint acknowledgement loss.");
        await Assert.ThrowsAsync<IOException>(() => Queue(app.Services, new LostAcknowledgement(fault)).CheckpointAsync(claim,
            new(ProcessDecisionRefreshDeliveryReason.PendingChild, TimeSpan.FromSeconds(1))));
        Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.NotOwned, await queue.CheckpointAsync(claim,
            new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation)));
        Assert.Equal(second.RunId, (await queue.ClaimAsync(Lease))!.RunId.Value);
        Assert.Null(await queue.ClaimAsync(Lease));
        var pending = await context.RefreshDeliveries.AsNoTracking().SingleAsync(row => row.RunId == first.RunId);
        Assert.Equal(ProcessDecisionRefreshDeliveryReason.PendingChild, pending.Reason);
        Assert.Equal(1, pending.ProgressRevision);
        await WaitExpiredAsync(app.Services, pending.DueAtUtc);
        Assert.Equal(first.RunId, (await queue.ClaimAsync(Lease))!.RunId.Value);
    }

    [Theory]
    [InlineData(ProcessDecisionRefreshDeliveryReason.Advanced)]
    [InlineData(ProcessDecisionRefreshDeliveryReason.CompletedOriginal)]
    [InlineData(ProcessDecisionRefreshDeliveryReason.Stopped)]
    [InlineData(ProcessDecisionRefreshDeliveryReason.LegacyUnknown)]
    [InlineData(ProcessDecisionRefreshDeliveryReason.SourceProjectionRequired)]
    [InlineData(ProcessDecisionRefreshDeliveryReason.ReviewRequired)]
    [InlineData(ProcessDecisionRefreshDeliveryReason.InvalidEvidence)]
    public async Task Definite_and_review_outcomes_leave_the_due_index_without_changing_business_state(ProcessDecisionRefreshDeliveryReason reason) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var row = await SeedAsync(context);
        var state = await context.RuntimeStates.AsNoTracking().SingleAsync();
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(Lease))!;
        Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.Released, await queue.CheckpointAsync(claim, new(reason)));
        Assert.Null(await queue.ClaimAsync(Lease));
        var saved = await context.RefreshDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(reason, saved.Reason);
        Assert.Equal(new ProcessDecisionRefreshDeliveryCheckpoint(reason).Disposition, saved.Disposition);
        Assert.Equal(row.AdmissionCommandId, saved.AdmissionCommandId);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(await context.RuntimeStates.AsNoTracking().SingleAsync()));
        Assert.Equal(1, await context.IdempotencyKeys.CountAsync());
    }

    [Fact]
    public async Task Expiry_between_selection_and_write_refuses_checkpoint_without_leaving_locks() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await SeedAsync(context);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(TimeSpan.FromSeconds(1)))!;
        var before = await RowsAsync(context);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpoint = Queue(app.Services, new CheckpointGate(entered, resume)).CheckpointAsync(claim,
            new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await WaitExpiredAsync(app.Services, claim.LeaseExpiresAtUtc);
            Assert.Null(await queue.ClaimAsync(Lease));
            Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.NotOwned, await queue.CheckpointAsync(claim,
                new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation)));
            resume.TrySetResult();
            Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.NotOwned, await checkpoint.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(before, await RowsAsync(context));
            Assert.NotNull(await queue.ClaimAsync(Lease));
        } finally {
            resume.TrySetResult();
            await Record.ExceptionAsync(() => checkpoint.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public async Task Invalid_bounds_and_checkpoint_outcomes_fail_before_mutation() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await SeedAsync(context);
        var queue = Queue(app.Services);
        foreach (var duration in new[] { TimeSpan.Zero, TimeSpan.FromMinutes(16) }) {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => queue.ClaimAsync(duration));
        }
        var claim = (await queue.ClaimAsync(Lease))!;
        var saved = await RowsAsync(context);
        foreach (var checkpoint in new[] { new ProcessDecisionRefreshDeliveryCheckpoint((ProcessDecisionRefreshDeliveryReason)99),
                new(ProcessDecisionRefreshDeliveryReason.PendingStop), new(ProcessDecisionRefreshDeliveryReason.PendingChild, TimeSpan.FromTicks(-1)),
                new(ProcessDecisionRefreshDeliveryReason.PendingCapture, TimeSpan.FromHours(2)), new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation, TimeSpan.Zero) }) {
            await Assert.ThrowsAsync<InvalidDataException>(() => queue.CheckpointAsync(claim, checkpoint));
        }
        Assert.Equal(saved, await RowsAsync(context));
    }

    internal static EfProcessDecisionRefreshDeliveryQueue Queue(IServiceProvider services, params IInterceptor[] interceptors)
        => new(new Factory(Options(services, interceptors)));

    internal static Task<string[]> RowsAsync(ProcessPersistenceDbContext context)
        => context.Database.SqlQueryRaw<string>("SELECT to_jsonb(delivery)::text AS \"Value\" FROM process_decision_refresh_delivery delivery ORDER BY \"RunId\", \"AdmissionCommandId\"").ToArrayAsync();

    internal static async Task<ProcessDecisionRefreshDeliveryEntity> SeedAsync(ProcessPersistenceDbContext context) {
        var initial = ProcessProjectAdmissionFixture.Initial();
        var state = (await new EfProcessRuntimeUnitOfWork(context).CommitAsync(initial)).State;
        ProcessDecisionRefreshDeliveryEntity row = new() { RootRunId = state.RootRunId.Value, RunId = state.RunId.Value, AdmissionCommandId = initial.CommandId.Value };
        context.RefreshDeliveries.Add(row);
        await context.SaveChangesAsync();
        return row;
    }

    private sealed class CheckpointGate(TaskCompletionSource entered, TaskCompletionSource resume) : DbCommandInterceptor {
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("UPDATE process_decision_refresh_delivery SET", StringComparison.Ordinal)) {
                entered.TrySetResult();
                await resume.Task.WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
            }
            return result;
        }
    }
}
