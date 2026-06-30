using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    private const string ProjectStructureWorkflowResultTitle = "Browser workflow generated summary";

    [Fact]
    public async Task Project_structure_workflow_nodes_can_be_added_started_and_inspected_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(
            repoRoot,
            ".codex",
            "bundles",
            "project-structure-workflow-runs",
            "proof",
            "browser");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 950
            }
        });

        var definition = await SaveProjectStructureWorkflowDefinitionAsync(fixture.BaseUrl);
        var page = await context.NewPageAsync();
        await CreateProjectAsync(page, "Playwright Workflow Structure", "Validation");
        await page.WaitForSelectorAsync("text=Structure canvas");
        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page);
        await HideFloatingToolbarWindowAsync(
            page,
            "project-structure-agents-toggle",
            "project-structure-agents-window");

        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");
        var parentNodeId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-feature",
            projectRootId,
            projectRootId,
            "Offer documents folder",
            "Product offer source folder",
            "Parent node for project-structure workflow input preview.");
        var parentSelector = SelectorForNodeId(parentNodeId);

        var contextMenuLabels = await OpenCanvasContextMenuAsync(page, parentSelector);
        Assert.Contains(contextMenuLabels, label => label.Contains("Add workflow", StringComparison.OrdinalIgnoreCase));
        await ClickContextMenuActionAsync(page, "add-workflow");

        var addDialog = page.GetByTestId("project-structure-workflow-add-dialog");
        if (!await WaitForLocatorAsync(addDialog, 3_000))
        {
            await page.Keyboard.PressAsync("Escape");
            await EnsureCanvasSelectionAsync(page, parentSelector);
            await ClickSelectionWindowActionAsync(page, "Add workflow");
            await addDialog.WaitForAsync();
        }
        await addDialog.GetByTestId("project-structure-workflow-add-select")
            .SelectOptionAsync(definition.Id.Value.ToString("D"));
        await addDialog.GetByTestId("project-structure-workflow-add-include-subtree")
            .CheckAsync();
        await addDialog.GetByTestId("project-structure-workflow-add-source-value")
            .FillAsync(@"C:\programovani\testdata\testworkflows\offer-documents");
        await addDialog.GetByTestId("project-structure-workflow-add-source-kind")
            .SelectOptionAsync("FolderPath");
        await addDialog.GetByTestId("project-structure-workflow-add-source-key")
            .FillAsync("offer-documents");
        await addDialog.GetByTestId("project-structure-workflow-add-source-label")
            .FillAsync("Offer source folder");
        await addDialog.GetByTestId("project-structure-workflow-add-manual-json")
            .FillAsync("""{"task":"summarize-offer-documents"}""");
        await addDialog.WaitForAsync();
        await addDialog.GetByText("Project", new() { Exact = true }).WaitForAsync();
        await addDialog.GetByText("Parent node", new() { Exact = true }).WaitForAsync();
        await addDialog.GetByText("Offer source folder", new() { Exact = false }).First.WaitForAsync();
        await CaptureLocatorAsync(
            addDialog,
            Path.Combine(artifactsDir, "project-structure-add-workflow-desktop.png"));
        await addDialog.GetByTestId("project-structure-workflow-add-submit")
            .EvaluateAsync("button => button.click()");

        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\"project-structure-workflow-add-dialog\"]')");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node => string.Equals(node.Title, definition.Name, StringComparison.Ordinal)),
            "workflow node appears after add dialog submit",
            timeoutMs: 15_000);
        var workflowNodeId = await FindNodeIdByTitleAsync(page, definition.Name);
        var workflowSelector = SelectorForNodeId(workflowNodeId);
        await EnsureCanvasSelectionAsync(page, workflowSelector);
        var workflowStatusCard = page.GetByTestId("project-structure-workflow-status-card");
        await workflowStatusCard.WaitForAsync();
        await workflowStatusCard.GetByText("Workflow is ready to start", new() { Exact = false }).WaitForAsync();
        await CaptureLocatorAsync(
            workflowStatusCard,
            Path.Combine(artifactsDir, "project-structure-workflow-selection-status.png"));

        var workflowContextLabels = await OpenCanvasContextMenuAsync(page, workflowSelector);
        Assert.Contains(workflowContextLabels, label => label.Contains("Start workflow", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(workflowContextLabels, label => label.Contains("matching", StringComparison.OrdinalIgnoreCase));
        await ClickContextMenuActionAsync(page, "start-workflow");

        var startDialog = page.GetByTestId("project-structure-workflow-start-dialog");
        if (!await WaitForLocatorAsync(startDialog, 3_000))
        {
            await page.Keyboard.PressAsync("Escape");
            await EnsureCanvasSelectionAsync(page, workflowSelector);
            await ClickSelectionWindowActionAsync(page, "Start workflow");
            await startDialog.WaitForAsync();
        }
        await startDialog.GetByText(definition.Name, new() { Exact = false }).First.WaitForAsync();
        Assert.Equal(0, await page.GetByTestId("processes-launch-name-input").CountAsync());
        Assert.Equal(0, await page.GetByText("Use HR manager suggestions", new() { Exact = false }).CountAsync());
        await CaptureLocatorAsync(
            startDialog,
            Path.Combine(artifactsDir, "project-structure-start-workflow-confirmation.png"));
        await startDialog.GetByTestId("project-structure-workflow-start-submit")
            .EvaluateAsync("button => button.click()");

        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\"project-structure-workflow-start-dialog\"]')");
        await EnsureCanvasSelectionAsync(page, workflowSelector);
        try
        {
            await page.WaitForFunctionAsync(
                @"() => {
                    const statusCard = document.querySelector('[data-testid=""project-structure-workflow-status-card""]');
                    const text = statusCard?.textContent || '';
                    return text.includes('Completed') &&
                        text.includes('100%') &&
                        /\b\d+\s*\/\s*\d+\b/.test(text);
                }",
                null,
                new PageWaitForFunctionOptions { Timeout = 30_000 });
        }
        catch (PlaywrightException exception) when (exception.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            var renderedStatus = await workflowStatusCard.TextContentAsync();
            throw new InvalidOperationException(
                $"Workflow status did not complete in the selection panel. Rendered status: {renderedStatus}",
                exception);
        }

        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node => string.Equals(node.Title, ProjectStructureWorkflowResultTitle, StringComparison.Ordinal)),
            "workflow-created result asset appears under the workflow node",
            timeoutMs: 15_000);
        var resultNodeId = await FindNodeIdByTitleAsync(page, ProjectStructureWorkflowResultTitle);
        var resultParentId = await ReadCanvasNodeParentIdAsync(page, resultNodeId);
        Assert.Equal(workflowNodeId, resultParentId);
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "project-structure-workflow-result-child-desktop.png"),
            FullPage = true
        });
        await workflowStatusCard.GetByTestId("project-structure-workflow-created-nodes")
            .WaitForAsync();
        await workflowStatusCard.GetByTestId("project-structure-workflow-created-assets")
            .WaitForAsync();

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task<WorkflowDefinition> SaveProjectStructureWorkflowDefinitionAsync(string baseUrl)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };

        var response = await client.PostAsJsonAsync(
            "/api/workflows/definitions",
            BuildProjectStructureWorkflowDefinitionSaveRequest());
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        return await response.Content.ReadFromJsonAsync<WorkflowDefinition>() ??
            throw new InvalidOperationException("Workflow definition save returned no payload.");
    }

    private static async Task HideFloatingToolbarWindowAsync(
        IPage page,
        string toggleTestId,
        string windowTestId)
    {
        var window = page.GetByTestId(windowTestId);
        if (!await WaitForLocatorAsync(window, 750) ||
            !await window.IsVisibleAsync())
        {
            return;
        }

        await page.GetByTestId(toggleTestId).ClickAsync();
        await page.WaitForFunctionAsync(
            @"requestedTestId => {
                const element = document.querySelector(`[data-testid=""${requestedTestId}""]`);
                return !element || element.offsetParent === null || getComputedStyle(element).visibility === 'hidden';
            }",
            windowTestId);
    }

    private static WorkflowDefinitionSaveRequest BuildProjectStructureWorkflowDefinitionSaveRequest()
    {
        var executorSettingsJson = JsonSerializer.Serialize(
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateAsset,
                AssetKind = "md",
                Title = ProjectStructureWorkflowResultTitle,
                Content = """
                    # Browser workflow result

                    The project-structure workflow run created this result under its workflow node after the user confirmed start.
                    """,
                ContentType = "text/markdown"
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Project structure workflow proof",
            Description: "Browser proof workflow for project-structure add, start, and status flows.",
            Status: WorkflowLifecycleStatus.Active,
            Graph: new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateProjectStructureWorkflowNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateProjectStructureExecutorWorkflowNode("create-result-asset", executorSettingsJson),
                    CreateProjectStructureWorkflowNode("end", WorkflowNodeKind.End, inputShape: CreateProjectStructureJsonShape())
                ],
                [
                    CreateProjectStructureWorkflowEdge("start-to-result-asset", "start", "create-result-asset"),
                    CreateProjectStructureWorkflowEdge("result-asset-to-end", "create-result-asset", "end")
                ]),
            RuntimePolicy: new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static WorkflowNode CreateProjectStructureWorkflowNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));
    }

    private static WorkflowNode CreateProjectStructureExecutorWorkflowNode(
        string id,
        string executorSettingsJson)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: CreateProjectStructureJsonShape()) with
            {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = executorSettingsJson,
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
                {
                    CaptureOutputArtifact = true,
                    TimeoutSeconds = 45
                }
            });
    }

    private static WorkflowValueShape CreateProjectStructureJsonShape()
        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");

    private static WorkflowEdge CreateProjectStructureWorkflowEdge(string id, string source, string target)
    {
        return new WorkflowEdge(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);
    }

    private static async Task<string?> ReadCanvasNodeParentIdAsync(IPage page, string nodeId)
    {
        return await page.EvaluateAsync<string?>(
            @"requestedNodeId => {
                const host = document.querySelector('.cw-canvas-host');
                const node = host?.__canvasWorkbenchState?.lookups?.byId?.get(requestedNodeId) ?? null;
                return node?.parentId || node?.parentNodeId || null;
            }",
            nodeId);
    }
}
