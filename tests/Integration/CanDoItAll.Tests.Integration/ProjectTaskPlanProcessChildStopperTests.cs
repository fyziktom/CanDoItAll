using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessChildStopperTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Runtime_selection_delegates_inherited_work_only_to_its_retained_Process_child(bool missingChild) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await StopRootAsync(fixture);
        var before = await RetainedAsync(fixture);
        var owner = missingChild ? ActivatorUtilities.CreateInstance<AgentFrameworkProcessExecutionRecovery>(fixture.Services, new MissingChild())
            : fixture.Services.GetRequiredService<IProcessExecutionRecovery>();
        if (missingChild) {
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.InspectNextAsync(fixture.State.RootRunId, fixture.State.RunId, null, default));
        } else {
            var progress = await owner.InspectNextAsync(fixture.State.RootRunId, fixture.State.RunId, null, default);
            Assert.Equal(new ProcessExecutionRunId(fixture.Waiting.Id), progress.After);
            Assert.True(progress.Exhausted);
            Assert.Equal(ProcessCancellationRecoveryPending.None, progress.Pending);
        }
        Assert.Equal(before, await RetainedAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    private sealed class MissingChild : IProcessDecisionChildReader {
        public Task<ProcessDecisionChildObservation?> ReadAsync(ProcessDecisionChildAddress address, CancellationToken cancellationToken)
            => Task.FromResult<ProcessDecisionChildObservation?>(null);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_stop_closes_waiting_child_and_accounts_original_work_once(ReadBasisMode mode) {
        var queries = new BarrierQueries();
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, configureServices: services =>
            services.ConfigureDbContext<ProcessPersistenceDbContext>((_, options) => options.AddInterceptors(queries)));
        var stopped = await StopRootAsync(fixture);
        var owner = Owner(fixture);
        queries.Enabled = true;
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await owner.StopAsync(fixture.Parent));
        queries.Enabled = false;
        Assert.Equal(4, queries.Commands.Count);
        Assert.All(queries.Commands, query => {
            Assert.Equal(fixture.State.RootRunId.Value, Assert.Single(query.Roots));
            Assert.DoesNotContain("JOIN", query.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("process_runtime_steps", query.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("process_dispatch_claims", query.Sql, StringComparison.Ordinal);
        });
        var run = (await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id))!;
        Assert.Equal(RunOutcome.Cancelled, run.Outcome);
        Assert.Equal(fixture.Waiting.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks,
            run.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Equal(fixture.Waiting.ToolAdmission.PlanningDecision.InheritedOrigin, run.ToolAdmission.PlanningDecision.InheritedOrigin);
        var beforeReplay = await RetainedAsync(fixture);
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await owner.StopAsync(fixture.Parent));
        Assert.Equal(beforeReplay, await RetainedAsync(fixture));
        await AssertAccountingAsync(fixture, stopped, run, ProcessDecisionChildOutcome.NoEffect);
        Assert.Equal(beforeReplay, await RetainedAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task An_active_root_cannot_authorize_a_parent_stop() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var before = await RetainedAsync(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(fixture).StopAsync(fixture.Parent));
        Assert.Equal(before, await RetainedAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task An_already_cancelled_root_can_recover_its_original_waiting_child() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var barriers = fixture.Services.GetRequiredService<IProcessRootCancellationBarrierReader>();
        Assert.False(await barriers.IsStoppedAsync(fixture.State.RunId));
        Assert.False(await barriers.IsStoppedAsync(ProcessRunId.New()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => barriers.IsStoppedAsync(default));
        var stopped = await StopRootAsync(fixture);
        await using var context = Context(fixture.Services);
        var finalized = await Processes.ProcessRootCancellationFixture.LegacyCancelledAsync(context, stopped);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, finalized.Status);
        Assert.True(await barriers.IsStoppedAsync(finalized.RunId));
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await Owner(fixture).StopAsync(fixture.Parent));
        Assert.Equal(RunOutcome.Cancelled, (await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id))!.Outcome);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Changed_accepted_source_cannot_authorize_a_journal_write_after_root_stop() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await StopRootAsync(fixture);
        var before = await RetainedAsync(fixture);
        await using var context = Context(fixture.Services);
        var launch = await context.PreparedLaunches.SingleAsync(item => item.RunId == fixture.State.RunId.Value);
        launch.AcceptedSourceHeaderFingerprint = "sha256:" + new string('0', 64);
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(fixture).StopAsync(fixture.Parent));
        Assert.Equal(before, await RetainedAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Publication_before_parent_stop_retains_and_accounts_the_accepted_effect() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var accepted = await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        var before = await RetainedAsync(fixture);
        var stopped = await StopRootAsync(fixture);
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await Owner(fixture).StopAsync(fixture.Parent));
        await AssertAccountingAsync(fixture, stopped, accepted, ProcessDecisionChildOutcome.Accepted);
        Assert.Equal(before, await RetainedAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_profile_generation_is_retained_but_a_change_during_publication_refuses(bool changeDuringStop) {
        var generations = new ProjectTaskPlanProcessFixture.MutableGenerations();
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(configureServices: services =>
            services.AddSingleton<IAgentExecutionProfileGenerationSource>(provider => {
                generations.Generation = new(provider.GetRequiredService<ICanonicalRuntimeDatabase>().Generation);
                return generations;
            }));
        await StopRootAsync(fixture);
        var before = await RetainedAsync(fixture);
        var original = fixture.Waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin!;
        generations.Advance();
        var observations = 0;
        var observed = new OriginalSourceObservation(fixture.Services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>(), () => {
            if (++observations == 3 && changeDuringStop) {
                generations.Advance();
            }
        });
        var owner = ActivatorUtilities.CreateInstance<ProjectTaskPlanProcessChildStopper>(fixture.Services, observed);
        if (changeDuringStop) {
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.StopAsync(fixture.Parent));
            Assert.Equal(before, await RetainedAsync(fixture));
        } else {
            Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await owner.StopAsync(fixture.Parent));
            var run = (await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id))!;
            Assert.Equal(original, run.ToolAdmission!.PlanningDecision!.InheritedOrigin);
            Assert.NotEqual(generations.GetGeneration(), original.Profile.Generation);
        }
        Assert.Equal(3, observations);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_identity_or_fingerprint_cannot_change_in_place_after_original_source_observation(bool fingerprint) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await StopRootAsync(fixture);
        var before = await RetainedAsync(fixture);
        var profile = fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile;
        var originalId = profile.Id;
        var originalFingerprint = profile.Runtime.Fingerprint;
        var observations = 0;
        var observed = new OriginalSourceObservation(fixture.Services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>(), () => {
            if (++observations == 3) {
                if (fingerprint) {
                    profile.Runtime.Fingerprint = "changed-after-source-observation";
                } else {
                    profile.Id = Guid.NewGuid();
                }
            }
        });
        var owner = ActivatorUtilities.CreateInstance<ProjectTaskPlanProcessChildStopper>(fixture.Services, observed);
        try {
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.StopAsync(fixture.Parent));
        } finally {
            profile.Id = originalId;
            profile.Runtime.Fingerprint = originalFingerprint;
        }
        Assert.Equal(3, observations);
        Assert.Equal(before, await RetainedAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Another_child_binding_or_material_cannot_stop_the_original_journal(bool material) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(steps: 2);
        await StopRootAsync(fixture);
        var before = await RetainedAsync(fixture);
        var child = fixture.Parent.Child;
        var materialId = new ProcessDecisionMaterialId(Guid.NewGuid());
        var sibling = fixture.State.Steps.Single(step => step.StepInstanceId != child.Address.StepInstanceId).StepInstanceId;
        var changed = fixture.Parent with {
            Child = material ? child with { MaterialId = materialId } : child with { Address = child.Address with { StepInstanceId = sibling } },
            Material = material ? fixture.Parent.Material with {
                Reference = fixture.Parent.Material.Reference with { MaterialId = materialId }
            } : fixture.Parent.Material,
            Scope = material ? fixture.Parent.Scope : fixture.Parent.Scope with { StepInstanceId = sibling }
        };
        changed.Validate();
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(fixture).StopAsync(changed));
        Assert.Equal(before, await RetainedAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    private static IProcessDecisionChildStopper Owner(ProjectTaskPlanReviewFixture fixture)
        => Assert.Single(fixture.Services.GetServices<IProcessDecisionChildStopper>(),
            item => item.Contract == ProjectTaskPlanProcessContract.Execution);

    private static async Task<string> RetainedAsync(ProjectTaskPlanReviewFixture fixture)
        => JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id));

    private static async Task<ProcessRuntimeStateSnapshot> StopRootAsync(ProjectTaskPlanReviewFixture fixture) {
        var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RootRunId))!;
        var stopped = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .BeginRootCancellationAsync(state, fixture.CommandContext);
        Assert.True(stopped.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, stopped.State.Status);
        return stopped.State;
    }

    private static async Task AssertAccountingAsync(ProjectTaskPlanReviewFixture fixture, ProcessRuntimeStateSnapshot state,
        ExecutionRunRecord closed, ProcessDecisionChildOutcome outcome) {
        var selected = (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(state.RunId, fixture.Request.Child.StepInstanceId, new(closed.Id)))!;
        ProcessDecisionTerminalAccountingRequest request = new(fixture.Request.Child, selected.Head!.Expectation,
            new(fixture.Request.Child.StepInstanceId, selected.Budget.BudgetId, selected.Budget.Revision),
            state.Steps.Single(step => step.StepInstanceId == fixture.Request.Child.StepInstanceId).Status, RuntimeCommandId.New());
        await using var budgetContext = Context(fixture.Services);
        var budgets = new EfProcessDecisionBudgetStore(budgetContext);
        var before = (await budgets.ReadAsync(state.RootRunId, null))!;
        var accounting = fixture.Services.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>();
        var completed = await accounting.CompleteAsync(state, fixture.CommandContext, request);
        Assert.True(completed.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, completed.State.Status);
        Assert.Equal(outcome, completed.Decision!.Child!.Closure!.Outcome);
        var ticks = closed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks;
        Assert.Equal(ticks, completed.DecisionBudget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        var balance = completed.DecisionBudget.Funding!.Receipt.Change.Balance;
        Assert.Equal(before.ConsumedWorkTicks + ticks, balance.ConsumedWorkTicks);
        Assert.Equal(0, balance.ReservedWorkTicks);
        Assert.Equal(balance, await budgets.ReadAsync(state.RootRunId, null));
        var replay = await accounting.CompleteAsync(completed.State, fixture.CommandContext, request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Equal(completed.DecisionBudget, replay.DecisionBudget);
        Assert.Equal(balance, await budgets.ReadAsync(state.RootRunId, null));
    }

    private sealed class OriginalSourceObservation(IProcessOriginalReceiptAuthorityReader inner, Action observed)
        : IProcessOriginalReceiptAuthorityReader {
        public Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalForReceiptReadAsync(ProcessExecutionClaimEvidence original,
            CancellationToken cancellationToken = default) => inner.ObserveOriginalForReceiptReadAsync(original, cancellationToken);

        public async Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalDecisionForReceiptReadAsync(ProcessExecutionClaimEvidence original,
            ProcessDecisionChildMaterialBinding binding, ProcessDecisionExecutionContract supportedExecution,
            ProcessDecisionContentContract? supportedContent, CancellationToken cancellationToken = default) {
            var result = await inner.ObserveOriginalDecisionForReceiptReadAsync(original, binding, supportedExecution, supportedContent, cancellationToken);
            observed();
            return result;
        }
    }

    private sealed class BarrierQueries : DbCommandInterceptor {
        internal bool Enabled { get; set; }
        internal ConcurrentQueue<(string Sql, Guid[] Roots)> Commands { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Enabled && command.CommandText.StartsWith("SELECT EXISTS", StringComparison.Ordinal) &&
                    command.CommandText.Contains("process_runtime_states", StringComparison.Ordinal)) {
                Commands.Enqueue((command.CommandText, command.Parameters.Cast<DbParameter>()
                    .Select(parameter => parameter.Value).OfType<Guid>().ToArray()));
            }
            return ValueTask.FromResult(result);
        }
    }
}
