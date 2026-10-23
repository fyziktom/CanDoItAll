using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshChildStopTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_refresh_stop_closes_only_the_original_child_and_replays_without_spending(ReadBasisMode mode) {
        var queries = new ReadProbe();
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, configureServices: services =>
            services.ConfigureDbContext<ProcessPersistenceDbContext>((_, options) => options.AddInterceptors(queries)));
        var id = await AdmitAsync(fixture);
        var before = await ParentStateAsync(fixture);
        queries.Enabled = true;
        await fixture.Services.GetRequiredService<IProcessDecisionRefreshAuthorityReader>().RequireCurrentAsync(fixture.Parent, id);
        queries.Enabled = false;
        Assert.Equal(3, queries.Reads.Count);
        Assert.All(queries.Reads, sql => {
            Assert.DoesNotContain("process_runtime_results", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("PayloadJson", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("process_runtime_events", sql, StringComparison.Ordinal);
        });
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await Owner(fixture).StopAsync(fixture.Parent, id));
        var run = (await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id))!;
        Assert.Equal(RunOutcome.Cancelled, run.Outcome);
        Assert.Equal(fixture.Waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin, run.ToolAdmission!.PlanningDecision!.InheritedOrigin);
        Assert.Equal(fixture.Waiting.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks, run.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Null(run.ToolAdmission.PlanningDecision.Work.Open);
        Assert.All(run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals), proposal => {
            Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
            Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
        });
        var journal = await JournalAsync(fixture);
        await using var restarted = fixture.Services.CreateAsyncScope();
        var owner = Assert.Single(restarted.ServiceProvider.GetServices<IProcessDecisionRefreshChildStopper>());
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await owner.StopAsync(fixture.Parent, id));
        Assert.Equal(journal, await JournalAsync(fixture));
        Assert.Equal(before, await ParentStateAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_admission_or_another_action_cannot_stop_the_child(bool admit) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        if (admit) {
            await AdmitAsync(fixture);
        }
        var before = await JournalAsync(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(fixture).StopAsync(fixture.Parent, RuntimeCommandId.New()));
        var cancellation = Assert.Single(fixture.Services.GetServices<IProcessDecisionChildStopper>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => cancellation.StopAsync(fixture.Parent));
        Assert.Equal(before, await JournalAsync(fixture));
    }

    public enum ChangedBinding { Child, Material, Grant }

    [Theory]
    [InlineData(ChangedBinding.Child)]
    [InlineData(ChangedBinding.Material)]
    [InlineData(ChangedBinding.Grant)]
    public async Task An_admission_cannot_be_reused_for_changed_original_work(ChangedBinding change) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var id = await AdmitAsync(fixture);
        var original = fixture.Parent;
        var material = new ProcessDecisionMaterialId(Guid.NewGuid());
        var changed = change switch {
            ChangedBinding.Child => original with { Child = original.Child with { Address = original.Child.Address with { ChildId = new(Guid.NewGuid()) } } },
            ChangedBinding.Material => original with {
                Child = original.Child with { MaterialId = material },
                Material = original.Material with { Reference = original.Material.Reference with { MaterialId = material } }
            },
            ChangedBinding.Grant => original with { Child = original.Child with { Grant = original.Child.Grant with { WorkTicks = original.Child.Grant.WorkTicks + 1 } } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var before = await JournalAsync(fixture);
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(fixture).StopAsync(changed, id));
        Assert.Equal(before, await JournalAsync(fixture));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Cancellation_under_the_dispatch_lease_or_before_journal_commit_revokes_refresh(int boundary) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var id = await AdmitAsync(fixture);
        var before = await JournalAsync(fixture);
        var authority = new ObserveAuthority(fixture.Services.GetRequiredService<IProcessDecisionRefreshAuthorityReader>(), async count => {
            if (count != boundary) {
                return;
            }
            var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
            var cancelled = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
                .BeginRootCancellationAsync(state, fixture.CommandContext);
            Assert.True(cancelled.CommittedNow);
        });
        var owner = (IProcessDecisionRefreshChildStopper)ActivatorUtilities.CreateInstance<ProjectTaskPlanProcessChildStopper>(fixture.Services, authority);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.StopAsync(fixture.Parent, id));
        Assert.Equal(boundary, authority.Count);
        Assert.Equal(before, await JournalAsync(fixture));
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting,
            await Assert.Single(fixture.Services.GetServices<IProcessDecisionChildStopper>()).StopAsync(fixture.Parent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_historical_receipt_cannot_override_changed_local_or_shared_authority(bool shared) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(ReadBasisMode.RunSnapshot);
        var id = await AdmitAsync(fixture);
        await using var context = Context(fixture);
        if (shared) {
            var scope = await context.DecisionRunScopes.SingleAsync();
            scope.Phase = ProcessDecisionRunScopePhase.Active;
        } else {
            var generation = await context.DecisionGenerations.SingleAsync();
            generation.RefreshCommandId = Guid.NewGuid();
        }
        await context.SaveChangesAsync();
        var before = await JournalAsync(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(fixture).StopAsync(fixture.Parent, id));
        Assert.Equal(before, await JournalAsync(fixture));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task A_completed_effect_before_refresh_retains_its_receipt_and_actual_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var accepted = await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        var id = await AdmitAsync(fixture);
        var before = await JournalAsync(fixture);
        var parent = await ParentStateAsync(fixture);
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await Owner(fixture).StopAsync(fixture.Parent, id));
        var result = await Assert.Single(fixture.Services.GetServices<IProcessDecisionChildOutcomeReader>())
            .ReadAsync(fixture.Parent);
        var closed = Assert.IsType<ProcessDecisionChildResult.Closed>(result);
        Assert.Equal(ProcessDecisionChildTerminalStatus.Succeeded, closed.Status);
        Assert.Equal(ProcessDecisionChildOutcome.Accepted, closed.Closure.Outcome);
        Assert.Equal(accepted.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks, closed.Work.Consumed.WorkTicks);
        Assert.Equal(before, await JournalAsync(fixture));
        Assert.Equal(parent, await ParentStateAsync(fixture));
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_held_Agent_lease_stays_pending_or_propagates_caller_cancellation(bool cancel) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var id = await AdmitAsync(fixture);
        var before = await JournalAsync(fixture);
        var parent = await ParentStateAsync(fixture);
        await using (var held = await fixture.Entry.Files.Store.AcquireToolDispatchLeaseAsync(fixture.Waiting.Id)) {
            if (cancel) {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Owner(fixture).StopAsync(fixture.Parent, id, cancellation.Token));
            } else {
                Assert.Equal(ProcessDecisionChildStopResult.LeaseBusy, await Owner(fixture).StopAsync(fixture.Parent, id));
            }
        }
        Assert.Equal(before, await JournalAsync(fixture));
        Assert.Equal(parent, await ParentStateAsync(fixture));
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await Owner(fixture).StopAsync(fixture.Parent, id));
    }

    [Fact]
    public async Task Changed_accepted_source_refuses_refresh_journal_mutation() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var id = await AdmitAsync(fixture);
        await using var context = Context(fixture);
        var accepted = await context.PreparedLaunches.SingleAsync(row => row.RunId == fixture.State.RunId.Value);
        accepted.AcceptedSourceHeaderFingerprint = "sha256:" + new string('0', 64);
        await context.SaveChangesAsync();
        var before = await JournalAsync(fixture);
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(fixture).StopAsync(fixture.Parent, id));
        Assert.Equal(before, await JournalAsync(fixture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_profile_change_during_refresh_authority_observation_cannot_publish(bool fingerprint) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var id = await AdmitAsync(fixture);
        var before = await JournalAsync(fixture);
        var profile = fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile;
        var originalId = profile.Id;
        var originalFingerprint = profile.Runtime.Fingerprint;
        var authority = new ObserveAuthority(fixture.Services.GetRequiredService<IProcessDecisionRefreshAuthorityReader>(), count => {
            if (count == 2) {
                if (fingerprint) {
                    profile.Runtime.Fingerprint = "changed-refresh-profile";
                } else {
                    profile.Id = Guid.NewGuid();
                }
            }
            return Task.CompletedTask;
        }, afterRead: true);
        var owner = (IProcessDecisionRefreshChildStopper)ActivatorUtilities.CreateInstance<ProjectTaskPlanProcessChildStopper>(fixture.Services, authority);
        try {
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.StopAsync(fixture.Parent, id));
        } finally {
            profile.Id = originalId;
            profile.Runtime.Fingerprint = originalFingerprint;
        }
        Assert.Equal(before, await JournalAsync(fixture));
    }

    [Fact]
    public async Task Unknown_saved_effect_keeps_its_original_open_work_and_grant_reserved() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        fixture.Entry.Files.Sql.Faults.Commit.AfterCommit = () => {
            fixture.Entry.Files.Sql.Probe.BeforeCommand = (command, _) => command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)
                ? Task.FromException(new IOException("Original receipt temporarily unavailable.")) : Task.CompletedTask;
            throw new IOException("Original publication acknowledgement lost.");
        };
        try {
            await Assert.ThrowsAnyAsync<Exception>(fixture.PublishAsync);
        } finally {
            fixture.Entry.Files.Sql.Probe.BeforeCommand = null;
        }
        var uncertain = (await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id))!;
        Assert.NotNull(uncertain.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired,
            AgentToolJournalTransitions.RequireProposal(uncertain.ToolAdmission, ProjectTaskPlanAgentFixture.Binding(fixture.Waiting)).State);
        var id = await AdmitAsync(fixture);
        var before = await JournalAsync(fixture);
        var parent = await ParentStateAsync(fixture);
        Assert.Equal(ProcessDecisionChildStopResult.ActiveWork, await Owner(fixture).StopAsync(fixture.Parent, id));
        Assert.Equal(before, await JournalAsync(fixture));
        Assert.Equal(parent, await ParentStateAsync(fixture));
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Fact]
    public async Task Missing_child_journal_does_not_release_original_Process_work() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var id = await AdmitAsync(fixture);
        var parent = await ParentStateAsync(fixture);
        var owner = (IProcessDecisionRefreshChildStopper)ActivatorUtilities.CreateInstance<ProjectTaskPlanProcessChildStopper>(fixture.Services, new MissingJournal());
        Assert.Equal(ProcessDecisionChildStopResult.MissingJournal, await owner.StopAsync(fixture.Parent, id));
        Assert.Equal(parent, await ParentStateAsync(fixture));
    }

    private static async Task<RuntimeCommandId> AdmitAsync(ProjectTaskPlanReviewFixture fixture) {
        var selected = (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.State.RunId, fixture.Request.Child.StepInstanceId, new(fixture.Waiting.Id)))!;
        var review = fixture.Request;
        ProcessDecisionRefreshRequest request = new(new(Guid.NewGuid()), review.Child, review.StrategyId, review.WaitingResultKey,
            review.Decision, review.Budget, selected.SharedScope, ProcessDecisionRefreshKind.ManagerRefresh);
        Assert.True((await fixture.Services.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(fixture.State, fixture.CommandContext, request)).CommittedNow);
        return request.CommandId;
    }

    private static IProcessDecisionRefreshChildStopper Owner(ProjectTaskPlanReviewFixture fixture)
        => Assert.Single(fixture.Services.GetServices<IProcessDecisionRefreshChildStopper>());

    private static ProcessPersistenceDbContext Context(ProjectTaskPlanReviewFixture fixture)
        => new(fixture.Services.GetRequiredService<DbContextOptions<ProcessPersistenceDbContext>>());

    private static async Task<string> JournalAsync(ProjectTaskPlanReviewFixture fixture)
        => JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id));

    private static async Task<string> ParentStateAsync(ProjectTaskPlanReviewFixture fixture) {
        await using var context = Context(fixture);
        return JsonSerializer.Serialize(new {
            Runtime = await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId),
            Budgets = await context.DecisionBudgets.AsNoTracking().OrderBy(row => row.BudgetId).ToArrayAsync(),
            Allocations = await context.DecisionBudgetAllocations.AsNoTracking().OrderBy(row => row.BudgetId).ThenBy(row => row.AllocationId).ToArrayAsync(),
            Generations = await context.DecisionGenerations.AsNoTracking().ToArrayAsync(),
            Children = await context.DecisionChildren.AsNoTracking().ToArrayAsync(),
            Scopes = await context.DecisionRunScopes.AsNoTracking().ToArrayAsync()
        });
    }

    private sealed class ObserveAuthority(IProcessDecisionRefreshAuthorityReader inner, Func<int, Task> observe, bool afterRead = false)
        : IProcessDecisionRefreshAuthorityReader {
        internal int Count { get; private set; }
        public async Task RequireCurrentAsync(ProcessDecisionChildMaterialBinding original, RuntimeCommandId admissionId, CancellationToken cancellationToken) {
            Count++;
            if (!afterRead) {
                await observe(Count);
            }
            await inner.RequireCurrentAsync(original, admissionId, cancellationToken);
            if (afterRead) {
                await observe(Count);
            }
        }
    }

    private sealed class ReadProbe : DbCommandInterceptor {
        internal bool Enabled { get; set; }
        internal List<string> Reads { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Enabled) {
                Reads.Add(command.CommandText);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class MissingJournal : ISandboxWorkspaceExecutionRunStore {
        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExecutionRunRecord?> GetExecutionRunAsync(Guid executionRunId, CancellationToken cancellationToken) => Task.FromResult<ExecutionRunRecord?>(null);
        public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(ExecutionRunDetail detail, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
