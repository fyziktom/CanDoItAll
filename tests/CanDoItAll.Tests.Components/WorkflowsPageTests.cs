using Bunit;
using AngleSharp.Dom;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using CanDoItAll.Tools.Documents;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
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

        var workflowsTab = cut.Find("[data-testid='workflows-tab-workflows']");
        Assert.Contains("Workflows", workflowsTab.TextContent);
        workflowsTab.Click();
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
    public async Task Workflows_page_defers_component_library_until_component_sections_need_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterCountingWorkflowComponentLibrary);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var counter = harness.Context.Services.GetRequiredService<WorkflowComponentLibraryCallCounter>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-create-starter']");
        cut.WaitForElement("[data-testid='workflows-tabs']");

        Assert.Equal(0, counter.ListComponentsCount);
        Assert.Equal(0, counter.ListProviderOptionsCount);

        cut.Find("[data-testid='workflows-tab-editor']").Click();

        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, counter.ListComponentsCount);
            Assert.Equal(1, counter.ListProviderOptionsCount);
        });

        cut.Find("[data-testid='workflows-tab-templates']").Click();

        cut.WaitForElement("[data-testid='workflows-components']");
        Assert.Equal(1, counter.ListComponentsCount);
        Assert.Equal(1, counter.ListProviderOptionsCount);
    }

    [Fact]
    public async Task Workflows_page_loads_full_selected_definition_before_rendering_editor_canvas()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var definition = await CreateCanvasLoadDefinitionAsync(catalogService);
        await CreateHistoryDefinitionAsync(catalogService);

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.DoesNotContain(surface.Nodes, node => node.Id == "work");
        });

        cut.Find("[data-testid='workflows-tab-workflows']").Click();
        cut.WaitForElement("[data-testid='workflows-catalog']");
        cut.FindAll("button")
            .First(button => button.TextContent.Contains(definition.Name, StringComparison.Ordinal))
            .Click();
        cut.Find("[data-testid='workflows-tab-editor']").Click();
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");

        cut.WaitForAssertion(() =>
        {
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.Equal(definition.Graph.Nodes.Count, surface.Nodes.Count);
            Assert.Contains(surface.Nodes, node => node.Id == "work");
            Assert.Contains(surface.Links, link => link.SourceId == "start" && link.TargetId == "work");
            Assert.Contains(surface.Links, link => link.SourceId == "work" && link.TargetId == "end");
        });
    }

    [Fact]
    public async Task Persistent_workflow_catalog_uses_same_latest_version_for_summary_and_detail()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var workflowId = WorkflowId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var oldVersion = new WorkflowVersionId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var latestVersion = new WorkflowVersionId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var oldDefinition = CreateCanvasLoadDefinition(
            workflowId,
            oldVersion,
            "Older tied workflow version",
            includeWorkNode: false,
            timestamp);
        var latestDefinition = CreateCanvasLoadDefinition(
            workflowId,
            latestVersion,
            "Latest tied workflow version",
            includeWorkNode: true,
            timestamp);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<WorkflowDefinitionRecord>().AddRange(
                WorkflowDefinitionRecord.FromDefinition(oldDefinition),
                WorkflowDefinitionRecord.FromDefinition(latestDefinition));
            await dbContext.SaveChangesAsync();
        }

        var summary = Assert.Single(await catalogService.ListDefinitionsAsync(), item => item.Id == workflowId);
        var detail = await catalogService.GetDefinitionAsync(workflowId);

        Assert.NotNull(detail);
        Assert.Equal(summary.VersionId, detail.Definition.VersionId);
        Assert.Equal(summary.Name, detail.Definition.Name);
        Assert.Contains(detail.Definition.Graph.Nodes, node => node.Id.Value == "work");
    }

    [Fact]
    public async Task Workflows_page_defers_runtime_history_until_history_needs_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(RegisterCountingWorkflowRunStore);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        var counter = harness.Context.Services.GetRequiredService<WorkflowRunStoreCallCounter>();
        var definition = await CreateHistoryDefinitionAsync(catalogService);
        var runId = WorkflowRunId.New();
        var now = DateTimeOffset.UtcNow;
        await runStore.SaveRunAsync(new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            "lazy-history-run",
            "Lazy history run should load only after History is selected.",
            now,
            now));
        await runStore.SaveEventAsync(new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.Completed,
            new WorkflowNodeId("lazy-node"),
            "Lazy history event loaded on demand.",
            "{\"loaded\":true}",
            now));
        await runStore.SaveArtifactAsync(new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            runId,
            WorkflowArtifactKind.Text,
            new WorkflowNodeId("lazy-node"),
            "lazy-history.txt",
            "text/plain",
            "workflow-runs/lazy-history/lazy-history.txt",
            "Lazy history artifact.",
            now));
        await runStore.SaveExternalRequestAsync(new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            runId,
            WorkflowExternalRequestKind.Approval,
            new WorkflowNodeId("lazy-node"),
            "approval:lazy-history",
            "{}",
            string.Empty,
            now,
            RespondedAtUtc: null));

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tabs']");

        Assert.Equal(0, counter.ListRunPageCount);
        Assert.Equal(0, counter.GetRunCount);
        Assert.Equal(0, counter.ListEventPageCount);
        Assert.Equal(0, counter.ListArtifactsCount);
        Assert.Equal(0, counter.ListPendingExternalRequestsCount);
        Assert.DoesNotContain("Lazy history run", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='workflows-tab-history']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, counter.ListRunPageCount);
            Assert.Equal(1, counter.GetRunCount);
            Assert.Equal(1, counter.ListEventPageCount);
            Assert.Equal(1, counter.ListArtifactsCount);
            Assert.Equal(1, counter.ListPendingExternalRequestsCount);
            Assert.Single(cut.FindAll("[data-testid='workflows-run-item']"));
            Assert.Single(cut.FindAll("[data-testid='workflows-run-event']"));
            Assert.Contains("Lazy history run", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Workflows_templates_tab_lists_executor_catalog_examples()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-template-page-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-templates']");
        AssertNoWorkflowPageError(cut);
        ClickTabButton(cut, "Templates");
        cut.WaitForElement("[data-testid='workflows-templates']");

        cut.WaitForAssertion(() =>
        {
            var templates = cut.FindAll("[data-testid='workflows-template-pack-item']");
            Assert.Contains(templates, item => item.TextContent.Contains("Local Folder Summary Markdown Report", StringComparison.Ordinal));
            Assert.Contains(templates, item => item.TextContent.Contains("File Diff Markdown Report", StringComparison.Ordinal));
            Assert.Contains(templates, item => item.TextContent.Contains("HTTP Download Document Extraction Report", StringComparison.Ordinal));
            Assert.Contains(templates, item => item.TextContent.Contains("JSON Transform Project Task Creation", StringComparison.Ordinal));
            Assert.Contains(templates, item => item.TextContent.Contains("Approval Gated HTTP Action", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task Workflow_canvas_toolbox_exposes_executor_catalog_metadata()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-canvas-catalog-tests");
        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents/workflows");
        var cut = harness.Context.RenderComponent<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflows-tab-editor']");
        ClickTabButton(cut, "Editor");
        cut.WaitForElement("[data-testid='workflow-canvas-editor']");
        var toolboxSearch = EnsureWorkflowToolboxVisible(cut);

        toolboxSearch.Input("json");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflow-toolbox-executor-json-transform", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Deterministic preview", cut.Markup, StringComparison.Ordinal);
        });

        toolboxSearch.Input("markdown");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflow-toolbox-executor-markdown-render", cut.Markup, StringComparison.Ordinal);
        });

        toolboxSearch.Input("delay");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflow-toolbox-executor-utility-delay", cut.Markup, StringComparison.Ordinal);
        });

        toolboxSearch.Input("http");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Approval required", cut.Markup, StringComparison.Ordinal);
        });

        toolboxSearch.Input("command");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("workflow-toolbox-executor-command-process", cut.Markup, StringComparison.Ordinal);
            Assert.True(cut.Find("[data-testid='workflow-toolbox-executor-command-process']").HasAttribute("disabled"));
        });
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

        cut.Find("[data-testid='workflows-tab-workflows']").Click();
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
    public async Task Workflow_canvas_marks_planned_runtime_backends_unavailable()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var definition = CreatePreviewProgressDefinition();

        var cut = harness.Context.RenderComponent<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components, [])
            .Add(component => component.ProviderOptions, []));

        var runtimeSelect = cut.Find("[data-testid='workflow-canvas-runtime']");
        var durableOption = Assert.Single(
            runtimeSelect.QuerySelectorAll("option"),
            option => string.Equals(option.GetAttribute("value"), nameof(WorkflowRuntimeBackendKind.DurableTask), StringComparison.Ordinal));

        Assert.True(durableOption.HasAttribute("disabled"));
        Assert.Contains("Planned", durableOption.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not registered", durableOption.GetAttribute("title"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Workflow_canvas_stats_count_workflow_node_usages_not_available_inventory()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var usedComponent = CreateWorkflowComponent("Used summary call");
        var definition = CreateWorkflowUsageStatsDefinition(usedComponent.Id);

        var cut = harness.Context.RenderComponent<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components,
            [
                usedComponent,
                CreateWorkflowComponent("Unused research call"),
                CreateWorkflowComponent("Unused validation call")
            ])
            .Add(component => component.ProviderOptions, []));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workflow usage stats target", cut.Markup, StringComparison.Ordinal);
            var stats = cut.FindAll(".cw-stage-stats .cw-stat-chip")
                .ToDictionary(
                    chip => chip.QuerySelector("span")!.TextContent.Trim(),
                    chip => chip.QuerySelector("strong")!.TextContent.Trim());

            Assert.Equal("6", stats["Nodes"]);
            Assert.Equal("5", stats["Edges"]);
            Assert.Equal("2", stats["Components"]);
            Assert.Equal("2", stats["Executors"]);
            Assert.Equal("Valid", stats["Validation"]);
        });
    }

    [Fact]
    public async Task Workflow_canvas_preview_selects_running_node_from_progress()
    {
        var runner = new NodeProgressWorkflowTestRunner(new WorkflowNodeId("work"));
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IWorkflowTestRunner>();
            services.AddSingleton<IWorkflowTestRunner>(runner);
        });
        var definition = CreatePreviewProgressDefinition();

        var cut = harness.Context.RenderComponent<WorkflowCanvasEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Components, [])
            .Add(component => component.ProviderOptions, []));

        cut.WaitForElement("[data-testid='workflow-canvas-run-preview']");
        cut.Find("[data-testid='workflow-canvas-run-preview']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(runner.HadProgressObserver);
            Assert.Contains("Succeeded", cut.Find("[data-testid='workflow-canvas-test-result']").TextContent);
            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.Equal(["work"], surface.UiState.SelectedNodeIds);
        });
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
        cut.WaitForElement("[data-testid='workflow-canvas-place-component']");

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
        cut.WaitForElement("[data-testid='workflow-canvas-place-component']");
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

        var folderReport = Assert.Single(examples, item => item.Name == "Example: Local Folder Summary Markdown Report");
        var folderReportDetail = await catalogService.GetDefinitionAsync(folderReport.Id);
        Assert.NotNull(folderReportDetail);
        Assert.True(folderReportDetail!.Validation.Succeeded);
        Assert.Contains(folderReportDetail.Definition.Graph.Nodes, node => node.Settings.ExecutorId == WorkflowExecutorIds.MarkdownRender);

        var taskTransform = Assert.Single(examples, item => item.Name == "Example: JSON Transform Project Task Creation");
        var taskTransformDetail = await catalogService.GetDefinitionAsync(taskTransform.Id);
        Assert.NotNull(taskTransformDetail);
        Assert.True(taskTransformDetail!.Validation.Succeeded);
        Assert.Contains(taskTransformDetail.Definition.Graph.Nodes, node => node.Settings.ExecutorId == WorkflowExecutorIds.JsonTransform);
    }

    [Fact]
    public async Task Workflow_example_seed_preserves_non_managed_definitions_with_template_names()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"workflow-example-seed-preserve-{Guid.NewGuid():N}");
        var store = new InMemoryWorkflowCatalogStore();
        var catalogService = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var templatePack = new WorkflowTemplatePackLoader().Load();
        var template = templatePack.Workflows[0];
        var component = await catalogService.SaveComponentAsync(new LlmCallComponentSaveRequest(
            Id: null,
            Name: "User component",
            ProviderProfileId: null,
            Model: "gpt-5-mini",
            WorkflowModality.Text,
            new WorkflowModelSettings(0.2, 256, RequireJsonOutput: false, ResponseFormatJsonSchema: string.Empty),
            "User-owned workflow component.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default));
        var userDescription = "User-owned workflow. No managed seed marker.";
        var userDefinition = await catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: $"{templatePack.Manifest.DefinitionNamePrefix}{template.Name}",
            Description: userDescription,
            WorkflowLifecycleStatus.Active,
            CreateStarterGraph(component.Id),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
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
                SeedSampleWorkspaceFiles = false
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

        var preserved = await catalogService.GetDefinitionAsync(userDefinition.Id);
        var definitions = await catalogService.ListDefinitionsAsync();

        Assert.NotNull(preserved);
        Assert.Equal(userDescription, preserved!.Definition.Description);
        Assert.DoesNotContain(templatePack.Manifest.SeedMarker, preserved.Definition.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(templatePack.Workflows.Count, definitions.Count(item => item.Name.StartsWith("Example:", StringComparison.Ordinal)));
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

    private static void RegisterCountingWorkflowComponentLibrary(IServiceCollection services)
    {
        services.AddSingleton<WorkflowComponentLibraryCallCounter>();
        services.RemoveAll<IWorkflowComponentLibraryService>();
        services.AddScoped<IWorkflowComponentLibraryService>(serviceProvider => new CountingWorkflowComponentLibraryService(
            serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>(),
            serviceProvider.GetRequiredService<WorkflowComponentLibraryCallCounter>()));
    }

    private static void RegisterCountingWorkflowRunStore(IServiceCollection services)
    {
        services.AddSingleton<WorkflowRunStoreCallCounter>();
        services.RemoveAll<IWorkflowRunStore>();
        services.RemoveAll<IWorkflowArtifactStore>();
        services.RemoveAll<IWorkflowExternalRequestStore>();
        services.RemoveAll<IWorkflowCheckpointStore>();
        services.AddSingleton<CountingWorkflowRunStore>();
        services.AddSingleton<IWorkflowRunStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
        services.AddSingleton<IWorkflowArtifactStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
        services.AddSingleton<IWorkflowExternalRequestStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
        services.AddSingleton<IWorkflowCheckpointStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
    }

    private static Task<ComponentTestHarness> CreateInMemoryWorkflowHarnessAsync(CanDoItAllTestEnvironment environment)
    {
        var profile = environment.CreateInMemoryProfile("primary");
        return ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = environment,
            ActiveProfile = profile,
            SchemaModules = TestSchemaBootstrapModules.Default
        });
    }

    private static IElement FindButtonByTitle(IRenderedFragment cut, string title)
        => cut.FindAll("button")
            .First(button => button.GetAttribute("title")?.Contains(title, StringComparison.Ordinal) == true);

    private static void ClickTabButton(IRenderedFragment cut, string text)
    {
        var button = cut.FindAll("button")
            .First(button => button.TextContent.Contains(text, StringComparison.OrdinalIgnoreCase));
        button.Click();
    }

    private static IElement EnsureWorkflowToolboxVisible(IRenderedFragment cut)
    {
        const string searchSelector = "input[placeholder='Search nodes, executors, files, HTTP, spreadsheets']";
        var inputs = cut.FindAll(searchSelector);
        if (inputs.Count > 0)
        {
            return inputs[0];
        }

        cut.Find("[data-testid='workflow-canvas-toggle-toolbox']").Click();
        return cut.WaitForElement(searchSelector);
    }

    private static void AssertNoWorkflowPageError(IRenderedFragment cut)
    {
        var errors = cut.FindAll("[data-testid='workflows-error']");
        Assert.True(errors.Count == 0, string.Join(" | ", errors.Select(error => error.TextContent.Trim())));
    }

    private static WorkflowDefinition CreateCanvasLoadDefinition(
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        string name,
        bool includeWorkNode,
        DateTimeOffset timestamp)
    {
        var start = new WorkflowNodeId("start");
        var work = new WorkflowNodeId("work");
        var end = new WorkflowNodeId("end");
        var graph = includeWorkNode
            ? new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-work"),
                        start,
                        SourcePortId: null,
                        work,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("work-to-end"),
                        work,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ])
            : new WorkflowGraph(
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
                ]);

        return new WorkflowDefinition(
            workflowId,
            versionId,
            name,
            "Workflow definition used to verify selected editor loading.",
            WorkflowLifecycleStatus.Active,
            graph,
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            timestamp,
            timestamp);
    }

    private static Task<WorkflowDefinition> CreateCanvasLoadDefinitionAsync(IWorkflowCatalogService catalogService)
    {
        var start = new WorkflowNodeId("start");
        var work = new WorkflowNodeId("work");
        var end = new WorkflowNodeId("end");
        return catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Editor full load workflow",
            Description: "Workflow definition used to verify selected editor loading.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-work"),
                        start,
                        SourcePortId: null,
                        work,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("work-to-end"),
                        work,
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

    private static WorkflowDefinition CreatePreviewProgressDefinition()
    {
        var start = new WorkflowNodeId("start");
        var work = new WorkflowNodeId("work");
        var end = new WorkflowNodeId("end");
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Preview progress workflow",
            "Workflow used to verify canvas selection follows preview execution.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-work"),
                        start,
                        SourcePortId: null,
                        work,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("work-to-end"),
                        work,
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

    private static WorkflowDefinition CreateWorkflowUsageStatsDefinition(WorkflowComponentId componentId)
    {
        var start = new WorkflowNodeId("start");
        var firstLlm = new WorkflowNodeId("llm-a");
        var firstExecutor = new WorkflowNodeId("executor-a");
        var secondLlm = new WorkflowNodeId("llm-b");
        var secondExecutor = new WorkflowNodeId("executor-b");
        var end = new WorkflowNodeId("end");
        var now = DateTimeOffset.UtcNow;

        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Workflow usage stats target",
            "Workflow used to verify canvas stat counts.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateLlmUsageNode(firstLlm, componentId),
                    CreateExecutorUsageNode(firstExecutor),
                    CreateLlmUsageNode(secondLlm, componentId),
                    CreateExecutorUsageNode(secondExecutor),
                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    CreateWorkflowEdge("start-to-llm-a", start, firstLlm),
                    CreateWorkflowEdge("llm-a-to-executor-a", firstLlm, firstExecutor),
                    CreateWorkflowEdge("executor-a-to-llm-b", firstExecutor, secondLlm),
                    CreateWorkflowEdge("llm-b-to-executor-b", secondLlm, secondExecutor),
                    CreateWorkflowEdge("executor-b-to-end", secondExecutor, end)
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

    private static WorkflowNode CreateLlmUsageNode(WorkflowNodeId id, WorkflowComponentId componentId)
        => new(
            id,
            WorkflowNodeKind.LlmCall,
            id.Value,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Summarize the current payload.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowNode CreateExecutorUsageNode(WorkflowNodeId id)
        => new(
            id,
            WorkflowNodeKind.Executor,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Store the current payload.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text)
            {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = "{}"
            });

    private static WorkflowEdge CreateWorkflowEdge(
        string id,
        WorkflowNodeId sourceNodeId,
        WorkflowNodeId targetNodeId)
        => new(
            new WorkflowEdgeId(id),
            sourceNodeId,
            SourcePortId: null,
            targetNodeId,
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private static LlmCallComponent CreateWorkflowComponent(string name)
    {
        var now = DateTimeOffset.UtcNow;

        return new LlmCallComponent(
            WorkflowComponentId.New(),
            name,
            ProviderProfileId: null,
            "gpt-5.4",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            "Summarize the input.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default,
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

    private static WorkflowGraph CreateStarterGraph(WorkflowComponentId componentId)
    {
        var start = new WorkflowNodeId("start");
        var llm = new WorkflowNodeId("llm");
        var end = new WorkflowNodeId("end");
        return new WorkflowGraph(
            start,
            [
                CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                new WorkflowNode(
                    llm,
                    WorkflowNodeKind.LlmCall,
                    "LLM",
                    [],
                    new WorkflowNodeSettings(
                        componentId,
                        AgentId: null,
                        SubworkflowId: null,
                        ExternalRequestKind: null,
                        Instructions: "Summarize.",
                        InputShape: WorkflowValueShape.Text,
                        ResultShape: WorkflowValueShape.Text)),
                CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                new WorkflowEdge(
                    new WorkflowEdgeId("start-to-llm"),
                    start,
                    SourcePortId: null,
                    llm,
                    TargetPortId: null,
                    WorkflowEdgeKind.Direct,
                    ConditionExpression: string.Empty),
                new WorkflowEdge(
                    new WorkflowEdgeId("llm-to-end"),
                    llm,
                    SourcePortId: null,
                    end,
                    TargetPortId: null,
                    WorkflowEdgeKind.Direct,
                    ConditionExpression: string.Empty)
            ]);
    }

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

    private sealed class WorkflowComponentLibraryCallCounter
    {
        private int listComponentsCount;
        private int listProviderOptionsCount;

        public int ListComponentsCount => listComponentsCount;

        public int ListProviderOptionsCount => listProviderOptionsCount;

        public void IncrementListComponents()
        {
            Interlocked.Increment(ref listComponentsCount);
        }

        public void IncrementListProviderOptions()
        {
            Interlocked.Increment(ref listProviderOptionsCount);
        }
    }

    private sealed class CountingWorkflowComponentLibraryService(
        IWorkflowComponentLibraryService inner,
        WorkflowComponentLibraryCallCounter counter) : IWorkflowComponentLibraryService
    {
        public async Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(CancellationToken cancellationToken = default)
        {
            counter.IncrementListProviderOptions();
            return await inner.ListProviderOptionsAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(CancellationToken cancellationToken = default)
        {
            counter.IncrementListComponents();
            return await inner.ListComponentsAsync(cancellationToken);
        }

        public Task<LlmCallComponent?> GetComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => inner.GetComponentAsync(componentId, cancellationToken);

        public Task<LlmCallComponent> SaveComponentAsync(
            LlmCallComponentSaveRequest request,
            CancellationToken cancellationToken = default)
            => inner.SaveComponentAsync(request, cancellationToken);

        public Task DeleteComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => inner.DeleteComponentAsync(componentId, cancellationToken);
    }

    private sealed class WorkflowRunStoreCallCounter
    {
        private int getRunCount;
        private int listRunPageCount;
        private int listEventsCount;
        private int listEventPageCount;
        private int listArtifactsCount;
        private int listPendingExternalRequestsCount;

        public int GetRunCount => getRunCount;

        public int ListRunPageCount => listRunPageCount;

        public int ListEventsCount => listEventsCount;

        public int ListEventPageCount => listEventPageCount;

        public int ListArtifactsCount => listArtifactsCount;

        public int ListPendingExternalRequestsCount => listPendingExternalRequestsCount;

        public void IncrementGetRun()
        {
            Interlocked.Increment(ref getRunCount);
        }

        public void IncrementListRunPage()
        {
            Interlocked.Increment(ref listRunPageCount);
        }

        public void IncrementListEvents()
        {
            Interlocked.Increment(ref listEventsCount);
        }

        public void IncrementListEventPage()
        {
            Interlocked.Increment(ref listEventPageCount);
        }

        public void IncrementListArtifacts()
        {
            Interlocked.Increment(ref listArtifactsCount);
        }

        public void IncrementListPendingExternalRequests()
        {
            Interlocked.Increment(ref listPendingExternalRequestsCount);
        }
    }

    private sealed class CountingWorkflowRunStore(WorkflowRunStoreCallCounter counter) :
        IWorkflowRunStore,
        IWorkflowArtifactStore,
        IWorkflowExternalRequestStore
    {
        private readonly InMemoryWorkflowRunStore inner = new();

        public Task SaveRunAsync(
            WorkflowRunSnapshot run,
            CancellationToken cancellationToken = default)
            => inner.SaveRunAsync(run, cancellationToken);

        public Task<WorkflowRunSnapshot?> GetRunAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementGetRun();
            return inner.GetRunAsync(runId, cancellationToken);
        }

        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
            WorkflowId? workflowId = null,
            CancellationToken cancellationToken = default)
            => inner.ListRunsAsync(workflowId, cancellationToken);

        public Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
            WorkflowRunPageRequest request,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListRunPage();
            return inner.ListRunPageAsync(request, cancellationToken);
        }

        public Task SaveEventAsync(
            WorkflowEventRecord workflowEvent,
            CancellationToken cancellationToken = default)
            => inner.SaveEventAsync(workflowEvent, cancellationToken);

        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListEvents();
            return inner.ListEventsAsync(runId, cancellationToken);
        }

        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
            WorkflowEventPageRequest request,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListEventPage();
            return inner.ListEventPageAsync(request, cancellationToken);
        }

        public Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
            WorkflowCheckpointRecord checkpoint,
            CancellationToken cancellationToken = default)
            => inner.SaveCheckpointAsync(checkpoint, cancellationToken);

        public Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
            WorkflowCheckpointId checkpointId,
            CancellationToken cancellationToken = default)
            => inner.GetCheckpointAsync(checkpointId, cancellationToken);

        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => inner.ListCheckpointsAsync(runId, cancellationToken);

        public Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
            WorkflowCheckpointId checkpointId,
            DateTimeOffset resumedAtUtc,
            CancellationToken cancellationToken = default)
            => inner.MarkCheckpointResumedAsync(checkpointId, resumedAtUtc, cancellationToken);

        public Task SaveExternalRequestAsync(
            WorkflowExternalRequestRecord request,
            CancellationToken cancellationToken = default)
            => inner.SaveExternalRequestAsync(request, cancellationToken);

        public Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
            WorkflowExternalRequestId requestId,
            CancellationToken cancellationToken = default)
            => inner.GetExternalRequestAsync(requestId, cancellationToken);

        public Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListPendingExternalRequests();
            return inner.ListPendingExternalRequestsAsync(runId, cancellationToken);
        }

        public Task SaveArtifactAsync(
            WorkflowArtifactRecord artifact,
            CancellationToken cancellationToken = default)
            => inner.SaveArtifactAsync(artifact, cancellationToken);

        public Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            counter.IncrementListArtifacts();
            return inner.ListArtifactsAsync(runId, cancellationToken);
        }

        async Task<WorkflowArtifactRecord> IWorkflowArtifactStore.SaveArtifactAsync(
            WorkflowArtifactRecord artifact,
            CancellationToken cancellationToken)
        {
            await inner.SaveArtifactAsync(artifact, cancellationToken);
            return artifact;
        }

        Task<IReadOnlyList<WorkflowExternalRequestRecord>> IWorkflowExternalRequestStore.ListPendingRequestsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken)
            => ListPendingExternalRequestsAsync(runId, cancellationToken);

        Task<WorkflowExternalRequestRecord> IWorkflowExternalRequestStore.SaveRequestAsync(
            WorkflowExternalRequestRecord request,
            CancellationToken cancellationToken)
            => ((IWorkflowExternalRequestStore)inner).SaveRequestAsync(request, cancellationToken);

        Task<WorkflowExternalRequestRecord> IWorkflowExternalRequestStore.MarkRespondedAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            DateTimeOffset respondedAtUtc,
            CancellationToken cancellationToken)
            => ((IWorkflowExternalRequestStore)inner).MarkRespondedAsync(requestId, responseJson, respondedAtUtc, cancellationToken);
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

    private sealed class NodeProgressWorkflowTestRunner(WorkflowNodeId runningNodeId) : IWorkflowTestRunner
    {
        public bool HadProgressObserver { get; private set; }

        public async Task<WorkflowTestRunResult> RunAsync(
            WorkflowTestRunRequest request,
            CancellationToken cancellationToken = default)
        {
            var definition = request.DraftDefinition ?? CreatePreviewProgressDefinition();
            var observer = WorkflowNodeExecutionProgressScope.Current;
            HadProgressObserver = observer is not null;
            if (observer is not null)
            {
                await observer.RecordAsync(
                    new WorkflowNodeExecutionProgress(
                        definition.Id,
                        definition.VersionId,
                        RunId: null,
                        runningNodeId,
                        WorkflowNodeExecutionProgressState.Started,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
            }

            var now = DateTimeOffset.UtcNow;
            var run = new WorkflowRunSnapshot(
                WorkflowRunId.New(),
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                WorkflowRuntimeBackendKind.InProcess,
                BackendRunId: "progress-preview",
                Summary: "Progress preview completed.",
                now,
                now);
            return new WorkflowTestRunResult(
                Succeeded: true,
                WorkflowValidationResult.Success,
                run,
                Events: [],
                Artifacts: [],
                PendingExternalRequests: [],
                ErrorMessage: string.Empty);
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
