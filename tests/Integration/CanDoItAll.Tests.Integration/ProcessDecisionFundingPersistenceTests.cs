using System.Data.Common;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessDecisionFundingPersistenceTests {
    [Fact]
    public async Task Root_admission_and_original_funding_survive_replay_without_recreating_an_allowance() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var root = await RootBudgetAsync(context, state);
        var local = await BudgetAsync(context, state);
        Assert.Equal(initial.InitialRunDecisionAdmission!.BudgetId, root.BudgetId);
        Assert.Equal(root.Ceiling, local.Ceiling);
        Assert.NotEqual(root.BudgetId, local.BudgetId);
        Assert.True((await owner.CommitAsync(initial)).Succeeded);
        Assert.Equal(root, await RootBudgetAsync(context, state));
        Assert.Equal(local, await BudgetAsync(context, state));
        var rows = await context.DecisionBudgets.AsNoTracking().ToArrayAsync();
        var rootRow = Assert.Single(rows, row => row.StepInstanceId is null);
        Assert.Equal(initial.CommandId.Value, rootRow.AdmissionCommandId);
        Assert.Equal(ProcessDecisionRunAdmissionCodec.Serialize(initial.InitialRunDecisionAdmission), rootRow.RunAdmissionJson);
        Assert.Equal(root.BudgetId.Value, Assert.Single(rows, row => row.StepInstanceId is not null).ParentBudgetId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(initial with { InitialRunDecisionAdmission = null }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(initial with {
            InitialRunDecisionAdmission = initial.InitialRunDecisionAdmission with { BudgetId = new(Guid.NewGuid()) }
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Invalid_original_root_profile_or_child_binding_never_creates_a_partial_budget(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = Initial(app.Services);
        var assignment = initial.InitialAssignments!.Single();
        initial = change switch {
            0 => initial with { InitialRunDecisionAdmission = initial.InitialRunDecisionAdmission! with { DatabaseProfileId = Guid.NewGuid() } },
            1 => initial,
            2 => initial with { InitialRunDecisionAdmission = null },
            3 => initial with { InitialAssignments = [assignment with { DecisionDataPolicy = null }] },
            4 => initial with { InitialAssignments = [assignment with { DecisionDataPolicy = assignment.DecisionDataPolicy! with { WorkspaceHeadRevision = Guid.NewGuid() } }] },
            _ => initial with { InitialAssignments = [assignment with { DecisionDataPolicy = assignment.DecisionDataPolicy! with {
                Funding = new(ProcessRunId.New(), initial.InitialRunDecisionAdmission!.BudgetId)
            } }] }
        };
        var owner = change == 1 ? new EfProcessRuntimeUnitOfWork(context) : Owner(context, app.Services);
        if (change == 1) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(initial));
        } else {
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.CommitAsync(initial));
        }
        Assert.Empty(await context.DecisionBudgets.ToArrayAsync());
        Assert.Empty(await context.RuntimeStates.ToArrayAsync());
        Assert.Empty(await context.IdempotencyKeys.ToArrayAsync());
    }

    [Fact]
    public async Task Real_descendant_budgets_compete_for_one_root_allowance_atomically() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var root = await ActivateAsync(context, app.Services, (await owner.CommitAsync(initial)).State);
        var children = new[] { (await owner.CommitAsync(Descendant(root, initial.InitialRunDecisionAdmission!))).State,
            (await owner.CommitAsync(Descendant(root, initial.InitialRunDecisionAdmission!))).State };
        var requests = new List<ProcessRuntimeCommitRequest>();
        foreach (var child in children) {
            requests.Add(Change(child, Reserve(Step(child), await BudgetAsync(context, child), 300)));
        }
        var outcomes = await Task.WhenAll(requests.Select(async request => {
            await using var contender = Context(app.Services);
            return await Record.ExceptionAsync(() => Owner(contender, app.Services).CommitAsync(request));
        }));
        Assert.Single(outcomes, item => item is null);
        Assert.Equal(DecisionBudgetRefusal.WorkExhausted, Assert.IsType<DecisionBudgetException>(Assert.Single(outcomes, item => item is not null)).Reason);
        var parentBalance = await RootBudgetAsync(context, root);
        Assert.Equal(300, parentBalance.ReservedWorkTicks);
        Assert.Equal(1, parentBalance.AllocationCount);
        Assert.Equal(1, await context.DecisionBudgets.CountAsync(item => item.StepInstanceId == null));
        Assert.Equal(2, await context.DecisionBudgetAllocations.CountAsync());
        List<DecisionBudgetBalance> balances = [];
        foreach (var child in children) {
            balances.Add(await BudgetAsync(context, child));
        }
        Assert.Equal(new long[] { 0, 300 }, balances.Select(item => item.ReservedWorkTicks).Order().ToArray());
    }

    [Fact]
    public async Task Cancelled_root_refuses_new_descendant_work_but_accepts_closed_usage_without_recrediting_unknown_work() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var root = await ActivateAsync(context, app.Services, (await owner.CommitAsync(initial)).State);
        var child = (await owner.CommitAsync(Descendant(root, initial.InitialRunDecisionAdmission!))).State;
        var first = await owner.CommitAsync(Change(child, Reserve(Step(child), await BudgetAsync(context, child), 100)));
        var second = await owner.CommitAsync(Change(first.State, Reserve(Step(child), first.DecisionBudget!.Change.Balance, 100)));
        await ProcessRootCancellationFixture.StopAsync(owner, root);
        var refused = await owner.CommitAsync(Change(second.State, Reserve(Step(child), second.DecisionBudget!.Change.Balance, 1)));
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Diagnostics, item => item.Code == EfProcessRootCancellationGuard.WorkAdmissionStopped);
        var settled = await owner.CommitAsync(Change(second.State, Settle(Step(child), second.DecisionBudget!.Change.Balance,
            first.DecisionBudget!.Command.AllocationId, 60)));
        Assert.Equal(60, settled.DecisionBudget!.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(100, settled.DecisionBudget.Change.Balance.ReservedWorkTicks);
        var rootBalance = await RootBudgetAsync(context, root);
        Assert.Equal(60, rootBalance.ConsumedWorkTicks);
        Assert.Equal(100, rootBalance.ReservedWorkTicks);
        Assert.Null((await new EfProcessDecisionBudgetStore(context).ReadAllocationAsync(rootBalance.BudgetId,
            second.DecisionBudget.Funding!.Receipt.Command.AllocationId))!.Settlement);
    }

    [Fact]
    public async Task Child_overrun_is_charged_to_both_balances_and_stops_a_different_descendant() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var root = await ActivateAsync(context, app.Services, (await owner.CommitAsync(initial)).State);
        var child = (await owner.CommitAsync(Descendant(root, initial.InitialRunDecisionAdmission!))).State;
        var other = (await owner.CommitAsync(Descendant(root, initial.InitialRunDecisionAdmission!))).State;
        var reserved = await owner.CommitAsync(Change(child, Reserve(Step(child), await BudgetAsync(context, child), 100)));
        var settled = await owner.CommitAsync(Change(reserved.State, Settle(Step(child), reserved.DecisionBudget!.Change.Balance,
            reserved.DecisionBudget.Command.AllocationId, 101)));
        Assert.True(settled.DecisionBudget!.Change.Balance.OverrunDetected);
        Assert.True(settled.DecisionBudget.Funding!.Receipt.Change.Balance.OverrunDetected);
        Assert.Equal(101, (await RootBudgetAsync(context, root)).ConsumedWorkTicks);
        var otherBalance = await BudgetAsync(context, other);
        Assert.False(otherBalance.OverrunDetected);
        var refused = await Assert.ThrowsAsync<DecisionBudgetException>(() => owner.CommitAsync(Change(other, Reserve(Step(other), otherBalance, 1))));
        Assert.Equal(DecisionBudgetRefusal.Overrun, refused.Reason);
        Assert.Equal(otherBalance, await BudgetAsync(context, other));
    }

    [Fact]
    public async Task Direct_root_commands_cannot_reuse_a_funded_child_allocation() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(Initial(app.Services))).State;
        var reserved = await owner.CommitAsync(Change(state, Reserve(Step(state), await BudgetAsync(context, state), 100)));
        var funding = reserved.DecisionBudget!.Funding!.Receipt;
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(Change(reserved.State,
            Settle(null, funding.Change.Balance, funding.Command.AllocationId, 0))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(Change(reserved.State,
            new ProcessDecisionBudgetReservation(new(null, funding.Change.Balance.BudgetId, funding.Change.Balance.Revision), funding.Command.AllocationId, new(100, 0)))));
        Assert.Equal(100, (await RootBudgetAsync(context, state)).ReservedWorkTicks);
        Assert.Equal(100, (await BudgetAsync(context, state)).ReservedWorkTicks);
    }

    [Fact]
    public async Task An_existing_unrelated_root_allocation_cannot_be_adopted_as_child_funding() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(Initial(app.Services))).State;
        var local = await BudgetAsync(context, state);
        var root = await RootBudgetAsync(context, state);
        var childCommand = Reserve(Step(state), local, 100);
        var id = ProcessDecisionFundingIdentity.Derive(root.BudgetId, local.BudgetId, childCommand.AllocationId);
        var occupied = await owner.CommitAsync(Change(state, new ProcessDecisionBudgetReservation(new(null, root.BudgetId, root.Revision), id, new(100, 0))));
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CommitAsync(Change(occupied.State, childCommand)));
        Assert.Equal(local, await BudgetAsync(context, state));
        Assert.Equal(100, (await RootBudgetAsync(context, state)).ReservedWorkTicks);
        Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Run_snapshot_replanning_is_charged_once_by_the_root_and_not_by_each_child() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(Initial(app.Services, ReadBasisMode.RunSnapshot, OnBasisChange.Replan))).State;
        var local = await BudgetAsync(context, state);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(Change(state, Reserve(Step(state), local, 10, 1))));
        var root = await RootBudgetAsync(context, state);
        var reserved = await owner.CommitAsync(Change(state, Reserve(null, root, 10, 1)));
        var settlement = Change(reserved.State, Settle(null, reserved.DecisionBudget!.Change.Balance,
            reserved.DecisionBudget.Command.AllocationId, 5, 1));
        var settled = await owner.CommitAsync(settlement);
        Assert.Equal(1, settled.DecisionBudget!.Change.Balance.ConsumedReplans);
        Assert.Equal(5, settled.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Null(settled.DecisionBudget.Funding);
        Assert.Equal(local, await BudgetAsync(context, state));
        var json = JsonNode.Parse((await context.IdempotencyKeys.AsNoTracking().SingleAsync(item => item.CommandId == settlement.CommandId.Value)).DecisionBudgetReceiptJson!)!;
        Assert.Equal(2, json["version"]!.GetValue<int>());
        Assert.Null(json["stepId"]);
        Assert.False(json.AsObject().ContainsKey("funding"));
    }

    private static ProcessRuntimeCommitRequest Initial(IServiceProvider services, ReadBasisMode read = ReadBasisMode.StepSnapshot,
        OnBasisChange change = OnBasisChange.PauseForReview) {
        var initial = ProcessProjectAdmissionFixture.Initial();
        return Fund(initial, services, read, change);
    }

    private static ProcessRuntimeCommitRequest Fund(ProcessRuntimeCommitRequest initial, IServiceProvider services,
        ReadBasisMode read = ReadBasisMode.StepSnapshot, OnBasisChange change = OnBasisChange.PauseForReview) {
        var root = ProcessDecisionRunPolicyFixture.Root(initial.OriginalState.RunId, initial.InitialPlan!.Definition.VersionId,
            services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id, read, change);
        return initial with { InitialRunDecisionAdmission = root,
            InitialAssignments = initial.InitialAssignments!.Select(item => item with {
                DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, item.StepInstanceId)
            }).ToArray() };
    }

    private static ProcessRuntimeCommitRequest Descendant(ProcessRuntimeStateSnapshot parent, ProcessDecisionRunAdmission root) {
        var initial = ProcessProjectAdmissionFixture.Initial();
        var state = initial.OriginalState with { RootRunId = parent.RootRunId };
        var assignment = initial.InitialAssignments!.Single();
        return initial with { OriginalState = state, Mutation = initial.Mutation with { State = state,
            Events = initial.Mutation.Events.Select(item => item with { RootRunId = parent.RootRunId }).ToArray() },
            ParentStepPrecondition = new(parent.RunId, Step(parent)),
            InitialAssignments = [assignment with {
                DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, assignment.StepInstanceId, initial.InitialPlan!.Definition.VersionId),
                LaunchVariables = new Dictionary<string, string> {
                    [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parent.RunId.Value.ToString("D"),
                    [ProcessRuntimeLaunchVariables.ParentProcessStepId] = Step(parent).Value.ToString("D")
                }
            }] };
    }

    private static async Task<ProcessRuntimeStateSnapshot> ActivateAsync(ProcessPersistenceDbContext context, IServiceProvider services,
        ProcessRuntimeStateSnapshot state) {
        var claim = new DispatchClaimState(new(Guid.NewGuid()), Step(state), new("funding-fixture"), DispatchClaimStatus.Claimed,
            1, ProcessProjectAdmissionFixture.Now, ProcessProjectAdmissionFixture.Now.AddHours(1), null, null);
        var request = Change(state);
        var target = request.Mutation.State with { Status = ProcessRuntimeStatus.Active,
            Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Running, AttemptNumber = claim.AttemptNumber,
                ActiveClaimToken = claim.ClaimToken }], Claims = [claim] };
        return (await Owner(context, services).CommitAsync(request with { Mutation = request.Mutation with { State = target } })).State;
    }

    private static ProcessRuntimeCommitRequest Change(ProcessRuntimeStateSnapshot state, ProcessDecisionBudgetCommand? command = null)
        => new(RuntimeCommandId.New(), state, new(ProcessRuntimeTransitionOutcome.Applied,
            state with { UpdatedAtUtc = state.UpdatedAtUtc.AddSeconds(1) }, [], [], [], [])) { DecisionBudget = command };

    private static ProcessDecisionBudgetReservation Reserve(ProcessStepInstanceId? step, DecisionBudgetBalance balance, long work, int replans = 0)
        => new(new(step, balance.BudgetId, balance.Revision), new(Guid.NewGuid()), new(work, replans));

    private static ProcessDecisionBudgetSettlement Settle(ProcessStepInstanceId? step, DecisionBudgetBalance balance,
        DecisionBudgetAllocationId allocation, long work, int replans = 0)
        => new(new(step, balance.BudgetId, balance.Revision), allocation, new(7, new(work, replans)));

    private static async Task<DecisionBudgetBalance> RootBudgetAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state)
        => (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RootRunId, null))!;

    private static EfProcessRuntimeUnitOfWork Owner(ProcessPersistenceDbContext context, IServiceProvider services)
        => new(context, timeProvider: new FixedClock(), runtimeDatabase: services.GetRequiredService<ICanonicalRuntimeDatabase>());

    private sealed class FixedClock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now;
    }
}
