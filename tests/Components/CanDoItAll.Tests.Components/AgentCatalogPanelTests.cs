using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
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
    public async Task Catalog_opens_focused_floating_chat_only_for_the_stable_hr_agent()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.ComponentFactories.AddStub<AgentChatPanel>();
        var hrAgent = CreateAgent(HrAgentIdentity.AgentId, "HR Agent", HrAgentIdentity.TemplateKey);
        var spoofedAgent = CreateAgent(Guid.NewGuid(), "Spoofed HR Agent", HrAgentIdentity.TemplateKey);

        var cut = harness.Context.RenderComponent<AgentCatalogPanel>(parameters => parameters
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

        cut.WaitForElement("[data-testid='agents-hr-agent-chat-content']");
        var viewportHost = cut.Find("[data-testid='agents-hr-agent-viewport']");
        Assert.Equal("true", viewportHost.GetAttribute("data-cda-overlay-container"));
        Assert.NotNull(viewportHost.QuerySelector("[data-testid='agents-hr-agent-window']"));
        var focusedChat = cut.FindComponent<Stub<AgentChatPanel>>();
        Assert.Equal(hrAgent.Id, focusedChat.Instance.Parameters.Get(component => component.PreferredAgentId));
        Assert.Equal(
            AgentChatPanelDisplayMode.FocusedFloating,
            focusedChat.Instance.Parameters.Get(component => component.DisplayMode));
    }

    [Fact]
    public async Task Catalog_toolbar_uses_icon_reset_tooltip_and_single_line_team_header()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var hrAgent = CreateAgent(HrAgentIdentity.AgentId, "HR Agent", HrAgentIdentity.TemplateKey);

        var cut = harness.Context.RenderComponent<AgentCatalogPanel>(parameters => parameters
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

        var tooltip = harness.Context.Services.GetRequiredService<TooltipService>().Current;
        Assert.Equal("Reset agent search", tooltip?.Text);
        Assert.Equal(TooltipPosition.Bottom, tooltip?.Options.Position);
        Assert.Equal("agents-catalog-reset-tooltip", tooltip?.Options.TestId);

        var teamHeader = cut.Find("[data-testid='agents-team-header']");
        Assert.Contains("flex-row", teamHeader.ClassList);
        Assert.DoesNotContain("flex-col", teamHeader.ClassList);
        var teamHeaderContent = Assert.IsAssignableFrom<AngleSharp.Dom.IElement>(teamHeader.FirstElementChild);
        Assert.Contains("flex-nowrap", teamHeaderContent.ClassList);
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
}
