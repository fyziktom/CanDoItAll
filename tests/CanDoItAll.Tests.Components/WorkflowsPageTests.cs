using Bunit;
using AngleSharp.Dom;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components;

public sealed class WorkflowsPageTests
{
    [Fact]
    public async Task Workflows_page_creates_starter_workflow_and_runs_preview()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var componentLibrary = harness.Context.Services.GetRequiredService<IWorkflowComponentLibraryService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-create-starter']");
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("disabled", cut.Find("[data-testid='workflows-create-starter']").OuterHtml, StringComparison.OrdinalIgnoreCase);
        });

        cut.Find("[data-testid='workflows-create-starter']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow created");
        });
        Assert.Single(await catalogService.ListDefinitionsAsync());
        var component = Assert.Single(await componentLibrary.ListComponentsAsync());
        var defaultProvider = (await componentLibrary.ListProviderOptionsAsync()).FirstOrDefault(option => option.IsEnabled);
        if (defaultProvider is not null)
        {
            Assert.Equal(defaultProvider.ProviderProfileId, component.ProviderProfileId);
            var expectedModel = string.IsNullOrWhiteSpace(defaultProvider.DefaultModel)
                ? defaultProvider.ModelOptions.FirstOrDefault() ?? "gpt-5.4"
                : defaultProvider.DefaultModel;
            Assert.Equal(expectedModel, component.Model);
        }

        cut.Find("[data-testid='workflows-tab-processes']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='workflows-catalog-item']"));
        });

        cut.Find("[data-testid='workflows-tab-history']").Click();
        cut.WaitForElement("[data-testid='workflows-run-test']");
        cut.Find("[data-testid='workflows-run-test']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow test completed");
            Assert.Contains("Succeeded", cut.Find("[data-testid='workflows-test-result']").TextContent);
            Assert.NotEmpty(cut.FindAll("[data-testid='workflows-run-event']"));
        });
        Assert.Single(await runStore.ListRunsAsync());
    }

    [Fact]
    public async Task Workflow_canvas_places_llm_component_validates_runs_and_saves_definition()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.Find("[data-testid='workflow-canvas-toggle-components']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-provider-options']");
        cut.Find("[data-testid='workflow-canvas-create-component']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "LLM component created");
            Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-component']"));
        });
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("disabled", cut.Find("[data-testid='workflow-canvas-validate']").OuterHtml, StringComparison.OrdinalIgnoreCase);
        });

        cut.Find("[data-testid='workflow-canvas-place-component']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("LLM call", cut.Markup);
            Assert.NotEmpty(cut.FindAll("[data-testid='workflow-canvas-edge-row']"));
        });

        cut.Find("[data-testid='workflow-canvas-node-instructions']").Change("Return a concise workflow canvas test summary.");
        cut.Find("[data-testid='workflow-canvas-validate']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
            Assert.DoesNotContain("workflow-canvas-validation-issue", cut.Markup);
        });

        cut.Find("[data-testid='workflow-canvas-run-preview']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow preview completed");
            Assert.Contains("Succeeded", cut.Find("[data-testid='workflow-canvas-test-result']").TextContent);
        });
        Assert.Single(await runStore.ListRunsAsync());

        cut.Find("[data-testid='workflow-canvas-save']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow saved");
        });

        cut.Find("[data-testid='workflows-tab-processes']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='workflows-catalog-item']"));
        });

        var definition = Assert.Single(await catalogService.ListDefinitionsAsync());
        var detail = await catalogService.GetDefinitionAsync(definition.Id);

        Assert.NotNull(detail);
        Assert.Contains(detail!.Definition.Graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall);
        Assert.Contains(detail.Definition.Graph.Nodes, node => node.CanvasX != 0 && node.CanvasY != 0);
    }

    [Fact]
    public async Task Workflow_history_paginates_runs_and_events_and_moves_full_payload_to_detail_dialog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        var definition = await CreateHistoryDefinitionAsync(catalogService);
        var newestRunId = new WorkflowRunId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < 12; index++)
        {
            var runId = index == 0
                ? newestRunId
                : new WorkflowRunId(Guid.Parse($"00000000-0000-0000-0000-{index:x12}"));
            await runStore.SaveRunAsync(new WorkflowRunSnapshot(
                runId,
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                WorkflowRuntimeBackendKind.InProcess,
                $"history-run-{index}",
                $"History run {index} completed with compact card coverage.",
                now.AddMinutes(-index),
                now.AddMinutes(-index)));
        }

        var hiddenTail = "UNIQUE_FULL_EVENT_TAIL";
        for (var index = 0; index < 11; index++)
        {
            var message = index == 0
                ? $"Executor completed with a long payload summary {new string('x', 180)} {hiddenTail}"
                : $"Executor event {index}";
            await runStore.SaveEventAsync(new WorkflowEventRecord(
                Guid.Parse($"00000000-0000-0000-0000-{index + 1:x12}"),
                newestRunId,
                index % 2 == 0 ? WorkflowEventKind.ExecutorCompleted : WorkflowEventKind.SuperStep,
                new WorkflowNodeId("history-node"),
                message,
                $"{{\"index\":{index},\"marker\":\"payload-{index}\"}}",
                now.AddSeconds(index)));
        }

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-history']");
        cut.Find("[data-testid='workflows-tab-history']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(8, cut.FindAll("[data-testid='workflows-run-item']").Count);
            Assert.Equal(8, cut.FindAll("[data-testid='workflows-run-event']").Count);
            Assert.Contains("Page 1 of 2 - 12 runs", cut.Find("[data-testid='workflows-run-pager']").TextContent);
            Assert.Contains("Page 1 of 2 - 11 events", cut.Find("[data-testid='workflows-event-pager']").TextContent);
            Assert.DoesNotContain(hiddenTail, cut.Markup);
        });

        cut.FindAll("[data-testid='workflows-event-detail']").First().Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflows-event-detail-dialog", cut.Markup);
            Assert.Contains(hiddenTail, cut.Markup);
            Assert.Contains("payload-0", cut.Markup);
        });

        cut.Find("[data-testid='workflows-event-detail-dialog'] button[aria-label='Close']").Click();
        cut.FindAll("[data-testid='workflows-run-detail']").First().Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflows-run-detail-dialog", cut.Markup);
            Assert.Contains("history-run-0", cut.Markup);
        });

        cut.Find("[data-testid='workflows-run-page-next']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Page 2 of 2 - 12 runs", cut.Find("[data-testid='workflows-run-pager']").TextContent);
            Assert.Equal(4, cut.FindAll("[data-testid='workflows-run-item']").Count);
        });
    }

    [Fact]
    public async Task Workflow_canvas_preserves_maximized_state_when_selection_changes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");

        FindButtonByTitle(cut, "Maximize canvas").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.FindComponent<CanvasWorkbench>().Instance.Surface.UiState.IsMaximized);
        });

        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged("end", "[\"end\"]", 1));

        cut.WaitForAssertion(() =>
        {
            var canvas = cut.FindComponent<CanvasWorkbench>().Instance;
            Assert.True(canvas.Surface.UiState.IsMaximized);
            Assert.Equal(new[] { "end" }, canvas.Surface.UiState.SelectedNodeIds);
        });
    }

    [Fact]
    public async Task Agents_shell_exposes_workflows_page_navigation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents");
        var cut = harness.Context.RenderComponent<AgentsHomePage>();

        cut.WaitForElement("[data-testid='agents-shell-open-workflows']");
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Loading canonical agent runtime", cut.Markup);
        });
        cut.Find("[data-testid='agents-shell-open-workflows']").Click();

        Assert.EndsWith("/agents/workflows", navigation.Uri, StringComparison.Ordinal);
    }

    private static void RegisterDeterministicWorkflowLlmInvoker(IServiceCollection services)
    {
        services.RemoveAll<IWorkflowLlmComponentInvoker>();
        services.AddScoped<IWorkflowLlmComponentInvoker, DeterministicWorkflowLlmComponentInvoker>();
    }

    private static IElement FindButtonByTitle(IRenderedFragment cut, string title)
        => cut.FindAll("button")
            .First(button => button.GetAttribute("title")?.Contains(title, StringComparison.Ordinal) == true);

    private static Task<WorkflowDefinition> CreateHistoryDefinitionAsync(IWorkflowCatalogService catalogService)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Paged history workflow",
            Description: "Workflow definition used to verify bounded history paging.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        start,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
    }

    private static WorkflowNode CreateHistoryNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
        => new(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));

    private sealed class DeterministicWorkflowLlmComponentInvoker : IWorkflowLlmComponentInvoker
    {
        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                $"Workflow LLM test output: {input.PayloadJson}",
                component.ResultShape));
        }
    }
}
