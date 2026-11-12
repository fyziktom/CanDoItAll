using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationScopeRecoveryTests {
    [Fact]
    public async Task Interrupted_native_capture_stays_pending_until_original_owner_supplies_measured_abort() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync(promptCharacters: 64);
        await using var context = Context(fixture.Services);
        var owner = ProcessRootMaterialFixture.Owner(context, fixture.Services);
        var selected = (await fixture.Query(context).ReadAsync(fixture.State))!;
        var begun = await owner.CommitAsync(await ProcessDecisionRunScopeFixture.BeginRequestAsync(context, fixture.State, selected.Scope));
        Assert.True(begun.CommittedNow);
        var stopped = await StopAsync(owner, begun.State);
        var queue = Queue(fixture.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        claim = (await queue.CheckpointAsync(claim, new(new(ProcessCancellationRecoveryStage.Decisions)))).Claim!;
        claim = (await queue.CheckpointAsync(claim, new(new(ProcessCancellationRecoveryStage.SharedScope)))).Claim!;
        var stage = fixture.Services.GetRequiredService<ProcessCancellationScopeRecoveryStep>();
        var pending = await stage.AdvanceAsync(claim, default);
        pending.Validate(claim);
        Assert.Equal(ProcessCancellationRecoveryPending.CapturePending, pending.Pending);
        Assert.True(pending.Position.CycleHadPending);
        Assert.Null(pending.Intent);
        var capture = begun.DecisionRunScope!.State;
        var budget = (await new EfProcessDecisionBudgetStore(context).ReadAsync(stopped.RunId, null))!;
        var allocation = await context.DecisionBudgetAllocations.AsNoTracking().SingleAsync(item => item.AllocationId == capture.Capture!.AllocationId.Value);
        Assert.Null(allocation.OwnerWorkRevision);
        var aborted = await owner.CommitAsync(ProcessDecisionRunScopeFixture.ScopeRequest(stopped,
            new(capture.Expectation, ProcessDecisionRunScopeCommandKind.AbortCapture),
            new ProcessDecisionBudgetSettlement(new(null, budget.BudgetId, budget.Revision), capture.Capture!.AllocationId, new(9, new(4, 0)))));
        Assert.True(aborted.CommittedNow);
        var intent = await stage.AdvanceAsync(claim, default);
        Assert.Equal(ProcessDecisionRunScopeCommandKind.Cancel, Assert.IsType<ProcessCancellationChangeScope>(intent.Intent).Command.Kind);
        claim = (await queue.CheckpointAsync(claim, intent)).Claim!;
        var resolved = await stage.AdvanceAsync(claim, default);
        resolved.Validate(claim);
        Assert.Equal(ProcessCancellationRecoveryStage.Quiescence, resolved.Position.Stage);
        Assert.Equal(ProcessCancellationIntentOutcome.Committed, resolved.Resolution!.Outcome);
        context.ChangeTracker.Clear();
        allocation = await context.DecisionBudgetAllocations.AsNoTracking().SingleAsync(item => item.AllocationId == capture.Capture.AllocationId.Value);
        Assert.Equal(4, allocation.ConsumedWorkTicks);
        Assert.Equal(9, allocation.OwnerWorkRevision);
        Assert.Equal(ProcessDecisionRunScopePhase.Cancelled, (await context.DecisionRunScopes.AsNoTracking().SingleAsync()).Phase);
    }
}
