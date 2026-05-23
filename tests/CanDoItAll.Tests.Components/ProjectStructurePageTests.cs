using Bunit;
using AngleSharp.Dom;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using WorkflowDefinition = CanDoItAll.AgentFramework.Models.WorkflowDefinition;
using WorkflowDefinitionSaveRequest = CanDoItAll.AgentFramework.Models.WorkflowDefinitionSaveRequest;
using WorkflowEdge = CanDoItAll.AgentFramework.Models.WorkflowEdge;
using WorkflowEdgeId = CanDoItAll.AgentFramework.Models.WorkflowEdgeId;
using WorkflowEdgeKind = CanDoItAll.AgentFramework.Models.WorkflowEdgeKind;
using WorkflowGraph = CanDoItAll.AgentFramework.Models.WorkflowGraph;
using WorkflowLifecycleStatus = CanDoItAll.AgentFramework.Models.WorkflowLifecycleStatus;
using WorkflowNode = CanDoItAll.AgentFramework.Models.WorkflowNode;
using WorkflowNodeId = CanDoItAll.AgentFramework.Models.WorkflowNodeId;
using WorkflowNodeKind = CanDoItAll.AgentFramework.Models.WorkflowNodeKind;
using WorkflowNodeSettings = CanDoItAll.AgentFramework.Models.WorkflowNodeSettings;
using WorkflowProjectStructureOperation = CanDoItAll.AgentFramework.Models.WorkflowProjectStructureOperation;
using WorkflowRunState = CanDoItAll.AgentFramework.Models.WorkflowRunState;
using WorkflowRuntimeBackendKind = CanDoItAll.AgentFramework.Models.WorkflowRuntimeBackendKind;
using WorkflowRuntimePolicy = CanDoItAll.AgentFramework.Models.WorkflowRuntimePolicy;
using WorkflowValueShape = CanDoItAll.AgentFramework.Models.WorkflowValueShape;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageTests
{
    [Fact]
    public async Task Workflow_start_dialog_renders_project_structure_skip_options()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var changes = new List<ProjectStructureWorkflowStartSimulationChange>();
        var cut = harness.Context.RenderComponent<ProjectStructureCanvasDialogs>(
            parameters => parameters
                .Add(component => component.WorkflowStartDialog, new ProjectStructureWorkflowStartDialogState(
                    "workflow-node-1",
                    "Office365 category email summary",
                    CreateWorkflowStartStatus(),
                    [
                        new ProjectStructureWorkflowPreviewSimulationOption(
                            "store-office365-summary",
                            "Store Office365 summary",
                            "project-structure",
                            WorkflowProjectStructureOperation.CreateAsset,
                            "Skip project-structure asset creation and keep the step input as preview output.")
                    ],
                    WorkflowRuntimeBackendKind.DurableTask,
                    WorkflowRuntimeBackendKind.InProcess,
                    [
                        new ProjectStructureWorkflowStartBackendOption(
                            WorkflowRuntimeBackendKind.InProcess,
                            "InProcess",
                            "Use for local development.",
                            IsSelected: true)
                    ],
                    "Workflow definition prefers DurableTask, but this host has not registered that runtime. Project Structure explicitly requested InProcess for this local start.",
                    [],
                    IsBusy: false,
                    Error: string.Empty))
                .Add(component => component.WorkflowStartSimulationChanged, change => changes.Add(change)));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-workflow-start-dialog", cut.Markup);
            Assert.Contains("Run Preview", cut.Markup);
            Assert.Contains("Store Office365 summary", cut.Markup);
            Assert.Contains("skip project-structure", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Run backend", cut.Markup);
            Assert.Contains("Requested: InProcess", cut.Markup);
            Assert.Contains("Definition: DurableTask", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-workflow-start-simulate-store-office365-summary']").Change(true);

        var change = Assert.Single(changes);
        Assert.Equal("store-office365-summary", change.NodeId);
        Assert.True(change.IsEnabled);
    }

    [Fact]
    public async Task Storage_infrastructure_nodes_render_workspace_storage_summary_in_selection_panel() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var workspaceService = harness.Context.Services.GetRequiredService<WorkspaceService>();
        var storageRoot = Path.Combine(harness.ActiveProfile.WorkspaceRootPath, "storage", "component-assets");
        Directory.CreateDirectory(storageRoot);

        var storageSave = await workspaceService.SaveStorageAsync(new StorageCatalogEditorModel {
            Name = "Project assets storage",
            ProviderKind = StorageProviderKind.FileSystem,
            ConnectionMode = StorageConnectionMode.Local,
            EndpointOrRoot = storageRoot,
            DisplayOrder = 10,
            DefaultPurposes = [StorageUsagePurpose.ProjectAsset]
        });

        Assert.True(storageSave.IsSuccess, string.Join(" | ", storageSave.Errors.Select(error => error.Message)));

        var projectId = await CreateProjectAsync(projectsService, "Storage summary project");
        var metadata = new ProjectObjectMetadataEnvelope {
            Infrastructure = new ProjectInfrastructureMetadata {
                InfrastructureKind = ProjectInfrastructureKind.StorageSystem,
                StoragePurpose = nameof(StorageUsagePurpose.ProjectAsset),
                StoragePathPrefix = "projects/component-tests/assets",
                ConnectionReference = "/storage/assets"
            }
        };

        await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Infrastructure,
                "Project assets lane",
                "Storage infrastructure",
                "Routes editable assets through the workspace storage catalog.",
                $"project:{projectId}",
                460,
                260,
                null,
                null,
                "storage-system",
                null,
                ProjectObjectMetadataSerializer.Serialize(metadata),
                null,
                new ProjectNodeReferenceCollection
                {
                    InfrastructureStorageCatalogId = storageSave.Value
                }));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => {
            Assert.Contains("Project assets lane", cut.Markup);
        });

        FindButtonByLabel(cut, "Project assets lane").Click();

        cut.WaitForAssertion(() => {
            Assert.Contains("project-structure-storage-summary", cut.Markup);
            Assert.Contains("Project assets storage", cut.Markup);
            Assert.Contains("Project assets", cut.Markup);
            Assert.Contains("projects/component-tests/assets", cut.Markup);
            Assert.Contains("/storage/assets", cut.Markup);
            Assert.Contains("File system", cut.Markup);
        });
    }

    [Fact]
    public async Task Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Windowed Structure Project";
        project.Description = "Verify floating workbench windows.";
        project.Objective = "Keep inspector and health in the canvas.";
        project.CurrentPhase = "Validation";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.SeedProjectObjectsAsync(
            projectId,
            [
                new ProjectObjectSeedRequest(
                    ProjectObjectType.Note,
                    "Floating window note",
                    "Window seed",
                    "Exercise the selection window.")
            ]);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Inspector", cut.Markup);
            Assert.Contains("Health", cut.Markup);
            Assert.Contains("Blocks", cut.Markup);
            Assert.Contains("Signals", cut.Markup);
            Assert.Contains("project-structure-selection-window", cut.Markup);
            Assert.DoesNotContain("cw-minimap", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-validation-window", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-toolbox-window", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-signals-window", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-standard-blocks-toolbox", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-signals-toolbox", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("cw-inspector-column", cut.Markup, StringComparison.Ordinal);
        });

        FindButtonByLabel(cut, "Health").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-validation-window", cut.Markup);
            Assert.Contains("Canvas health", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-toolbox-toggle']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-toolbox-window", cut.Markup);
            Assert.Contains("project-structure-standard-blocks-toolbox", cut.Markup);
            Assert.Contains("Project structure toolbox", cut.Markup);
            Assert.Contains("Search the shared block catalog, drag the window where you need it", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-signals-toggle']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-signals-window", cut.Markup);
            Assert.Contains("project-structure-signals-toolbox", cut.Markup);
            Assert.Contains("Signals toolbox", cut.Markup);
            Assert.Contains("Markers", cut.Markup);
            Assert.Contains("Progress", cut.Markup);
            Assert.Contains("Priority", cut.Markup);
        });
    }

    [Fact]
    public async Task Selection_panel_component_skips_rerender_when_the_page_rerenders_without_selection_changes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Selection Render Isolation");
        var note = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Selection node",
                "Render isolation",
                "Keep the panel stable when only the page rerenders.",
                $"project:{projectId}",
                420,
                260));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, note.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Selection node", cut.Markup);
            Assert.Contains("project-structure-selection-window", cut.Markup);
        });

        var selectionPanel = cut.FindComponent<ProjectStructureSelectionPanel>();
        var selectionPanelRenderCount = selectionPanel.RenderCount;

        cut.Render();

        selectionPanel = cut.FindComponent<ProjectStructureSelectionPanel>();
        Assert.Equal(selectionPanelRenderCount, selectionPanel.RenderCount);
    }

    [Fact]
    public async Task Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Offset Health Window Project";
        project.Description = "Verify the default health-window placement.";
        project.Objective = "Keep the toolbox clear on first render.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("project-structure-validation-window", cut.Markup, StringComparison.Ordinal);
        });

        FindButtonByLabel(cut, "Health").Click();

        cut.WaitForAssertion(() =>
        {
            var windows = cut.FindComponents<CanvasFloatingWindow>();
            var healthWindow = Assert.Single(windows, candidate => string.Equals(candidate.Instance.TestId, "project-structure-validation-window", StringComparison.Ordinal));
            Assert.Equal(866d, healthWindow.Instance.State.Left);
            Assert.True(healthWindow.Instance.State.IsVisible);
        });
    }

    [Fact]
    public async Task Renders_shared_structure_workbench_and_updates_inspector_from_outline_selection()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Structure Test Project";
        project.Description = "Project structure coverage";
        project.Objective = "Verify the shared structure canvas page";
        project.CurrentPhase = "Discovery";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.SeedProjectObjectsAsync(
            projectId,
            [
                new ProjectObjectSeedRequest(
                    ProjectObjectType.Note,
                    "Architecture note",
                    "Tracks the first implementation idea",
                    "Shared canvas test note",
                    null,
                    null)
            ]);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Structure canvas", cut.Markup);
            Assert.Contains("Project object index", cut.Markup);
            Assert.Contains("Graph health", cut.Markup);
            Assert.Contains("Architecture note", cut.Markup);
            Assert.DoesNotContain("project-structure-action-catalog-adapter", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-placement-policy", cut.Markup, StringComparison.Ordinal);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Architecture note", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Create next to source", cut.Markup);
            Assert.Contains("Open standard blocks", cut.Markup);
            Assert.Contains("Architecture note", cut.Markup);
            Assert.Contains("Tracks the first implementation idea", cut.Markup);
            Assert.Contains("project-structure-standard-blocks-toolbox", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Open standard blocks", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-standard-blocks-toolbox", cut.Markup);
            Assert.Contains("project-structure-toolbox-group-capture", cut.Markup);
            Assert.Contains("project-structure-toolbox-group-work", cut.Markup);
            Assert.Contains("project-structure-toolbox-group-assets", cut.Markup);
            Assert.DoesNotContain("project-structure-toolbox-group-body-work", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='project-structure-toolbox-group-work']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-toolbox-group-body-work", cut.Markup);
            Assert.Contains(">Task<", cut.Markup);
            Assert.Contains(">Issue<", cut.Markup);
            Assert.Contains("project-structure-toolbox-add-work-task", cut.Markup);
            Assert.DoesNotContain("project-structure-toolbox-group-body-capture", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='project-structure-toolbox-group-assets']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-toolbox-group-body-assets", cut.Markup);
            Assert.DoesNotContain("project-structure-toolbox-group-body-work", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("input.project-structure-toolbox__search").Input("pdf");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-toolbox-group-body-assets", cut.Markup);
            Assert.Contains("project-structure-toolbox-add-file-pdf", cut.Markup);
            Assert.Contains(">PDF<", cut.Markup);
            Assert.Contains("Search blocks, files, runtime, or infrastructure", cut.Markup);
            Assert.DoesNotContain("Unknown icon token", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Copy_actions_write_rich_info_format_to_the_clipboard()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Clipboard Info Project");
        var deploymentNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Main servers",
                "Runtime cluster",
                "Parent node for copy formatting coverage.",
                $"project:{projectId}",
                560,
                220,
                null,
                null,
                "deployment"));
        var taskNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "API rollout",
                "Execution",
                "Child node for tree formatting coverage.",
                deploymentNode.Id,
                860,
                420,
                null,
                null,
                "task"));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Main servers", cut.Markup));

        await InvokeCanvasContextActionAsync(cut, deploymentNode.Id, "copy-info");
        await InvokeCanvasContextActionAsync(cut, deploymentNode.Id, "copy-subtree-ids");

        cut.WaitForAssertion(() =>
        {
            var clipboardWrites = harness.Context.JSInterop.Invocations
                .Where(invocation => string.Equals(invocation.Identifier, "navigator.clipboard.writeText", StringComparison.Ordinal))
                .ToList();

            Assert.Equal(2, clipboardWrites.Count);

            var copiedInfo = Assert.IsType<string>(clipboardWrites[0].Arguments[0]);
            var copiedTree = Assert.IsType<string>(clipboardWrites[1].Arguments[0]);

            Assert.Equal($"deployment_Main-servers:{ExtractNodeHash(deploymentNode.Id)}", copiedInfo);
            Assert.Equal(
                string.Join(
                    Environment.NewLine,
                    [
                        $"deployment_Main-servers:{ExtractNodeHash(deploymentNode.Id)}",
                        $"  task_API-rollout:{ExtractNodeHash(taskNode.Id)}"
                    ]),
                copiedTree);

            Assert.Contains("Main servers tree info was copied.", cut.Markup);
        });
    }

    [Fact]
    public async Task Double_clicking_prompt_flow_nodes_opens_quick_action_modal_and_wizard_new_tab_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Prompt Flow Quick Action";
        project.Description = "Verify prompt-flow double-click behavior.";
        project.Objective = "Keep the canvas open while the wizard opens in a new tab.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var flowNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.PromptFlow,
                "Checkout assistant flow",
                "Prompt orchestration",
                "Drive the checkout assistant prompt flow from the structure canvas.",
                $"project:{projectId}",
                460,
                240));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Checkout assistant flow", cut.Markup));

        var uriBeforeOpen = navigation.Uri;
        await OpenNodeFromCanvasAsync(cut, flowNode.Id);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-node-quick-actions", cut.Markup);
            Assert.Contains("Open Wizard in New Tab", cut.Markup);
            Assert.Contains(">Edit<", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-quick-action-primary']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("project-structure-node-quick-actions", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(
                harness.Context.JSInterop.Invocations,
                invocation => string.Equals(invocation.Identifier, "open", StringComparison.Ordinal));
        });

        var invocation = harness.Context.JSInterop.Invocations
            .Last(candidate => string.Equals(candidate.Identifier, "open", StringComparison.Ordinal));
        var route = Assert.IsType<string>(invocation.Arguments[0]);

        Assert.Contains("/prompt-factory?sessionId=", route, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(flowNode.ArtifactId!.Value.ToString(), route, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(uriBeforeOpen, navigation.Uri);
    }

    [Fact]
    public async Task Double_clicking_projected_process_definition_nodes_opens_the_process_workspace_in_a_new_tab()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var projectId = await CreateProjectAsync(projectsService, "Projected processes project");
        var definitionResult = await processesService.SaveAsync(BuildProcessDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(definitionResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(definitionResult.Value)).IsSuccess);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Workbench-visible process", cut.Markup));

        var uriBeforeOpen = navigation.Uri;
        await OpenNodeFromCanvasAsync(cut, BuildProcessDefinitionNodeKey(definitionResult.Value));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-node-quick-actions", cut.Markup);
            Assert.Contains("Open Processes", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-quick-action-primary']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("project-structure-node-quick-actions", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(
                harness.Context.JSInterop.Invocations,
                invocation => string.Equals(invocation.Identifier, "open", StringComparison.Ordinal));
        });

        var invocation = harness.Context.JSInterop.Invocations
            .Last(candidate => string.Equals(candidate.Identifier, "open", StringComparison.Ordinal));
        var route = Assert.IsType<string>(invocation.Arguments[0]);

        Assert.Contains($"/projects/{projectId}/processes?processId={definitionResult.Value}", route, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(uriBeforeOpen, navigation.Uri);
    }

    [Fact]
    public async Task Workflow_nodes_can_be_added_started_and_inspected_from_project_structure()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var workflowCatalog = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();

        var definition = await CreateComponentWorkflowDefinitionAsync(workflowCatalog, "Canvas workflow proof");
        var projectId = await CreateProjectAsync(projectsService, "Workflow structure project");
        var parentNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "SEAMARK folder",
                "Vendor documents",
                "Folder with xray device PDFs and price lists.",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "vendor-folder"));
        await SaveSelectedNodeStateAsync(workbenchService, projectId, parentNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("SEAMARK folder", cut.Markup);
            Assert.Contains("Add workflow", cut.Markup);
        });

        FindButtonByLabel(cut, "Add workflow").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-workflow-add-dialog", cut.Markup);
            Assert.Contains("Project", cut.Markup);
            Assert.Contains("Parent node", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-workflow-add-select']")
            .Change(definition.Id.Value.ToString("D"));
        cut.Find("[data-testid='project-structure-workflow-add-include-subtree']")
            .Change(true);
        cut.Find("[data-testid='project-structure-workflow-add-source-value']")
            .Input("C:\\programovani\\testdata\\testworkflows\\SEAMARK");
        cut.Find("[data-testid='project-structure-workflow-add-manual-json']")
            .Input("{\"task\":\"summarize-xray-devices\"}");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("SEAMARK", cut.Markup);
            Assert.Contains("summarize-xray-devices", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-workflow-add-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Canvas workflow proof", cut.Markup);
            Assert.Contains("project-structure-workflow-status-card", cut.Markup);
            Assert.Contains("Ready", cut.Markup);
            Assert.Contains("Start workflow", cut.Markup);
        });

        FindButtonByLabel(cut, "Start workflow").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-workflow-start-dialog", cut.Markup);
            Assert.DoesNotContain("project-structure-process-start-dialog", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Use HR manager suggestions", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });

        cut.Find("[data-testid='project-structure-workflow-start-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-workflow-status-card", cut.Markup);
            Assert.Contains("Completed", cut.Markup);
            Assert.Contains("2 / 2", cut.Markup);
            Assert.Contains("100%", cut.Markup);
        });
    }

    [Fact]
    public async Task Double_clicking_project_subproject_nodes_opens_related_structure_in_new_tab()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var currentProjectId = await CreateProjectAsync(projectsService, "Current project");
        var childProjectId = await CreateProjectAsync(projectsService, "Canvas child");

        Assert.True((await projectsService.AddSubprojectAsync(currentProjectId, childProjectId)).IsSuccess);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, currentProjectId));

        cut.WaitForAssertion(() => Assert.Contains("Canvas child", cut.Markup));

        var uriBeforeOpen = navigation.Uri;
        await OpenNodeFromCanvasAsync(cut, BuildProjectChildNodeKey(childProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("project-structure-node-quick-actions", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(
                harness.Context.JSInterop.Invocations,
                invocation => string.Equals(invocation.Identifier, "open", StringComparison.Ordinal));
        });

        var invocation = harness.Context.JSInterop.Invocations
            .Last(candidate => string.Equals(candidate.Identifier, "open", StringComparison.Ordinal));
        var route = Assert.IsType<string>(invocation.Arguments[0]);

        Assert.Contains($"/projects/{childProjectId}/structure", route, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(uriBeforeOpen, navigation.Uri);
    }

    [Fact]
    public async Task Double_clicking_shared_parent_project_nodes_opens_related_structure_in_new_tab()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var currentProjectId = await CreateProjectAsync(projectsService, "Current project");
        var directParentProjectId = await CreateProjectAsync(projectsService, "Direct parent");
        var childProjectId = await CreateProjectAsync(projectsService, "Shared child");
        var sharedParentProjectId = await CreateProjectAsync(projectsService, "Shared parent");

        Assert.True((await projectsService.AddSubprojectAsync(directParentProjectId, currentProjectId)).IsSuccess);
        Assert.True((await projectsService.AddSubprojectAsync(currentProjectId, childProjectId)).IsSuccess);
        Assert.True((await projectsService.AddSubprojectAsync(sharedParentProjectId, childProjectId)).IsSuccess);

        var surface = await workbenchService.GetStructureAsync(currentProjectId);
        var sharedParentNode = Assert.Single(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.AdditionalParentProject &&
            node.RelatedProjectId == sharedParentProjectId);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, currentProjectId));

        cut.WaitForAssertion(() => Assert.Contains("Shared parent", cut.Markup));

        var uriBeforeOpen = navigation.Uri;
        await OpenNodeFromCanvasAsync(cut, sharedParentNode.Id);

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("project-structure-node-quick-actions", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(
                harness.Context.JSInterop.Invocations,
                invocation => string.Equals(invocation.Identifier, "open", StringComparison.Ordinal));
        });

        var invocation = harness.Context.JSInterop.Invocations
            .Last(candidate => string.Equals(candidate.Identifier, "open", StringComparison.Ordinal));
        var route = Assert.IsType<string>(invocation.Arguments[0]);

        Assert.Contains($"/projects/{sharedParentProjectId}/structure", route, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(uriBeforeOpen, navigation.Uri);
    }

    [Fact]
    public async Task Selected_root_project_can_add_subproject_from_the_selection_panel()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var currentProjectId = await CreateProjectAsync(projectsService, "Current project");
        var childProjectId = await CreateProjectAsync(projectsService, "Detached child");

        await SaveSelectedNodeStateAsync(workbenchService, currentProjectId, BuildProjectRootNodeKey(currentProjectId));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, currentProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Add subproject", cut.Markup);
        });

        FindButtonByLabel(cut, "Add subproject").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-hierarchy-dialog", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-hierarchy-project-select']").Change(childProjectId.ToString());
        cut.Find("[data-testid='project-structure-hierarchy-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Detached child is now visible under Current project.", cut.Markup);
        });

        var surface = await workbenchService.GetStructureAsync(currentProjectId);
        Assert.Contains(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.Subproject &&
            node.RelatedProjectId == childProjectId);
    }

    [Fact]
    public async Task Add_subproject_dialog_filters_out_direct_children_and_parent_projects()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();

        var currentProjectId = await CreateProjectAsync(projectsService, "Current project");
        var directChildProjectId = await CreateProjectAsync(projectsService, "Direct child");
        var remainingProjectId = await CreateProjectAsync(projectsService, "Remaining project");

        Assert.True((await projectsService.AddSubprojectAsync(currentProjectId, directChildProjectId)).IsSuccess);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, currentProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Direct child", cut.Markup);
        });

        await InvokeCanvasContextActionAsync(cut, BuildProjectRootNodeKey(currentProjectId), "project:add-subproject");

        cut.WaitForAssertion(() =>
        {
            var options = ReadHierarchyProjectOptions(cut);
            Assert.DoesNotContain(options, option => option.Value == directChildProjectId.ToString());
            Assert.Contains(options, option => option.Value == remainingProjectId.ToString());
        });

        await InvokeCanvasContextActionAsync(cut, BuildProjectChildNodeKey(directChildProjectId), "project:add-subproject");

        cut.WaitForAssertion(() =>
        {
            var options = ReadHierarchyProjectOptions(cut);
            Assert.DoesNotContain(options, option => option.Value == currentProjectId.ToString());
            Assert.Contains(options, option => option.Value == remainingProjectId.ToString());
        });
    }

    [Fact]
    public async Task Hierarchy_dialog_can_create_a_project_and_refresh_the_candidate_list()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();

        var currentProjectId = await CreateProjectAsync(projectsService, "Current project");

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, currentProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Current project structure", cut.Markup);
        });

        await InvokeCanvasContextActionAsync(cut, BuildProjectRootNodeKey(currentProjectId), "project:add-subproject");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-hierarchy-dialog", cut.Markup);
            Assert.Contains("Create new project", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-hierarchy-create-project']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("projects-editor-modal", cut.Markup);
        });

        cut.Find("[data-testid='project-name-input']").Change("Created from hierarchy");
        cut.Find("[data-testid='project-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-hierarchy-dialog", cut.Markup);
            var options = ReadHierarchyProjectOptions(cut);
            Assert.Contains(options, option => string.Equals(option.Label, "Created from hierarchy", StringComparison.Ordinal));
        });

        cut.Find("[data-testid='project-structure-hierarchy-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Created from hierarchy is now visible under Current project.", cut.Markup);
        });
    }

    [Fact]
    public async Task Selected_subproject_can_reconnect_parent_from_the_selection_panel()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var currentProjectId = await CreateProjectAsync(projectsService, "Current parent");
        var childProjectId = await CreateProjectAsync(projectsService, "Reconnect me");
        var newParentProjectId = await CreateProjectAsync(projectsService, "Target parent");

        Assert.True((await projectsService.AddSubprojectAsync(currentProjectId, childProjectId)).IsSuccess);
        await SaveSelectedNodeStateAsync(workbenchService, currentProjectId, BuildProjectChildNodeKey(childProjectId));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, currentProjectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Reconnect parent", cut.Markup);
        });

        FindButtonByLabel(cut, "Reconnect parent").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-hierarchy-dialog", cut.Markup);
            Assert.Contains("Current parent", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-hierarchy-project-select']").Change(newParentProjectId.ToString());
        cut.Find("[data-testid='project-structure-hierarchy-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Reconnect me now belongs to Target parent.", cut.Markup);
        });

        var currentSurface = await workbenchService.GetStructureAsync(currentProjectId);
        Assert.DoesNotContain(currentSurface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.Subproject &&
            node.RelatedProjectId == childProjectId);

        var hierarchy = await projectsService.GetHierarchyAsync(childProjectId);
        Assert.DoesNotContain(hierarchy.ParentProjects, project => project.Id == currentProjectId);
        Assert.Contains(hierarchy.ParentProjects, project => project.Id == newParentProjectId);
    }

    [Fact]
    public async Task Persisted_multi_select_state_renders_common_actions_in_selection_window()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Persisted Multi Select";
        project.Description = "Verify the shared multi-select action surface.";
        project.Objective = "Restore batch actions from saved workbench state.";
        project.CurrentPhase = "Validation";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var feature = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Feature block",
                "Feature cluster",
                "Use for multi-select shared actions.",
                $"project:{projectId}",
                620,
                220,
                null,
                null,
                "feature"));

        var support = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Support block",
                "Support cluster",
                "Use for multi-select shared actions.",
                $"project:{projectId}",
                860,
                360,
                null,
                null,
                "support"));

        await workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = [feature.Id, support.Id],
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = true },
                    ["project-structure.health"] = new CanvasWorkbenchWindowState { IsVisible = true }
                }
            }.ToJson());

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2 nodes selected", cut.Markup);
            Assert.Contains("Grouping", cut.Markup);
            Assert.Contains(">P1<", cut.Markup);
            Assert.Contains(">50%<", cut.Markup);
            Assert.Contains(">Question<", cut.Markup);
            Assert.Contains(">Border<", cut.Markup);
        });
    }

    [Fact]
    public async Task Selected_nodes_with_children_open_summary_modal_and_show_export_actions()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Summary Modal Project";
        project.Description = "Verify the progress summary modal.";
        project.Objective = "Expose summary exports from the selection window.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var feature = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Execution feature",
                "Delivery branch",
                "Use this node as the summary root.",
                $"project:{projectId}",
                520,
                240,
                null,
                null,
                "feature"));

        await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Ship checklist",
                "Ready for release",
                "Confirm the rollout tasks.",
                feature.Id,
                780,
                340,
                new DateTimeOffset(2026, 3, 28, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 29, 18, 0, 0, TimeSpan.Zero),
                "task"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, feature.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Execution feature", cut.Markup);
            Assert.Contains(">Summary<", cut.Markup);
        });

        FindButtonByLabel(cut, "Summary").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Export XLSX", cut.Markup);
            Assert.Contains("Export Gantt", cut.Markup);
            Assert.Contains("Ship checklist", cut.Markup);
        });
    }

    [Fact]
    public async Task Export_gantt_creates_mermaid_file_with_dependency_order_and_default_duration()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Dependency Gantt Export");
        var feature = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Execution feature",
                "Delivery branch",
                "Use this node as the summary root.",
                $"project:{projectId}",
                520,
                240,
                null,
                null,
                "feature"));

        var dependencyNote = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Architect dependency note",
                "Prerequisite note",
                "A note can still block a downstream task.",
                feature.Id,
                760,
                340));

        var deliveryTask = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Ship canvas dependency flow",
                "Implementation task",
                "Create the dependency UX and delete mode.",
                feature.Id,
                780,
                420,
                null,
                null,
                "task",
                null,
                null,
                7200));

        await workbenchService.LinkObjectsAsync(projectId, deliveryTask.Id, dependencyNote.Id, ProjectObjectLinkKind.DependsOn);
        await SaveSelectedNodeStateAsync(workbenchService, projectId, feature.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Execution feature", cut.Markup);
            Assert.Contains(">Summary<", cut.Markup);
        });

        FindButtonByLabel(cut, "Summary").Click();
        cut.WaitForAssertion(() => Assert.Contains("Export Gantt", cut.Markup));

        FindButtonByLabel(cut, "Export Gantt").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("was exported as a Mermaid Gantt node.", cut.Markup));

        var updatedSurface = await workbenchService.GetStructureAsync(projectId);
        var exportedNode = Assert.Single(
            updatedSurface.Nodes,
            node => node.ObjectType == ProjectObjectType.File &&
                    string.Equals(node.ObjectSubtype, "mermaid", StringComparison.Ordinal) &&
                    string.Equals(node.ParentId, feature.Id, StringComparison.Ordinal));

        Assert.Equal("Execution feature gantt", exportedNode.Title);
        Assert.Contains(">[Unscheduled] Architect dependency note (Draft) :task2, ", exportedNode.Notes, StringComparison.Ordinal);
        Assert.Contains(">Ship canvas dependency flow (Draft) :task3, after task2, 2h", exportedNode.Notes, StringComparison.Ordinal);
        Assert.Contains("task2, 202", exportedNode.Notes, StringComparison.Ordinal);
        Assert.Contains(", 1h", exportedNode.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summary_dialog_can_export_workbook_and_then_gantt_from_the_same_open_dialog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Sequential summary exports");
        var feature = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Execution feature",
                "Delivery branch",
                "Use this node as the summary root.",
                $"project:{projectId}",
                520,
                240,
                null,
                null,
                "feature"));
        _ = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Ship checklist",
                "Ready for release",
                "Confirm the rollout tasks.",
                feature.Id,
                780,
                340,
                new DateTimeOffset(2026, 3, 28, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 29, 18, 0, 0, TimeSpan.Zero),
                "task"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, feature.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Execution feature", cut.Markup);
            Assert.Contains(">Summary<", cut.Markup);
        });

        FindButtonByLabel(cut, "Summary").Click();
        cut.WaitForAssertion(() => Assert.Contains("Export XLSX", cut.Markup));

        FindButtonByLabel(cut, "Export XLSX").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("was exported as an Excel attachment.", cut.Markup));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Export XLSX", cut.Markup);
            Assert.Contains("Export Gantt", cut.Markup);
        });

        FindButtonByLabel(cut, "Export Gantt").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("was exported as a Mermaid Gantt node.", cut.Markup));

        var updatedSurface = await workbenchService.GetStructureAsync(projectId);
        var workbookNode = Assert.Single(
            updatedSurface.Nodes,
            node => node.ObjectType == ProjectObjectType.File &&
                    string.Equals(node.ObjectSubtype, "excel", StringComparison.Ordinal) &&
                    string.Equals(node.ParentId, feature.Id, StringComparison.Ordinal));
        var ganttNode = Assert.Single(
            updatedSurface.Nodes,
            node => node.ObjectType == ProjectObjectType.File &&
                    string.Equals(node.ObjectSubtype, "mermaid", StringComparison.Ordinal) &&
                    string.Equals(node.ParentId, feature.Id, StringComparison.Ordinal));

        Assert.Equal("Execution feature progress workbook", workbookNode.Title);
        Assert.Equal("Execution feature gantt", ganttNode.Title);
    }

    [Fact]
    public async Task Selected_mermaid_nodes_open_viewer_modal_with_detected_diagram_type()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Mermaid Viewer Project";
        project.Description = "Verify Mermaid viewing from project structure.";
        project.Objective = "Open Mermaid source in a typed viewer.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var mermaidMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            File = new ProjectFileMetadata
            {
                FileSubtype = ProjectFileSubtype.Mermaid,
                MermaidDiagramKind = MermaidDiagramKind.Gantt
            }
        });

        var mermaidNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Release gantt",
                "Mermaid file",
                "gantt\n    title Release plan\n    section Build\n    Kickoff :done, task1, 2026-03-28, 1d",
                $"project:{projectId}",
                560,
                260,
                null,
                null,
                "mermaid",
                null,
                mermaidMetadata));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, mermaidNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Mermaid viewer", cut.Markup);
            Assert.Contains("View Mermaid", cut.Markup);
        });

        FindButtonByLabel(cut, "View Mermaid").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Detected diagram type: Gantt", cut.Markup);
            Assert.Contains("Release plan", cut.Markup);
            Assert.Contains("Kickoff", cut.Markup);
        });
    }

    [Fact]
    public async Task Transcript_nodes_open_confirmation_dialog_with_provider_selection()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var workspaceService = harness.Context.Services.GetRequiredService<WorkspaceService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Transcript Workflow Project";
        project.Description = "Verify transcript confirmation and provider selection.";
        project.Objective = "Require confirmation before sending transcript actions.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var providerSave = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "Local llama",
            ConnectorPluginKey = OllamaProviderAdapter.PluginKey,
            ConfigSchemaVersion = "1.0",
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = "http://localhost:11434",
                ["defaultModel"] = "llama3.1",
                ["timeoutSeconds"] = "30"
            }),
            IsEnabled = true
        });

        Assert.True(providerSave.IsSuccess);

        var transcriptMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Transcript = new ProjectTranscriptMetadata
            {
                TranscriptText = "Alice promised the rollout checklist and Bob owes the final screenshots.",
                LastProviderName = "Legacy reviewer"
            }
        });

        var transcriptNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Transcript,
                "Workshop transcript",
                "Client call",
                "Alice promised the rollout checklist and Bob owes the final screenshots.",
                $"project:{projectId}",
                540,
                280,
                null,
                null,
                null,
                null,
                transcriptMetadata));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, transcriptNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(">Summarize<", cut.Markup);
            Assert.Contains(">Find my tasks<", cut.Markup);
            Assert.Contains(">Find others delivery to me<", cut.Markup);
        });

        FindButtonByLabel(cut, "Find my tasks").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("This action will send transcript content to an external or local provider.", cut.Markup);
            Assert.Contains("Local llama", cut.Markup);
            Assert.Contains("Select a provider", cut.Markup);
            Assert.Contains("Last provider: Legacy reviewer", cut.Markup);
            Assert.Contains("Send request", cut.Markup);
        });
    }

    [Fact]
    public async Task Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var project = await projectsService.GetAsync(null);
        project.Name = "PDF Preview Project";
        project.Description = "Verify attachment previews in the inspector.";
        project.Objective = "Keep PDF viewing inside project structure.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Architecture spec",
                "Uploaded PDF",
                "Attachment preview coverage",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                string.Empty,
                new ProjectObjectMediaPayload(
                    "architecture-spec.pdf",
                    "application/pdf",
                    Convert.ToBase64String("%PDF-1.4 test payload"u8.ToArray()))));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Architecture spec", cut.Markup));

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Architecture spec", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Attachment preview", cut.Markup);
            Assert.Contains("application/pdf", cut.Markup);
            Assert.Contains("project-structure-document-preview", cut.Markup);
        });

        var uriBeforeOpen = navigation.Uri;

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Expand preview", cut.Markup);
            Assert.Contains("Open in new tab", cut.Markup);
        });

        FindButtonByLabel(cut, "Expand preview").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-preview-dialog", cut.Markup);
            Assert.Contains("dialog preview", cut.Markup);
            Assert.Single(cut.FindAll(".cw-stage-surface .project-structure-preview-backdrop--canvas"));
        });

        Assert.Equal(uriBeforeOpen, navigation.Uri);
    }

    [Fact]
    public async Task Text_file_asset_nodes_open_readable_preview_on_canvas_double_click()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Text Asset Preview Project";
        project.Description = "Verify source-like file preview from canvas double-click.";
        project.Objective = "Open text assets before editing node details.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var markdownNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Operator README",
                "Markdown asset",
                "# Operator checklist\n\n- Review workflow output\n- Attach evidence",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                "markdown"));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Operator README", cut.Markup));

        await OpenNodeFromCanvasAsync(cut, markdownNode.Id);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Canvas preview", cut.Markup);
            Assert.Contains("Operator checklist", cut.Markup);
            Assert.Contains("project-structure-text-asset-preview", cut.Markup);
            Assert.Contains("Edit details", cut.Markup);
            Assert.DoesNotContain("project-structure-node-quick-actions", cut.Markup);
        });
    }

    [Fact]
    public async Task Markdown_summary_nodes_with_mermaid_keywords_open_text_preview_not_mermaid_viewer()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Markdown Summary Preview Project";
        project.Description = "Verify workflow markdown summaries do not get classified as Mermaid diagrams.";
        project.Objective = "Open markdown summaries as readable text even when they mention diagram concepts.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var markdownNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Office365 category email summary",
                "Markdown summary",
                "# Summary\n\nThe customer asked for a static Tetris website. A gantt plan can be prepared later, but this node is markdown output.",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                "md"));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Office365 category email summary", cut.Markup));

        await OpenNodeFromCanvasAsync(cut, markdownNode.Id);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Canvas preview", cut.Markup);
            Assert.Contains("The customer asked for a static Tetris website", cut.Markup);
            Assert.Contains("project-structure-text-asset-preview", cut.Markup);
            Assert.DoesNotContain("project-structure-mermaid-diagram", cut.Markup);
            Assert.DoesNotContain("Detected diagram type", cut.Markup);
        });
    }

    [Fact]
    public async Task Audio_attachment_nodes_render_audio_preview_and_local_open_action_when_host_supports_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Audio Preview Project";
        project.Description = "Verify audio and local-open coverage.";
        project.Objective = "Keep audio attachment handling inside project structure.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var audioNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Interview clip",
                "Uploaded audio",
                "Audio preview coverage",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                string.Empty,
                new ProjectObjectMediaPayload(
                    "interview-clip.mp3",
                    "audio/mpeg",
                    Convert.ToBase64String(new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x21 }))));

        await workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = [audioNode.Id]
            }.ToJson());

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Interview clip", cut.Markup);
            Assert.Contains("project-structure-audio-preview", cut.Markup);
            Assert.Contains("audio/mpeg", cut.Markup);
            Assert.Contains("Open in File Explorer", cut.Markup);
            Assert.Contains("Expand preview", cut.Markup);
        });
    }

    [Fact]
    public async Task Artifact_folder_nodes_render_open_in_file_explorer_as_a_node_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Artifact Folder Action");
        var artifactRelativePath = "artifacts/scopes/organization/component-tests/process/qa-validation";
        Directory.CreateDirectory(Path.Combine(
            harness.ActiveProfile.WorkspaceRootPath,
            artifactRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        var artifactNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "QA evidence folder",
                "Stored run output",
                "Artifact folders should expose the local open action from the inspector.",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                "artifact-folder"));

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var record = await dbContext.Set<ProjectObjectRecord>()
                .SingleAsync(item => item.ProjectId == projectId && item.NodeKey == artifactNode.Id);
            var binding = await dbContext.Set<ProjectNodeBindingRecord>()
                .SingleAsync(item => item.ProjectObjectId == record.Id);
            binding.Route = $"/managed-files/{artifactRelativePath}";
            binding.ExternalArtifactKind = ProjectObjectType.File.ToString();
            binding.StorageObjectReferenceJson = StorageJson.SerializeReference(
                new StorageObjectReference(
                    null,
                    StorageProviderKind.FileSystem,
                    StorageLocatorKind.RelativePath,
                    artifactRelativePath,
                    string.Empty,
                    "application/x-directory",
                    null,
                    $"/managed-files/{artifactRelativePath}"));
            await dbContext.SaveChangesAsync();
        }

        await SaveSelectedNodeStateAsync(workbenchService, projectId, artifactNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            var actionGrid = cut.Find("[data-testid='project-structure-node-actions']");
            Assert.Contains("QA evidence folder", cut.Markup);
            Assert.Contains("Open in File Explorer", actionGrid.TextContent);
        });
    }

    [Fact]
    public async Task Repository_folder_nodes_render_open_in_file_explorer_as_a_node_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Local Folder Action");
        var folderPath = Path.Combine(harness.ActiveProfile.WorkspaceRootPath, "project-structure-folder-node");
        Directory.CreateDirectory(folderPath);

        var folderNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Repository,
                "Local app folder",
                "Workspace folder",
                "Folder nodes should expose the local open action from the inspector.",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                "folder",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Repository = new ProjectRepositoryMetadata
                    {
                        RepositoryMode = ProjectRepositoryMode.LocalFolder,
                        LocalPath = folderPath
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, folderNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            var actionGrid = cut.Find("[data-testid='project-structure-node-actions']");
            Assert.Contains("Local app folder", cut.Markup);
            Assert.Contains("Open in File Explorer", actionGrid.TextContent);
        });
    }

    [Fact]
    public async Task Canvas_state_changes_ignore_manual_position_only_updates()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Manual Position Ignore");
        var rootNodeId = $"project:{projectId}";

        await SaveSelectedNodeStateAsync(workbenchService, projectId, rootNodeId);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var baselineSurface = await workbenchService.GetStructureAsync(projectId);
        var baselineState = CanvasWorkbenchUiState.Parse(baselineSurface.ViewStateJson);

        var transientState = new CanvasWorkbenchUiState
        {
            SelectedNodeIds = [rootNodeId],
            ManualPositions = new Dictionary<string, CanvasWorkbenchPoint>(StringComparer.Ordinal)
            {
                [rootNodeId] = new CanvasWorkbenchPoint
                {
                    X = 960,
                    Y = 640
                }
            }
        };

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnStateChanged(transientState.ToJson()));

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedState = CanvasWorkbenchUiState.Parse(persistedSurface.ViewStateJson);

        Assert.Empty(persistedState.ManualPositions);
        Assert.Equal(baselineState.SelectedNodeIds, persistedState.SelectedNodeIds);
        Assert.Equal(baselineState.Zoom, persistedState.Zoom, 3);
        Assert.Equal(baselineState.PanX, persistedState.PanX, 3);
        Assert.Equal(baselineState.PanY, persistedState.PanY, 3);
        Assert.Equal(baselineState.ShowMinimap, persistedState.ShowMinimap);
        Assert.Empty(cut.FindComponent<CanvasWorkbench>().Instance.Surface.UiState.ManualPositions);
    }

    [Fact]
    public async Task Canvas_state_changes_persist_viewport_without_manual_positions()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Viewport Persistence");
        var rootNodeId = $"project:{projectId}";

        await SaveSelectedNodeStateAsync(workbenchService, projectId, rootNodeId);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

        var stateChange = new CanvasWorkbenchUiState
        {
            SelectedNodeIds = [rootNodeId],
            ManualPositions = new Dictionary<string, CanvasWorkbenchPoint>(StringComparer.Ordinal)
            {
                [rootNodeId] = new CanvasWorkbenchPoint
                {
                    X = 720,
                    Y = 480
                }
            },
            Zoom = 1.25,
            PanX = 240,
            PanY = 180,
            ShowMinimap = false
        };

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnStateChanged(stateChange.ToJson()));

        CanvasWorkbenchUiState? persistedState = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var persistedSurface = await workbenchService.GetStructureAsync(projectId);
            persistedState = CanvasWorkbenchUiState.Parse(persistedSurface.ViewStateJson);
            if (Math.Abs(persistedState.Zoom - 1.25) < 0.001 &&
                Math.Abs(persistedState.PanX - 240) < 0.001 &&
                Math.Abs(persistedState.PanY - 180) < 0.001 &&
                persistedState.SelectedNodeIds.SequenceEqual([rootNodeId], StringComparer.Ordinal) &&
                !persistedState.ShowMinimap)
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.NotNull(persistedState);
        Assert.Empty(persistedState!.ManualPositions);
        Assert.Equal(1.25, persistedState.Zoom, 3);
        Assert.Equal(240, persistedState.PanX, 3);
        Assert.Equal(180, persistedState.PanY, 3);
        Assert.Equal([rootNodeId], persistedState.SelectedNodeIds);
        Assert.False(persistedState.ShowMinimap);

        var renderedState = cut.FindComponent<CanvasWorkbench>().Instance.Surface.UiState;
        Assert.Empty(renderedState.ManualPositions);
        Assert.Equal(1.25, renderedState.Zoom, 3);
        Assert.Equal(240, renderedState.PanX, 3);
        Assert.Equal(180, renderedState.PanY, 3);
    }

    [Fact]
    public async Task Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Inspector Layout Project";
        project.Description = "Verify advanced details layout.";
        project.Objective = "Tighten the selection panel information architecture.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var block = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Main AMU server",
                "Server lane",
                "Operational server block.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "server"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, block.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Advanced details", cut.Markup);
            Assert.DoesNotContain("Typed details", cut.Markup, StringComparison.Ordinal);
        });

        var quickSignals = cut.Find("[data-testid='project-structure-quick-signals']");
        Assert.Contains("Progress", quickSignals.TextContent);
        Assert.Contains("Priority", quickSignals.TextContent);
        Assert.Contains("Marker", quickSignals.TextContent);

        var advancedDetails = cut.Find("[data-testid='project-structure-advanced-details']");
        Assert.False(advancedDetails.HasAttribute("open"));

        var actionLabels = cut.FindAll("[data-testid='project-structure-node-actions'] button")
            .Select(button => button.TextContent.Trim())
            .ToList();
        Assert.Contains(actionLabels, label => label.Contains("Edit", StringComparison.Ordinal));
        Assert.Contains("Delete", actionLabels.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_selection_panel_uses_semantic_badges_and_suppresses_duplicate_type_metadata()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Selection Badge Project";
        project.Description = "Verify file badge semantics and duplicate suppression.";
        project.Objective = "Keep file selection panels concise and readable.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var excelNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Customers with fake emails",
                "Import source",
                "Workbook used for validation coverage.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "excel",
                new ProjectObjectMediaPayload(
                    "customers.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    Convert.ToBase64String("excel selection coverage"u8.ToArray()))));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, excelNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Show advanced details help", cut.Markup);
            Assert.Contains("project-structure-selection-badge-excel", cut.Markup);
            Assert.Contains("project-structure-selection-badge-uploaded", cut.Markup);
        });

        var advancedDetails = cut.Find("[data-testid='project-structure-advanced-details']");
        Assert.DoesNotContain("Kind", advancedDetails.TextContent, StringComparison.Ordinal);
        Assert.Contains("project-structure-storage-summary", cut.Markup);

        var excelBadge = cut.Find("[data-testid='project-structure-selection-badge-excel']");
        Assert.Equal("FileExcel", excelBadge.GetAttribute("data-badge-style"));

        var uploadedBadge = cut.Find("[data-testid='project-structure-selection-badge-uploaded']");
        Assert.Equal("Uploaded", uploadedBadge.GetAttribute("data-badge-style"));

        var helpButton = cut.Find("button[aria-label='Show advanced details help']");
        Assert.Contains("pf-help-popover__toggle--compact", helpButton.GetAttribute("class"), StringComparison.Ordinal);
        helpButton.Click();
        cut.WaitForAssertion(() =>
        {
            var helpPanel = cut.Find(".pf-help-popover__panel--above-end");
            Assert.Contains("artifact metadata", helpPanel.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Edit_actions_open_prefilled_canvas_composer_for_supported_nodes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Edit Composer Project";
        project.Description = "Verify edit composer prefill.";
        project.Objective = "Open the shared composer with current node values.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Environment,
                "API runtime",
                "dotnet watch",
                "Launch the selected runtime from the inspector.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "dotnet-watch",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                        ProjectPath = @"C:\repos\api\Api.csproj",
                        LaunchProfileName = "https",
                        RuntimeProtocol = ProjectRuntimeProtocol.Https,
                        LocalhostUrl = "https://localhost:7143"
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true)
            .SetResult(true);
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.update", _ => true)
            .SetResult(true);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Edit", cut.Markup);
        });
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                harness.Context.JSInterop.Invocations,
                invocation => string.Equals(invocation.Identifier, "CanDoItAll.canvasWorkbench.create", StringComparison.Ordinal));
        });

        FindButtonByLabel(cut, "Edit", "[data-testid='project-structure-node-actions'] button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                harness.Context.JSInterop.Invocations,
                invocation => string.Equals(invocation.Identifier, "CanDoItAll.canvasWorkbench.openCreateComposer", StringComparison.Ordinal));
        });

        var invocation = harness.Context.JSInterop.Invocations
            .Last(candidate => string.Equals(candidate.Identifier, "CanDoItAll.canvasWorkbench.openCreateComposer", StringComparison.Ordinal));
        var action = Assert.IsType<CanvasWorkbenchAction>(invocation.Arguments[1]);
        var request = Assert.IsType<CanvasWorkbenchCreateActionRequest>(invocation.Arguments[2]);

        Assert.Equal("edit:add-environment-dotnet-watch", action.ActionId);
        Assert.Equal("Save changes", action.SubmitLabel);
        Assert.DoesNotContain(action.InputFields, field => string.Equals(field.Key, "repositoryRef", StringComparison.Ordinal));

        Assert.Equal("API runtime", request.Title);
        Assert.Equal("dotnet watch", request.Subtitle);
        Assert.Equal("Launch the selected runtime from the inspector.", request.Notes);
        Assert.Contains(request.InputValues!, value => value.Key == "environmentKind" && value.Value == "dotNetWatch");
        Assert.Contains(request.InputValues!, value => value.Key == "projectPath" && value.Value == @"C:\repos\api\Api.csproj");
        Assert.Contains(request.InputValues!, value => value.Key == "launchProfileName" && value.Value == "https");
        Assert.Contains(request.InputValues!, value => value.Key == "localhostUrl" && value.Value == "https://localhost:7143");
    }

    [Fact]
    public async Task Edit_create_actions_update_existing_nodes_and_refresh_selection_panel()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Edit Update Project";
        project.Description = "Verify edit submission updates existing nodes.";
        project.Objective = "Persist shared-composer edits against the selected node.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Environment,
                "API runtime",
                "dotnet watch",
                "Original runtime description.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "dotnet-watch",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                        ProjectPath = @"C:\repos\api\Api.csproj",
                        LaunchProfileName = "https",
                        RuntimeProtocol = ProjectRuntimeProtocol.Https,
                        LocalhostUrl = "https://localhost:7143"
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnCreateAction(JsonSerializer.Serialize(
            new CanvasWorkbenchCreateActionRequest(
                "edit:add-environment-dotnet-watch",
                runtimeNode.Id,
                runtimeNode.X,
                runtimeNode.Y,
                runtimeNode.ParentId,
                "API runtime updated",
                "Release host",
                "Edited runtime description.",
                "edit",
                "dialog",
                "dotnet-watch",
                null,
                [
                    new CanvasWorkbenchInputValue { Key = "environmentKind", Value = "dotNetWatch" },
                    new CanvasWorkbenchInputValue { Key = "projectPath", Value = @"C:\repos\api\Updated\Api.csproj" },
                    new CanvasWorkbenchInputValue { Key = "launchProfileName", Value = "staging" },
                    new CanvasWorkbenchInputValue { Key = "runtimeProtocol", Value = "http" },
                    new CanvasWorkbenchInputValue { Key = "localhostUrl", Value = "http://localhost:5099" }
                ]))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("API runtime updated", cut.Markup);
            Assert.Contains("API runtime updated was updated.", cut.Markup);
        });

        var surface = await workbenchService.GetStructureAsync(projectId);
        var updatedNode = Assert.Single(surface.Nodes, node => node.Id == runtimeNode.Id);
        Assert.Equal("API runtime updated", updatedNode.Title);
        Assert.Equal("Release host", updatedNode.Subtitle);
        Assert.Equal("Edited runtime description.", updatedNode.Notes);

        var metadata = ProjectObjectMetadataSerializer.Parse(updatedNode.MetadataJson);
        Assert.NotNull(metadata.Environment);
        Assert.Equal(ProjectEnvironmentKind.DotNetWatch, metadata.Environment!.EnvironmentKind);
        Assert.Equal(@"C:\repos\api\Updated\Api.csproj", metadata.Environment.ProjectPath);
        Assert.Equal("staging", metadata.Environment.LaunchProfileName);
        Assert.Equal(ProjectRuntimeProtocol.Http, metadata.Environment.RuntimeProtocol);
        Assert.Equal("http://localhost:5099", metadata.Environment.LocalhostUrl);
    }

    [Fact]
    public async Task Launchable_runtime_nodes_render_powershell_actions_and_surface_launch_feedback()
    {
        var runtimeLauncher = new TestRuntimeLauncher();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IProjectStructureRuntimeLauncher>(runtimeLauncher));

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Runtime Launch Project";
        project.Description = "Verify runtime launch actions.";
        project.Objective = "Launch runtime nodes from the selection panel.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Environment,
                "API runtime",
                "dotnet watch",
                "Launch the selected runtime from the inspector.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "dotnet-watch",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                        ProjectPath = @"C:\repos\api\Api.csproj",
                        LaunchProfileName = "https"
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Open PowerShell", cut.Markup);
            Assert.Contains("Open PowerShell (Admin)", cut.Markup);
        });

        FindButtonByLabel(cut, "Open PowerShell (Admin)").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Opened elevated PowerShell and started dotnet watch.", cut.Markup);
        });

        Assert.Single(runtimeLauncher.Requests);
        Assert.Equal(runtimeNode.Id, runtimeLauncher.Requests[0].NodeId);
        Assert.True(runtimeLauncher.Requests[0].RunAsAdministrator);
    }

    [Fact]
    public async Task Docker_runtime_nodes_render_powershell_actions_and_surface_launch_feedback()
    {
        var runtimeLauncher = new TestRuntimeLauncher();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IProjectStructureRuntimeLauncher>(runtimeLauncher));

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Docker Runtime Launch");

        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Infrastructure,
                "Docker compose runtime",
                "Compose stack",
                "Launch Docker from the project structure inspector.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "docker-mode",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Infrastructure = new ProjectInfrastructureMetadata
                    {
                        InfrastructureKind = ProjectInfrastructureKind.DockerMode,
                        RuntimeCommand = "docker compose up",
                        RuntimeArguments = "--build",
                        WorkingDirectory = @"C:\repos\api"
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Open PowerShell", cut.Markup);
            Assert.Contains("Open PowerShell (Admin)", cut.Markup);
        });

        FindButtonByLabel(cut, "Open PowerShell").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Opened PowerShell and started Docker runtime.", cut.Markup);
        });

        Assert.Single(runtimeLauncher.Requests);
        Assert.Equal(runtimeNode.Id, runtimeLauncher.Requests[0].NodeId);
        Assert.False(runtimeLauncher.Requests[0].RunAsAdministrator);
    }

    [Fact]
    public async Task Non_launchable_nodes_do_not_render_runtime_launch_actions()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IProjectStructureRuntimeLauncher>(new TestRuntimeLauncher()));

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "No Runtime Launch";
        project.Description = "Verify unsupported nodes stay clean.";
        project.Objective = "Do not show runtime launch buttons on non-runtime nodes.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var noteNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Design note",
                "Context",
                "Notes should not expose runtime launch actions.",
                $"project:{projectId}",
                500,
                240));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, noteNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Open PowerShell", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Open PowerShell (Admin)", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task File_backed_nodes_map_compact_path_payload_with_promoted_file_name()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Compact Path Project";
        project.Description = "Verify compact path payload mapping.";
        project.Objective = "Promote file names on file-backed paths.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Environment,
                "Billing API runtime",
                "dotnet watch",
                "Use the compact path payload for project files.",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                "dotnet-watch",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                        ProjectPath = @"C:\repositories\pveinvoicing\src\PVEInvoicing.ServerApp\PVEInvoicing.csproj",
                        LaunchProfileName = "https"
                    }
                })));

        var surface = await workbenchService.GetStructureAsync(projectId);
        var adaptedNode = BuildCanvasNode(surface, runtimeNode.Id);

        Assert.NotNull(adaptedNode.CompactPath);
        Assert.Equal(@"C:\repositories\pveinvoicing\src\PVEInvoicing.ServerApp\PVEInvoicing.csproj", adaptedNode.CompactPath!.FullPath);
        Assert.Equal("PVEInvoicing.csproj", adaptedNode.CompactPath.PromotedText);
        Assert.Contains("...", adaptedNode.CompactPath.DisplayText, StringComparison.Ordinal);
        Assert.DoesNotContain(adaptedNode.CompactPath.FullPath, adaptedNode.LeadText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repository_nodes_strip_full_path_from_lead_text_when_compact_path_is_present()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Repository Path Project";
        project.Description = "Verify lead text no longer dumps full paths.";
        project.Objective = "Keep repository cards readable.";
        project.CurrentPhase = "Discovery";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var repositoryNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Repository,
                "Main repository",
                "Workspace clone",
                "Repository cards should not dump long local paths.",
                $"project:{projectId}",
                500,
                220,
                null,
                null,
                "git",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Repository = new ProjectRepositoryMetadata
                    {
                        RepositoryMode = ProjectRepositoryMode.LocalRepository,
                        LocalPath = @"C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench",
                        RepositoryUrl = "https://github.com/example/CanDoItAll",
                        DefaultBranch = "main"
                    }
                })));

        var surface = await workbenchService.GetStructureAsync(projectId);
        var adaptedNode = BuildCanvasNode(surface, repositoryNode.Id);

        Assert.NotNull(adaptedNode.CompactPath);
        Assert.DoesNotContain(@"C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench", adaptedNode.LeadText, StringComparison.Ordinal);
        Assert.Contains("URL: https://github.com/example/CanDoItAll", adaptedNode.LeadText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, adaptedNode.CompactPath!.PromotedText);
    }

    private static Task SaveSelectedNodeStateAsync(ProjectWorkbenchService workbenchService, Guid projectId, params string[] selectedNodeIds)
        => workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = selectedNodeIds.ToList(),
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = true }
                }
            }.ToJson());

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<WorkflowDefinition> CreateComponentWorkflowDefinitionAsync(
        IWorkflowCatalogService catalogService,
        string name)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: name,
            Description: "Component proof workflow for project-structure execution.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateComponentWorkflowNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateComponentWorkflowNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
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

    private static ProjectStructureWorkflowRunStatus CreateWorkflowStartStatus()
        => new(
            RunId: null,
            State: WorkflowRunState.NotStarted,
            Status: "Ready",
            ProgressMode: "progress",
            ProgressPercent: 0,
            MarkerIcon: "progress",
            MarkerTone: "neutral",
            MarkerLabel: "Ready",
            CurrentStepIndex: 0,
            StepCount: 0,
            Message: "Workflow is ready to start from project structure.",
            Summary: new ProjectStructureWorkflowExecutionSummary(
                RunId: null,
                State: WorkflowRunState.NotStarted,
                WorkflowName: "Office365 category email summary",
                RunSummary: "Workflow is ready to start from project structure.",
                CurrentStepIndex: 0,
                StepCount: 0,
                Artifacts: [],
                CreatedNodeIds: [],
                CreatedAssetIds: [],
                CreatedFilePaths: []),
            RecentEvents: []);

    private static WorkflowNode CreateComponentWorkflowNode(
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

    private static ProcessDefinitionEditorModel BuildProcessDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Workbench-visible process",
            Summary = "Project the process definition into the structure graph.",
            ValueStatement = "Keep structure and process authoring aligned.",
            CustomerName = "Workbench validation customer",
            OwnerName = "Process architecture reviewer",
            GovernancePolicySummary = "Projected process nodes stay read-only in the structure canvas.",
            ChangeSummary = "Initial workbench projection test definition.",
            ConstitutionRuleSummary = "The role contract remains stable while executors change.",
            OperatingModeSummary = "Assisted execution routed through the project-scoped process workspace.",
            SimulationReadinessSummary = "Safe for component validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own the projected process flow.",
                    StaffingIntent = "Assigned from the project manager lane.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Delivery owner role snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "intake",
                    Title = "Capture integration intake",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Structure-side scope request.",
                    OutputContractSummary = "Typed intake package.",
                    EvidenceContractSummary = "Capture the intake context.",
                    DecisionRightsSummary = "Delivery owner moves the request forward.",
                    ExceptionPolicySummary = "Escalate missing scope or governance details.",
                    TargetLeadHours = 2,
                    CanvasX = 140,
                    CanvasY = 140,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Rebind to the current delivery owner."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "review",
                    Title = "Review delivery readiness",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Typed intake package.",
                    OutputContractSummary = "Ready-to-execute decision.",
                    EvidenceContractSummary = "Decision-ready evidence bundle.",
                    DecisionRightsSummary = "Delivery owner can approve, block, or escalate.",
                    ExceptionPolicySummary = "Block when evidence or staffing is incomplete.",
                    TargetLeadHours = 4,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = intakeStepId
                        }
                    ],
                    CanvasX = 420,
                    CanvasY = 140,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Delivery owner remains attached."
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Projected structure review evidence",
                            ValidationRequirementSummary = "Human review remains required."
                        }
                    ]
                }
            ]
        };
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
        => $"project:{projectId}";

    private static string BuildProjectChildNodeKey(Guid projectId)
        => $"project-child:{projectId}";

    private static string BuildProcessDefinitionNodeKey(Guid definitionId)
        => $"process-definition:{definitionId}";

    private static string ExtractNodeHash(string nodeId)
    {
        var separatorIndex = nodeId.IndexOf(':', StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex < nodeId.Length - 1
            ? nodeId[(separatorIndex + 1)..]
            : nodeId;
    }

    private static CanvasWorkbenchNode BuildCanvasNode(ProjectStructureSurface surface, string nodeId)
    {
        var adapter = new ProjectStructureGraphAdapter();
        var canvasSurface = adapter.BuildSurface(
            surface,
            new CanvasWorkbenchUiState(),
            new CanvasWorkbenchChrome(),
            new ProjectStructureActionCatalogAdapter());

        return Assert.Single(canvasSurface.Nodes, node => node.Id == nodeId);
    }

    private static Task OpenNodeFromCanvasAsync(IRenderedComponent<ProjectStructurePage> cut, string nodeId)
        => cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnNodeOpened(nodeId));

    private static IElement FindButtonByLabel(
        IRenderedFragment cut,
        string label,
        string selector = "button")
        => cut.FindAll(selector)
            .First(button =>
                button.TextContent.Contains(label, StringComparison.Ordinal)
                || (button.GetAttribute("title")?.Contains(label, StringComparison.Ordinal) ?? false)
                || (button.GetAttribute("aria-label")?.Contains(label, StringComparison.Ordinal) ?? false));

    private static Task InvokeCanvasContextActionAsync(IRenderedComponent<ProjectStructurePage> cut, string nodeId, string actionId)
        => cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(nodeId, actionId, 0, 0));

    private static IReadOnlyList<(string Value, string Label)> ReadHierarchyProjectOptions(IRenderedComponent<ProjectStructurePage> cut)
        => cut.FindAll("[data-testid='project-structure-hierarchy-project-select'] option")
            .Select(option => (option.GetAttribute("value") ?? string.Empty, option.TextContent.Trim()))
            .Where(option => !string.IsNullOrWhiteSpace(option.Item1))
            .Select(option => (Value: option.Item1, Label: option.Item2))
            .ToList();

    private sealed class TestRuntimeLauncher : IProjectStructureRuntimeLauncher
    {
        public bool IsAvailable => true;

        public List<(string NodeId, bool RunAsAdministrator)> Requests { get; } = [];

        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
            => node?.ObjectType switch
            {
                ProjectObjectType.Environment or ProjectObjectType.Script => new(
                    new ProjectStructureRuntimeLaunchPlan(
                        @"C:\repos\api",
                        "Set-Location -LiteralPath 'C:\\repos\\api'",
                        "dotnet watch --project 'C:\\repos\\api\\Api.csproj' run --launch-profile 'https'",
                        "dotnet watch",
                        new ProjectStructureRuntimeLaunchTarget("project path", @"C:\repos\api\Api.csproj", false)),
                    "Launch plan resolved."),
                ProjectObjectType.Infrastructure => new(
                    new ProjectStructureRuntimeLaunchPlan(
                        @"C:\repos\api",
                        "Set-Location -LiteralPath 'C:\\repos\\api'",
                        "docker compose up --build",
                        "Docker runtime",
                        new ProjectStructureRuntimeLaunchTarget("Docker working directory", @"C:\repos\api", true)),
                    "Launch plan resolved."),
                _ => new(null, "PowerShell launch is only available for runtime-capable nodes.")
            };

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(ProjectStructureNode node, bool runAsAdministrator, CancellationToken cancellationToken = default)
        {
            Requests.Add((node.Id, runAsAdministrator));
            var displayName = node.ObjectType == ProjectObjectType.Infrastructure
                ? "Docker runtime"
                : "dotnet watch";
            var message = runAsAdministrator
                ? $"Opened elevated PowerShell and started {displayName}."
                : $"Opened PowerShell and started {displayName}.";
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, message));
        }
    }
}


