using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureAgentChangesBrowserTests {
    [Fact]
    public async Task Agent_commit_refreshes_its_clean_workbench_and_notifies_peers_and_editors() {
        await using var wire = await AgentResponseFixture.StartAsync("This fixture commits through the native Structure service.");
        await using var host = await ProcessNativeBrowserHost.StartAsync(wire.BaseUrl);
        var project = host.ProjectId;
        var anchor = await host.ReadAsync(services => services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(
            project, new(ProjectObjectType.Link, "Editable anchor", "https://example.com/anchor", "", $"project:{project:D}", 420, 260)));
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var peerBrowser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await using var peerContext = await peerBrowser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        var peer = await peerContext.NewPageAsync();
        var errors = new ConcurrentQueue<string>();
        try {
            foreach (var current in new[] { page, peer }) {
                current.PageError += (_, error) => errors.Enqueue(error);
                await current.GotoAsync($"{host.BaseUrl}/projects/{project:D}/structure");
                await PlaywrightAppFixture.CompleteDatabaseStartupAsync(current);
                await Smoke.ProjectFilesUiJourney.ReadyFileCanvasAsync(current);
            }
            await Smoke.AgentUiJourneySupport.OpenFloatingChatFromCatalogAsync(page, "project-structure-agents-toggle", host.AgentId,
                Path.Combine(host.Evidence, "structure-agent-catalog-failure.png"));
            var chatId = await host.ReadAsync(async services => Assert.Single(await services.GetRequiredService<IAgentFrameworkWorkspaceService>()
                .ListChatSessionsAsync(host.AgentId)).Id);
            var updates = page.GetByTestId("project-structure-changes-window");
            var peerUpdates = peer.GetByTestId("project-structure-changes-window");
            await page.EvaluateAsync("() => window.structureChangeProof = true");

            var first = await CreateNodeAsync("Agent result on clean canvas", chatId);
            await WaitForNodeAsync(page, first.Id);
            await Assertions.Expect(updates).ToBeHiddenAsync();
            await Assertions.Expect(peerUpdates).ToContainTextAsync(first.Title);
            await peerUpdates.GetByRole(AriaRole.Button).Filter(new() { HasTextString = first.Title }).ClickAsync();
            await Assertions.Expect(peer.GetByTestId("project-structure-selection-window")).ToContainTextAsync(first.Title);
            await Assertions.Expect(peerUpdates).Not.ToContainTextAsync(first.Title);

            var catalog = page.GetByTestId("floating-agent-catalog-window");
            await catalog.GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true }).ClickAsync();
            var chat = page.GetByTestId("floating-agent-chat-window");
            var drag = await chat.Locator("[data-cda-overlay-drag='true']").BoundingBoxAsync();
            Assert.NotNull(drag);
            await page.Mouse.MoveAsync(drag.X + 80, drag.Y + 25);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(drag.X + 1150, drag.Y + 45, new() { Steps = 12 });
            await page.Mouse.UpAsync();
            await Assertions.Expect(chat).ToBeVisibleAsync();
            var point = await ProcessCanvasBrowser.ReadNodePointAsync(page, anchor.Id);
            await page.Mouse.ClickAsync(point.X, point.Y, new() { Button = MouseButton.Right });
            await page.Locator(".cw-context-menu__action[data-action-id='edit']").ClickAsync();
            var draft = page.Locator(".cw-canvas-composer.is-dialog .cw-canvas-composer__input").First;
            const string unsaved = "Local Structure draft";
            await draft.FillAsync(unsaved);
            var second = await CreateNodeAsync("Agent result during edit", chatId);
            await Assertions.Expect(updates).ToContainTextAsync(second.Title);
            await Assertions.Expect(draft).ToHaveValueAsync(unsaved);
            Assert.False(await HasNodeAsync(page, second.Id));
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "structure-agent-update-with-draft.png") });

            await updates.GetByTestId("project-structure-changes-refresh").ClickAsync();
            await WaitForNodeAsync(page, second.Id);
            await Assertions.Expect(draft).ToHaveValueAsync(unsaved);
            await Assertions.Expect(updates).Not.ToContainTextAsync(second.Title);
            Assert.True(await page.EvaluateAsync<bool>("() => window.structureChangeProof === true"));
            Assert.Empty(errors);
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "structure-agent-change-failure.png") });
            await peer.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "structure-agent-peer-failure.png") });
            throw;
        }

        Task<ProjectStructureNode> CreateNodeAsync(string title, Guid? chatSessionId = null) => host.ReadAsync(async services => {
            var now = DateTimeOffset.UtcNow;
            using var audit = chatSessionId is null ? null : WorkspaceExecutionAuditContext.BeginScope(new ExecutionRunRecord(
                Guid.NewGuid(), host.AgentId, chatSessionId, "Fixture agent commit", AgentChatTrustedSourceKinds.ProjectStructure, project.ToString("D"),
                string.Empty, string.Empty, string.Empty, string.Empty, "{}", string.Empty, string.Empty, "fixture", "fixture", ExecutionState.Running,
                null, now, now, now, null, string.Empty, null, []));
            return await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project, new(
                ProjectObjectType.ProjectBlock, title, "", "", $"project:{project:D}", 420, 260));
        });
    }

    private const string HasNode = "id => document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes?.some(node => node.id === id) === true";

    private static Task<IJSHandle> WaitForNodeAsync(IPage page, string id) => page.WaitForFunctionAsync(HasNode, id);
    private static Task<bool> HasNodeAsync(IPage page, string id) => page.EvaluateAsync<bool>(HasNode, id);
}
