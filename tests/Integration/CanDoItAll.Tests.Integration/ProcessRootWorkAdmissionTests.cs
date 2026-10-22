using System.Data.Common;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessRootWorkAdmissionTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Same_token_renewal_cannot_escape_root_stop_by_substituting_original_claim_fields(bool legacyFinalized, bool forgedOriginal) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var root = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim((await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State))).State;
        var child = (await owner.CommitAsync(Descendant(root))).State;
        child = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(child))).State;
        root = await StopAsync(owner, root);
        if (legacyFinalized) {
            root = await LegacyCancelledAsync(context, root);
        }
        var next = child with { UpdatedAtUtc = child.UpdatedAtUtc.AddSeconds(1),
            Claims = [child.Claims.Single() with { ExpiresAtUtc = child.Claims.Single().ExpiresAtUtc.AddMinutes(1) }] };
        var original = forgedOriginal ? child with { Claims = next.Claims } : child;
        var request = new ProcessRuntimeCommitRequest(RuntimeCommandId.New(), original,
            new(ProcessRuntimeTransitionOutcome.Applied, next, [], [], [], []));
        var before = await RetainedAsync(context, child);
        var refused = await owner.CommitAsync(request);
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Diagnostics, item => item.Code == EfProcessRootCancellationGuard.WorkAdmissionStopped);
        Assert.Equal(before, await RetainedAsync(context, child));
        var closed = await new ProcessRuntimeEngine(owner).RequestCancellationAsync(child, Command(child));
        Assert.True(closed.CommittedNow);
        Assert.Equal(ProcessRootCancellationPending.None, await EfProcessRootCancellationGuard.ReadPendingAsync(context, root.RunId));
        if (!legacyFinalized) {
            Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(root, Command(root))).CommittedNow);
        }
    }

    [Fact]
    public async Task Nonterminal_descendant_keeps_root_pending_until_its_original_cancellation_commits() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var root = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim((await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State))).State;
        var child = (await owner.CommitAsync(Descendant(root))).State;
        root = await StopAsync(owner, root);
        Assert.Equal(ProcessRootCancellationPending.RuntimeWork, await EfProcessRootCancellationGuard.ReadPendingAsync(context, root.RunId));
        var before = await RetainedAsync(context, root);
        var refused = await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(root, Command(root));
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Diagnostics, item => item.Code == EfProcessRootCancellationGuard.WorkPending);
        Assert.Equal(before, await RetainedAsync(context, root));
        var newClaim = await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(child));
        Assert.False(newClaim.Succeeded);
        Assert.Contains(newClaim.Diagnostics, item => item.Code == EfProcessRootCancellationGuard.WorkAdmissionStopped);
        Assert.True((await new ProcessRuntimeEngine(owner).RequestCancellationAsync(child, Command(child))).Succeeded);
        Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(root, Command(root))).Succeeded);
    }

    [Fact]
    public async Task Previously_accepted_claim_command_replays_after_root_stop_and_child_closure() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var root = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim((await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State))).State;
        var child = (await owner.CommitAsync(Descendant(root))).State;
        var request = ProcessDecisionRunScopeFixture.Claim(child);
        child = (await owner.CommitAsync(request)).State;
        root = await StopAsync(owner, root);
        child = (await new ProcessRuntimeEngine(owner).RequestCancellationAsync(child, Command(child))).State;
        var before = await RetainedAsync(context, child);
        var replay = await owner.CommitAsync(request);
        Assert.False(replay.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, replay.State.Status);
        Assert.Equal(before, await RetainedAsync(context, child));
        Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(root, Command(root))).Succeeded);
    }

    [Fact]
    public async Task Legitimate_failed_root_rework_remains_available() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State;
        var failed = state with { Status = ProcessRuntimeStatus.Failed, UpdatedAtUtc = state.UpdatedAtUtc.AddSeconds(1),
            Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Failed }] };
        state = (await owner.CommitAsync(new(RuntimeCommandId.New(), state,
            new(ProcessRuntimeTransitionOutcome.Applied, failed, [], [], [], [])))).State;
        var reworked = await new ProcessRuntimeEngine(owner).RequestStepReworkAsync(state, Command(state), new(Step(state), "Retry failed work."));
        Assert.True(reworked.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.Active, reworked.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, reworked.State.Steps.Single().Status);
    }

    [Theory]
    [InlineData(LegacyRuntimeWork.Claim)]
    [InlineData(LegacyRuntimeWork.StepBinding)]
    [InlineData(LegacyRuntimeWork.RunningStep)]
    public async Task Legacy_terminal_descendant_with_unclosed_runtime_work_still_blocks_finalization(LegacyRuntimeWork work) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var root = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim((await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State))).State;
        var child = (await owner.CommitAsync(Descendant(root))).State;
        child = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(child))).State;
        root = await StopAsync(owner, root);
        var row = await context.RuntimeStates.Include(item => item.Claims).Include(item => item.Steps)
            .SingleAsync(item => item.RunId == child.RunId.Value);
        row.Status = ProcessRuntimeStatus.Cancelled;
        if (work == LegacyRuntimeWork.Claim) {
            row.Steps.Single().ActiveClaimToken = null;
            row.Steps.Single().Status = ProcessRuntimeStepStatus.Cancelled;
        } else {
            row.Claims.Single().Status = DispatchClaimStatus.Completed;
            if (work == LegacyRuntimeWork.RunningStep) {
                row.Steps.Single().ActiveClaimToken = null;
            } else {
                row.Steps.Single().Status = ProcessRuntimeStepStatus.Cancelled;
            }
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(ProcessRootCancellationPending.RuntimeWork, await EfProcessRootCancellationGuard.ReadPendingAsync(context, root.RunId));
        var before = await RetainedAsync(context, root);
        Assert.False((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(root, Command(root))).Succeeded);
        Assert.Equal(before, await RetainedAsync(context, root));
    }

    [Fact]
    public async Task Shared_scope_reconciliation_also_fences_same_token_renewal() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var initial = ProcessDecisionRunScopeFixture.Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scope = (await ProcessDecisionRunScopeFixture.Scopes(context, app.Services)
            .ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        var captured = await ProcessDecisionRunScopeFixture.CaptureAsync(context, app.Services, state, scope);
        state = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(captured.State))).State;
        state = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.ScopeRequest(state,
            new(captured.DecisionRunScope!.State.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)))).State;
        var before = await RetainedAsync(context, state);
        var request = Request(state, null);
        request = request with { Mutation = request.Mutation with { State = request.Mutation.State with {
            Claims = [state.Claims.Single() with { ExpiresAtUtc = state.Claims.Single().ExpiresAtUtc.AddSeconds(1) }]
        } } };
        var error = await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => owner.CommitAsync(request));
        Assert.Equal(ProcessDecisionRunScopeRefusal.InvalidPhase, error.Reason);
        Assert.Equal(before, await RetainedAsync(context, state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_root_identity_cannot_be_substituted_in_either_snapshot(bool bothSnapshots) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = await StopAsync(owner, (await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State);
        var next = state with { RootRunId = ProcessRunId.New(), UpdatedAtUtc = state.UpdatedAtUtc.AddSeconds(1) };
        var request = new ProcessRuntimeCommitRequest(RuntimeCommandId.New(), bothSnapshots ? state with { RootRunId = next.RootRunId } : state,
            new(ProcessRuntimeTransitionOutcome.Applied, next, [], [], [], []));
        var before = await RetainedAsync(context, state);
        if (bothSnapshots) {
            await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() => owner.CommitAsync(request));
        } else {
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(request));
        }
        Assert.Equal(before, await RetainedAsync(context, state));
    }

    [Fact]
    public async Task Committing_root_barrier_fences_waiting_claim_without_serializing_another_root() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var root = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim((await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State))).State;
        var child = (await owner.CommitAsync(Descendant(root))).State;
        var independent = (await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State;
        var pause = new BeforeCommit();
        await using var held = Context(app.Services, pause);
        await using var contender = Context(app.Services);
        var stopping = StopAsync(ProcessDecisionRunScopeFixture.Owner(held, app.Services), root);
        Task<ProcessRuntimeCommitResult>? waiting = null;
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            waiting = ProcessDecisionRunScopeFixture.Owner(contender, app.Services).CommitAsync(ProcessDecisionRunScopeFixture.Claim(child));
            var progressed = await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(independent)).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(progressed.CommittedNow);
            Assert.False(waiting.IsCompleted);
            Assert.False(stopping.IsCompleted);
        } finally {
            pause.Release.TrySetResult();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            if (waiting is not null) {
                var refused = await waiting.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(refused.Succeeded);
                Assert.Contains(refused.Diagnostics, item => item.Code == EfProcessRootCancellationGuard.WorkAdmissionStopped);
            }
        }
        Assert.Empty((await owner.LoadAsync(child.RunId))!.Claims);
    }

    private sealed class BeforeCommit : DbTransactionInterceptor {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }

    public enum LegacyRuntimeWork { Claim, StepBinding, RunningStep }
}
