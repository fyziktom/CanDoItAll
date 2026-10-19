using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed partial class AgentChatPanelResponsivenessTests {
    [Theory]
    [InlineData(AgentPlanningAction.Approve)]
    [InlineData(AgentPlanningAction.Refresh)]
    [InlineData(AgentPlanningAction.Cancel)]
    public async Task Planning_actions_use_exact_review_and_preserve_typing_while_duplicate_intents_are_rejected(AgentPlanningAction action) {
        using var fixture = CreatePlanningPanel(paused: action == AgentPlanningAction.Refresh);
        var cut = fixture.Cut;
        await cut.Find("[data-testid='agents-chat-planning-open']").ClickAsync(new MouseEventArgs());
        Assert.DoesNotContain("approval-1", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.FindComponent<AgentChatActionsSurface>().Instance.State.HasPendingApprovals);
        Assert.Empty(cut.FindAll("script"));
        var surface = cut.FindComponent<AgentChatPlanningSurface>().Instance;
        var request = PlanningAction(fixture.Detail.PlanningReview!, action);
        var intent = new AgentChatActionIntent(surface.State.Origin, AgentChatAction.ApplyPlanningAction) { PlanningRequest = request };
        cut.Find("[data-testid='chat-prompt-input']").Input("Unsent draft before publication");
        await cut.InvokeAsync(() => surface.Intent.InvokeAsync(intent));
        var operation = CaptureSubmittedOperation(cut.Instance);
        try {
            await fixture.Writer.PlanningStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cut.InvokeAsync(() => surface.Intent.InvokeAsync(intent));
            Assert.Equal(request, Assert.Single(fixture.Writer.PlanningRequests).Request);
            Assert.Equal(fixture.Agent.Id, fixture.Writer.PlanningRequests[0].AgentId);
            Assert.True(cut.Find($"[data-testid='agents-chat-planning-{action.ToString().ToLowerInvariant()}']").HasAttribute("disabled"));
            cut.Find("[data-testid='chat-prompt-input']").Input("Newer unsent draft during publication");
            var current = fixture.Detail with {
                Run = fixture.Detail.Run with { Revision = fixture.Detail.Run.Revision + 1 },
                PlanningReview = fixture.Detail.PlanningReview! with {
                    Expected = new(fixture.Detail.Run.Id, fixture.Detail.Run.Revision + 1, 12,
                        fixture.Detail.PlanningReview!.Expected.DecisionId, 2),
                    AvailableActions = [], Publication = null, State = AgentPlanningGenerationState.Completed
                }
            };
            fixture.Reads.InitialRunDetail = current;
            fixture.Reads.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, fixture.Session, current.Run));
            fixture.Writer.PlanningCompletion.SetResult(current);
            await operation.WaitAsync(TimeSpan.FromSeconds(10));
            cut.WaitForAssertion(() => Assert.False(cut.FindComponent<ChatWorkspacePanel>().Instance.IsBusy));
            Assert.Equal("Newer unsent draft during publication", cut.FindComponent<ChatWorkspacePanel>().Instance.DraftPrompt);
            Assert.Empty(cut.FindAll("[data-testid='agents-chat-planning-dialog']"));
            await cut.InvokeAsync(() => surface.Intent.InvokeAsync(intent));
            Assert.Single(fixture.Writer.PlanningRequests);
            Assert.Empty(fixture.Writer.SendRequests);
            Assert.False(fixture.Writer.ApprovalStarted.Task.IsCompleted);
        } finally {
            fixture.Writer.PlanningCompletion.TrySetCanceled();
            await operation.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task Planning_failure_preserves_unknown_outcome_until_explicit_read_and_never_retries() {
        using var fixture = CreatePlanningPanel();
        var cut = fixture.Cut;
        cut.Find("[data-testid='chat-prompt-input']").Input("Keep this draft");
        await cut.Find("[data-testid='agents-chat-planning-open']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='agents-chat-planning-approve']").ClickAsync(new MouseEventArgs());
        var operation = CaptureSubmittedOperation(cut.Instance);
        fixture.Writer.PlanningCompletion.SetException(new IOException("PRIVATE_PLANNING_FAILURE"));
        await operation.WaitAsync(TimeSpan.FromSeconds(10));
        cut.WaitForElement("[data-testid='agents-chat-planning-unconfirmed']");
        Assert.DoesNotContain("PRIVATE_PLANNING_FAILURE", cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='agents-chat-planning-approve']").HasAttribute("disabled"));
        Assert.Equal("Keep this draft", cut.FindComponent<ChatWorkspacePanel>().Instance.DraftPrompt);
        Assert.Single(fixture.Reads.ExecutionRunDetailRequests);
        fixture.Reads.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, fixture.Session, fixture.Detail.Run));
        await cut.Find("[data-testid='agents-chat-planning-reload']").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, fixture.Reads.ExecutionRunDetailRequests.Count);
        Assert.Empty(cut.FindAll("[data-testid='agents-chat-planning-dialog']"));
        Assert.Single(fixture.Writer.PlanningRequests);
        Assert.DoesNotContain(fixture.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity == NotificationSeverity.Success);
    }

    [Theory]
    [InlineData(RecoveryDetach.SwitchThread)]
    [InlineData(RecoveryDetach.ProfileBeforeCompletion)]
    [InlineData(RecoveryDetach.Dispose)]
    public async Task Planning_completion_cannot_retarget_another_opening_or_profile(RecoveryDetach detach) {
        using var fixture = CreatePlanningPanel();
        var cut = fixture.Cut;
        await cut.Find("[data-testid='agents-chat-planning-open']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='agents-chat-planning-cancel']").ClickAsync(new MouseEventArgs());
        var operation = CaptureSubmittedOperation(cut.Instance);
        try {
            if (detach == RecoveryDetach.SwitchThread) {
                var other = CreateSession(fixture.Agent.Id);
                fixture.Reads.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, other));
                await cut.InvokeAsync(() => cut.Render(parameters => parameters
                    .Add(component => component.PreferredAgentId, fixture.Agent.Id)
                    .Add(component => component.PreferredSessionId, other.Id)));
                cut.Find("[data-testid='chat-prompt-input']").Input("Other thread draft");
            } else if (detach == RecoveryDetach.ProfileBeforeCompletion) {
                fixture.Generation.Value = new(2);
                cut.Find("[data-testid='chat-prompt-input']").Input("Current profile draft");
            } else {
                await cut.Instance.DisposeAsync();
            }
            var reads = fixture.Reads.WorkspaceRequestCount;
            fixture.Writer.PlanningCompletion.SetResult(fixture.Detail);
            await operation.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(reads, fixture.Reads.WorkspaceRequestCount);
            Assert.Single(fixture.Writer.PlanningRequests);
            if (detach != RecoveryDetach.Dispose) {
                Assert.Equal(detach == RecoveryDetach.SwitchThread ? "Other thread draft" : "Current profile draft",
                    cut.FindComponent<ChatWorkspacePanel>().Instance.DraftPrompt);
            }
        } finally {
            fixture.Writer.PlanningCompletion.TrySetCanceled();
            await operation.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    internal static ExecutionRunDetail CreatePlanningDetail(AgentDefinition agent, ChatSessionRecord session) {
        var run = CreateWaitingApprovalRun(agent.Id, session.Id) with { Revision = 7 };
        var admission = new AgentToolSessionAdmission(new(run.Id, session.Id, AgentExecutionAuthorityId.Create()), agent.Id,
            AgentRuntimeContextPurpose.InteractiveChat, new(Guid.NewGuid(), "planning-ui-fixture", new(0)));
        var journal = AgentPlanningDecisionTestData.AwaitApproval(AgentPlanningDecisionTestData.Respond(
            AgentPlanningDecisionTestData.Enter(AgentPlanningDecisionTestData.Attach(
                AgentPlanningDecisionTestData.Begin(AgentPlanningDecisionTestData.Create(admission))))), TimeSpan.FromSeconds(2));
        var decision = journal.PlanningDecision!;
        var binding = decision.Current.Steps[^1].Publications.Single().Proposal;
        run = run with { ToolAdmission = journal, PendingApprovals = [run.PendingApprovals.Single() with { ToolAdmission = binding }] };
        var review = new AgentPlanningReview(AgentPlanningDecisionExpectation.From(run), session.Id, decision.Owner,
            decision.Policy, decision.Current.State, DateTimeOffset.UnixEpoch.AddSeconds(3), DateTimeOffset.UnixEpoch,
            TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(4), false, 0,
            [AgentPlanningAction.Approve, AgentPlanningAction.Refresh, AgentPlanningAction.Cancel],
            new(binding, run.PendingApprovals.Single().ApprovalId, "Original project: reviewed project\n1. <script>private example</script>\nDescription: Keep exact content."));
        return new(run, session, [], []) { PlanningReview = review };
    }

    internal static AgentPlanningActionRequest PlanningAction(AgentPlanningReview review, AgentPlanningAction action)
        => new(action, review.ChatSessionId, review.Expected,
            action == AgentPlanningAction.Approve ? review.Publication!.Binding : null,
            action == AgentPlanningAction.Approve ? review.Publication!.ApprovalId : null);

    private static PlanningPanelFixture CreatePlanningPanel(bool paused = false) {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var detail = CreatePlanningDetail(agent, session);
        if (paused) {
            detail = detail with {
                Run = detail.Run with { PendingApprovals = [] },
                PlanningReview = detail.PlanningReview! with {
                    State = AgentPlanningGenerationState.PausedBasisChanged,
                    AvailableActions = [AgentPlanningAction.Refresh, AgentPlanningAction.Cancel], Publication = null
                }
            };
        }
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var reads = (DeferredWorkspaceProxy)(object)service;
        reads.Service = service;
        reads.Agents = [agent];
        reads.InitialWorkspace = CreateWorkspace(agent.Id, session, detail.Run);
        reads.InitialRunDetail = detail;
        var orchestration = DispatchProxy.Create<IAgentChatExecutionOrchestrator, PlanningOrchestratorProxy>();
        var writer = (PlanningOrchestratorProxy)(object)orchestration;
        var context = CreateContext(service, orchestration);
        var generation = new RecoveryPanelGeneration();
        context.Services.AddSingleton<IAgentExecutionProfileGenerationSource>(generation);
        var cut = context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredSessionId, session.Id));
        cut.WaitForElement("[data-testid='agents-chat-planning-open']");
        return new(context, cut, reads, writer, generation, agent, session, detail);
    }

    private sealed record PlanningPanelFixture(BunitContext Context, IRenderedComponent<AgentChatPanel> Cut,
        DeferredWorkspaceProxy Reads, PlanningOrchestratorProxy Writer, RecoveryPanelGeneration Generation,
        AgentDefinition Agent, ChatSessionRecord Session, ExecutionRunDetail Detail) : IDisposable {
        public void Dispose() => Context.Dispose();
    }

    private class PlanningOrchestratorProxy : CompletedRunOrchestratorProxy {
        public List<(Guid AgentId, AgentPlanningActionRequest Request)> PlanningRequests { get; } = [];
        public TaskCompletionSource PlanningStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ExecutionRunDetail> PlanningCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name != nameof(IAgentChatExecutionOrchestrator.StartPlanningAction)) {
                return base.Invoke(targetMethod, args);
            }
            PlanningRequests.Add(((Guid)args![0]!, (AgentPlanningActionRequest)args[1]!));
            PlanningStarted.TrySetResult();
            return (CreateActivityStreamId(), PlanningCompletion.Task);
        }
    }
}
