using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCompactListTests
{
    [Fact]
    public void List_renders_dense_rows_with_icon_only_actions_and_selection_state()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var selectedAgent = CreateAgent("Selected agent");
        var busyAgent = CreateAgent("Busy agent");

        var cut = context.Render<AgentCompactList>(parameters => parameters
            .Add(component => component.Agents, [selectedAgent, busyAgent])
            .Add(component => component.SelectedAgentId, selectedAgent.Id)
            .Add(component => component.BusyAgentIds, new HashSet<Guid> { busyAgent.Id })
            .Add(component => component.TestId, "test-agent-list")
            .Add(component => component.NewChatRequested, _ => { })
            .Add(component => component.HistoryRequested, _ => { }));

        Assert.Equal(2, cut.FindAll(".agent-compact-list-item").Count);
        var selectedRow = cut.Find($"[data-testid='test-agent-list-item-{selectedAgent.Id:N}']");
        Assert.Contains("agent-compact-list-item--selected", selectedRow.ClassList);
        Assert.Equal(
            "true",
            cut.Find($"[data-testid='test-agent-list-select-{selectedAgent.Id:N}']")
                .GetAttribute("aria-pressed"));

        var newChat = cut.Find($"[data-testid='test-agent-list-new-chat-{selectedAgent.Id:N}']");
        var history = cut.Find($"[data-testid='test-agent-list-history-{selectedAgent.Id:N}']");
        Assert.Equal("Start a new chat with Selected agent", newChat.GetAttribute("aria-label"));
        Assert.Equal("Open chat history for Selected agent", history.GetAttribute("aria-label"));
        Assert.Null(newChat.QuerySelector(".rz-button-text"));
        Assert.Null(history.QuerySelector(".rz-button-text"));
        Assert.Equal("add_comment", newChat.QuerySelector(".cda-material-icon")?.TextContent.Trim());
        Assert.Equal("history", history.QuerySelector(".cda-material-icon")?.TextContent.Trim());

        var selectionButton = cut.Find($"[data-testid='test-agent-list-select-{selectedAgent.Id:N}']");
        Assert.Null(selectionButton.QuerySelector("[data-testid^='test-agent-list-new-chat-']"));
        Assert.Null(selectionButton.QuerySelector("[data-testid^='test-agent-list-history-']"));

        var busyNewChat = cut.Find($"[data-testid='test-agent-list-new-chat-{busyAgent.Id:N}']");
        Assert.True(busyNewChat.HasAttribute("disabled"));
    }

    [Fact]
    public void List_routes_typed_agent_callbacks()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var agent = CreateAgent("Callback agent");
        AgentDefinition? selected = null;
        AgentDefinition? opened = null;
        AgentDefinition? history = null;

        var cut = context.Render<AgentCompactList>(parameters => parameters
            .Add(component => component.Agents, [agent])
            .Add(component => component.TestId, "callback-agent-list")
            .Add(component => component.AgentSelected, value => selected = value)
            .Add(component => component.NewChatRequested, value => opened = value)
            .Add(component => component.HistoryRequested, value => history = value));

        cut.Find($"[data-testid='callback-agent-list-select-{agent.Id:N}']").Click();
        cut.Find($"[data-testid='callback-agent-list-new-chat-{agent.Id:N}']").Click();
        cut.Find($"[data-testid='callback-agent-list-history-{agent.Id:N}']").Click();

        Assert.Same(agent, selected);
        Assert.Same(agent, opened);
        Assert.Same(agent, history);
    }

    private static AgentDefinition CreateAgent(string name)
    {
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: name,
            RoleTitle: ".NET specialist",
            Summary: "Builds maintainable applications.",
            Instructions: "Work carefully.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-test",
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: true,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["blazor", "dotnet"],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }
}
