using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessRuntimeDispatchPreconditionTests {
    [Fact]
    public async Task Concurrent_shared_capture_reservations_give_one_fresh_owner_and_attach_once() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var clock = new CaptureClock();
        var owner = Owner(context, app.Services, clock);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scope = (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        var request = await BeginRequestAsync(context, state, scope);
        var blockedClaim = await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => owner.CommitAsync(Claim(state)));
        Assert.Equal(ProcessDecisionRunScopeRefusal.InvalidPhase, blockedClaim.Reason);
        var reservations = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
            await using var contender = Context(app.Services);
            var result = await Owner(contender, app.Services, clock).CommitAsync(request);
            Assert.Null(contender.Database.CurrentTransaction);
            return result;
        }));
        var fresh = Assert.Single(reservations, item => item.CommittedNow);
        Assert.Single(reservations, item => !item.CommittedNow);
        Assert.All(reservations, item => Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, item.Outcome));
        Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
        var captures = 0;
        foreach (var result in reservations) {
            if (!result.CommittedNow) {
                continue;
            }
            captures++;
            var complete = await CompleteRequestAsync(context, result.State, result.DecisionRunScope!.State);
            var committed = await owner.CommitAsync(complete);
            Assert.True(committed.CommittedNow);
            Assert.Equal(ProcessDecisionRunScopePhase.Active, committed.DecisionRunScope!.State.Phase);
            Assert.Equal(3, committed.DecisionBudget!.Change.Balance.ConsumedWorkTicks);
            Assert.Equal(0, committed.DecisionBudget.Change.Balance.ReservedWorkTicks);
            Assert.True((await owner.CommitAsync(Claim(committed.State))).CommittedNow);
        }
        Assert.Equal(1, captures);
        Assert.Single(await context.DecisionMaterials.ToArrayAsync());
        var historical = await Owner(context, app.Services, clock).FindCompletedCommandAsync(request.CommandId);
        Assert.False(historical!.CommittedNow);
        Assert.Equal(fresh.DecisionBudget, historical.DecisionBudget);
    }

    [Theory]
    [InlineData(DispatchClaimStatus.Claimed)]
    [InlineData(DispatchClaimStatus.LeaseRenewed)]
    [InlineData(DispatchClaimStatus.Reclaimed)]
    public async Task All_live_claim_states_allow_the_exact_guarded_reservation(DispatchClaimStatus status) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await PrepareAsync(context, app.Services, status);
        var result = await Owner(context, app.Services, new CaptureClock()).CommitAsync(request);
        Assert.True(result.CommittedNow);
        Assert.Equal(10, result.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        Assert.Null(result.DecisionBudget.Change.Allocation.Settlement);
        var complete = await CompleteStepCaptureAsync(context, app.Services, result);
        var attached = await Owner(context, app.Services, new CaptureClock()).CommitAsync(complete);
        Assert.True(attached.CommittedNow);
        Assert.Equal(complete.DecisionMaterial!.Stamp, attached.Decision!.Command.Material);
        Assert.Equal(3, attached.DecisionBudget!.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(0, attached.DecisionBudget.Change.Balance.ReservedWorkTicks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Guard_rejects_wrong_identity_or_mutation_of_its_claim(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await PrepareAsync(context, app.Services);
        var next = request.Mutation.State;
        request = change switch {
            0 => request with { DispatchPrecondition = request.DispatchPrecondition! with { OwnerId = new("another-owner") } },
            1 => request with { DispatchPrecondition = request.DispatchPrecondition! with { ClaimToken = DispatchClaimToken.New() } },
            2 => request with { DispatchPrecondition = request.DispatchPrecondition! with { StepInstanceId = ProcessStepInstanceId.New() } },
            3 => request with { Mutation = request.Mutation with { State = next with { Claims = [next.Claims.Single() with { ExpiresAtUtc = next.Claims.Single().ExpiresAtUtc.AddMinutes(1) }] } } },
            4 => request with { Mutation = request.Mutation with { State = next with { Claims = [next.Claims.Single() with { Status = DispatchClaimStatus.Released }] } } },
            5 => request with { Mutation = request.Mutation with { State = next with { Steps = [next.Steps.Single() with { AttemptNumber = 99 }] } } },
            _ => request with { Mutation = request.Mutation with { State = next with { Claims = [next.Claims.Single() with { ResultIdempotencyKey = StrategyResultIdempotencyKey.New() }] } } }
        };
        var owner = Owner(context, app.Services, new CaptureClock());
        if (change == 2) {
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.CommitAsync(request));
        } else {
            var failure = await Assert.ThrowsAsync<ProcessRuntimeDispatchPreconditionException>(() => owner.CommitAsync(request));
            Assert.Equal(ProcessRuntimeDispatchPreconditionRefusal.ClaimChanged, failure.Refusal);
        }
        await AssertUnchangedAsync(context, owner, request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expiry_before_admission_or_after_flush_keeps_all_participants_unchanged(bool afterFlush) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await PrepareAsync(context, app.Services);
        var deadline = request.OriginalState.Claims.Single().ExpiresAtUtc;
        var clock = new CaptureClock { Now = afterFlush ? ProcessProjectAdmissionFixture.Now : deadline };
        var fault = new AfterFlush(() => clock.Now = deadline);
        await using (var observed = Context(app.Services, fault)) {
            var failure = await Assert.ThrowsAsync<ProcessRuntimeDispatchPreconditionException>(() =>
                Owner(observed, app.Services, clock).CommitAsync(request));
            Assert.Equal(ProcessRuntimeDispatchPreconditionRefusal.LeaseExpired, failure.Refusal);
            Assert.Equal(afterFlush, fault.Flushed);
        }
        await AssertUnchangedAsync(context, Owner(context, app.Services, clock), request);
        clock.Now = ProcessProjectAdmissionFixture.Now;
        Assert.True((await Owner(context, app.Services, clock).CommitAsync(request)).CommittedNow);
    }

    [Fact]
    public async Task Expiry_after_material_flush_rolls_back_material_settlement_scope_and_receipt() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var begin = await PrepareAsync(context, app.Services);
        var clock = new CaptureClock();
        var reserved = await Owner(context, app.Services, clock).CommitAsync(begin);
        var complete = await CompleteStepCaptureAsync(context, app.Services, reserved);
        var fault = new AfterFlush(() => clock.Now = reserved.State.Claims.Single().ExpiresAtUtc);
        await using (var observed = Context(app.Services, fault)) {
            var failure = await Assert.ThrowsAsync<ProcessRuntimeDispatchPreconditionException>(() =>
                Owner(observed, app.Services, clock).CommitAsync(complete));
            Assert.Equal(ProcessRuntimeDispatchPreconditionRefusal.LeaseExpired, failure.Refusal);
            Assert.True(fault.Flushed);
        }
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Null(await Owner(context, app.Services, clock).FindCompletedCommandAsync(complete.CommandId));
        Assert.Empty(await context.DecisionHeads.ToArrayAsync());
        Assert.Equal(reserved.DecisionBudget!.Change.Balance, await BudgetAsync(context, reserved.State));
        Assert.Equal(reserved.State.UpdatedAtUtc, (await Owner(context, app.Services, clock).LoadAsync(reserved.State.RunId))!.UpdatedAtUtc);
    }

    [Fact]
    public async Task Historical_reservation_after_expiry_is_readable_without_fresh_capture_ownership() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await PrepareAsync(context, app.Services);
        var clock = new CaptureClock();
        var original = await Owner(context, app.Services, clock).CommitAsync(request);
        clock.Now = original.State.Claims.Single().ExpiresAtUtc;
        var replay = await Owner(context, app.Services, clock).CommitAsync(request);
        Assert.False(replay.CommittedNow);
        Assert.Equal(original.DecisionBudget, replay.DecisionBudget);
        Assert.Null(replay.DecisionBudget!.Change.Allocation.Settlement);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
    }

    [Fact]
    public async Task Lost_acknowledgement_retains_pending_capture_without_granting_another_owner() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await PrepareAsync(context, app.Services);
        var clock = new CaptureClock();
        var failure = new IOException("capture-reservation-acknowledgement-lost");
        await using (var interrupted = Context(app.Services, new AfterCommitFault(failure))) {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Owner(interrupted, app.Services, clock).CommitAsync(request)));
        }
        var replay = await Owner(context, app.Services, clock).CommitAsync(request);
        Assert.False(replay.CommittedNow);
        Assert.Null(replay.DecisionBudget!.Change.Allocation.Settlement);
        Assert.Equal(10, replay.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        Assert.Equal(0, replay.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
    }

    [Fact]
    public async Task Replaced_claim_during_capture_prevents_material_attachment() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var begin = await PrepareAsync(context, app.Services);
        var clock = new CaptureClock();
        var owner = Owner(context, app.Services, clock);
        var reserved = await owner.CommitAsync(begin);
        var complete = await CompleteStepCaptureAsync(context, app.Services, reserved);
        var replacement = await owner.CommitAsync(Claim(reserved.State));
        var failure = await Assert.ThrowsAsync<ProcessRuntimeDispatchPreconditionException>(() =>
            owner.CommitAsync(complete with { DispatchPrecondition = begin.DispatchPrecondition }));
        Assert.Equal(ProcessRuntimeDispatchPreconditionRefusal.ClaimChanged, failure.Refusal);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Null(await owner.FindCompletedCommandAsync(complete.CommandId));
        Assert.Equal(replacement.State.UpdatedAtUtc, (await owner.LoadAsync(reserved.State.RunId))!.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Root_cancellation_prevents_new_work_in_root_or_descendant(bool descendant) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var clock = new CaptureClock();
        var initial = InitialStepCapture(app.Services);
        var owner = Owner(context, app.Services, clock);
        var parent = (await owner.CommitAsync(Claim((await owner.CommitAsync(initial)).State))).State;
        var selected = parent;
        if (descendant) {
            var child = await owner.CommitAsync(Descendant(parent, initial.InitialRunDecisionAdmission!, ProcessDecisionChildOwner.Agent));
            selected = (await owner.CommitAsync(Claim(child.State))).State;
        }
        var request = Request(selected, null) with { DispatchPrecondition = Precondition(selected) };
        await ProcessRootCancellationFixture.StopAsync(owner, parent);
        var failure = await Assert.ThrowsAsync<ProcessRuntimeDispatchPreconditionException>(() => owner.CommitAsync(request));
        Assert.Equal(ProcessRuntimeDispatchPreconditionRefusal.RuntimeInactive, failure.Refusal);
        Assert.Null(await owner.FindCompletedCommandAsync(request.CommandId));
    }

    [Fact]
    public async Task Claim_expiring_while_waiting_for_root_lock_cannot_reserve_after_the_lock_is_granted() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await PrepareAsync(context, app.Services);
        var clock = new CaptureClock();
        var pause = new BeforeCommitPause();
        await using var holder = Context(app.Services, pause);
        var holding = Owner(holder, app.Services, clock).CommitAsync(Request(request.OriginalState, null));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await using var waiting = Context(app.Services);
        var pending = Owner(waiting, app.Services, clock).CommitAsync(request);
        try {
            Assert.False(pending.IsCompleted);
            clock.Now = request.OriginalState.Claims.Single().ExpiresAtUtc;
        } finally {
            pause.Release.TrySetResult(true);
        }
        await holding;
        var failure = await Assert.ThrowsAsync<ProcessRuntimeDispatchPreconditionException>(() => pending);
        Assert.Equal(ProcessRuntimeDispatchPreconditionRefusal.LeaseExpired, failure.Refusal);
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
        Assert.Null(await Owner(context, app.Services, clock).FindCompletedCommandAsync(request.CommandId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Guard_rejects_a_command_addressing_another_step(bool decision) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await PrepareAsync(context, app.Services);
        request = decision
            ? request with { Decision = new ProcessDecisionStart(ProcessStepInstanceId.New(), new(Guid.NewGuid()), new(Guid.NewGuid())) }
            : request with { DecisionBudget = request.DecisionBudget! with {
                Expectation = request.DecisionBudget.Expectation with { StepInstanceId = ProcessStepInstanceId.New() }
            } };
        var owner = Owner(context, app.Services, new CaptureClock());
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CommitAsync(request));
        await AssertUnchangedAsync(context, owner, request);
    }

    private static async Task<ProcessRuntimeCommitRequest> PrepareAsync(ProcessPersistenceDbContext context, IServiceProvider services,
        DispatchClaimStatus status = DispatchClaimStatus.Claimed) {
        var owner = Owner(context, services, new CaptureClock());
        var initial = InitialStepCapture(services);
        var state = (await owner.CommitAsync(initial)).State;
        var claim = Claim(state);
        claim = claim with { Mutation = claim.Mutation with { State = claim.Mutation.State with {
            Claims = [claim.Mutation.State.Claims.Single() with { Status = status }]
        } } };
        state = (await owner.CommitAsync(claim)).State;
        var budget = await BudgetAsync(context, state);
        return Request(state, null, new ProcessDecisionBudgetReservation(new(Step(state), budget.BudgetId, budget.Revision),
            new(Guid.NewGuid()), new(10, 0))) with { DispatchPrecondition = Precondition(state) };
    }

    private static ProcessRuntimeCommitRequest InitialStepCapture(IServiceProvider services) {
        var initial = ProcessProjectAdmissionFixture.Initial();
        var admission = ProcessDecisionRunPolicyFixture.Root(initial.OriginalState.RunId, initial.InitialPlan!.Definition.VersionId,
            services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id);
        return initial with { InitialRunDecisionAdmission = admission, InitialAssignments = initial.InitialAssignments!.Select(item => item with {
            DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(admission, item.StepInstanceId)
        }).ToArray() };
    }

    private static async Task<ProcessRuntimeCommitRequest> CompleteStepCaptureAsync(ProcessPersistenceDbContext context,
        IServiceProvider services, ProcessRuntimeCommitResult reserved) {
        var state = reserved.State;
        var admission = (await new EfProcessRuntimeStepAssignmentStore(context).LoadAsync(state.RunId, Step(state)))!.DecisionDataPolicy!;
        ProcessDecisionMaterialScope scope = new(new(Guid.NewGuid()), services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id,
            admission.WorkspaceNamespaceId, state.RootRunId, state.RunId, Step(state));
        var material = ProcessDecisionMaterial.FromContent(scope, new(scope.Id, 1, new(Guid.NewGuid())), admission.ContentContract!,
            ProcessProjectAdmissionFixture.Now, "{\"facts\":[]}");
        var budget = reserved.DecisionBudget!;
        return Request(state, new ProcessDecisionStart(Step(state), new(Guid.NewGuid()), material.Reference.MaterialId) { Material = material.Stamp },
            new ProcessDecisionBudgetSettlement(new(Step(state), budget.Change.Balance.BudgetId, budget.Change.Balance.Revision),
                budget.Command.AllocationId, new(7, new(3, 0)))) with {
            DecisionMaterial = material, DispatchPrecondition = Precondition(state)
        };
    }

    private static ProcessRuntimeDispatchPrecondition Precondition(ProcessRuntimeStateSnapshot state) {
        var claim = state.Claims.Single(item => item.ClaimToken == state.Steps.Single().ActiveClaimToken);
        return new(claim.StepInstanceId, claim.ClaimToken, claim.OwnerId);
    }

    private static EfProcessRuntimeUnitOfWork Owner(ProcessPersistenceDbContext context, IServiceProvider services, CaptureClock clock)
        => new(context, clock, runtimeDatabase: services.GetRequiredService<ICanonicalRuntimeDatabase>());

    private static async Task AssertUnchangedAsync(ProcessPersistenceDbContext context, EfProcessRuntimeUnitOfWork owner,
        ProcessRuntimeCommitRequest request) {
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Null(await owner.FindCompletedCommandAsync(request.CommandId));
        Assert.Equal(request.OriginalState.UpdatedAtUtc, (await owner.LoadAsync(request.OriginalState.RunId))!.UpdatedAtUtc);
    }

    private sealed class CaptureClock : TimeProvider {
        public DateTimeOffset Now { get; set; } = ProcessProjectAdmissionFixture.Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class AfterFlush(Action action) : SaveChangesInterceptor {
        public bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            action();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AfterCommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }

    private sealed class BeforeCommitPause : DbTransactionInterceptor {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
