using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanDispatchTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_strategy_preserves_original_policy_then_reviews_and_accounts_one_publication(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanDispatchFixture.CreateAsync(mode);
        var result = await fixture.ExecuteStrategyAsync();
        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var run = await fixture.ReadAsync(result.ExecutionRunId!.Value);
        var decision = run.ToolAdmission!.PlanningDecision!;
        var parent = ProjectTaskPlanProcessCodec.Parent(decision.InheritedOrigin!);
        var input = await fixture.Services.GetRequiredService<IProcessAcceptedExecutionInputReader>()
            .ReadAsync(fixture.Request.RunId, fixture.Process.Claim.StepInstanceId);
        Assert.Equal(input.Assignment.Prompt, run.ToolAdmission.BackgroundInput!.Content);
        Assert.Equal(input.Assignment.DecisionDataPolicy!.Policy, decision.InheritedOrigin!.ParentPolicy);
        Assert.Equal(mode, parent.Scope.ReadBasisMode);
        Assert.Equal(parent.Child.MaterialId.Value, decision.InheritedOrigin.BasisId);
        Assert.Single(run.PendingApprovals);
        Assert.False(run.AutoApprovePendingToolCalls);
        Assert.Single(fixture.Entry.Wire.Requests);
        var replay = await fixture.ExecuteAsync();
        Assert.Equal(result.ExecutionRunId, replay.ExecutionRunId);
        Assert.Equal(run.Revision, (await fixture.ReadAsync(result.ExecutionRunId.Value)).Revision);
        Assert.Single(fixture.Entry.Wire.Requests);

        var blocked = await fixture.SubmitAsync(result);
        Assert.True(blocked.Succeeded, string.Join("; ", blocked.Diagnostics.Select(item => item.Message)));
        Assert.Equal(ProcessDecisionPhase.WaitingForReview, blocked.Decision!.Current.Phase);
        Assert.Equal(DispatchClaimStatus.Completed, Assert.Single(blocked.State.Claims).Status);
        var selected = (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.Request.RunId, fixture.Process.Claim.StepInstanceId, result.ExecutionRunId))!;
        var action = Guid.NewGuid();
        ProcessDecisionReviewRequest processRequest = new(parent.Child.Address, result.StrategyId, new(result.IdempotencyKey), new(action),
            selected.Head!.Expectation, new(fixture.Process.Claim.StepInstanceId, selected.Budget.BudgetId, selected.Budget.Revision));
        ProjectTaskPlanProcessReviewRequest request = new(new(action, AgentPlanningReviewChoice.Approve,
            AgentPlanningDecisionExpectation.From(run), ProjectTaskPlanAgentFixture.Binding(run),
            Assert.Single(run.PendingApprovals).ApprovalId), processRequest);
        ProcessPlanningManagerAuthority manager = new(await fixture.Services.GetRequiredService<ProjectProcessLaunchAuthorityService>()
            .CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface), null, new AllowedAccess());
        var service = fixture.Services.GetRequiredService<ProjectTaskPlanProcessReviewService>();
        var accepted = await service.ReviewAsync(request, manager, AgentExecutionOperationId.New());
        Assert.True(accepted.Completion.Succeeded);
        Assert.Equal(ProcessDecisionPhase.Completed, accepted.Completion.Decision!.Current.Phase);
        Assert.Equal(0, accepted.Completion.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        var duplicate = await service.ReviewAsync(request, manager, AgentExecutionOperationId.New());
        Assert.True(duplicate.Agent.Replayed);
        Assert.False(duplicate.Completion.CommittedNow);
        Assert.Equal(accepted.Completion.DecisionBudget, duplicate.Completion.DecisionBudget);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Competing_adapter_does_not_submit_or_restart_active_provider_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanDispatchFixture.CreateAsync(mode);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        fixture.Entry.Wire.OnRequest = async token => {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        var first = fixture.ExecuteAsync(cancellationToken: deadline.Token);
        try {
            await entered.Task.WaitAsync(deadline.Token);
            var pending = await Assert.ThrowsAsync<ProcessRuntimeDispatchInProgressException>(() =>
                fixture.ExecuteAsync(cancellationToken: deadline.Token));
            Assert.Single(fixture.Entry.Wire.Requests);
            var held = await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.Request.RunId);
            Assert.Equal(fixture.Process.Claim.ClaimToken, Assert.Single(held!.Claims).ClaimToken);
            Assert.Equal(DispatchClaimStatus.Claimed, Assert.Single(held.Claims).Status);
            Assert.Empty(held.AppliedResults);
            Assert.NotEqual(Guid.Empty, pending.ExecutionRunId.Value);
        } finally {
            release.TrySetResult();
            await first;
        }
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Competing_adapter_preserves_original_claim_while_material_preparation_has_no_child() {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = await ProjectTaskPlanDispatchFixture.CreateAsync(ReadBasisMode.StepSnapshot,
            configureServices: services => {
                services.RemoveAll<IProcessDecisionMaterialCapturer>();
                services.AddScoped<IProcessDecisionMaterialCapturer>(provider => new HeldCapture(
                    ActivatorUtilities.CreateInstance<ProjectTaskPlanMaterialCapturer>(provider), entered, release));
            });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var first = fixture.ExecuteAsync(cancellationToken: deadline.Token);
        try {
            await entered.Task.WaitAsync(deadline.Token);
            var pending = await Assert.ThrowsAsync<ProcessDecisionPreparationInProgressException>(() =>
                fixture.ExecuteAsync(cancellationToken: deadline.Token));
            Assert.Equal(fixture.Process.Claim with { DecisionContract = ProjectTaskPlanProcessContract.Execution }, pending.Claim);
            var held = await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.Request.RunId);
            Assert.Equal(fixture.Process.Claim.ClaimToken, Assert.Single(held!.Claims).ClaimToken);
            Assert.Equal(DispatchClaimStatus.Claimed, Assert.Single(held.Claims).Status);
            Assert.Empty(held.AppliedResults);
            Assert.Empty(fixture.Entry.Wire.Requests);
            var selection = await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
                .ReadAsync(fixture.Request.RunId, fixture.Process.Claim.StepInstanceId, null);
            Assert.Null(selection!.Child);
            Assert.True(selection.OtherWorkPending);
        } finally {
            release.TrySetResult();
            await first;
        }
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Current_assignment_repair_cannot_replace_original_accepted_provider_input(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanDispatchFixture.CreateAsync(mode);
        var input = await fixture.Services.GetRequiredService<IProcessAcceptedExecutionInputReader>()
            .ReadAsync(fixture.Request.RunId, fixture.Process.Claim.StepInstanceId);
        await using var context = ProcessDecisionPersistenceFixture.Context(fixture.Services);
        await context.RuntimeStepAssignments.Where(item => item.RunId == fixture.Request.RunId.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Prompt, "Changed current planning prompt"));

        var result = await fixture.ExecuteStrategyAsync();

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var run = await fixture.ReadAsync(result.ExecutionRunId!.Value);
        Assert.Equal(input.Assignment.Prompt, run.ToolAdmission!.BackgroundInput!.Content);
        Assert.NotEqual("Changed current planning prompt", run.ToolAdmission.BackgroundInput.Content);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Existing_enrollment_with_no_journal_is_not_recreated_or_refunded(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanDispatchFixture.CreateAsync(mode);
        var prepared = await fixture.Services.GetRequiredService<ProcessDecisionStepPreparationCoordinator>()
            .PrepareAsync(fixture.Process.State, fixture.Process.Claim, TimeSpan.FromSeconds(30));
        Assert.True(prepared.EnrolledNow);
        var original = prepared.Child!;
        var result = await fixture.ExecuteStrategyAsync();
        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Equal(original.Child.Address.ChildId.Value, result.ExecutionRunId!.Value.Value);
        Assert.Null(result.OwnerEvidence);
        Assert.Null(await fixture.Entry.Files.Store.GetExecutionRunAsync(original.Child.Address.ChildId.Value));
        Assert.Empty(fixture.Entry.Wire.Requests);
        var blocked = await fixture.SubmitAsync(result);
        Assert.True(blocked.Succeeded);
        Assert.Equal(ProcessDecisionPhase.Reconciling, blocked.Decision!.Current.Phase);
        Assert.Null(blocked.DecisionBudget);
        var child = await fixture.Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(original.Child.Address);
        Assert.Null(child!.Closure);
        Assert.Null(child.Settlement);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Unsupported_obligations_are_refused_before_step_allocation_or_provider_work(ReadBasisMode mode, bool additionalHost) {
        await using var fixture = await ProjectTaskPlanDispatchFixture.CreateAsync(mode);
        var before = JsonSerializer.Serialize(await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.Request.RunId));
        var result = await fixture.ExecuteAsync(fixture.Request with {
            StepContract = additionalHost
                ? ProcessStepExecutionContract.Empty with { RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId> { ProcessHostCapabilityIds.ManagedProcessAdapter, new("host.workspace") } }
                : ProcessStepExecutionContract.Empty with { RequiredRuntimeToolNames = ["unadmitted_tool"] }
        });
        Assert.Equal(StrategyOutcome.Failed, result.Outcome);
        Assert.Null(result.ExecutionRunId);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.Request.RunId)));
        Assert.Empty(fixture.Entry.Wire.Requests);
        var selected = await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.Request.RunId, fixture.Process.Claim.StepInstanceId, null);
        Assert.Null(selected!.Head);
        Assert.Equal(0, selected.Budget.ReservedWorkTicks);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Expired_claim_cannot_enroll_a_child_or_start_provider_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanDispatchFixture.CreateAsync(mode, TimeSpan.FromMinutes(-1));
        var result = await fixture.ExecuteAsync();
        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Null(result.ExecutionRunId);
        Assert.Empty(fixture.Entry.Wire.Requests);
        var selected = await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.Request.RunId, fixture.Process.Claim.StepInstanceId, null);
        Assert.Null(selected!.Head);
        Assert.Equal(0, selected.Budget.ReservedWorkTicks);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Closed_startup_refusal_retains_actual_child_and_measured_evidence(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanDispatchFixture.CreateAsync(mode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var executor = Assert.Single(scope.ServiceProvider.GetServices<IProcessDecisionStepExecutor>());
        ProcessRuntimeStepAssignment? encoded = null;
        var result = await executor.ExecuteAsync(fixture.Request, original => {
            encoded = original;
            return "{}";
        });
        var input = await fixture.Services.GetRequiredService<IProcessAcceptedExecutionInputReader>()
            .ReadAsync(fixture.Request.RunId, fixture.Process.Claim.StepInstanceId);
        Assert.Equal(JsonSerializer.Serialize(input.Assignment), JsonSerializer.Serialize(encoded));
        Assert.Equal(StrategyOutcome.Failed, result.Outcome);
        var evidence = Assert.IsType<ProcessExecutionOwnerEvidence<AgentInheritedPlanningPreparationFailure>>(result.OwnerEvidence);
        Assert.Equal(result.ExecutionRunId!.Value.Value, evidence.Value.ExecutionRunId);
        Assert.Equal(fixture.Entry.Files.Agent.Id, evidence.Value.AgentId);
        Assert.Null(await fixture.Entry.Files.Store.GetExecutionRunAsync(evidence.Value.ExecutionRunId));
        Assert.Empty(fixture.Entry.Wire.Requests);
        var selected = (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.Request.RunId, fixture.Process.Claim.StepInstanceId, result.ExecutionRunId))!;
        var reader = Assert.IsAssignableFrom<IProcessDecisionLiveOutcomeReader>(
            Assert.Single(fixture.Services.GetServices<IProcessDecisionChildOutcomeReader>()));
        var closed = Assert.IsType<ProcessDecisionChildResult.Closed>(await reader.ReadAsync(selected.Child!, evidence));
        Assert.Equal(ProcessDecisionChildOutcome.NoEffect, closed.Closure.Outcome);
        Assert.Equal(evidence.Value.WorkTicks, closed.Work.Consumed.WorkTicks);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    private sealed class HeldCapture(IProcessDecisionMaterialCapturer inner, TaskCompletionSource entered,
        TaskCompletionSource release) : IProcessDecisionMaterialCapturer {
        public ProcessDecisionContentContract ContentContract => inner.ContentContract;

        public async Task<ProcessDecisionMaterialCaptureResult> CaptureAsync(ProcessDecisionMaterialCaptureRequest request,
            CancellationToken cancellationToken = default) {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return await inner.CaptureAsync(request, cancellationToken);
        }
    }

    private sealed class AllowedAccess : IProjectDefinitionCurrentWriteAccess {
        public ValueTask<ProjectDefinitionWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ProjectDefinitionWriteAccessDecision.Allowed);
        }
    }
}
