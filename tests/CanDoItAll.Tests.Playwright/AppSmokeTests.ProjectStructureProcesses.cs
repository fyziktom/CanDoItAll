using System.IO;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    private const string AgentFrameworkIntegrationProjectId = "eaee1691-f5cf-49b1-a43d-1c8cd07d50f0";

    [Fact]
    [Trait("Category", "Quarantined")]
    [Trait("Surface", "ProjectStructure")]
    public async Task Seeded_project_structure_projects_process_nodes_and_opens_process_workspace()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "project-structure-process-projection");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{AgentFrameworkIntegrationProjectId}/structure");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected seeded structure route to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync("text=Structure canvas", new PageWaitForSelectorOptions
        {
            Timeout = 90_000
        });
        await page.Locator(".cw-workbench-shell").WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = 90_000
        });
        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page);

        var projection = await page.EvaluateAsync<ProjectStructureProcessProjection>(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const surfaceNodes = Array.isArray(host?.__canvasWorkbenchState?.surface?.nodes)
                    ? host.__canvasWorkbenchState.surface.nodes
                    : [];

                const definitions = surfaceNodes
                    .filter(node => typeof node?.id === 'string' && node.id.startsWith('process-definition:'))
                    .map(node => ({
                        id: node.id,
                        title: node.title || node.subtitle || node.id
                    }));

                const runs = surfaceNodes
                    .filter(node => typeof node?.id === 'string' && node.id.startsWith('process-run:'))
                    .map(node => ({
                        id: node.id,
                        title: node.title || node.subtitle || node.id
                    }));

                return {
                    definitions,
                    runs
                };
            }");

        Assert.NotNull(projection);
        Assert.NotNull(projection!.Definitions);
        Assert.NotNull(projection.Runs);
        Assert.True(projection.Definitions.Length >= 5, $"Expected projected process definitions in the structure graph, got {projection.Definitions.Length}.");
        Assert.True(projection.Runs.Length >= 5, $"Expected projected process runs in the structure graph, got {projection.Runs.Length}.");
        Assert.Contains(
            projection.Definitions,
            node => node.Title.Contains("role-first operating model baseline", StringComparison.OrdinalIgnoreCase));

        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "01-structure-process-projection.png"),
            FullPage = false
        });

        var definitionNode = projection.Definitions[0];
        var runNode = projection.Runs[0];

        await OpenProjectedNodeQuickActionsByIdAsync(page, definitionNode.Id);
        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        var primaryQuickAction = page.GetByTestId("project-structure-quick-action-primary");
        await primaryQuickAction.WaitForAsync();
        Assert.Contains("Open Processes", await primaryQuickAction.TextContentAsync(), StringComparison.Ordinal);
        await CaptureLocatorAsync(quickActionDialog, Path.Combine(artifactsDir, "02-process-definition-quick-actions.png"));

        var definitionPopupTask = context.WaitForPageAsync();
        await primaryQuickAction.ClickAsync();
        var definitionPopup = await definitionPopupTask;
        await definitionPopup.WaitForURLAsync($"**/projects/{AgentFrameworkIntegrationProjectId}/processes?processId=*");
        Assert.Contains($"processId={ExtractEntityId(definitionNode.Id)}", definitionPopup.Url, StringComparison.OrdinalIgnoreCase);
        await definitionPopup.CloseAsync();

        await quickActionDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        await OpenProjectedNodeQuickActionsByIdAsync(page, runNode.Id);
        quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        primaryQuickAction = page.GetByTestId("project-structure-quick-action-primary");
        await primaryQuickAction.WaitForAsync();
        Assert.Contains("Open Processes", await primaryQuickAction.TextContentAsync(), StringComparison.Ordinal);
        await CaptureLocatorAsync(quickActionDialog, Path.Combine(artifactsDir, "03-process-run-quick-actions.png"));

        var runPopupTask = context.WaitForPageAsync();
        await primaryQuickAction.ClickAsync();
        var runPopup = await runPopupTask;
        await runPopup.WaitForURLAsync($"**/projects/{AgentFrameworkIntegrationProjectId}/processes?runId=*");
        Assert.Contains($"runId={ExtractEntityId(runNode.Id)}", runPopup.Url, StringComparison.OrdinalIgnoreCase);
        await runPopup.CloseAsync();

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task OpenProjectedNodeQuickActionsByIdAsync(IPage page, string nodeId)
    {
        var opened = await page.EvaluateAsync<bool>(
            @"targetId => {
                const host = document.querySelector('.cw-canvas-host');
                const runtime = window.CanDoItAll?.canvasWorkbench;
                if (!host || !runtime?.openNode || !targetId) {
                    return false;
                }

                return runtime.openNode(host, targetId);
            }",
            nodeId);

        Assert.True(opened, $"Expected to open quick actions for projected canvas node '{nodeId}'.");
    }

    private static string ExtractEntityId(string nodeId)
    {
        var separatorIndex = nodeId.IndexOf(':', StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex < nodeId.Length - 1
            ? nodeId[(separatorIndex + 1)..]
            : nodeId;
    }

    private sealed class ProjectStructureProcessProjection
    {
        public ProjectStructureProcessNodeProof[] Definitions { get; set; } = [];

        public ProjectStructureProcessNodeProof[] Runs { get; set; } = [];
    }

    private sealed class ProjectStructureProcessNodeProof
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
    }
}
