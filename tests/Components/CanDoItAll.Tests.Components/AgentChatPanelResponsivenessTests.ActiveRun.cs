using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed partial class AgentChatPanelResponsivenessTests {
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
