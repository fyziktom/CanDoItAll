using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed partial class AgentChatPanelResponsivenessTests {
    [Fact]
    public async Task Floating_send_finishing_after_the_catalog_checkpoint_keeps_approval_and_cancellation_available() {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var waiting = CreateWaitingApprovalRun(agent.Id, session.Id);
        var (service, reads) = AgentChatSessionTests.CreateReads();
        reads.Workspace = (_, _, _) => Task.FromResult(CreateWorkspace(agent.Id, session));
        reads.Detail = (_, _) => Task.FromResult(new ExecutionRunDetail(waiting, session, [], []));
        var operations = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var writer = (CompletedRunOrchestratorProxy)(object)operations;
        writer.SendCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var chat = registry.Open(new(agent.Id, agent.Name, agent.RoleTitle, agent.AvatarImageUrl), session.Id, FloatingAgentChatSettings.Default);
        await using var coordinator = new FloatingAgentChatCoordinator(service, registry, new EmptyPreparationPool(),
            DispatchProxy.Create<IFloatingAgentChatSettingsService, UnexpectedCallProxy>(), TimeProvider.System, NullLogger<FloatingAgentChatCoordinator>.Instance);
        using var context = CreateContext(service, operations);
        context.Services.AddSingleton<IFloatingAgentChatCoordinator>(coordinator);
        var cut = context.Render<AgentChatPanel>(p => p
            .Add(x => x.DisplayMode, AgentChatPanelDisplayMode.FocusedFloating)
            .Add(x => x.PreferredAgent, agent).Add(x => x.PreferredAgentId, agent.Id)
            .Add(x => x.PreferredSessionId, session.Id).Add(x => x.ActiveChatHandleId, chat.HandleId));
        cut.Find("[data-testid='chat-prompt-input']").Input("Request a tool decision.");
        cut.Find("[data-testid='chat-send-button']").Click();
        await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try {
            var settled = registry.Restore(chat with {
                ActivityStreamId = writer.LastStreamId, ExecutionRunId = waiting.Id,
                RunState = ActiveAgentChatRunState.AwaitingApproval, ExecutionRevision = 2
            });
            await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.PersistedActivityStreamId, settled.ActivityStreamId)
                .Add(x => x.PersistedExecutionRunId, settled.ExecutionRunId).Add(x => x.PersistedActiveChatRunState, settled.RunState)));
            reads.Workspace = (_, _, _) => Task.FromResult(CreateWorkspace(agent.Id, session, waiting));
            writer.SendCompletion.SetResult(CreateRunResult(agent.Id, session.Id, waiting.Id) with { State = ExecutionState.WaitingOnTool });
            cut.WaitForAssertion(() => {
                Assert.False(cut.FindComponent<ChatWorkspacePanel>().Instance.IsBusy);
                Assert.Equal(ActiveAgentChatRunState.AwaitingApproval, Assert.Single(registry.Snapshot()).RunState);
                AssertDecisionButtonsEnabled(cut);
            });
        } finally {
            writer.SendCompletion.TrySetCanceled();
        }
    }

    [Fact]
    public async Task Approval_event_for_the_displayed_run_loads_decisions_without_reopening_the_floating_chat() {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var waiting = CreateWaitingApprovalRun(agent.Id, session.Id);
        var running = waiting with { State = ExecutionState.Running, PendingApprovals = [] };
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var reads = (DeferredWorkspaceProxy)(object)service;
        reads.Service = service;
        reads.InitialWorkspace = CreateWorkspace(agent.Id, session, running);
        reads.InitialRunDetail = new(running, session, [], []);
        using var context = CreateContext(service, DispatchProxy.Create<IAgentChatExecutionOrchestrator, UnexpectedCallProxy>());
        var cut = RenderFocusedChat(context, agent, session);
        Assert.True(cut.FindComponent<ChatWorkspacePanel>().Instance.IsBusy);
        var checkpoint = new ExecutionLogEntry(Guid.NewGuid(), agent.Id, session.Id, DateTimeOffset.UtcNow,
            ExecutionState.WaitingOnTool, AgentExecutionActivityRuntimeProgressPolicy.ApprovalPhase, "Tool approval requested.") { ExecutionRunId = waiting.Id };
        reads.InitialRunDetail = new(waiting, session, [checkpoint], []);
        reads.PostRunWorkspace.SetResult(CreateWorkspace(agent.Id, session, waiting));
        await cut.InvokeAsync(() => reads.RaiseExecutionUpdated(checkpoint));
        await cut.InvokeAsync(() => reads.RaiseExecutionUpdated(checkpoint));
        cut.WaitForAssertion(() => {
            AssertDecisionButtonsEnabled(cut);
            Assert.Equal(2, reads.WorkspaceRequestCount);
            Assert.Equal(waiting.Id, cut.FindComponent<ChatWorkspacePanel>().Instance.ActiveRun!.Id);
        });
    }

    private static void AssertDecisionButtonsEnabled(IRenderedComponent<AgentChatPanel> cut) {
        Assert.False(cut.Find("[data-testid='chat-approve-once-button']").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid='chat-reject-button']").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid='agents-chat-cancel-pending-run']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='chat-send-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task First_live_execution_loads_its_run_once_and_tool_activity_stays_busy() {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var run = CreateRunningRun(agent.Id, session.Id) with { State = ExecutionState.WaitingOnTool };
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session);
        var orchestrator = DispatchProxy.Create<IAgentChatExecutionOrchestrator, UnexpectedCallProxy>();
        using var context = CreateContext(workspaceService, orchestrator);
        var dialogs = context.Render<DialogHost>();
        var cut = RenderFocusedChat(context, agent, session);
        var started = new ExecutionLogEntry(Guid.NewGuid(), agent.Id, session.Id, DateTimeOffset.UtcNow,
            ExecutionState.WaitingOnTool, "Tool", "Reading the requested project.") { ExecutionRunId = run.Id };
        workspace.InitialRunDetail = new(run, session, [started], []);

        workspace.RaiseExecutionUpdated(started);
        workspace.RaiseExecutionUpdated(started);
        await workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, workspace.WorkspaceRequestCount);
        workspace.PostRunWorkspace.SetResult(CreateWorkspace(agent.Id, session, run));

        cut.WaitForAssertion(() => {
            var panel = cut.FindComponent<ChatWorkspacePanel>().Instance;
            Assert.Equal(run.Id, panel.ActiveRun!.Id);
            Assert.True(panel.IsBusy);
            Assert.Equal("WaitingOnTool", panel.RunStateText);
        });
        await cut.InvokeAsync(() => workspace.RaiseExecutionUpdated(started with {
            Id = Guid.NewGuid(), ExecutionRunId = Guid.NewGuid(),
            CreatedAtUtc = started.CreatedAtUtc.AddMinutes(-1), State = ExecutionState.Preparing
        }));
        Assert.Equal(2, workspace.WorkspaceRequestCount);
        Assert.Equal(run.Id, Assert.Single(workspace.ExecutionRunDetailRequests));
        Assert.Equal("WaitingOnTool", cut.FindComponent<ChatWorkspacePanel>().Instance.RunStateText);
        cut.Find("[data-testid='agents-chat-open-runtime-details']").Click();
        dialogs.WaitForAssertion(() => Assert.Equal(run.Id, dialogs.FindComponent<AgentRuntimeDetailsDialog>().Instance.Run!.Id));
    }
}
