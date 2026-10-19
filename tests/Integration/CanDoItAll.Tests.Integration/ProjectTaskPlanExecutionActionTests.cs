using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using EntryFixture = CanDoItAll.Tests.Integration.ProjectTaskPlanExecutionEntryTests.EntryFixture;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanExecutionActionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_publishes_original_plan_once_without_provider_dependency(bool disableProvider) {
        await using var fixture = await EntryFixture.CreateAsync();
        var original = await StartAsync(fixture);
        var request = Action(original, AgentPlanningAction.Approve);
        var review = Assert.IsType<AgentPlanningReview>(original.PlanningReview);
        Assert.Null(original.EffectContinuation);
        Assert.Equal(request.Expected, review.Expected);
        Assert.Equal(original.Run.ToolAdmission!.PlanningDecision!.Policy, review.Policy);
        Assert.Equal(new[] { AgentPlanningAction.Approve, AgentPlanningAction.Refresh, AgentPlanningAction.Cancel }, review.AvailableActions);
        Assert.Equal(request.Binding, review.Publication!.Binding);
        Assert.Contains("Review concurrent edits", review.Publication.Content, StringComparison.Ordinal);
        Assert.Contains("Priority: 3", review.Publication.Content, StringComparison.Ordinal);
        var serializedReview = JsonSerializer.Serialize(review, JsonSerializerOptions.Web);
        Assert.DoesNotContain("originalTarget", serializedReview, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(original.Run.ToolAdmission.Session.Profile.Fingerprint, serializedReview, StringComparison.Ordinal);
        if (disableProvider) {
            var providers = fixture.Files.Sql.Services.GetRequiredService<IProviderProfileRegistry>();
            var editor = await providers.GetProviderEditorAsync(fixture.Files.Provider.Id);
            editor.IsEnabled = false;
            await providers.SaveProviderAsync(editor);
        }
        var saved = await ApplyAsync(fixture, request);
        Assert.Equal(RunOutcome.Succeeded, saved.Run.Outcome);
        Assert.Empty(saved.PlanningReview!.AvailableActions);
        Assert.Null(saved.PlanningReview.Publication);
        Assert.Equal(AgentPlanningGenerationState.Completed, saved.PlanningReview.State);
        Assert.Equal(AgentPlanningGenerationState.Completed, saved.Run.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Null(saved.Run.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Empty(saved.Run.PendingApprovals);
        Assert.Equal(ExecutionApprovalStatus.Approved, Assert.Single(saved.Approvals).Status);
        Assert.Equal(AgentToolEffectState.Committed,
            AgentToolJournalTransitions.RequireProposal(saved.Run.ToolAdmission, request.Binding!).EffectState);
        Assert.Single(fixture.Wire.Requests);
        Assert.Equal(JsonSerializer.Serialize(original.Metrics), JsonSerializer.Serialize(saved.Metrics));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => ApplyAsync(fixture, request));
        await fixture.Files.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(AgentPlanningAction.Approve)]
    [InlineData(AgentPlanningAction.Refresh)]
    [InlineData(AgentPlanningAction.Cancel)]
    public async Task Refresh_renews_review_and_rejects_every_stale_action(AgentPlanningAction staleAction) {
        await using var fixture = await EntryFixture.CreateAsync();
        var original = await StartAsync(fixture);
        var stale = Action(original, staleAction);
        var refreshed = await ApplyAsync(fixture, Action(original, AgentPlanningAction.Refresh));
        var decision = refreshed.Run.ToolAdmission!.PlanningDecision!;
        Assert.Equal(original.Run.Id, refreshed.Run.Id);
        Assert.Equal(2, decision.CurrentGeneration);
        Assert.Equal(AgentPlanningDecisionExpectation.From(refreshed.Run), refreshed.PlanningReview!.Expected);
        Assert.NotEqual(original.PlanningReview!.Publication!.Binding, refreshed.PlanningReview.Publication!.Binding);
        Assert.Equal(0, decision.AutomaticReplans);
        Assert.Equal(original.Run.ToolAdmission!.PlanningDecision!.Policy, decision.Policy);
        Assert.True(decision.Work.ClosedWorkTicks >= original.Run.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Null(decision.Work.Open);
        Assert.Equal(AgentPlanningGenerationState.AwaitingApproval, decision.Current.State);
        Assert.NotEqual(original.Run.PendingApprovals.Single().ToolAdmission, Assert.Single(refreshed.Run.PendingApprovals).ToolAdmission);
        Assert.Equal(ExecutionApprovalStatus.Rejected,
            Assert.Single(refreshed.Approvals, item => item.ApprovalId == original.Run.PendingApprovals.Single().ApprovalId).Status);
        Assert.Equal(ExecutionApprovalStatus.Pending,
            Assert.Single(refreshed.Approvals, item => item.ApprovalId == refreshed.Run.PendingApprovals.Single().ApprovalId).Status);
        Assert.Single(refreshed.ChatSession!.Messages, item => item.Role == ChatMessageRole.User);
        Assert.Equal(2, refreshed.Metrics.Count);
        Assert.Equal(22, refreshed.UsageObservations.Sum(item => item.InputTokens));
        var before = JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => ApplyAsync(fixture, stale));
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id)));
        Assert.Equal(2, fixture.Wire.Requests.Count);
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Changed_provider_contract_refuses_refresh_before_retiring_approval() {
        await using var fixture = await EntryFixture.CreateAsync();
        var original = await StartAsync(fixture);
        var providers = fixture.Files.Sql.Services.GetRequiredService<IProviderProfileRegistry>();
        var editor = await providers.GetProviderEditorAsync(fixture.Files.Provider.Id);
        editor.BaseUrl = "https://changed-provider.example.test/v1";
        await providers.SaveProviderAsync(editor);
        var before = JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id));
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => ApplyAsync(fixture, Action(original, AgentPlanningAction.Refresh)));
        Assert.Equal("planning.runtime-contract-changed", failure.Code);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id)));
        Assert.Single(fixture.Wire.Requests);
        var cancelled = await ApplyAsync(fixture, Action(original, AgentPlanningAction.Cancel));
        Assert.Equal(RunOutcome.Cancelled, cancelled.Run.Outcome);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, cancelled.Run.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Null(cancelled.Run.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Empty(cancelled.Run.PendingApprovals);
        Assert.Equal(ExecutionApprovalStatus.Rejected, Assert.Single(cancelled.Approvals).Status);
        Assert.Single(fixture.Wire.Requests);
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(OnBasisChange.Fail)]
    [InlineData(OnBasisChange.PauseForReview)]
    [InlineData(OnBasisChange.Replan)]
    public async Task Confirmed_basis_change_obeys_original_policy_and_bounded_replan(OnBasisChange behavior) {
        var policy = new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.RequireCurrent, behavior,
            new(behavior == OnBasisChange.Replan ? 1 : 0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)));
        await using var fixture = await EntryFixture.CreateAsync(policy);
        var original = await StartAsync(fixture);
        await ChangeBasisAsync(fixture);
        var saved = await ApplyAsync(fixture, Action(original, AgentPlanningAction.Approve));
        var decision = saved.Run.ToolAdmission!.PlanningDecision!;
        Assert.Equal(policy, decision.Policy);
        Assert.Null(decision.Work.Open);
        await fixture.Files.Sql.VerifyAsync(1, 1);
        if (behavior == OnBasisChange.Replan) {
            Assert.Equal(2, decision.CurrentGeneration);
            Assert.Equal(1, decision.AutomaticReplans);
            Assert.Equal(AgentPlanningGenerationState.AwaitingApproval, decision.Current.State);
            Assert.Single(saved.Run.PendingApprovals);
            Assert.Equal(2, fixture.Wire.Requests.Count);
            await ChangeBasisAsync(fixture);
            var exhausted = await ApplyAsync(fixture, Action(saved, AgentPlanningAction.Approve));
            Assert.Equal(2, exhausted.Run.ToolAdmission!.PlanningDecision!.CurrentGeneration);
            Assert.Equal(AgentPlanningGenerationState.PausedBasisChanged, exhausted.Run.ToolAdmission.PlanningDecision.Current.State);
            Assert.Equal(1, exhausted.Run.ToolAdmission.PlanningDecision.AutomaticReplans);
            Assert.Empty(exhausted.Run.PendingApprovals);
            Assert.Equal(2, fixture.Wire.Requests.Count);
            await fixture.Files.Sql.VerifyAsync(2, 2);
        } else {
            Assert.Equal(1, decision.CurrentGeneration);
            Assert.Equal(behavior == OnBasisChange.Fail ? AgentPlanningGenerationState.Failed : AgentPlanningGenerationState.PausedBasisChanged,
                decision.Current.State);
            Assert.Equal(behavior == OnBasisChange.Fail ? RunOutcome.Failed : (RunOutcome?)null, saved.Run.Outcome);
            Assert.Empty(saved.Run.PendingApprovals);
            Assert.Single(fixture.Wire.Requests);
        }
    }

    [Fact]
    public async Task Unconfirmed_commit_preserves_original_work_and_cannot_refresh_or_cancel_it() {
        await using var fixture = await EntryFixture.CreateAsync();
        var original = await StartAsync(fixture);
        fixture.Files.Sql.Faults.Commit.AfterCommit = () => {
            fixture.Files.Sql.Probe.BeforeCommand = (command, _) => command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)
                ? Task.FromException(new IOException("receipt-temporarily-unavailable")) : Task.CompletedTask;
            throw new IOException("commit-acknowledgement-lost");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => ApplyAsync(fixture, Action(original, AgentPlanningAction.Approve)));
        fixture.Files.Sql.Probe.BeforeCommand = null;
        var uncertain = (await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id))!;
        var work = uncertain.Run.ToolAdmission!.PlanningDecision!.Work;
        Assert.NotNull(work.Open);
        var review = (await fixture.Workspace.GetExecutionRunDetailAsync(original.Run.Id)).PlanningReview!;
        Assert.True(review.HasUnsettledWork);
        Assert.Null(review.RemainingWork);
        Assert.Empty(review.AvailableActions);
        Assert.Equal(AgentToolEffectState.Unknown,
            AgentToolJournalTransitions.RequireProposal(uncertain.Run.ToolAdmission, original.Run.PendingApprovals.Single().ToolAdmission!).EffectState);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => ApplyAsync(fixture, Action(uncertain, AgentPlanningAction.Refresh)));
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => ApplyAsync(fixture, Action(uncertain, AgentPlanningAction.Cancel)));
        Assert.Equal(work, (await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id))!.Run.ToolAdmission!.PlanningDecision!.Work);
        Assert.Single(fixture.Wire.Requests);
        await fixture.Files.Sql.VerifyAsync(1, 1);
    }

    internal static async Task<ExecutionRunDetail> StartAsync(EntryFixture fixture) {
        var result = await fixture.Workspace.ExecuteRunAsync(fixture.Request);
        Assert.True(result.State == ExecutionState.WaitingOnTool, result.ResponseText);
        return await fixture.Workspace.GetExecutionRunDetailAsync(result.ExecutionRunId);
    }

    private static Task<ExecutionRunDetail> ApplyAsync(EntryFixture fixture, AgentPlanningActionRequest request)
        => fixture.Workspace.ApplyPlanningActionAsync(request, AgentExecutionOperationId.New());

    internal static AgentPlanningActionRequest Action(ExecutionRunDetail detail, AgentPlanningAction action) {
        var pending = action == AgentPlanningAction.Approve ? Assert.Single(detail.Run.PendingApprovals) : null;
        return new(action, detail.Run.ChatSessionId!.Value, AgentPlanningDecisionExpectation.From(detail.Run),
            pending?.ToolAdmission, pending?.ApprovalId);
    }

    private static async Task ChangeBasisAsync(EntryFixture fixture)
        => Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Files.Sql.Publisher()
            .PublishAsync(await fixture.Files.Sql.CommandAsync(1), fixture.Files.Sql.Invocation));
}
