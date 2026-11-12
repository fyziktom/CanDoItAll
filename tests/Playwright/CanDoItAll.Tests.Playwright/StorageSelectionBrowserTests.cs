using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Playwright.StorageCatalog;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class StorageSelectionBrowserTests {
    [Fact]
    public async Task Actual_agent_child_apply_parent_cancel_and_save_preserve_authority() {
        await using var host = new StorageCatalogBrowserHost(false);
        await host.ReadyAsync(false);
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(host.Profile!, "Storage.Selection.Browser", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
        await using var scope = provider.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var agents = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var catalog = workspace.CreateStorageDraft(StorageProviderKind.FileSystem);
        catalog.Name = "Selection browser catalog";
        catalog.EndpointOrRoot = Path.Combine(host.Profile!.WorkspaceRootPath, "selection-catalog");
        var storageSave = await workspace.SaveStorageAsync(catalog);
        Assert.True(storageSave.IsSuccess);
        var catalogId = storageSave.Value;
        var agentId = await agents.SaveAgentAsync(new AgentEditorModel {
            Name = "Selection browser agent", RoleTitle = "Storage selection tester", Instructions = "Preserve original instructions.",
            WorkspaceToolAccess = new() { CanReadFiles = true, CanReadStorage = true, AllowAllStorageCatalogs = false }
        });
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        await page.GotoAsync(host.BaseUrl + "/agents?tab=agents");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("agents-catalog-search").FillAsync("Selection browser agent");
        var card = page.GetByTestId("agents-catalog-card").Filter(new() { HasText = "Selection browser agent" });
        await Assertions.Expect(card).ToHaveCountAsync(1);
        await OpenParent();
        await ApplyChild();
        Assert.Empty((await agents.GetAgentEditorAsync(agentId)).WorkspaceToolAccess.AllowedStorageCatalogIds);
        await page.GetByTestId("agents-details-dialog").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
        Assert.Empty((await agents.GetAgentEditorAsync(agentId)).WorkspaceToolAccess.AllowedStorageCatalogIds);
        await OpenParent();
        await ApplyChild();
        await page.GetByTestId("agents-catalog-save").ClickAsync();
        await Assertions.Expect(page.GetByText("Technical agent saved.", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("agents-catalog-save")).ToBeEnabledAsync();
        var saved = await agents.GetAgentEditorAsync(agentId);
        Assert.Equal([catalogId], saved.WorkspaceToolAccess.AllowedStorageCatalogIds);
        Assert.False(saved.WorkspaceToolAccess.AllowAllStorageCatalogs);
        Assert.True(saved.WorkspaceToolAccess.CanReadStorage);
        Assert.False(saved.WorkspaceToolAccess.CanWriteStorage);
        Assert.True(saved.WorkspaceToolAccess.CanReadFiles);
        Assert.False(saved.WorkspaceToolAccess.CanWriteFiles);
        Assert.Equal("Preserve original instructions.", saved.Instructions);
        Assert.Empty(saved.AllowedSecretReferences);
        Assert.Empty(saved.SelectedCapabilityIds);
        Assert.Empty(saved.ProjectStructureAccess.AllowedProjectIds);
        await page.GetByTestId("agents-details-dialog").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
        await OpenParent();
        await Assertions.Expect(page.GetByTestId($"agents-catalog-storage-selection-selected-row-{catalogId:N}")).ToContainTextAsync(catalog.Name);
        Assert.Empty(errors);

        async Task OpenParent() {
            await card.ClickAsync();
            await card.DispatchEventAsync("dblclick");
            await page.GetByTestId("agents-catalog-name").WaitForAsync();
            await page.GetByTestId("agents-details-dialog").GetByRole(AriaRole.Tab, new() { Name = "Workspace Tools", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-catalog-storage-all")).Not.ToBeCheckedAsync();
        }
        async Task ApplyChild() {
            await page.GetByTestId("agents-catalog-storage-selection-choose").ClickAsync();
            var option = page.GetByTestId($"agents-catalog-storage-selection-dialog-option-{catalogId:N}");
            await Assertions.Expect(option).ToBeVisibleAsync();
            await Assertions.Expect(option).ToHaveAttributeAsync("aria-pressed", "false");
            await option.FocusAsync();
            await Assertions.Expect(option).ToBeFocusedAsync();
            await option.PressAsync("Space");
            await Assertions.Expect(option).ToHaveAttributeAsync("aria-pressed", "true");
            var search = page.GetByTestId("agents-catalog-storage-selection-dialog-picker-search");
            await search.FillAsync("Selection browser catalog");
            await Assertions.Expect(search).ToBeFocusedAsync();
            await page.GetByTestId("agents-catalog-storage-selection-dialog-apply").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-details-dialog")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId($"agents-catalog-storage-selection-selected-row-{catalogId:N}")).ToContainTextAsync(catalog.Name);
            var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-storage-selection-ui");
            Directory.CreateDirectory(evidence);
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "production-agent-selection.png"), FullPage = true });
        }
    }
}
