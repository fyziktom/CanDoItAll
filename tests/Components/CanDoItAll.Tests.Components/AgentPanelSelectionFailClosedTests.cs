using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AgentPanelSelectionFailClosedTests
{
    [Fact]
    public void Chat_panel_clears_a_valid_selection_when_the_requested_agent_becomes_missing()
    {
        var availableAgent = CreateAgent("Available agent");
        var missingAgentId = Guid.NewGuid();
        var workspace = CreateWorkspace([availableAgent]);
        using var context = CreateChatTestContext(workspace);
        var selectedAgents = new List<AgentDefinition?>();
        var accessStates = new List<AgentChatContextAccessState>();

        var cut = context.RenderComponent<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, availableAgent.Id)
            .Add(component => component.SelectedAgentChanged,
                EventCallback.Factory.Create<AgentDefinition?>(this, selectedAgents.Add))
            .Add(component => component.ContextAccessStateChanged,
                EventCallback.Factory.Create<AgentChatContextAccessState>(this, accessStates.Add)));

        cut.WaitForAssertion(() =>
        {
            Assert.Same(availableAgent, selectedAgents.Last());
            Assert.Equal(AgentChatContextAccessState.Ready, accessStates.Last());
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.PreferredAgentId, missingAgentId));

        cut.WaitForAssertion(() =>
        {
            Assert.Null(selectedAgents.Last());
            Assert.Equal(AgentChatContextAccessState.Failed, accessStates.Last());
            Assert.DoesNotContain(selectedAgents, agent => agent?.Id == missingAgentId);
            Assert.Equal([availableAgent.Id], workspace.RequestedChatWorkspaceAgentIds);
        });
    }

    [Fact]
    public void Capabilities_panel_fails_closed_when_the_initial_requested_agent_is_missing()
    {
        var availableAgent = CreateAgent("Available agent");
        var missingAgentId = Guid.NewGuid();
        var workspace = CreateWorkspace([availableAgent]);
        using var context = CreateCapabilitiesTestContext(workspace);
        var selectedAgents = new List<AgentDefinition?>();
        var accessStates = new List<AgentChatContextAccessState>();

        var cut = context.RenderComponent<AgentCapabilitiesPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, missingAgentId)
            .Add(component => component.SelectedAgentChanged,
                EventCallback.Factory.Create<AgentDefinition?>(this, selectedAgents.Add))
            .Add(component => component.ContextAccessStateChanged,
                EventCallback.Factory.Create<AgentChatContextAccessState>(this, accessStates.Add)));

        cut.WaitForAssertion(() =>
        {
            Assert.Null(Assert.Single(selectedAgents));
            Assert.Equal(AgentChatContextAccessState.Failed, accessStates.Last());
            Assert.Empty(workspace.RequestedAgentEditorIds);
        });
    }

    [Fact]
    public void Capabilities_panel_clears_a_valid_selection_when_the_requested_agent_becomes_missing()
    {
        var availableAgent = CreateAgent("Available agent");
        var missingAgentId = Guid.NewGuid();
        var workspace = CreateWorkspace([availableAgent]);
        using var context = CreateCapabilitiesTestContext(workspace);
        var selectedAgents = new List<AgentDefinition?>();
        var accessStates = new List<AgentChatContextAccessState>();

        var cut = context.RenderComponent<AgentCapabilitiesPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, availableAgent.Id)
            .Add(component => component.SelectedAgentChanged,
                EventCallback.Factory.Create<AgentDefinition?>(this, selectedAgents.Add))
            .Add(component => component.ContextAccessStateChanged,
                EventCallback.Factory.Create<AgentChatContextAccessState>(this, accessStates.Add)));

        cut.WaitForAssertion(() =>
        {
            Assert.Same(availableAgent, selectedAgents.Last());
            Assert.Equal(AgentChatContextAccessState.Ready, accessStates.Last());
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.PreferredAgentId, missingAgentId));

        cut.WaitForAssertion(() =>
        {
            Assert.Null(selectedAgents.Last());
            Assert.Equal(AgentChatContextAccessState.Failed, accessStates.Last());
            Assert.DoesNotContain(selectedAgents, agent => agent?.Id == missingAgentId);
            Assert.Equal([availableAgent.Id], workspace.RequestedAgentEditorIds);
        });
    }

    private static TestContext CreateChatTestContext(WorkspaceServiceProxy workspace)
    {
        var context = CreateBaseTestContext(workspace.Service);
        context.Services.AddSingleton(
            DispatchProxy.Create<IAgentVoiceService, UnexpectedCallProxy>());
        context.Services.AddSingleton(
            DispatchProxy.Create<IAgentChatAttachmentStagingService, UnexpectedCallProxy>());
        context.Services.AddSingleton(
            DispatchProxy.Create<IFloatingAgentChatCoordinator, UnexpectedCallProxy>());
        context.Services.AddSingleton(
            DispatchProxy.Create<IAgentChatExecutionOrchestrator, UnexpectedCallProxy>());
        return context;
    }

    private static TestContext CreateCapabilitiesTestContext(WorkspaceServiceProxy workspace)
    {
        var context = CreateBaseTestContext(workspace.Service);
        context.Services.AddSingleton(
            DispatchProxy.Create<IAgentCapabilitySetupFlowService, UnexpectedCallProxy>());
        return context;
    }

    private static TestContext CreateBaseTestContext(IAgentFrameworkWorkspaceService workspace)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(workspace);
        return context;
    }

    private static WorkspaceServiceProxy CreateWorkspace(IReadOnlyList<AgentDefinition> agents)
    {
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        var proxy = (WorkspaceServiceProxy)(object)service;
        proxy.Service = service;
        proxy.Agents = agents;
        return proxy;
    }

    private static AgentDefinition CreateAgent(string name)
    {
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: name,
            RoleTitle: "Test agent",
            Summary: "Tests selection behavior.",
            Instructions: "Stay scoped.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-test",
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: true,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private class WorkspaceServiceProxy : DispatchProxy
    {
        public IAgentFrameworkWorkspaceService Service { get; set; } = default!;

        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        public List<Guid> RequestedChatWorkspaceAgentIds { get; } = [];

        public List<Guid> RequestedAgentEditorIds { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult(Agents),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) =>
                    Task.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]),
                nameof(IAgentFrameworkWorkspaceService.GetAgentEditorAsync) =>
                    GetAgentEditor(Assert.IsType<Guid>(args![0])),
                nameof(IAgentFrameworkWorkspaceService.GetChatAgentWorkspaceAsync) =>
                    GetChatWorkspace(Assert.IsType<Guid>(args![0])),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
        }

        private Task<AgentEditorModel> GetAgentEditor(Guid agentId)
        {
            RequestedAgentEditorIds.Add(agentId);
            var agent = Agents.Single(item => item.Id == agentId);
            return Task.FromResult(AgentEditorModel.FromDefinition(agent));
        }

        private Task<ChatAgentWorkspaceSnapshot> GetChatWorkspace(Guid agentId)
        {
            RequestedChatWorkspaceAgentIds.Add(agentId);
            return Task.FromResult(new ChatAgentWorkspaceSnapshot(
                agentId,
                Sessions: [],
                SelectedSession: null,
                SelectedSessionId: null,
                LatestRun: null));
        }
    }

    private class UnexpectedCallProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(
                $"Service member '{targetMethod?.Name}' was not expected in this component test.");
    }
}
