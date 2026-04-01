using Bunit;
using AngleSharp.Dom;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageTests
{
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
            Assert.Contains("project-structure-selection-window", cut.Markup);
            Assert.DoesNotContain("cw-minimap", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-validation-window", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-toolbox-window", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("project-structure-standard-blocks-toolbox", cut.Markup, StringComparison.Ordinal);
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
            Assert.Equal(462d, healthWindow.Instance.State.Left);
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
    public async Task Prompt_flow_nodes_expose_wizard_navigation_from_the_inspector()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Prompt Flow Structure";
        project.Description = "Prompt flow navigation coverage";
        project.Objective = "Open the prompt wizard from the structure page";
        project.CurrentPhase = "Discovery";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var created = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.PromptFlow,
                "Feature wizard flow",
                "Feature discovery",
                "Start from the structure canvas.",
                $"project:{projectId}",
                420,
                260));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Feature wizard flow", cut.Markup));

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Feature wizard flow", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(">Wizard<", cut.Markup);
        });

        FindButtonByLabel(cut, "Wizard").Click();

        Assert.Contains("/prompt-factory?sessionId=", navigation.Uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(created.ArtifactId!.Value.ToString(), navigation.Uri, StringComparison.OrdinalIgnoreCase);
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
            ProviderKind = ProviderKind.OllamaLocal,
            BaseUrl = "http://localhost:11434",
            DefaultModel = "llama3.1",
            TimeoutSeconds = 30,
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
    public async Task Double_clicking_pdf_attachment_nodes_keeps_preview_modal_behavior()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var project = await projectsService.GetAsync(null);
        project.Name = "PDF Double Click Project";
        project.Description = "Verify attachment preview wins over quick actions.";
        project.Objective = "Keep previewable attachments in the existing modal flow.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var pdfBytes = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 quick action coverage"));
        var pdfNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Architecture spec",
                "Uploaded PDF",
                "Attachment preview coverage",
                $"project:{projectId}",
                520,
                260,
                null,
                null,
                string.Empty,
                new ProjectObjectMediaPayload("architecture.pdf", "application/pdf", pdfBytes)));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Architecture spec", cut.Markup));

        var uriBeforeOpen = navigation.Uri;
        await OpenNodeFromCanvasAsync(cut, pdfNode.Id);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-preview-dialog", cut.Markup);
            Assert.DoesNotContain("project-structure-node-quick-actions", cut.Markup, StringComparison.Ordinal);
        });

        Assert.Equal(uriBeforeOpen, navigation.Uri);
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
            Assert.Contains("Open locally", cut.Markup);
            Assert.Contains("Expand preview", cut.Markup);
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
        Assert.Empty(cut.FindAll("[data-testid='project-structure-node-facts']"));

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
                        LocalhostUrl = "https://localhost:7143",
                        RepositoryResourceId = Guid.NewGuid()
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Edit", cut.Markup);
        });

        FindButtonByLabel(cut, "Edit", "[data-testid='project-structure-node-actions'] button").Click();

        harness.Context.JSInterop.VerifyInvoke("CanDoItAll.canvasWorkbench.openCreateComposer");

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
    public async Task Double_clicking_launchable_runtime_nodes_opens_quick_action_modal_and_runs_powershell()
    {
        var runtimeLauncher = new TestRuntimeLauncher();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IProjectStructureRuntimeLauncher>(runtimeLauncher));

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Runtime Quick Action Project";
        project.Description = "Verify runtime double-click actions.";
        project.Objective = "Launch PowerShell from the quick action modal.";
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
                "Launch the runtime from a quick action modal.",
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
                        ProjectPath = @"C:\repos\api\Api.csproj",
                        LaunchProfileName = "https"
                    }
                })));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("API runtime", cut.Markup));

        await OpenNodeFromCanvasAsync(cut, runtimeNode.Id);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-node-quick-actions", cut.Markup);
            Assert.Contains("Run PowerShell", cut.Markup);
            Assert.Contains(">Edit<", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-quick-action-primary']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Opened PowerShell and started dotnet watch.", cut.Markup);
            Assert.DoesNotContain("project-structure-node-quick-actions", cut.Markup, StringComparison.Ordinal);
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
        Assert.Contains("Mode: git", adaptedNode.LeadText, StringComparison.OrdinalIgnoreCase);
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

    private static string BuildProjectRootNodeKey(Guid projectId)
        => $"project:{projectId}";

    private static string BuildProjectChildNodeKey(Guid projectId)
        => $"project-child:{projectId}";

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
            .First(button => button.TextContent.Contains(label, StringComparison.Ordinal));

    private sealed class TestRuntimeLauncher : IProjectStructureRuntimeLauncher
    {
        public bool IsAvailable => true;

        public List<(string NodeId, bool RunAsAdministrator)> Requests { get; } = [];

        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
            => node?.ObjectType is ProjectObjectType.Environment or ProjectObjectType.Script
                ? new(
                    new ProjectStructureRuntimeLaunchPlan(
                        @"C:\repos\api",
                        "Set-Location -LiteralPath 'C:\\repos\\api'",
                        "dotnet watch --project 'C:\\repos\\api\\Api.csproj' run --launch-profile 'https'",
                        "dotnet watch",
                        new ProjectStructureRuntimeLaunchTarget("project path", @"C:\repos\api\Api.csproj", false)),
                    "Launch plan resolved.")
                : new(null, "PowerShell launch is only available for runtime-capable nodes.");

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(ProjectStructureNode node, bool runAsAdministrator, CancellationToken cancellationToken = default)
        {
            Requests.Add((node.Id, runAsAdministrator));
            var message = runAsAdministrator
                ? "Opened elevated PowerShell and started dotnet watch."
                : "Opened PowerShell and started dotnet watch.";
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, message));
        }
    }
}


