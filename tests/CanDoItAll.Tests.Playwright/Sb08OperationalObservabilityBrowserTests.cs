using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class Scenario08OperationalObservabilityBrowserTests
{
    private const string AppUrlEnvironmentVariable = "CANDOITALL_Scenario08_APP_URL";
    private const string OutputRootEnvironmentVariable = "CANDOITALL_Scenario08_BROWSER_OUTPUT_ROOT";
    private const string ExecutorNodeName = "Scenario08 proof executor";

    [Fact]
    [Trait("Category", "Scenario08")]
    public async Task Operational_observability_surfaces_render_desktop_and_mobile_proof()
    {
        var appUrl = Environment.GetEnvironmentVariable(AppUrlEnvironmentVariable);
        var outputRoot = Environment.GetEnvironmentVariable(OutputRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(appUrl) || string.IsNullOrWhiteSpace(outputRoot))
        {
            return;
        }

        ResetDirectory(outputRoot);
        var workflowDefinition = await SaveWorkflowDefinitionAsync(appUrl);
        var viewportResults = new List<Scenario08ViewportProofResult>();

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        try
        {
            foreach (var viewport in Scenario08ViewportSpec.All)
            {
                viewportResults.Add(await CaptureViewportProofAsync(browser, appUrl, outputRoot, workflowDefinition, viewport));
                await WriteSummaryAsync(outputRoot, viewportResults);
            }
        }
        catch
        {
            await WriteSummaryAsync(outputRoot, viewportResults);
            throw;
        }
    }

    private static async Task<Scenario08ViewportProofResult> CaptureViewportProofAsync(
        IBrowser browser,
        string appUrl,
        string outputRoot,
        WorkflowDefinition workflowDefinition,
        Scenario08ViewportSpec viewport)
    {
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            IsMobile = viewport.IsMobile,
            ViewportSize = new ViewportSize
            {
                Width = viewport.Width,
                Height = viewport.Height
            }
        });

        var page = await context.NewPageAsync();
        var diagnostics = new Scenario08BrowserDiagnostics();
        AttachDiagnostics(page, diagnostics);

        var processDetailCaptured = await CaptureLiveProcessProofAsync(page, appUrl, outputRoot, viewport);
        var workflowExecutorCaptured = await CaptureWorkflowExecutorProofAsync(page, appUrl, outputRoot, workflowDefinition, viewport);

        await WriteDiagnosticsAsync(outputRoot, viewport, diagnostics);
        return new Scenario08ViewportProofResult(
            viewport.Name,
            viewport.Width,
            viewport.Height,
            processDetailCaptured,
            workflowExecutorCaptured,
            diagnostics.ConsoleMessages.Count,
            diagnostics.ConsoleErrors.Count,
            diagnostics.PageErrors.Count,
            diagnostics.FailedResponses.Count,
            diagnostics.FailedRequests.Count,
            DateTimeOffset.UtcNow);
    }

    private static async Task<bool> CaptureLiveProcessProofAsync(
        IPage page,
        string appUrl,
        string outputRoot,
        Scenario08ViewportSpec viewport)
    {
        await page.GotoAsync(BuildRoute(appUrl, "/processes/live"), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 45_000
        });
        await DismissDatabaseProfileDialogAsync(page);
        await page.GetByTestId("live-processes-page").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        var historyWindow = page.GetByLabel("History window");
        if (await TryWaitForAsync(historyWindow, 5_000))
        {
            await historyWindow.SelectOptionAsync("OneDay");
        }

        await page.WaitForFunctionAsync(
            """
            () => !/Loading process projection/i.test(document.body.innerText || '')
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputRoot, $"live-processes-{viewport.Name}.png"),
            FullPage = true
        });
        await WritePageTextAsync(outputRoot, $"live-processes-{viewport.Name}.txt", page);

        var bodyText = await page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("0.000000 USD", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("An unhandled error has occurred", bodyText, StringComparison.OrdinalIgnoreCase);

        var runCards = page.GetByTestId("live-process-run-card");
        if (!await TryWaitForAsync(runCards.First, 30_000))
        {
            return false;
        }

        await runCards.First.ClickAsync(new LocatorClickOptions { Timeout = 15_000 });
        var processDialog = page.GetByTestId("live-processes-process-detail-dialog");
        await processDialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await page.GetByTestId("live-processes-process-detail-tabs")
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        await processDialog.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(outputRoot, $"live-process-detail-{viewport.Name}.png")
        });

        var detailText = await processDialog.InnerTextAsync();
        Assert.Contains("Invariant diagnostics", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Recommended action", detailText, StringComparison.OrdinalIgnoreCase);

        var stepsTab = processDialog.GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions { Name = "Steps" });
        if (await stepsTab.CountAsync() > 0)
        {
            await stepsTab.First.EvaluateAsync("element => element.click()");
            await processDialog.ScreenshotAsync(new LocatorScreenshotOptions
            {
                Path = Path.Combine(outputRoot, $"live-process-detail-steps-{viewport.Name}.png")
            });

            var stepItems = page.GetByTestId("live-processes-step-item");
            if (await stepItems.CountAsync() > 0)
            {
                await stepItems.First.ClickAsync(new LocatorClickOptions { Timeout = 15_000 });
                var stepDialog = page.GetByTestId("live-processes-stage-detail-dialog");
                await stepDialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
                await stepDialog.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = Path.Combine(outputRoot, $"live-step-detail-{viewport.Name}.png")
                });

                var stepText = await stepDialog.InnerTextAsync();
                Assert.Contains("Target scope", stepText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Allowed operations", stepText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Recovery", stepText, StringComparison.OrdinalIgnoreCase);
            }
        }

        await page.Keyboard.PressAsync("Escape");
        return true;
    }

    private static async Task<bool> CaptureWorkflowExecutorProofAsync(
        IPage page,
        string appUrl,
        string outputRoot,
        WorkflowDefinition workflowDefinition,
        Scenario08ViewportSpec viewport)
    {
        await page.GotoAsync(BuildRoute(appUrl, "/agents/workflows"), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 45_000
        });
        await DismissDatabaseProfileDialogAsync(page);
        await page.GetByTestId("workflows-tabs").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });

        await ClickTabAsync(page, "Workflows");
        var catalog = page.GetByTestId("workflows-catalog");
        await catalog.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        var definitionEntry = catalog.GetByText(workflowDefinition.Name, new LocatorGetByTextOptions { Exact = false }).First;
        await definitionEntry.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await definitionEntry.EvaluateAsync("element => (element.closest('button') || element).click()");
        await page.GetByTestId("workflows-detail")
            .GetByText(workflowDefinition.Name, new LocatorGetByTextOptions { Exact = false })
            .First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });

        await ClickTabAsync(page, "Editor");
        var editor = page.GetByTestId("workflow-canvas-editor");
        await editor.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        var selectionWindow = page.GetByTestId("workflow-canvas-selection-window");
        if (!await selectionWindow.IsVisibleAsync())
        {
            await page.GetByTestId("workflow-canvas-toggle-selection").ClickAsync();
        }

        await selectionWindow.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputRoot, $"workflow-selection-window-{viewport.Name}.png"),
            FullPage = true
        });
        await WritePageTextAsync(outputRoot, $"workflow-selection-window-{viewport.Name}.txt", page);

        var nodeButtons = page.GetByTestId("workflow-canvas-select-node");
        var nodeTexts = await nodeButtons.AllInnerTextsAsync();
        var executorNode = nodeButtons
            .Filter(new LocatorFilterOptions { HasTextString = ExecutorNodeName })
            .First;
        if (!await TryWaitForAsync(executorNode, 10_000))
        {
            throw new InvalidOperationException(
                $"Could not find workflow executor node '{ExecutorNodeName}'. Selection nodes: {string.Join(" | ", nodeTexts)}");
        }

        await executorNode.ClickAsync();

        await ClickTabAsync(editor, "Node setup");
        await page.GetByTestId("workflow-canvas-executor-preview-commit")
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputRoot, $"workflow-executor-editor-{viewport.Name}.png"),
            FullPage = true
        });
        await WritePageTextAsync(outputRoot, $"workflow-executor-editor-{viewport.Name}.txt", page);

        var editorText = await editor.InnerTextAsync();
        Assert.Contains("Side effects", editorText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Preview", editorText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Commit", editorText, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private static async Task<WorkflowDefinition> SaveWorkflowDefinitionAsync(string appUrl)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(appUrl.TrimEnd('/') + "/")
        };

        var response = await client.PostAsJsonAsync(
            "/api/workflows/definitions",
            BuildWorkflowDefinitionSaveRequest());
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Workflow definition save failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        return await response.Content.ReadFromJsonAsync<WorkflowDefinition>() ??
            throw new InvalidOperationException("Workflow definition save returned no payload.");
    }

    private static WorkflowDefinitionSaveRequest BuildWorkflowDefinitionSaveRequest()
    {
        var settingsJson = JsonSerializer.Serialize(
            new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.WriteText,
                Path = "artifacts/scenario08-observability-proof.txt",
                Content = "Scenario08 executor preview/commit observability proof.",
                Overwrite = true,
                DryRun = true
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: $"Scenario08 executor observability proof {DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            Description: "Browser proof workflow containing a storage executor node so preview/commit and side-effect badges render in the workflow editor.",
            Status: WorkflowLifecycleStatus.Active,
            Graph: new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateWorkflowNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateExecutorWorkflowNode("scenario08-proof-executor", settingsJson),
                    CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    CreateWorkflowEdge("start-to-executor", "start", "scenario08-proof-executor"),
                    CreateWorkflowEdge("executor-to-end", "scenario08-proof-executor", "end")
                ]),
            RuntimePolicy: new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static WorkflowNode CreateWorkflowNode(
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

    private static WorkflowNode CreateExecutorWorkflowNode(string id, string executorSettingsJson)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            ExecutorNodeName,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Write a deterministic Scenario08 proof artifact during committed workflow execution.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text) with
            {
                ExecutorId = WorkflowExecutorIds.StorageFile,
                ExecutorSettingsJson = executorSettingsJson,
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
                {
                    CaptureOutputArtifact = true,
                    TimeoutSeconds = 45
                }
            });
    }

    private static WorkflowEdge CreateWorkflowEdge(string id, string source, string target)
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

    private static async Task DismissDatabaseProfileDialogAsync(IPage page)
    {
        var closeButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Close" }).First;
        if (!await TryWaitForAsync(closeButton, 2_000))
        {
            return;
        }

        await closeButton.ClickAsync();
        await page.WaitForFunctionAsync(
            """
            () => !Array.from(document.querySelectorAll('[role="dialog"]'))
                .some(dialog => /Database profiles/i.test(dialog.textContent || ''))
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    private static async Task ClickTabAsync(IPage page, string name)
    {
        var tab = page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = name }).First;
        if (await TryWaitForAsync(tab, 2_000))
        {
            await tab.ClickAsync();
            return;
        }

        await page.GetByText(name, new PageGetByTextOptions { Exact = true }).First.ClickAsync();
    }

    private static async Task ClickTabAsync(ILocator scope, string name)
    {
        var tab = scope.GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions { Name = name }).First;
        if (await TryWaitForAsync(tab, 2_000))
        {
            await tab.ClickAsync();
            return;
        }

        await scope.GetByText(name, new LocatorGetByTextOptions { Exact = true }).First.ClickAsync();
    }

    private static void AttachDiagnostics(IPage page, Scenario08BrowserDiagnostics diagnostics)
    {
        page.Console += (_, message) =>
        {
            var text = $"{message.Type}: {message.Text}";
            diagnostics.ConsoleMessages.Add(text);
            if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.ConsoleErrors.Add(text);
            }
        };
        page.PageError += (_, error) => diagnostics.PageErrors.Add(error);
        page.RequestFailed += (_, request) => diagnostics.FailedRequests.Add($"{request.Failure} {request.Url}");
        page.Response += (_, response) =>
        {
            diagnostics.NetworkResponses.Add($"{response.Status} {response.Url}");
            if (response.Status >= 400)
            {
                diagnostics.FailedResponses.Add($"{response.Status} {response.Url}");
            }
        };
    }

    private static async Task WriteDiagnosticsAsync(
        string outputRoot,
        Scenario08ViewportSpec viewport,
        Scenario08BrowserDiagnostics diagnostics)
    {
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-console-{viewport.Name}.txt"), diagnostics.ConsoleMessages);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-console-errors-{viewport.Name}.txt"), diagnostics.ConsoleErrors);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-page-errors-{viewport.Name}.txt"), diagnostics.PageErrors);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-network-{viewport.Name}.txt"), diagnostics.NetworkResponses);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-failed-responses-{viewport.Name}.txt"), diagnostics.FailedResponses);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-failed-requests-{viewport.Name}.txt"), diagnostics.FailedRequests);

        var actionableFailedRequests = diagnostics.FailedRequests
            .Where(request => !IsExpectedBrowserShutdownRequest(request))
            .ToArray();

        Assert.Empty(diagnostics.ConsoleErrors);
        Assert.Empty(diagnostics.PageErrors);
        Assert.DoesNotContain(diagnostics.FailedResponses, response => response.StartsWith("5", StringComparison.Ordinal));
        Assert.Empty(actionableFailedRequests);
    }

    private static bool IsExpectedBrowserShutdownRequest(string failedRequest)
        => failedRequest.Contains("/_blazor/disconnect", StringComparison.OrdinalIgnoreCase)
            && failedRequest.Contains("ERR_ABORTED", StringComparison.OrdinalIgnoreCase);

    private static async Task WritePageTextAsync(string outputRoot, string fileName, IPage page)
    {
        await File.WriteAllTextAsync(
            Path.Combine(outputRoot, fileName),
            await page.Locator("body").InnerTextAsync());
    }

    private static async Task WriteSummaryAsync(
        string outputRoot,
        IReadOnlyCollection<Scenario08ViewportProofResult> results)
    {
        await File.WriteAllTextAsync(
            Path.Combine(outputRoot, "browser-validation-summary.json"),
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<bool> TryWaitForAsync(ILocator locator, float timeout)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeout,
                State = WaitForSelectorState.Visible
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (PlaywrightException exception) when (exception.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    private static string BuildRoute(string appUrl, string relativePath)
        => $"{appUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";

    private static void ResetDirectory(string path)
    {
        Directory.CreateDirectory(path);
        var directory = new DirectoryInfo(path);
        foreach (var file in directory.EnumerateFiles())
        {
            if (file.Name.StartsWith("web-host.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            file.Delete();
        }

        foreach (var childDirectory in directory.EnumerateDirectories())
        {
            childDirectory.Delete(recursive: true);
        }
    }

    private sealed record Scenario08ViewportSpec(
        string Name,
        int Width,
        int Height,
        bool IsMobile)
    {
        public static IReadOnlyList<Scenario08ViewportSpec> All { get; } =
        [
            new("desktop", 1440, 1000, false),
            new("mobile", 390, 844, true)
        ];
    }

    private sealed record Scenario08ViewportProofResult(
        string Viewport,
        int Width,
        int Height,
        bool ProcessDetailCaptured,
        bool WorkflowExecutorCaptured,
        int ConsoleMessageCount,
        int ConsoleErrorCount,
        int PageErrorCount,
        int FailedResponseCount,
        int FailedRequestCount,
        DateTimeOffset CapturedAtUtc);

    private sealed class Scenario08BrowserDiagnostics
    {
        public List<string> ConsoleMessages { get; } = [];

        public List<string> ConsoleErrors { get; } = [];

        public List<string> PageErrors { get; } = [];

        public List<string> NetworkResponses { get; } = [];

        public List<string> FailedResponses { get; } = [];

        public List<string> FailedRequests { get; } = [];
    }
}
