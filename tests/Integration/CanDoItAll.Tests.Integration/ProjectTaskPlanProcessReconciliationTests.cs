using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessReconciliationTests {
    [Theory]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.RunSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.RunSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.StepSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.StepSnapshot)]
    public async Task Closed_original_child_can_be_accounted_without_the_lost_approval_request(
        AgentPlanningReviewChoice choice, ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var closed = await fixture.CloseWithoutAccountingAsync(choice);
        var before = await ReadOriginalAsync(fixture);
        var view = await ViewAsync(fixture);
        Assert.Equal(ProcessPlanningReviewState.RecoveryRequired, view.State);
        Assert.Null(view.Process);
        Assert.Empty(view.AvailableChoices);
        var request = Request(view);

        var result = await Service(fixture).ReconcileAsync(request, await fixture.ManagerAsync());

        Assert.True(result.Completion.CommittedNow);
        Assert.True(result.Completion.Succeeded, string.Join("; ", result.Completion.Diagnostics.Select(item => item.Message)));
        Assert.Equal(closed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks,
            result.Completion.DecisionBudget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.Equal(0, result.Completion.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Equal(request.Process.CompletionKey.Value, result.ActionId);
        await using var scope = fixture.Services.CreateAsyncScope();
        var replay = await scope.ServiceProvider.GetRequiredService<ProjectTaskPlanProcessReconciliationService>()
            .ReconcileAsync(request, await fixture.ManagerAsync());
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion.Outcome);
        Assert.False(replay.Completion.CommittedNow);
        Assert.Equal(result.Completion.DecisionBudget, replay.Completion.DecisionBudget);
        Assert.Equal(before, await ReadOriginalAsync(fixture));
        var settled = await ViewAsync(fixture);
        Assert.Equal(ProcessPlanningReviewState.Settled, settled.State);
        Assert.Null(settled.Accounting);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(choice == AgentPlanningReviewChoice.Approve ? 1 : 0, choice == AgentPlanningReviewChoice.Approve ? 1 : 0);
    }

    [Fact]
    public async Task Pending_approval_cannot_be_turned_into_closed_work_by_accounting() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var before = await ReadOriginalAsync(fixture);
        var view = await ViewAsync(fixture);
        Assert.Null(view.Accounting);
        var request = new ProjectTaskPlanProcessReconciliationRequest(view.Expected, fixture.Request);
        var manager = await fixture.ManagerAsync();

        var exception = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            Service(fixture).ReconcileAsync(request, manager));

        Assert.Equal("process-accounting.child-pending", exception.Code);
        Assert.Equal(before, await ReadOriginalAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stale_Agent_or_Process_expectation_cannot_settle_current_work(bool process) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        var request = Request(await ViewAsync(fixture));
        var before = await ReadOriginalAsync(fixture);
        var manager = await fixture.ManagerAsync();
        if (process) {
            request = request with { Process = request.Process with { Decision = request.Process.Decision with { Revision = request.Process.Decision.Revision + 1 } } };
            var refused = await Service(fixture).ReconcileAsync(request, manager);
            Assert.False(refused.Completion.Succeeded);
            Assert.Contains(refused.Completion.Diagnostics, item => item.Code == "Runtime.ReviewExpectationChanged");
        } else {
            request = request with { Expected = AgentPlanningDecisionExpectation.From(fixture.Waiting) };
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Service(fixture).ReconcileAsync(request, manager));
        }
        Assert.NotNull((await ViewAsync(fixture)).Accounting);
        Assert.Equal(before, await ReadOriginalAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Current_manager_revocation_blocks_accounting_and_can_be_reauthorized() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Reject);
        var request = Request(await ViewAsync(fixture));
        var manager = await fixture.ManagerAsync();
        fixture.ManagerAllowed = false;

        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => Service(fixture).ReconcileAsync(request, manager));

        fixture.ManagerAllowed = true;
        Assert.NotNull((await ViewAsync(fixture)).Accounting);
        Assert.True((await Service(fixture).ReconcileAsync(request, manager)).Completion.CommittedNow);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Renewed_manager_can_recover_accounting_but_a_different_actor_cannot_adopt_its_receipt() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(configureServices: services =>
            services.PostConfigure<ApiAccessOptions>(options => {
                options.Enabled = true;
                options.Authorization.Enabled = true;
            }));
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Reject);
        var request = Request(await ViewAsync(fixture));
        var sources = fixture.Services.GetRequiredService<ProjectProcessLaunchAuthorityService>();
        async Task<ProcessPlanningManagerAuthority> ManagerAsync(string subject)
            => new(await sources.CaptureAuthenticatedAsync(null, subject, fixture.Clock.GetUtcNow().AddHours(1)),
                "accounting-fixture", new AllowedManagerAccess());
        var first = await ManagerAsync("original-manager");
        var completed = await Service(fixture).ReconcileAsync(request, first);
        Assert.True(completed.Completion.CommittedNow);
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        var renewed = await ManagerAsync("original-manager");
        Assert.Equal(first.Actor, renewed.Actor);

        var replay = await Service(fixture).ReconcileAsync(request, renewed);
        var refused = await Service(fixture).ReconcileAsync(request, await ManagerAsync("different-manager"));

        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion.Outcome);
        Assert.Equal(completed.Completion.DecisionBudget, replay.Completion.DecisionBudget);
        Assert.False(refused.Completion.Succeeded);
        Assert.False(refused.Completion.CommittedNow);
        Assert.Contains(refused.Completion.Diagnostics, item => item.Code == "Runtime.ReviewExpectationChanged");
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Lost_accounting_acknowledgement_replays_one_original_settlement_in_a_new_scope() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        var request = Request(await ViewAsync(fixture));
        var before = await ReadOriginalAsync(fixture);
        var fault = new IOException("Accounting response lost after the original transaction committed.");
        var completion = fixture.Coordinator(new CommitHook(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(),
            afterCommit: _ => throw fault));
        var manager = await fixture.ManagerAsync();

        Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => Service(fixture, completion).ReconcileAsync(request, manager)));

        await using var scope = fixture.Services.CreateAsyncScope();
        var replay = await scope.ServiceProvider.GetRequiredService<ProjectTaskPlanProcessReconciliationService>().ReconcileAsync(request, manager);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion.Outcome);
        Assert.False(replay.Completion.CommittedNow);
        Assert.Equal(0, replay.Completion.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        Assert.Equal(before, await ReadOriginalAsync(fixture));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Root_cancellation_does_not_turn_accounting_into_a_new_runtime_completion() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Reject);
        var request = Request(await ViewAsync(fixture));
        var owner = fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        var cancellation = await new ProcessRuntimeEngine(owner).BeginRootCancellationAsync(current, fixture.CommandContext);
        Assert.True(cancellation.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, cancellation.State.Status);

        var result = await Service(fixture).ReconcileAsync(request, await fixture.ManagerAsync());

        Assert.False(result.Completion.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, result.CurrentState.Status);
        Assert.Null((await ViewAsync(fixture)).Accounting);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revocation_or_cancellation_after_commit_refuses_response_and_preserves_exact_recovery(bool cancel) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Reject);
        var request = Request(await ViewAsync(fixture));
        var manager = await fixture.ManagerAsync();
        using var cancellation = new CancellationTokenSource();
        var completion = fixture.Coordinator(new CommitHook(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), afterCommit: _ => {
            if (cancel) {
                cancellation.Cancel();
            } else {
                fixture.ManagerAllowed = false;
            }
            return Task.CompletedTask;
        }));

        var error = await Record.ExceptionAsync(() => Service(fixture, completion).ReconcileAsync(request, manager, cancellation.Token));

        if (cancel) {
            Assert.IsAssignableFrom<OperationCanceledException>(error);
        } else {
            Assert.IsType<AgentToolReceiptAccessDeniedException>(error);
        }
        fixture.ManagerAllowed = true;
        var replay = await Service(fixture).ReconcileAsync(request, manager);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion.Outcome);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Competing_accounting_actions_cannot_double_settle_the_same_closed_child() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        var first = Request(await ViewAsync(fixture));
        var second = first with { Process = first.Process with { CompletionKey = new(Guid.NewGuid()) } };
        var manager = await fixture.ManagerAsync();
        await using var left = fixture.Services.CreateAsyncScope();
        await using var right = fixture.Services.CreateAsyncScope();
        var arrived = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task BeforeCommit(CancellationToken token) {
            if (Interlocked.Increment(ref arrived) == 2) {
                release.TrySetResult();
            }
            await release.Task.WaitAsync(TimeSpan.FromSeconds(45), token);
        }
        async Task<(ProjectTaskPlanProcessReconciliationResult? Result, Exception? Error)> ExecuteAsync(
            IServiceProvider services, ProjectTaskPlanProcessReconciliationRequest request) {
            var hooked = ActivatorUtilities.CreateInstance<ProcessDecisionReviewCoordinator>(services,
                new CommitHook(services.GetRequiredService<IProcessRuntimeUnitOfWork>(), beforeCommit: BeforeCommit));
            var service = ActivatorUtilities.CreateInstance<ProjectTaskPlanProcessReconciliationService>(services, hooked);
            try {
                return (await service.ReconcileAsync(request, manager), null);
            } catch (ProcessRuntimeOptimisticConcurrencyException exception) {
                return (null, exception);
            }
        }
        var results = await Task.WhenAll(ExecuteAsync(left.ServiceProvider, first), ExecuteAsync(right.ServiceProvider, second));

        Assert.Equal(2, arrived);
        Assert.Single(results, item => item.Result?.Completion.CommittedNow == true);
        Assert.Single(results, item => item.Error is ProcessRuntimeOptimisticConcurrencyException || item.Result?.Completion.Succeeded == false);
        Assert.Equal(ProcessPlanningReviewState.Settled, (await ViewAsync(fixture)).State);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    private static ProjectTaskPlanProcessReconciliationRequest Request(ProjectTaskPlanProcessReview view)
        => new(view.Expected, Assert.IsType<ProcessDecisionPendingReview>(view.Accounting).ForAction(Guid.NewGuid()));

    private static async Task<ProjectTaskPlanProcessReview> ViewAsync(ProjectTaskPlanReviewFixture fixture)
        => await fixture.Services.GetRequiredService<ProjectTaskPlanProcessReviewReader>().ReadAsync(
            fixture.State.RunId, fixture.Request.Child.StepInstanceId, fixture.Waiting.Id, await fixture.ManagerAsync());

    private static async Task<string> ReadOriginalAsync(ProjectTaskPlanReviewFixture fixture)
        => JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id));

    private static ProjectTaskPlanProcessReconciliationService Service(ProjectTaskPlanReviewFixture fixture, ProcessDecisionReviewCoordinator? completion = null)
        => completion is null ? fixture.Services.GetRequiredService<ProjectTaskPlanProcessReconciliationService>()
            : ActivatorUtilities.CreateInstance<ProjectTaskPlanProcessReconciliationService>(fixture.Services, completion);

    private sealed class AllowedManagerAccess : IProjectDefinitionCurrentWriteAccess {
        public ValueTask<ProjectDefinitionWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ProjectDefinitionWriteAccessDecision.Allowed);
        }
    }

    private sealed class CommitHook(IProcessRuntimeUnitOfWork inner, Func<CancellationToken, Task>? beforeCommit = null,
        Func<CancellationToken, Task>? afterCommit = null) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            if (beforeCommit is not null) {
                await beforeCommit(cancellationToken);
            }
            var result = await inner.CommitAsync(request, cancellationToken);
            if (afterCommit is not null) {
                Assert.True(result.Succeeded);
                await afterCommit(cancellationToken);
            }
            return result;
        }
    }
}
