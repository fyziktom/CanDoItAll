using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed partial class AgentChatPanelResponsivenessTests {
    [Fact]
    public async Task Floating_new_thread_focuses_the_created_chat_window() {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.Agents = [agent];
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session);
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var identity = new AgentChatIdentity(agent.Id, agent.Name, agent.RoleTitle, agent.AvatarImageUrl);
        var original = registry.Open(identity, session.Id, FloatingAgentChatSettings.Default);
        var created = registry.Open(identity, Guid.NewGuid(), FloatingAgentChatSettings.Default);
        var coordinator = DispatchProxy.Create<IFloatingAgentChatCoordinator, NewThreadCoordinatorProxy>();
        var calls = (NewThreadCoordinatorProxy)(object)coordinator;
        calls.Created = created;
        var shell = new ConversationShellCoordinator();
        shell.FocusWindow(AgentConversationShellContributor.SourceIdentifier,
            AgentConversationShellContributor.BuildWindowId(original.HandleId));
        using var context = CreateContext(workspaceService,
            DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>());
        context.Services.AddSingleton(coordinator);
        context.Services.AddSingleton<IAgentChatLauncher>(new AgentChatLauncherCompatibilityFacade(coordinator, shell));
        var cut = context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredAgent, agent)
            .Add(component => component.PreferredSessionId, session.Id)
            .Add(component => component.ActiveChatHandleId, original.HandleId)
            .Add(component => component.DisplayMode, AgentChatPanelDisplayMode.FocusedFloating));

        await cut.WaitForElement("[data-testid='agents-chat-focused-new-thread']").ClickAsync(new MouseEventArgs());

        Assert.Equal(agent.Id, Assert.Single(calls.CreatedAgentIds));
        Assert.Equal(new ConversationShellWindowKey(AgentConversationShellContributor.SourceIdentifier,
            AgentConversationShellContributor.BuildWindowId(created.HandleId)), shell.Snapshot().FocusedWindow);
    }

    private class NewThreadCoordinatorProxy : DispatchProxy {
        public ActiveAgentChat Created { get; set; } = default!;
        public List<Guid> CreatedAgentIds { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(IFloatingAgentChatCoordinator.StartNewChatAsync)) {
                CreatedAgentIds.Add((Guid)args![0]!);
                return Task.FromResult(Created);
            }
            if (targetMethod?.Name == nameof(IFloatingAgentChatCoordinator.SetRunState)) {
                return Created;
            }
            throw new InvalidOperationException($"Unexpected coordinator call: {targetMethod?.Name}.");
        }
    }
}
