using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationQuiescenceRecoveryTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_final_receipt_closes_original_queue_after_lost_acknowledgement_and_new_lease(bool legacy) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var root = await RootAsync(context, legacy);
        var queue = Queue(app.Services);
        if (legacy) {
            Assert.Equal(1, (await queue.SeedLegacyAsync()).Inserted);
        }
        var origin = legacy ? ProcessCancellationRecoveryOrigin.LegacySeed : ProcessCancellationRecoveryOrigin.LiveBarrier;
        var claim = (await queue.ClaimAsync(origin, Lease))!;
        while (claim.Position.Stage != ProcessCancellationRecoveryStage.Quiescence) {
            claim = (await queue.CheckpointAsync(claim, new(new(claim.Position.Stage + 1)))).Claim!;
        }
        claim = (await queue.CheckpointAsync(claim, await AdvanceAsync(app.Services, claim))).Claim!;
        var original = Assert.IsType<ProcessCancellationFinalizeRoot>(claim.Intent);
        var events = await context.RuntimeEvents.CountAsync();
        var outbox = await context.OutboxMessages.CountAsync();
        var receipts = await context.IdempotencyKeys.CountAsync();
        var failure = new IOException("Native finalization committed before acknowledgement.");
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => AdvanceAsync(app.Services, claim, new LostAcknowledgement(failure))));
        var committed = await new EfProcessRuntimeUnitOfWork(context).FindCompletedCommandAsync(original.Context.CommandId);
        Assert.NotNull(committed);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, committed.State.Status);
        Assert.Equal(receipts + 1, await context.IdempotencyKeys.CountAsync());
        Assert.Equal(events + (legacy ? 0 : 1), await context.RuntimeEvents.CountAsync());
        Assert.Equal(outbox + (legacy ? 0 : 1), await context.OutboxMessages.CountAsync());
        await queue.CheckpointAsync(claim, new(claim.Position, claim.Intent, RetryAfter: TimeSpan.Zero));
        var reopened = Queue(app.Services);
        var recovered = (await reopened.ClaimAsync(origin, Lease))!;
        Assert.Equal(original, recovered.Intent);
        Assert.True(recovered.LeaseGeneration > claim.LeaseGeneration);
        var resolution = await AdvanceAsync(app.Services, recovered);
        Assert.Equal(ProcessCancellationRecoveryStage.Completed, resolution.Position.Stage);
        Assert.Equal(new(original.Context.CommandId, ProcessCancellationIntentOutcome.Committed), resolution.Resolution);
        Assert.Equal(ProcessCancellationCheckpointStatus.Released, (await reopened.CheckpointAsync(recovered, resolution)).Status);
        Assert.Null(await reopened.ClaimAsync(origin, Lease));
        Assert.True((await context.CancellationRecoveries.AsNoTracking().SingleAsync(row => row.RootRunId == root.RunId.Value)).Completed);
        Assert.Equal(receipts + 1, await context.IdempotencyKeys.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persisted_root_local_work_blocks_legacy_confirmation_even_when_the_request_hides_it(bool liveClaim) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var initial = await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.Succeeded);
        var active = await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(initial.State));
        Assert.True(active.Succeeded);
        var root = await LegacyCancelledAsync(context, active.State);
        if (liveClaim) {
            var step = await context.RuntimeSteps.SingleAsync();
            step.Status = ProcessRuntimeStepStatus.Cancelled;
            step.ActiveClaimToken = null;
        } else {
            var claim = await context.DispatchClaims.SingleAsync();
            claim.Status = DispatchClaimStatus.Cancelled;
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        root = (await owner.LoadAsync(root.RunId))!;
        var reader = new EfProcessCancellationQuiescenceReader(new Factory(Options(app.Services)));
        Assert.Equal(ProcessCancellationRecoveryPending.RuntimeWork, await reader.ReadAsync(root.RunId, default));
        var before = await RetainedAsync(context, root);
        var hidden = root with {
            Claims = root.Claims.Select(claim => claim with { Status = DispatchClaimStatus.Cancelled }).ToArray(),
            Steps = root.Steps.Select(step => step with { Status = ProcessRuntimeStepStatus.Cancelled, ActiveClaimToken = null }).ToArray()
        };
        var result = await owner.CommitAsync(new(RuntimeCommandId.New(), hidden, new(ProcessRuntimeTransitionOutcome.Applied,
            hidden with { UpdatedAtUtc = hidden.UpdatedAtUtc.AddSeconds(1) }, [], [], [], [])) { ConfirmRootCancellation = true });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == EfProcessRootCancellationGuard.WorkPending);
        Assert.Equal(before, await RetainedAsync(context, root));
    }

    private static async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(IServiceProvider services,
        ProcessCancellationRecoveryClaim claim, params IInterceptor[] interceptors) {
        await using var context = new ProcessPersistenceDbContext(Options(services, interceptors));
        var factory = new Factory(Options(services));
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var result = await new ProcessCancellationQuiescenceRecoveryStep(new EfProcessCancellationQuiescenceReader(factory),
            new EfProcessRootCancellationBarrierReader(factory), owner, owner, owner, TimeProvider.System).AdvanceAsync(claim, default);
        result.Validate(claim);
        return result;
    }
}
