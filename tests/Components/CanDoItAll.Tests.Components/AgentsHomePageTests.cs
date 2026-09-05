using AngleSharp.Dom;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
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
        cut.WaitForDashboardLoaded();

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
        cut.WaitForDashboardLoaded();
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
        cut.WaitForDashboardLoaded();
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

    [Fact]
    public async Task Simple_chats_follows_agents_and_renders_both_nested_workspaces()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<ILlmChatUiAuthorizationFacade>(new AllowSimpleChatsAuthorization());
            services.AddSimpleChatsComponents();
            services.AddScoped<ObservedConversationGateway>(provider => new(
                ActivatorUtilities.CreateInstance<LlmChatConversationUiGateway>(provider)));
            services.AddScoped<ILlmChatConversationUiGateway>(provider => provider.GetRequiredService<ObservedConversationGateway>());
        });
        var conversations = harness.Context.Services.GetRequiredService<ObservedConversationGateway>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents");

        var cut = harness.Context.Render<AgentsHomePage>();
        cut.WaitForDashboardLoaded();
        cut.WaitForElement("[data-testid='agents-shell-tabs']", TimeSpan.FromSeconds(10));
        var tabs = cut.FindAll("[data-testid='agents-shell-tabs'] button")
            .Select(tab => tab.TextContent.Trim())
            .ToArray();
        var agentsIndex = Array.FindIndex(tabs, label => label.StartsWith("Agents", StringComparison.Ordinal));
        var simpleChatsIndex = Array.FindIndex(tabs, label => label.StartsWith("Simple Chats", StringComparison.Ordinal));

        Assert.Equal(agentsIndex + 1, simpleChatsIndex);
        await FindTab(cut, "Simple Chats").ClickAsync();

        cut.WaitForElement("[data-testid='llm-chats-tabs']", TimeSpan.FromSeconds(10));
        cut.WaitForElement("[data-testid='llm-chat-definition-catalog']", TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => Assert.Matches("^[0-9]+ definitions?$",
            cut.FindComponent<LlmChatDefinitionCatalogPanel>()
                .FindComponent<FilterBar>().Instance.ResultText ?? string.Empty),
            TimeSpan.FromSeconds(10));
        Assert.Contains("tab=simple-chats", navigation.Uri, StringComparison.Ordinal);
        var workspaceTabs = cut.FindAll("[data-testid='llm-chats-tabs'] [role='tab']");
        Assert.Collection(
            workspaceTabs,
            tab => Assert.EndsWith("Definitions", tab.TextContent.Trim(), StringComparison.Ordinal),
            tab => Assert.EndsWith("Conversations", tab.TextContent.Trim(), StringComparison.Ordinal));
        Assert.Equal(
            "true",
            cut.Find("[data-testid='llm-chats-tab-definitions']").GetAttribute("aria-selected"));

        await cut.Find("[data-testid='llm-chats-tab-conversations']").ClickAsync();
        var loadedConversations = await conversations.Listed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(loadedConversations.IsSuccess);
        Assert.Empty(loadedConversations.Value!.Items);
        cut.WaitForElement("[data-testid='llm-chat-conversation-workspace']", TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => Assert.DoesNotContain(
            "Loading conversations...",
            cut.Find("[data-testid='llm-chat-conversation-workspace']").TextContent,
            StringComparison.Ordinal), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Usage_scope_defaults_to_both_and_is_forwarded_to_detail_dialogs()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents");
        var dialogHost = harness.Context.Render<DialogHost>();
        var cut = harness.Context.Render<AgentsHomePage>();
        cut.WaitForDashboardLoaded();

        var scope = cut.WaitForElement("[data-testid='agents-overview-usage-scope']", TimeSpan.FromSeconds(10));
        var scopeTabs = cut.FindComponents<SecondaryTabs>()
            .Single(component => component.Instance.Items.Any(item => item.Label == "Both"));
        Assert.Equal(nameof(ProviderUsageWorkloadSelection.Both), scopeTabs.Instance.SelectedKey);

        var chats = scope.QuerySelectorAll("button")
            .Single(button => button.TextContent.Trim().StartsWith("Chats", StringComparison.Ordinal));
        chats.Click();
        cut.WaitForAssertion(
            () => Assert.Equal(
                nameof(ProviderUsageWorkloadSelection.SimpleChats),
                cut.FindComponents<SecondaryTabs>()
                    .Single(component => component.Instance.Items.Any(item => item.Label == "Both"))
                    .Instance.SelectedKey),
            TimeSpan.FromSeconds(10));
        Assert.Contains("usageScope=simple-chats", navigation.Uri, StringComparison.Ordinal);

        cut.Find("[data-testid='agents-overview-open-provider-usage']").Click();
        var dialog = dialogHost.WaitForComponent<ProviderUsageDialog>(TimeSpan.FromSeconds(10));
        Assert.Equal(ProviderUsageWorkloadSelection.SimpleChats, dialog.Instance.Selection);
    }

    [Fact]
    public async Task Simple_chat_inner_view_is_restored_from_the_typed_query_state()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<ILlmChatUiAuthorizationFacade>(new AllowSimpleChatsAuthorization());
            services.AddSimpleChatsComponents();
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents?tab=simple-chats&simpleChatView=definitions");

        var cut = harness.Context.Render<AgentsHomePage>();
        cut.WaitForDashboardLoaded();

        cut.WaitForElement("[data-testid='llm-chat-definition-catalog']", TimeSpan.FromSeconds(10));
        var definitionsTab = cut.Find("[data-testid='llm-chats-tab-definitions']");
        Assert.Equal("true", definitionsTab.GetAttribute("aria-selected"));
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

    private sealed class ObservedConversationGateway(LlmChatConversationUiGateway inner) : ILlmChatConversationUiGateway {
        public TaskCompletionSource<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>> Listed { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>> ListPageAsync(
            LlmChatConversationQuery query, CancellationToken cancellationToken = default) {
            try {
                var result = await inner.ListPageAsync(query, cancellationToken);
                Listed.TrySetResult(result);
                return result;
            } catch (Exception exception) {
                Listed.TrySetException(exception);
                throw;
            }
        }

        public Task<LlmChatUiResult<LlmChatConversationView>> GetAsync(Guid conversationId,
            LlmChatTranscriptQuery query, CancellationToken cancellationToken = default)
            => inner.GetAsync(conversationId, query, cancellationToken);

        public Task<LlmChatUiResult<LlmChatConversationView>> CreateAsync(Guid definitionId, string title,
            CancellationToken cancellationToken = default)
            => inner.CreateAsync(definitionId, title, cancellationToken);

        public Task<LlmChatUiResult<LlmChatConversationView>> RenameAsync(Guid conversationId, string title,
            long expectedConcurrencyToken, long expectedTranscriptRevision, CancellationToken cancellationToken = default)
            => inner.RenameAsync(conversationId, title, expectedConcurrencyToken, expectedTranscriptRevision, cancellationToken);

        public Task<LlmChatUiResult<LlmChatConversationView>> ArchiveAsync(Guid conversationId,
            long expectedConcurrencyToken, CancellationToken cancellationToken = default)
            => inner.ArchiveAsync(conversationId, expectedConcurrencyToken, cancellationToken);
    }

    private sealed class AllowSimpleChatsAuthorization : ILlmChatUiAuthorizationFacade
    {
        public ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new LlmChatUiAuthorizationSnapshot(true, true, true));

        public ValueTask<bool> IsAllowedAsync(
            LlmChatUiPermission permission,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);
    }
}
