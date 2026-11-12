using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(PlaywrightCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class CollaborationBrowserTests(PlaywrightAppFixture fixture) {
    [Fact]
    public async Task Production_create_reply_mark_read_filter_deep_link_and_context_use_real_persistence() {
        var artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "collaboration-review");
        Directory.CreateDirectory(artifacts);
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var failedAssets = new List<string>();
        page.PageError += (_, value) => errors.Add(value);
        page.Console += (_, value) => {
            if (value.Type == "error") {
                errors.Add(value.Text);
            }
        };
        page.Response += (_, response) => {
            if (response.Status >= 400 && response.Request.ResourceType is "stylesheet" or "script" or "font") {
                failedAssets.Add(response.Url);
            }
        };
        page.RequestFailed += (_, request) => {
            if (request.ResourceType is "stylesheet" or "script" or "font") {
                failedAssets.Add(request.Url);
            }
        };

        await page.GotoAsync($"{fixture.BaseUrl}/collaboration");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await ReadyAsync(page);
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "production-initial.png") });
        await page.GetByTestId("collaboration-thread-create").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=collaboration-create-form] .validation-message").First).ToBeVisibleAsync();

        var subject = $"Browser collaboration {Guid.NewGuid():N}";
        await page.GetByTestId("collaboration-thread-subject").FillAsync(subject);
        await page.GetByTestId("collaboration-thread-context-label").FillAsync("Scheduler follow-up");
        await page.GetByTestId("collaboration-thread-context-route").FillAsync("/scheduler");
        await page.GetByTestId("collaboration-thread-message").FillAsync("First message with <script>escaped content</script>.");
        await page.GetByTestId("collaboration-thread-create").ClickAsync();
        await Assertions.Expect(page.GetByTestId("collaboration-thread-title")).ToHaveTextAsync(subject);
        await Assertions.Expect(page.GetByTestId("shell-nav-badge-collaboration")).ToBeVisibleAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "production-created.png") });

        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
            "Collaboration.Browser.Readback", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { ["DevelopmentManager:TuningModeEnabled"] = "false" });
        await using var scope = provider.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<CollaborationService>();
        var saved = (await owner.GetWorkspaceAsync()).Threads.Single(item => item.Subject == subject);
        Assert.Contains(saved.ThreadId.ToString("D"), page.Url, StringComparison.Ordinal);

        await AcceptedSelectionAsync(page, saved.ThreadId, unread: true);
        await page.GetByTestId("collaboration-filter-unread").ClickAsync();
        await page.GetByTestId("collaboration-reply-message").FillAsync("Unsent notification draft");
        await page.GetByTestId("collaboration-mark-read").ClickAsync();
        await AcceptedSelectionAsync(page, saved.ThreadId, unread: false);
        Assert.False((await owner.GetWorkspaceAsync(saved.ThreadId)).SelectedThread!.IsUnread);
        await Assertions.Expect(page.GetByTestId("collaboration-reply-message")).ToHaveValueAsync("Unsent notification draft");
        await Assertions.Expect(page.GetByTestId("collaboration-reply-retained")).ToBeVisibleAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "production-retained-reply.png") });
        await page.GetByRole(AriaRole.Button, new() { Name = "Clear", Exact = true }).ClickAsync();
        await page.GetByTestId("collaboration-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("collaboration-inbox-item").Filter(new() { HasText = subject })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("collaboration-workspace")).Not.ToHaveAttributeAsync("data-selected-thread-id", saved.ThreadId.ToString("D"));
        await page.GetByRole(AriaRole.Tab, new() { Name = "Threads", Exact = false }).ClickAsync();
        await page.GetByTestId("collaboration-thread-item").Filter(new() { HasText = subject }).GetByRole(AriaRole.Button).ClickAsync();
        await AcceptedSelectionAsync(page, saved.ThreadId, unread: false);
        await page.GetByTestId("collaboration-reply-kind").SelectOptionAsync(nameof(CollaborationMessageKind.System));
        await page.GetByTestId("collaboration-reply-message").FillAsync("Saved local reply");
        await page.GetByTestId("collaboration-reply-message").FocusAsync();
        await Assertions.Expect(page.GetByTestId("collaboration-reply-message")).ToBeFocusedAsync();
        await page.GetByTestId("collaboration-reply-submit").ClickAsync();
        await Assertions.Expect(page.GetByTestId("collaboration-thread-message-item")).ToHaveCountAsync(2);
        var detail = (await owner.GetWorkspaceAsync(saved.ThreadId)).SelectedThread!;
        Assert.Equal("Saved local reply", detail.Messages.Last().Body);
        Assert.Equal(CollaborationMessageKind.System, detail.Messages.Last().MessageKind);
        Assert.False(detail.IsUnread);
        await page.GetByRole(AriaRole.Tab, new() { Name = "Inbox", Exact = false }).ClickAsync();
        await page.GetByTestId("collaboration-filter-all").ClickAsync();
        var escalationSubject = $"Browser escalation {Guid.NewGuid():N}";
        await page.GetByTestId("collaboration-thread-subject").FillAsync(escalationSubject);
        await page.GetByTestId("collaboration-thread-kind").SelectOptionAsync(nameof(CollaborationInboxItemKind.Escalation));
        await page.GetByTestId("collaboration-thread-message").FillAsync("Escalation created through the production form.");
        await page.GetByTestId("collaboration-thread-create").ClickAsync();
        await Assertions.Expect(page.GetByTestId("collaboration-thread-title")).ToHaveTextAsync(escalationSubject);
        var escalation = (await owner.GetWorkspaceAsync()).Threads.Single(item => item.Subject == escalationSubject);
        await AcceptedSelectionAsync(page, escalation.ThreadId, unread: true);
        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Escalations", Exact = false })).ToHaveAttributeAsync("aria-selected", "true");
        var escalationDetail = (await owner.GetWorkspaceAsync(escalation.ThreadId)).SelectedThread!;
        Assert.Equal(CollaborationInboxItemKind.Escalation, escalationDetail.ItemKind);
        Assert.Equal("Escalation created through the production form.", Assert.Single(escalationDetail.Messages).Body);
        await page.GetByTestId("collaboration-reply-message").FillAsync("Deliberately leave this escalation draft");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Threads", Exact = false }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("collaboration-reply-message")).ToHaveValueAsync("Deliberately leave this escalation draft");
        await page.GetByTestId("collaboration-thread-item").Filter(new() { HasText = subject }).GetByRole(AriaRole.Button).ClickAsync();
        await Assertions.Expect(page.GetByTestId("collaboration-thread-title")).ToHaveTextAsync(subject);
        await Assertions.Expect(page.GetByTestId("collaboration-reply-message")).ToHaveValueAsync(string.Empty);
        await page.GetByRole(AriaRole.Tab, new() { Name = "Escalations", Exact = false }).ClickAsync();
        await page.GetByTestId("collaboration-escalation-item").Filter(new() { HasText = escalationSubject }).GetByRole(AriaRole.Button).ClickAsync();
        await AcceptedSelectionAsync(page, escalation.ThreadId, unread: true);
        await Assertions.Expect(page.GetByTestId("collaboration-reply-message")).ToHaveValueAsync(string.Empty);
        Assert.Single((await owner.GetWorkspaceAsync(escalation.ThreadId)).SelectedThread!.Messages);
        await page.GotoAsync($"{fixture.BaseUrl}/collaboration?threadId={escalation.ThreadId:D}");
        await ReadyAsync(page);
        await page.ReloadAsync();
        await ReadyAsync(page);
        await AcceptedSelectionAsync(page, escalation.ThreadId, unread: true);
        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Escalations", Exact = false })).ToHaveAttributeAsync("aria-selected", "true");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "production-escalation-deep-link.png") });

        await page.GotoAsync($"{fixture.BaseUrl}/collaboration?threadId={saved.ThreadId:D}");
        await ReadyAsync(page);
        await Assertions.Expect(page.GetByTestId("collaboration-thread-message-item")).ToHaveCountAsync(2);
        await page.ReloadAsync();
        await ReadyAsync(page);
        await Assertions.Expect(page.GetByTestId("collaboration-thread-title")).ToHaveTextAsync(subject);
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "production-deep-link.png"), FullPage = true });
        await page.GetByTestId("collaboration-open-context").ClickAsync();
        await page.WaitForURLAsync("**/scheduler");
        await page.GoBackAsync();
        await ReadyAsync(page);
        await page.GotoAsync($"{fixture.BaseUrl}/collaboration?threadId={Guid.NewGuid():D}");
        await ReadyAsync(page);
        await Assertions.Expect(page.GetByText("Thread not found", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Empty(errors);
        Assert.Empty(failedAssets);
        var serverLog = fixture.GetLogSnapshot(1000);
        await File.WriteAllTextAsync(Path.Combine(artifacts, "production-server.log"), serverLog);
        Assert.DoesNotContain("Unhandled exception", serverLog, StringComparison.OrdinalIgnoreCase);
    }

    private static Task AcceptedSelectionAsync(IPage page, Guid threadId, bool unread) =>
        Assertions.Expect(page.Locator($"[data-testid=collaboration-workspace][data-phase=ready][data-selected-thread-id='{threadId:D}'][data-selected-unread='{unread.ToString().ToLowerInvariant()}']")).ToHaveCountAsync(1);

    internal static async Task ReadyAsync(IPage page) {
        var workspace = page.GetByTestId("collaboration-workspace");
        await Assertions.Expect(workspace).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });
        await Assertions.Expect(workspace).ToHaveAttributeAsync("data-phase", "ready", new() { Timeout = 30_000 });
    }
}
