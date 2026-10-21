using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessReviewTests {
    [Theory]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.RunSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.RunSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.StepSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.StepSnapshot)]
    public async Task Registered_workspace_review_completes_original_work_and_replays_without_another_provider_or_effect(
        AgentPlanningReviewChoice choice, ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var access = new ManagerAccess();
        var manager = await ManagerAsync(fixture, access);
        var request = Request(fixture, choice);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        fixture.Entry.Files.Sql.Faults.Save.AfterFlush = _ => {
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            return Task.CompletedTask;
        };
        var result = await Service(fixture).ReviewAsync(request, manager, AgentExecutionOperationId.New());
        Assert.False(result.Agent.Replayed);
        Assert.True(result.Completion.Succeeded, string.Join("; ", result.Completion.Diagnostics.Select(item => item.Message)));
        Assert.True(result.Completion.CommittedNow);
        Assert.Equal(choice == AgentPlanningReviewChoice.Approve ? RunOutcome.Succeeded : RunOutcome.Cancelled, result.Agent.Run.Outcome);
        Assert.Equal(choice == AgentPlanningReviewChoice.Approve ? ProcessDecisionPhase.Completed : ProcessDecisionPhase.Cancelled,
            result.Completion.Decision!.Current.Phase);
        Assert.Equal(TimeSpan.FromSeconds(choice == AgentPlanningReviewChoice.Approve ? 3 : 1).Ticks,
            result.Completion.DecisionBudget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.Equal(0, result.Completion.DecisionBudget.Change.Balance.ReservedWorkTicks);
        await using var scope = fixture.Services.CreateAsyncScope();
        var replay = await scope.ServiceProvider.GetRequiredService<ProjectTaskPlanProcessReviewService>()
            .ReviewAsync(request, await ManagerAsync(fixture, access), AgentExecutionOperationId.New());
        Assert.True(replay.Agent.Replayed);
        Assert.False(replay.Completion.CommittedNow);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion.Outcome);
        Assert.Equal(result.Completion.DecisionBudget, replay.Completion.DecisionBudget);
        Assert.Equal(result.ActionId, replay.ActionId);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(choice == AgentPlanningReviewChoice.Approve ? 1 : 0,
            choice == AgentPlanningReviewChoice.Approve ? 1 : 0);
    }

    [Theory]
    [InlineData(ChangedProcessReview.WaitingResult)]
    [InlineData(ChangedProcessReview.Strategy)]
    [InlineData(ChangedProcessReview.Budget)]
    [InlineData(ChangedProcessReview.Decision)]
    public async Task Invalid_Process_selection_is_refused_before_approval_intent_or_publication(ChangedProcessReview change) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var request = Request(fixture, AgentPlanningReviewChoice.Approve);
        request = request with { Process = change switch {
            ChangedProcessReview.WaitingResult => request.Process with { WaitingResultKey = new(Guid.NewGuid()) },
            ChangedProcessReview.Strategy => request.Process with { StrategyId = new("different-strategy") },
            ChangedProcessReview.Budget => request.Process with { Budget = request.Process.Budget with { Revision = request.Process.Budget.Revision + 1 } },
            ChangedProcessReview.Decision => request.Process with { Decision = request.Process.Decision with { Revision = request.Process.Decision.Revision + 1 } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        } };
        var manager = await ManagerAsync(fixture, new());
        var refusal = await Assert.ThrowsAsync<ProcessDecisionReviewAdmissionException>(() => Service(fixture).ReviewAsync(
            request, manager, AgentExecutionOperationId.New()));
        Assert.Equal(change is ChangedProcessReview.WaitingResult or ChangedProcessReview.Strategy
            ? "Runtime.ReviewOriginalMissing" : "Runtime.ReviewExpectationChanged", refusal.Refusal.Code);
        var unchanged = await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id);
        Assert.Equal(JsonSerializer.Serialize(fixture.Waiting), JsonSerializer.Serialize(unchanged!.Run));
        Assert.DoesNotContain(unchanged.Approvals, approval => approval.DecisionSourceKind == AgentPlanningReviewIntent.SourceKind);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Renewed_manager_credential_recovers_original_action_but_a_different_actor_cannot_adopt_it() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(configureServices: services =>
            services.PostConfigure<ApiAccessOptions>(options => {
                options.Enabled = true;
                options.Authorization.Enabled = true;
            }));
        var access = new ManagerAccess();
        var sources = fixture.Services.GetRequiredService<ProjectProcessLaunchAuthorityService>();
        var authority = await sources.CaptureAuthenticatedAsync(null, "review-manager", fixture.Clock.GetUtcNow().AddHours(1));
        var first = new ProcessPlanningManagerAuthority(authority, "review-fixture", access);
        var request = Request(fixture, AgentPlanningReviewChoice.Reject);
        var completed = await Service(fixture).ReviewAsync(request, first, AgentExecutionOperationId.New());
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        var renewed = new ProcessPlanningManagerAuthority(
            await sources.CaptureAuthenticatedAsync(null, "review-manager", fixture.Clock.GetUtcNow().AddHours(1)), "review-fixture", access);
        Assert.Equal(first.Actor, renewed.Actor);
        var replay = await Service(fixture).ReviewAsync(request, renewed, AgentExecutionOperationId.New());
        Assert.True(replay.Agent.Replayed);
        Assert.Equal(completed.Completion.DecisionBudget, replay.Completion.DecisionBudget);
        var other = new ProcessPlanningManagerAuthority(
            await sources.CaptureAuthenticatedAsync(null, "different-manager", fixture.Clock.GetUtcNow().AddHours(1)), "review-fixture", access);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            Service(fixture).ReviewAsync(request, other, AgentExecutionOperationId.New()));
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Fact]
    public async Task Lost_Process_commit_acknowledgement_recovers_one_effect_and_one_settlement_in_a_new_scope() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var access = new ManagerAccess();
        var manager = await ManagerAsync(fixture, access);
        var request = Request(fixture, AgentPlanningReviewChoice.Approve);
        var fault = new IOException("manager-process-commit-ack");
        var hooked = new CommitHook(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(),
            _ => throw fault, afterCommit: true);
        Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => Service(fixture, fixture.Coordinator(hooked))
            .ReviewAsync(request, manager, AgentExecutionOperationId.New())));
        var saved = (await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id))!.Run;
        Assert.Equal(RunOutcome.Succeeded, saved.Outcome);
        Assert.Null(saved.ToolAdmission!.PlanningDecision!.Work.Open);
        await using var scope = fixture.Services.CreateAsyncScope();
        var replay = await scope.ServiceProvider.GetRequiredService<ProjectTaskPlanProcessReviewService>()
            .ReviewAsync(request, manager, AgentExecutionOperationId.New());
        Assert.True(replay.Agent.Replayed);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion.Outcome);
        Assert.False(replay.Completion.CommittedNow);
        Assert.Equal(0, replay.Completion.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revocation_or_caller_cancellation_after_effect_still_settles_then_refuses_response(bool cancel) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        using var caller = new CancellationTokenSource();
        var access = new ManagerAccess();
        var manager = await ManagerAsync(fixture, access);
        var request = Request(fixture, AgentPlanningReviewChoice.Approve);
        var hooked = new CommitHook(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), token => {
            if (cancel) {
                caller.Cancel();
            } else {
                access.Decision = ProjectDefinitionWriteAccessDecision.Denied;
            }
            Assert.False(token.IsCancellationRequested);
            return Task.CompletedTask;
        }, afterCommit: false);
        var failure = await Record.ExceptionAsync(() => Service(fixture, fixture.Coordinator(hooked))
            .ReviewAsync(request, manager, AgentExecutionOperationId.New(), caller.Token));
        if (cancel) {
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        } else {
            Assert.IsType<AgentToolReceiptAccessDeniedException>(failure);
        }
        access.Decision = ProjectDefinitionWriteAccessDecision.Allowed;
        var replay = await Service(fixture).ReviewAsync(request, manager, AgentExecutionOperationId.New());
        Assert.True(replay.Agent.Replayed);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion.Outcome);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Sibling_root_budget_change_preserves_local_review_and_replay_returns_the_latest_runtime_view(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, steps: 2);
        var request = Request(fixture, AgentPlanningReviewChoice.Approve);
        var sibling = fixture.State.Steps.Single(item => item.StepInstanceId != request.Process.Child.StepInstanceId).StepInstanceId;
        await using (var database = Context(fixture.Services)) {
            var budgets = new EfProcessDecisionBudgetStore(database);
            var localBefore = await budgets.ReadAsync(fixture.State.RunId, request.Process.Child.StepInstanceId);
            var rootBefore = (await budgets.ReadAsync(fixture.State.RootRunId, null))!;
            var other = (await budgets.ReadAsync(fixture.State.RunId, sibling))!;
            var reserved = await fixture.Process.Owner(database, fixture.Clock).CommitAsync(new(RuntimeCommandId.New(), fixture.State,
                new(ProcessRuntimeTransitionOutcome.Applied, fixture.State with {
                    UpdatedAtUtc = fixture.State.UpdatedAtUtc.AddTicks(1),
                    Steps = fixture.State.Steps.Select(step => step.StepInstanceId == sibling
                        ? step with { Status = ProcessRuntimeStepStatus.Pending } : step).ToArray()
                }, [], [], [], [])) {
                DecisionBudget = new ProcessDecisionBudgetReservation(new(sibling, other.BudgetId, other.Revision),
                    new(Guid.NewGuid()), new(1, 0))
            });
            Assert.True(reserved.Succeeded);
            Assert.Equal(localBefore, await budgets.ReadAsync(fixture.State.RunId, request.Process.Child.StepInstanceId));
            Assert.True((await budgets.ReadAsync(fixture.State.RootRunId, null))!.Revision > rootBefore.Revision);
        }
        var manager = await ManagerAsync(fixture, new());
        var result = await Service(fixture).ReviewAsync(request, manager, AgentExecutionOperationId.New());
        Assert.True(result.Completion.Succeeded, string.Join("; ", result.Completion.Diagnostics.Select(item => item.Message)));
        Assert.Equal(ProcessRuntimeStepStatus.Pending, result.Completion.State.Steps.Single(item => item.StepInstanceId == sibling).Status);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        var later = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .ScheduleReadyAsync(result.CurrentState, fixture.CommandContext);
        Assert.True(later.Succeeded);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, later.State.Steps.Single(item => item.StepInstanceId == sibling).Status);
        var replay = await Service(fixture).ReviewAsync(request, manager, AgentExecutionOperationId.New());
        Assert.True(replay.Agent.Replayed);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion.Outcome);
        Assert.Equal(result.Completion.DecisionBudget, replay.Completion.DecisionBudget);
        Assert.Equal(result.Completion.Decision!.Command, replay.Completion.Decision!.Command);
        Assert.Equal(result.Completion.Decision.Current.Revision, replay.Completion.Decision.Current.Revision);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, replay.Completion.State.Steps.Single(item => item.StepInstanceId == sibling).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, replay.CurrentState.Steps.Single(item => item.StepInstanceId == sibling).Status);
        Assert.Equal(later.State.UpdatedAtUtc, replay.CurrentState.UpdatedAtUtc);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    private static ProjectTaskPlanProcessReviewRequest Request(ProjectTaskPlanReviewFixture fixture, AgentPlanningReviewChoice choice) {
        var action = Guid.NewGuid();
        return new(new(action, choice, AgentPlanningDecisionExpectation.From(fixture.Waiting),
            ProjectTaskPlanAgentFixture.Binding(fixture.Waiting), Assert.Single(fixture.Waiting.PendingApprovals).ApprovalId),
            fixture.Request with { CompletionKey = new(action) });
    }

    private static async Task<ProcessPlanningManagerAuthority> ManagerAsync(ProjectTaskPlanReviewFixture fixture, ManagerAccess access)
        => new(await fixture.Services.GetRequiredService<ProjectProcessLaunchAuthorityService>()
            .CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface), null, access);

    private static ProjectTaskPlanProcessReviewService Service(ProjectTaskPlanReviewFixture fixture, ProcessDecisionReviewCoordinator? completion = null)
        => completion is null ? fixture.Services.GetRequiredService<ProjectTaskPlanProcessReviewService>()
            : ActivatorUtilities.CreateInstance<ProjectTaskPlanProcessReviewService>(fixture.Services, completion);

    private sealed class ManagerAccess : IProjectDefinitionCurrentWriteAccess {
        internal ProjectDefinitionWriteAccessDecision Decision { get; set; } = ProjectDefinitionWriteAccessDecision.Allowed;
        public ValueTask<ProjectDefinitionWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Decision);
        }
    }

    private sealed class CommitHook(IProcessRuntimeUnitOfWork inner, Func<CancellationToken, Task> hook, bool afterCommit) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            if (!afterCommit) {
                await hook(cancellationToken);
            }
            var result = await inner.CommitAsync(request, cancellationToken);
            if (afterCommit) {
                await hook(cancellationToken);
            }
            return result;
        }
    }

    public enum ChangedProcessReview { WaitingResult, Strategy, Budget, Decision }
}
