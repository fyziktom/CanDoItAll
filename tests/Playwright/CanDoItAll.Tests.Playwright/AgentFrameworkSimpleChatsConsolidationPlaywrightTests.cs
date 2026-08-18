using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Flows;

[Collection(PlaywrightCollection.Name)]
public sealed class AgentFrameworkSimpleChatsConsolidationPlaywrightTests(
    PlaywrightAppFixture fixture)
{
    [Fact]
    public async Task SimpleChatsTabImmediatelyFollowsAgents()
    {
        await using var session = await OpenAgentsAsync("/agents");
        var tabs = session.Page.GetByTestId("agents-shell-tabs").Locator("button");
        var labels = (await tabs.AllTextContentsAsync())
            .Select(value => value.Trim())
            .ToArray();
        var agentsIndex = Array.FindIndex(labels, value => value.StartsWith("Agents", StringComparison.Ordinal));
        var chatsIndex = Array.FindIndex(labels, value => value.StartsWith("Simple Chats", StringComparison.Ordinal));

        Assert.True(agentsIndex >= 0, "The Agents tab was not rendered.");
        Assert.Equal(agentsIndex + 1, chatsIndex);
        session.AssertNoErrors();
    }

    [Fact]
    public async Task ChatsRouteRedirectsAndPreservesRecognizedState()
    {
        var definitionId = Guid.NewGuid();
        await using var session = await OpenAgentsAsync(
            $"/chats?simpleChatView=definitions&definitionId={definitionId:D}&ignored=value");

        await session.Page.WaitForURLAsync(
            url => url.Contains("/agents?tab=simple-chats", StringComparison.Ordinal) &&
                   url.Contains("simpleChatView=definitions", StringComparison.Ordinal) &&
                   url.Contains($"definitionId={definitionId:D}", StringComparison.Ordinal));
        Assert.DoesNotContain("ignored", session.Page.Url, StringComparison.Ordinal);
        await Assertions.Expect(session.Page.GetByTestId("llm-chats-tab-definitions"))
            .ToHaveAttributeAsync("aria-selected", "true");

        await session.Page.ReloadAsync();
        await session.Page.GetByTestId("llm-chat-definition-catalog").WaitForAsync();
        await Assertions.Expect(session.Page.GetByTestId("llm-chats-tab-definitions"))
            .ToHaveAttributeAsync("aria-selected", "true");
        session.AssertNoErrors();
    }

    [Fact]
    public async Task MainAndFloatingAgentAndSimpleChatFlowsRemainOperational()
    {
        await using var session = await OpenAgentsAsync("/agents?tab=chat");
        await session.Page.GetByTestId("agents-chat-panel").WaitForAsync();

        await session.Page.GotoAsync(
            $"{fixture.BaseUrl}/agents?tab=simple-chats&simpleChatView=conversations");
        await session.Page.GetByTestId("llm-chat-conversation-workspace").WaitForAsync();

        await OpenFloatingCatalogAsync(session.Page);
        await session.Page.GetByTestId("conversation-shell-filter-agents").ClickAsync();
        await session.Page.GetByTestId("floating-agent-chat-agents-tab").WaitForAsync();
        await session.Page.GetByTestId("conversation-shell-filter-chats").ClickAsync();
        await session.Page.GetByTestId("floating-agent-chat-agents-tab").WaitForAsync();

        Assert.False(await session.Page.Locator("#blazor-error-ui").IsVisibleAsync());
        session.AssertNoErrors();
    }

    [Fact]
    public async Task AllUsageScopesDriveChartsAndDialogs()
    {
        await using var session = await OpenAgentsAsync("/agents");
        var scope = session.Page.GetByTestId("agents-overview-usage-scope");

        await ClickScopeAsync(scope, "Chats");
        await session.Page.WaitForURLAsync(url => url.Contains("usageScope=simple-chats", StringComparison.Ordinal));
        await Assertions.Expect(session.Page.GetByTestId("agents-overview-top-consumers"))
            .ToContainTextAsync("Top 5 Chats");
        await session.Page.GetByTestId("agents-overview-open-provider-usage").ClickAsync();
        await session.Page.GetByTestId("provider-usage-dialog").WaitForAsync();
        await session.Page.Keyboard.PressAsync("Escape");

        await ClickScopeAsync(scope, "Agents");
        await session.Page.WaitForURLAsync(url => url.Contains("usageScope=agents", StringComparison.Ordinal));
        await Assertions.Expect(session.Page.GetByTestId("agents-overview-top-consumers"))
            .ToContainTextAsync("Top 5 Agents");
        await session.Page.GetByTestId("agents-overview-open-model-usage").ClickAsync();
        await session.Page.GetByTestId("model-usage-dialog").WaitForAsync();
        await session.Page.Keyboard.PressAsync("Escape");

        await ClickScopeAsync(scope, "Both");
        await session.Page.WaitForURLAsync(url => !url.Contains("usageScope=", StringComparison.Ordinal));
        await Assertions.Expect(session.Page.GetByTestId("agents-overview-provider-bar")).ToBeVisibleAsync();
        await Assertions.Expect(session.Page.GetByTestId("agents-overview-provider-distribution")).ToBeVisibleAsync();
        session.AssertNoErrors();
    }

    [Fact]
    public async Task SimpleChatSettingsTabsAndSharedAvatarWorkflowRemainOperational()
    {
        await using var session = await OpenAgentsAsync(
            "/agents?tab=simple-chats&simpleChatView=definitions");
        await session.Page.GetByTestId("llm-chat-definition-create").ClickAsync();
        var dialog = session.Page.GetByTestId("llm-chat-definition-editor-dialog");
        await dialog.WaitForAsync();

        var identity = dialog.GetByTestId("llm-chat-definition-tab-identity");
        var runtime = dialog.GetByTestId("llm-chat-definition-tab-runtime");
        var output = dialog.GetByTestId("llm-chat-definition-tab-output");
        await Assertions.Expect(identity).ToHaveAttributeAsync("aria-selected", "true");
        await runtime.ClickAsync();
        await Assertions.Expect(runtime).ToHaveAttributeAsync("aria-selected", "true");
        await output.ClickAsync();
        await Assertions.Expect(output).ToHaveAttributeAsync("aria-selected", "true");
        await identity.ClickAsync();

        await dialog.GetByTestId("llm-chat-definition-avatar-open").ClickAsync();
        var avatarOptions = session.Page.GetByTestId("llm-chat-definition-avatar-options");
        await avatarOptions.WaitForAsync();
        Assert.True(await avatarOptions.Locator("button").CountAsync() >= 8);
        await session.Page.GetByTestId("llm-chat-definition-avatar-option-1").ClickAsync();
        Assert.True(
            await session.Page.GetByTestId("llm-chat-definition-avatar-ai-generate").CountAsync() == 1 ||
            await session.Page.GetByTestId("llm-chat-definition-avatar-ai-unavailable").CountAsync() == 1);
        await session.Page.GetByTestId("llm-chat-definition-avatar-close").ClickAsync();

        await Assertions.Expect(dialog.GetByTestId("llm-chat-definition-editor-save")).ToBeVisibleAsync();
        session.AssertNoErrors();
    }

    private async Task<BrowserSession> OpenAgentsAsync(string path)
    {
        var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var session = new BrowserSession(context, page);
        var response = await page.GotoAsync($"{fixture.BaseUrl}{path}");
        Assert.True(response?.Ok, $"Expected '{path}' to return 2xx, got {response?.Status}.");
        await DismissStartupModalIfPresentAsync(page);
        return session;
    }

    private static async Task ClickScopeAsync(ILocator scope, string label)
    {
        var button = scope.Locator("button").Filter(new LocatorFilterOptions
        {
            HasTextString = label
        });
        await button.ClickAsync();
    }

    private static async Task OpenFloatingCatalogAsync(IPage page)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(20);
        var action = page.GetByTestId("shell-agent-chats-action");
        var catalog = page.GetByTestId("floating-agent-catalog-window");
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await action.DispatchEventAsync("click");
            if (await catalog.IsVisibleAsync())
            {
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("The floating conversation catalog did not become visible after the shell initialized.");
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 5_000)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        try
        {
            await startupDialog.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
    }

    private sealed class BrowserSession : IAsyncDisposable
    {
        private readonly IBrowserContext context;
        private readonly List<string> errors = [];

        public BrowserSession(IBrowserContext context, IPage page)
        {
            this.context = context;
            Page = page;
            page.Console += (_, message) =>
            {
                if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(message.Text);
                }
            };
            page.PageError += (_, message) => errors.Add(message);
        }

        public IPage Page { get; }

        public void AssertNoErrors()
            => Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));

        public ValueTask DisposeAsync()
            => context.DisposeAsync();
    }
}
