using AngleSharp.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentsHomePageTests
{
    [Fact]
    public async Task Obsolete_scenarios_route_falls_back_to_overview_without_rendering_a_tab()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents?tab=scenarios");
        var cut = harness.Context.Render<AgentsHomePage>();

        cut.WaitForElement(
            "[data-testid='agents-overview-dashboard']",
            TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain(
                "...",
                cut.Find("[data-testid='agents-overview-metric-agents']").TextContent,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Teams are loading",
                cut.Markup,
                StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(
            cut.FindAll("[data-testid='agents-shell-tabs'] button"),
            tab => tab.TextContent.Trim().StartsWith("Scenarios", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Load_defaults_action_is_in_the_tabs_row_and_requires_confirmation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var dialogHost = harness.Context.Render<DialogHost>();

        navigation.NavigateTo("/agents");
        var cut = harness.Context.Render<AgentsHomePage>();
        var tabsRow = cut.WaitForElement(
            "[data-testid='agents-shell-tabs']",
            TimeSpan.FromSeconds(10));
        var loadDefaultsButton = Assert.IsAssignableFrom<IElement>(
            tabsRow.QuerySelector("[data-testid='agents-shell-feed-defaults']"));

        var clickTask = loadDefaultsButton.ClickAsync(new MouseEventArgs());

        dialogHost.WaitForElement(
            "[data-testid='agents-feed-defaults-confirmation']",
            TimeSpan.FromSeconds(10));
        Assert.False(clickTask.IsCompleted);

        dialogHost.Find("[data-testid='agents-feed-defaults-cancel']").Click();
        await clickTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(harness.Context.Services.GetRequiredService<DialogService>().Dialogs);
        Assert.False(cut.Find("[data-testid='agents-shell-feed-defaults']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Hr_agent_avatar_action_remains_in_the_page_header_across_module_tabs()
    {
        var launcher = new RecordingAgentChatLauncher();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IAgentChatLauncher>();
            services.AddSingleton<IAgentChatLauncher>(launcher);
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents");
        var cut = harness.Context.Render<AgentsHomePage>();
        var openButton = cut.WaitForElement(
            "[data-testid='agents-hr-agent-open-header']",
            TimeSpan.FromSeconds(10));

        cut.WaitForAssertion(() => Assert.False(
            cut.Find("[data-testid='agents-hr-agent-open-header']").HasAttribute("disabled")));
        Assert.Equal("Open HR Agent", openButton.GetAttribute("aria-label"));
        Assert.True(string.IsNullOrWhiteSpace(openButton.TextContent));
        Assert.EndsWith(
            "/avatar-07.jpg",
            Assert.IsAssignableFrom<IElement>(openButton.QuerySelector("img")).GetAttribute("src"),
            StringComparison.Ordinal);

        var avatarAction = Assert.Single(cut.FindComponents<AgentAvatarActionButton>());
        Assert.Equal("Open HR Agent", avatarAction.Instance.Label);
        var tooltipTarget = Assert.Single(avatarAction.FindComponents<TooltipTarget>());
        Assert.Equal(TooltipPosition.Bottom, tooltipTarget.Instance.Position);
        Assert.Equal("agents-hr-agent-tooltip", tooltipTarget.Instance.TestId);

        FindTab(cut, "Providers").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("tab=providers", navigation.Uri, StringComparison.Ordinal);
            Assert.Single(cut.FindAll("[data-testid='agents-hr-agent-open-header']"));
        });

        cut.Find("[data-testid='agents-hr-agent-open-header']").Click();
        cut.WaitForAssertion(() => Assert.Equal(HrAgentIdentity.AgentId, launcher.StartedAgentId));

        FindTab(cut, "Agents").Click();
        cut.WaitForElement("[data-testid='agents-catalog-workspace']", TimeSpan.FromSeconds(10));
        Assert.Single(cut.FindAll("[data-testid='agents-hr-agent-open-header']"));
        Assert.Empty(cut.FindAll("[data-testid='agents-hr-agent-open-top']"));
        Assert.Empty(cut.FindAll("[data-testid='agents-prompts-curator-open-top']"));
        Assert.Empty(cut.FindAll("[data-testid='agents-workflow-curator-open-top']"));
    }

    private static IElement FindTab(IRenderedComponent<IComponent> cut, string label)
        => cut.FindAll("[data-testid='agents-shell-tabs'] button")
            .Single(tab => tab.TextContent.Trim().StartsWith(label, StringComparison.Ordinal));

    private sealed class RecordingAgentChatLauncher : IAgentChatLauncher
    {
        public Guid? StartedAgentId { get; private set; }

        public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents)
        {
        }

        public Task<ActiveAgentChat> StartNewChatAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartedAgentId = agentId;
            return Task.FromResult(CreateActiveChat(agentId, chatSessionId: null));
        }

        public Task<ActiveAgentChat> OpenChatAsync(
            Guid agentId,
            Guid chatSessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateActiveChat(agentId, chatSessionId));
        }

        private static ActiveAgentChat CreateActiveChat(Guid agentId, Guid? chatSessionId)
        {
            var now = DateTimeOffset.UtcNow;
            return new ActiveAgentChat(
                AgentChatHandleId.Create(),
                new AgentChatIdentity(agentId, HrAgentIdentity.DefaultDisplayName, "HR specialist", HrAgentIdentity.DefaultAvatarImageUrl),
                chatSessionId,
                ActiveAgentChatVisibility.Visible,
                ActiveAgentChatRunState.Idle,
                now,
                now,
                HiddenAtUtc: null);
        }
    }
}
