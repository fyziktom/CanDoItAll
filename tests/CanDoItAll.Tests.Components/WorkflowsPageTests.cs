using Bunit;
using AngleSharp.Dom;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tools.Documents;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
            var dialog = cut.Find("[data-testid='workflows-run-detail-dialog']");
            Assert.Contains("Summary", dialog.TextContent);
            Assert.Contains("Result", dialog.TextContent);
            Assert.Contains("Workflow LLM test output", dialog.TextContent);
            Assert.Contains("Events", dialog.TextContent);
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
    public async Task Workflow_canvas_preview_prompts_for_project_context_and_can_skip_project_writes()
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var runner = new CapturingWorkflowTestRunner();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IWorkflowTestRunner>();
            services.AddSingleton<IWorkflowTestRunner>(runner);
            services.RemoveAll<IProjectStructureRuntimeGateway>();
            services.AddSingleton<IProjectStructureRuntimeGateway>(new PreviewProjectGateway(projectId));
        });
        var definition = CreateProjectStructurePreviewDefinition();

        var cut = harness.Context.RenderComponent<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components, [])
            .Add(component => component.ProviderOptions, []));

        cut.WaitForElement("[data-testid='workflow-canvas-run-preview']");
        cut.Find("[data-testid='workflow-canvas-run-preview']").Click();

        cut.WaitForElement("[data-testid='workflow-canvas-preview-input-dialog']");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project structure preview target", cut.Markup);
            Assert.Contains(projectId.ToString("D"), cut.Find("[data-testid='workflow-canvas-preview-project-id']").GetAttribute("value"));
        });

        cut.Find("[data-testid='workflow-canvas-preview-node-id']").Change("custom:test-parent-node");
        cut.Find("[data-testid='workflow-canvas-preview-simulate-store']").Change(true);
        cut.Find("[data-testid='workflow-canvas-preview-input-run']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(runner.LastRequest);
            Assert.Contains("Succeeded", cut.Find("[data-testid='workflow-canvas-test-result']").TextContent);
        });

        Assert.NotNull(runner.LastRequest);
        using var inputDocument = System.Text.Json.JsonDocument.Parse(runner.LastRequest!.InputJson);
        Assert.Equal(projectId.ToString("D"), inputDocument.RootElement.GetProperty("projectId").GetString());
        Assert.Equal(projectId.ToString("D"), inputDocument.RootElement.GetProperty("project").GetProperty("id").GetString());
        Assert.Equal("custom:test-parent-node", inputDocument.RootElement.GetProperty("nodeId").GetString());
        Assert.Equal("custom:test-parent-node", inputDocument.RootElement.GetProperty("runContext").GetProperty("workflowNodeId").GetString());
        var storeNode = Assert.Single(runner.LastRequest.DraftDefinition!.Graph.Nodes, node => node.Id.Value == "store");
        Assert.Equal(WorkflowNodeKind.Executor, storeNode.Kind);
        Assert.Equal(WorkflowExecutorIds.ProjectStructure, storeNode.Settings.ExecutorId);
        var simulatedStep = Assert.Single(runner.LastRequest.PreviewSimulationPlan.Steps);
        Assert.Equal(storeNode.Id, simulatedStep.NodeId);
        Assert.Equal(WorkflowExecutorIds.ProjectStructure, simulatedStep.SourceExecutorId);
        Assert.Contains("inputPayload", simulatedStep.OutputTemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_canvas_reconnects_linear_route_after_delete_and_accepts_canvas_connections()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.Find("[data-testid='workflow-canvas-toggle-components']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-create-component']");
        cut.Find("[data-testid='workflow-canvas-create-component']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "LLM component created");
        });

        cut.Find("[data-testid='workflow-canvas-place-component']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Single(
                cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
                node => node.Kind == WorkflowNodeKind.LlmCall.ToString());
        });
        cut.Find("[data-testid='workflow-canvas-place-component']").Click();
        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.Equal(CanvasWorkbenchModes.Authoring, surface.Mode);
            Assert.Equal(2, surface.Nodes.Count(node => node.Kind == WorkflowNodeKind.LlmCall.ToString()));
            Assert.Contains(surface.Links, link => link.SourceId == "start" && link.TargetId == "llm");
            Assert.Contains(surface.Links, link => link.SourceId == "llm" && link.TargetId == "llm-1");
            Assert.Contains(surface.Links, link => link.SourceId == "llm-1" && link.TargetId == "end");
        });

        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction("llm", "workflow-node:remove", 0, 0));

        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.DoesNotContain(surface.Nodes, node => node.Id == "llm");
            Assert.Contains(surface.Links, link => link.SourceId == "start" && link.TargetId == "llm-1");
            Assert.DoesNotContain(surface.Links, link => link.SourceId == "llm" || link.TargetId == "llm");
        });

        var deleteRequest = new CanvasWorkbenchContextActionRequest(
            NodeId: "llm-1",
            ActionId: "delete-link",
            X: 0,
            Y: 0,
            TargetKind: "link",
            LinkSourceId: "start",
            LinkTargetId: "llm-1",
            LinkKind: "Always",
            LinkSourcePortId: "workflow:output",
            LinkTargetPortId: "workflow:input");
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextActionRequest(SerializationPersistencePack.Serialize(deleteRequest)));

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain(
                cut.FindComponent<CanvasWorkbench>().Instance.Surface.Links,
                link => link.SourceId == "start" && link.TargetId == "llm-1");
        });

        var createRequest = deleteRequest with
        {
            ActionId = "connection:create"
        };
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextActionRequest(SerializationPersistencePack.Serialize(createRequest)));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                cut.FindComponent<CanvasWorkbench>().Instance.Surface.Links,
                link => link.SourceId == "start" && link.TargetId == "llm-1");
        });

        cut.Find("[data-testid='workflow-canvas-validate']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
            Assert.DoesNotContain("workflow-canvas-validation-issue", cut.Markup);
        });
    }

    [Fact]
    public async Task Workflow_canvas_authors_typed_predicate_route_metadata()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.Find("[data-testid='workflow-canvas-toggle-components']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-create-component']");
        cut.Find("[data-testid='workflow-canvas-create-component']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "LLM component created");
        });
        cut.Find("[data-testid='workflow-canvas-place-component']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-edit-edge']");

        cut.Find("[data-testid='workflow-canvas-edit-edge']").Click();
        cut.Find("[data-testid='workflow-canvas-edge-route-kind']").Change(WorkflowRouteKind.Predicate.ToString());
        cut.WaitForElement("[data-testid='workflow-canvas-edge-route-json-path']");
        cut.Find("[data-testid='workflow-canvas-edge-route-label']").Change("High value");
        cut.Find("[data-testid='workflow-canvas-edge-route-json-path']").Change("$.invoice.total");
        cut.Find("[data-testid='workflow-canvas-edge-route-operator']").Change(WorkflowRouteOperator.GreaterThanOrEqual.ToString());
        cut.Find("[data-testid='workflow-canvas-edge-route-value-kind']").Change(WorkflowRouteValueKind.Number.ToString());
        cut.Find("[data-testid='workflow-canvas-edge-route-expected-value']").Change("5000");
        cut.Find("[data-testid='workflow-canvas-add-edge']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("High value", cut.Find("[data-testid='workflow-canvas-edge-route-summary']").TextContent);
            Assert.Contains("$.invoice.total", cut.Find("[data-testid='workflow-canvas-edge-route-summary']").TextContent);
        });

        cut.Find("[data-testid='workflow-canvas-validate']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
            Assert.DoesNotContain("workflow-canvas-validation-issue", cut.Markup);
        });
    }

    [Fact]
    public async Task Workflow_canvas_decision_context_action_adds_and_edits_routes_in_node_dialog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");

        var request = new CanvasWorkbenchCreateActionRequest(
            "workflow-decision:create:Switch",
            SourceNodeId: "start",
            X: 420,
            Y: 220,
            ParentNodeId: "start",
            Title: "SWITCH",
            Subtitle: string.Empty,
            Notes: "Route by workflow category.",
            PlacementKind: "child",
            CreateMode: "dialog",
            ObjectSubtype: "Switch",
            UploadedFile: null,
            InputValues:
            [
                new CanvasWorkbenchInputValue { Key = "jsonPath", Value = "$.route" },
                new CanvasWorkbenchInputValue { Key = "caseValues", Value = "alpha, beta" },
                new CanvasWorkbenchInputValue { Key = "defaultLabel", Value = "DEFAULT" }
            ]);

        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnCreateAction(SerializationPersistencePack.Serialize(request)));

        cut.WaitForAssertion(() =>
        {
            var switchNode = cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Title == "SWITCH");
            Assert.Contains(switchNode.ContextActions, action =>
                action.Children.Any(child => child.ActionId == "workflow-decision:add-route"));
        });

        var nodeId = cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes.Single(node => node.Title == "SWITCH").Id;
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(nodeId, "workflow-decision:add-route", 0, 0));

        cut.WaitForElement("[data-testid='workflow-canvas-decision-route-editor']");
        cut.Find("[data-testid='workflow-canvas-decision-route-label']").Change("Case Gamma");
        cut.Find("[data-testid='workflow-canvas-decision-route-expected-value']").Change("gamma");
        cut.Find("[data-testid='workflow-canvas-decision-save-route']").Click();

        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.Contains(surface.Links, link => link.Label == "Case Gamma");
            Assert.Contains(surface.Nodes, node => node.Title == "Case Gamma");
            Assert.Contains("4 route(s)", cut.Markup);
        });

        cut.FindAll("[data-testid='workflow-canvas-decision-edit-route']").First().Click();
        cut.WaitForElement("[data-testid='workflow-canvas-decision-route-editor']");
        cut.Find("[data-testid='workflow-canvas-decision-route-label']").Change("Case Alpha Updated");
        cut.Find("[data-testid='workflow-canvas-decision-save-route']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Links, link => link.Label == "Case Alpha Updated");
            Assert.Contains("Case Alpha Updated", cut.Markup);
        });
    }

    [Fact]
    public async Task Workflow_example_seed_creates_production_examples_when_enabled()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"workflow-example-seed-{Guid.NewGuid():N}");
        var store = new InMemoryWorkflowCatalogStore();
        var catalogService = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var templatePack = new WorkflowTemplatePackLoader().Load();
        var seeder = new WorkflowExampleCatalogSeedService(
            catalogService,
            catalogService,
            catalogService,
            new WorkspaceFileService(workspaceRoot),
            new WorkspacePathResolutionService(workspaceRoot),
            new ClosedXmlSpreadsheetDocumentService(),
            Options.Create(new WorkflowExampleCatalogSeedOptions
            {
                Enabled = true,
                SeedSampleWorkspaceFiles = true
            }),
            NullLogger<WorkflowExampleCatalogSeedService>.Instance);

        try
        {
            await seeder.EnsureSeededAsync();
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        var definitions = await catalogService.ListDefinitionsAsync();
        var examples = definitions
            .Where(item => item.Name.StartsWith("Example:", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(templatePack.Workflows.Count, examples.Length);

        var components = await catalogService.ListComponentsAsync();
        Assert.Equal(templatePack.Workflows.Count, components.Count(component => component.Name.StartsWith("Example LLM:", StringComparison.Ordinal)));
        foreach (var example in examples)
        {
            var detail = await catalogService.GetDefinitionAsync(example.Id);
            Assert.NotNull(detail);
            Assert.True(detail!.Validation.Succeeded, string.Join("; ", detail.Validation.Issues.Select(issue => issue.Message)));
        }

        var invoice = Assert.Single(examples, item => item.Name == "Example: Invoice Workbook Risk Switch");
        var invoiceDetail = await catalogService.GetDefinitionAsync(invoice.Id);
        Assert.NotNull(invoiceDetail);
        Assert.True(invoiceDetail!.Validation.Succeeded);
        Assert.Contains(invoiceDetail.Definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);

        var fanOut = Assert.Single(examples, item => item.Name == "Example: Pipeline Workbook Fan-out");
        var fanOutDetail = await catalogService.GetDefinitionAsync(fanOut.Id);
        Assert.NotNull(fanOutDetail);
        Assert.True(fanOutDetail!.Validation.Succeeded);
        Assert.Contains(fanOutDetail.Definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.FanOutSelector);

        var internet = Assert.Single(examples, item => item.Name == "Example: Internet Research Capture");
        var internetDetail = await catalogService.GetDefinitionAsync(internet.Id);
        Assert.NotNull(internetDetail);
        Assert.True(internetDetail!.Validation.Succeeded);
        Assert.Contains(internetDetail.Definition.Graph.Nodes, node =>
            node.Settings.ExecutorId == WorkflowExecutorIds.HttpFetch &&
            node.Settings.ExecutorSettingsJson.Contains("urlJsonPath", StringComparison.Ordinal));
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

    private static WorkflowDefinition CreateProjectStructurePreviewDefinition()
    {
        var start = new WorkflowNodeId("start");
        var store = new WorkflowNodeId("store");
        var end = new WorkflowNodeId("end");
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Project structure preview target",
            "Workflow used to verify preview input prompting.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    new WorkflowNode(
                        store,
                        WorkflowNodeKind.Executor,
                        "Store preview output",
                        [],
                        new WorkflowNodeSettings(
                            ComponentId: null,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: "Create a project asset during preview.",
                            InputShape: WorkflowValueShape.Text,
                            ResultShape: WorkflowValueShape.Text)
                        {
                            ExecutorId = WorkflowExecutorIds.ProjectStructure,
                            ExecutorSettingsJson = "{\"operation\":\"CreateAsset\",\"title\":\"Preview artifact\",\"contentFromInput\":true}"
                        }),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-store"),
                        start,
                        SourcePortId: null,
                        store,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("store-to-end"),
                        store,
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
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
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

    private sealed class CapturingWorkflowTestRunner : IWorkflowTestRunner
    {
        public WorkflowTestRunRequest? LastRequest { get; private set; }

        public Task<WorkflowTestRunResult> RunAsync(
            WorkflowTestRunRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var definition = request.DraftDefinition ?? CreateProjectStructurePreviewDefinition();
            var now = DateTimeOffset.UtcNow;
            var run = new WorkflowRunSnapshot(
                WorkflowRunId.New(),
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                WorkflowRuntimeBackendKind.InProcess,
                BackendRunId: "captured-preview",
                Summary: "Captured preview completed.",
                now,
                now);
            return Task.FromResult(new WorkflowTestRunResult(
                Succeeded: true,
                WorkflowValidationResult.Success,
                run,
                Events: [],
                Artifacts: [],
                PendingExternalRequests: [],
                ErrorMessage: string.Empty));
        }
    }

    private sealed class PreviewProjectGateway(Guid projectId) : IProjectStructureRuntimeGateway
    {
        public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectStructureRuntimeProjectSummary>>(
            [
                new ProjectStructureRuntimeProjectSummary(
                    projectId,
                    "Project structure preview target",
                    ProjectStructureRuntimeProjectStatus.Active,
                    CurrentPhase: "Execution",
                    PhaseCount: 1,
                    ParentCount: 0,
                    ChildCount: 0,
                    DateTimeOffset.UtcNow)
            ]);

        public Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(
            Guid projectId,
            ProjectStructureRuntimeReadRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Preview gateway only lists projects.");

        public Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(
            Guid projectId,
            ProjectStructureRuntimeNodeCreateRequest request,
            ProjectStructureRuntimeAgentContext agent,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Preview gateway only lists projects.");

        public Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(
            Guid projectId,
            ProjectStructureRuntimeAssetCreateRequest request,
            ProjectStructureRuntimeAgentContext agent,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Preview gateway only lists projects.");
    }
}
