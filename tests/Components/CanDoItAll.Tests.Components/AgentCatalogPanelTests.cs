using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AgentCatalogPanelTests
{
    [Fact]
    public void Agent_selection_card_renders_actions_outside_the_selection_button()
    {
        using var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        RenderFragment actions = builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "data-testid", "agent-card-action");
            builder.AddContent(2, "Open chat");
            builder.CloseElement();
        };

        var cut = context.RenderComponent<AgentSelectionCard>(parameters => parameters
            .Add(component => component.Agent, CreateAgent(Guid.NewGuid(), "Agent", string.Empty))
            .Add(component => component.Actions, actions));

        Assert.Empty(cut.Find(".agent-selection-card__select").QuerySelectorAll("[data-testid='agent-card-action']"));
        Assert.NotNull(cut.Find(".agent-selection-card__actions [data-testid='agent-card-action']"));
    }

    [Fact]
    public void Catalog_delegates_new_chat_to_global_launcher_only_for_the_stable_hr_agent()
    {
        var hrAgent = CreateAgent(HrAgentIdentity.AgentId, "HR Agent", HrAgentIdentity.TemplateKey);
        var spoofedAgent = CreateAgent(Guid.NewGuid(), "Spoofed HR Agent", HrAgentIdentity.TemplateKey);
        var launcher = new RecordingAgentChatLauncher(hrAgent);
        using var context = CreateCatalogTestContext(launcher);

        var cut = context.RenderComponent<AgentCatalogPanel>(parameters => parameters
            .Add(component => component.InitialAgents, new[] { spoofedAgent, hrAgent })
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.InitialTeams, Array.Empty<AgentTeamDefinition>())
            .Add(component => component.SkipCatalogRepair, true));

        var openButtons = cut.WaitForElements("[data-testid='agents-hr-agent-open']");
        var openButton = Assert.Single(openButtons);
        var agentCard = openButton.Closest("[data-testid='agents-catalog-card-shell']");
        Assert.NotNull(agentCard);
        Assert.Equal("HR Agent", agentCard.QuerySelector(".agent-selection-card__name")?.TextContent);

        var topOpenButton = cut.Find("[data-testid='agents-hr-agent-open-top']");
        Assert.Contains("HR Agent", topOpenButton.TextContent, StringComparison.Ordinal);

        topOpenButton.Click();

        cut.WaitForAssertion(() => Assert.Equal(hrAgent.Id, launcher.StartedAgentId));
        Assert.Empty(cut.FindAll("[data-testid='agents-hr-agent-viewport']"));
    }

    [Fact]
    public void Catalog_toolbar_uses_icon_reset_tooltip_and_single_line_team_header()
    {
        var hrAgent = CreateAgent(HrAgentIdentity.AgentId, "HR Agent", HrAgentIdentity.TemplateKey);
        using var context = CreateCatalogTestContext(new RecordingAgentChatLauncher(hrAgent));

        var cut = context.RenderComponent<AgentCatalogPanel>(parameters => parameters
            .Add(component => component.InitialAgents, new[] { hrAgent })
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.InitialTeams, Array.Empty<AgentTeamDefinition>())
            .Add(component => component.SkipCatalogRepair, true));

        var resetButton = cut.Find("[data-testid='agents-catalog-reset']");
        Assert.Equal("Reset agent search", resetButton.GetAttribute("aria-label"));
        Assert.Equal("restart_alt", resetButton.QuerySelector(".material-icons")?.TextContent.Trim());
        Assert.Null(resetButton.QuerySelector(".rz-button-text"));

        var resetTarget = Assert.IsAssignableFrom<AngleSharp.Dom.IElement>(resetButton.ParentElement);
        resetTarget.TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 120, ClientY = 80 });

        var tooltip = context.Services.GetRequiredService<TooltipService>().Current;
        Assert.Equal("Reset agent search", tooltip?.Text);
        Assert.Equal(TooltipPosition.Bottom, tooltip?.Options.Position);
        Assert.Equal("agents-catalog-reset-tooltip", tooltip?.Options.TestId);

        var teamHeader = cut.Find("[data-testid='agents-team-header']");
        Assert.Contains("flex-row", teamHeader.ClassList);
        Assert.DoesNotContain("flex-col", teamHeader.ClassList);
        var teamHeaderContent = Assert.IsAssignableFrom<AngleSharp.Dom.IElement>(teamHeader.FirstElementChild);
        Assert.Contains("flex-nowrap", teamHeaderContent.ClassList);
    }

    [Fact]
    public void Catalog_reports_the_actual_selected_agent_to_its_page_owner()
    {
        var first = CreateAgent(Guid.NewGuid(), "First agent", string.Empty);
        var second = CreateAgent(Guid.NewGuid(), "Second agent", string.Empty);
        AgentDefinition? selected = null;
        using var context = CreateCatalogTestContext(new RecordingAgentChatLauncher(first));

        var cut = context.RenderComponent<AgentCatalogPanel>(parameters => parameters
            .Add(component => component.InitialAgents, new[] { first, second })
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.InitialTeams, Array.Empty<AgentTeamDefinition>())
            .Add(component => component.SkipCatalogRepair, true)
            .Add(component => component.SelectedAgentChanged,
                EventCallback.Factory.Create<AgentDefinition?>(this, value => selected = value)));

        var secondCard = cut.FindAll("[data-testid='agents-catalog-card-shell']")
            .Single(card => card.TextContent.Contains(second.Name, StringComparison.Ordinal));
        secondCard.QuerySelector("[data-testid='agents-catalog-card']")!.Click();

        cut.WaitForAssertion(() => Assert.Same(second, selected));
    }

    private static TestContext CreateCatalogTestContext(IAgentChatLauncher launcher)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(
            DispatchProxy.Create<IAgentFrameworkWorkspaceService, UnusedWorkspaceServiceProxy>());
        context.Services.AddSingleton<IAgentFrameworkOrganizationCatalogRepairService, UnusedOrganizationCatalogRepairService>();
        context.Services.AddSingleton(launcher);
        return context;
    }

    private static AgentDefinition CreateAgent(Guid id, string name, string templateKey)
    {
        return new AgentDefinition(
            Id: id,
            Name: name,
            RoleTitle: "Human resources specialist",
            Summary: "Handles scoped people operations.",
            Instructions: "Keep work scoped and explicit.",
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
            TemplateKey: templateKey,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private sealed class RecordingAgentChatLauncher(AgentDefinition agent) : IAgentChatLauncher
    {
        public Guid? StartedAgentId { get; private set; }

        public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents)
        {
        }

        public Task<ActiveAgentChat> StartNewChatAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
        {
            StartedAgentId = agentId;
            return Task.FromResult(CreateActiveChat(chatSessionId: null));
        }

        public Task<ActiveAgentChat> OpenChatAsync(
            Guid agentId,
            Guid chatSessionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateActiveChat(chatSessionId));

        private ActiveAgentChat CreateActiveChat(Guid? chatSessionId)
        {
            var now = DateTimeOffset.UtcNow;
            return new ActiveAgentChat(
                AgentChatHandleId.Create(),
                new AgentChatIdentity(agent.Id, agent.Name, agent.RoleTitle, agent.AvatarImageUrl),
                chatSessionId,
                ActiveAgentChatVisibility.Visible,
                ActiveAgentChatRunState.Idle,
                now,
                now,
                HiddenAtUtc: null);
        }
    }

    private sealed class UnusedOrganizationCatalogRepairService : IAgentFrameworkOrganizationCatalogRepairService
    {
        public Task EnsureCurrentOrganizationCatalogAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class UnusedWorkspaceServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(
                $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.");
    }
}
