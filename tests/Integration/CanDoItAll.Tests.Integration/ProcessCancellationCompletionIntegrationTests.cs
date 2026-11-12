using System.Data.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessCancellationCompletionIntegrationTests {
    [Theory]
    [InlineData(ReadBasisMode.StepSnapshot)]
    [InlineData(ReadBasisMode.RunSnapshot)]
    public async Task Registered_stages_stop_the_real_child_settle_original_work_and_close_root_after_lost_acknowledgement(ReadBasisMode mode) {
        var lost = new SelectedAcknowledgementLoss();
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode,
            configureServices: services => services.ConfigureDbContext<ProcessPersistenceDbContext>((_, options) => options.AddInterceptors(lost)));
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        var stopped = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .BeginRootCancellationAsync(fixture.State, fixture.CommandContext);
        Assert.True(stopped.CommittedNow);
        var queue = Queue(fixture.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        var failed = false;
        var completed = false;
        var actions = new HashSet<RuntimeCommandId>();
        for (var operation = 0; operation < 40; operation++) {
            await using var scope = fixture.Services.CreateAsyncScope();
            var step = scope.ServiceProvider.GetRequiredService<IProcessCancellationRecoveryStep>();
            var inject = !failed && (mode == ReadBasisMode.StepSnapshot ? claim.Intent is ProcessCancellationAccountChild
                : claim.Intent is ProcessCancellationChangeScope { Command.Kind: ProcessDecisionRunScopeCommandKind.Cancel });
            if (inject) {
                var original = claim.Intent;
                var position = claim.Position;
                lost.FailNext = true;
                await Assert.ThrowsAsync<IOException>(() => step.AdvanceAsync(claim, default));
                Assert.False(lost.FailNext);
                await queue.CheckpointAsync(claim, new(position, original, RetryAfter: TimeSpan.Zero));
                var lease = claim.LeaseGeneration;
                claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
                Assert.Equal(original, claim.Intent);
                Assert.Equal(position, claim.Position);
                Assert.True(claim.LeaseGeneration > lease);
                failed = true;
                continue;
            }
            var checkpoint = await step.AdvanceAsync(claim, default);
            checkpoint.Validate(claim);
            Assert.Equal(ProcessCancellationRecoveryPending.None, checkpoint.Pending);
            if (checkpoint.Intent is { } intent) {
                Assert.True(actions.Add(intent.Context.CommandId));
            }
            var saved = await queue.CheckpointAsync(claim, checkpoint);
            if (checkpoint.Position.Stage == ProcessCancellationRecoveryStage.Completed) {
                Assert.Equal(ProcessCancellationCheckpointStatus.Released, saved.Status);
                completed = true;
                break;
            }
            Assert.Equal(ProcessCancellationCheckpointStatus.Applied, saved.Status);
            claim = saved.Claim!;
        }
        Assert.True(completed);
        Assert.True(failed);
        Assert.Equal(mode == ReadBasisMode.StepSnapshot ? 2 : 4, actions.Count);
        var run = (await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id))!;
        Assert.Equal(RunOutcome.Cancelled, run.Outcome);
        var work = run.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks;
        Assert.Equal(fixture.Waiting.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks, work);
        await using var inspection = Context(fixture.Services);
        var root = (await new EfProcessRuntimeUnitOfWork(inspection).LoadAsync(stopped.State.RunId))!;
        Assert.Equal(ProcessRuntimeStatus.Cancelled, root.Status);
        Assert.Equal(ProcessDecisionPhase.Cancelled, (await new EfProcessDecisionStore(inspection).ReadAsync(root.RunId, fixture.Parent.Child.Address.StepInstanceId))!.Phase);
        var child = await inspection.DecisionChildren.AsNoTracking().SingleAsync();
        var allocation = await inspection.DecisionBudgetAllocations.AsNoTracking().SingleAsync(item => item.BudgetId == child.BudgetId && item.AllocationId == child.AllocationId);
        Assert.Equal(work, allocation.ConsumedWorkTicks);
        Assert.NotNull(child.OwnerRevision);
        Assert.Equal(child.OwnerRevision, allocation.OwnerWorkRevision);
        Assert.Equal(0, (await new EfProcessDecisionBudgetStore(inspection).ReadAsync(root.RunId, null))!.ReservedWorkTicks);
        if (mode == ReadBasisMode.RunSnapshot) {
            Assert.Equal(ProcessDecisionRunScopePhase.Cancelled, (await inspection.DecisionRunScopes.AsNoTracking().SingleAsync()).Phase);
        }
        Assert.True((await inspection.CancellationRecoveries.AsNoTracking().SingleAsync()).Completed);
        var receiptCount = await inspection.IdempotencyKeys.CountAsync();
        await using var replayScope = fixture.Services.CreateAsyncScope();
        var idle = await replayScope.ServiceProvider.GetRequiredService<ProcessCancellationRecoveryCoordinator>()
            .RunSliceAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, ProcessCancellationRecoverySliceLimits.Default);
        Assert.Equal(ProcessCancellationRecoverySliceStatus.Idle, idle.Status);
        Assert.Equal(receiptCount, await inspection.IdempotencyKeys.CountAsync());
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    private sealed class SelectedAcknowledgementLoss : DbTransactionInterceptor {
        internal bool FailNext { get; set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (FailNext) {
                FailNext = false;
                throw new IOException("The selected original recovery action committed before acknowledgement.");
            }
            return Task.CompletedTask;
        }
    }
}
