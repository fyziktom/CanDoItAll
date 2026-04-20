using System.Reflection;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessWorkspaceTests
{
    [Fact]
    public async Task Global_workspace_loads_persisted_definitions_on_the_first_render_without_query_parameters()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Global processes workspace project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workspace-visible process", cut.Markup);
        });
    }

    [Fact]
    public async Task Workspace_shell_uses_internal_scroll_regions_for_definition_list_and_detail_tabs()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Containment workspace project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workspace-visible process", cut.Markup);
            Assert.NotNull(cut.Find("[data-testid='processes-workspace-shell']"));
            Assert.NotNull(cut.Find("[data-testid='processes-page-scaffold']"));
        });

        var pageScaffold = cut.Find("[data-testid='processes-page-scaffold']");
        Assert.Contains("max-w-full", pageScaffold.ClassName, StringComparison.Ordinal);

        var summaryTiles = cut.FindAll(".cda-summary-tile--badge");
        Assert.Equal(4, summaryTiles.Count);

        var definitionListScroll = cut.Find("[data-testid='processes-definition-list-scroll']");
        Assert.Contains("h-full", definitionListScroll.ClassName, StringComparison.Ordinal);
        Assert.Contains("overflow-y-auto", definitionListScroll.ClassName, StringComparison.Ordinal);

        var detailTabs = cut.Find("[data-testid='processes-detail-tabs']");
        Assert.Contains("cad-tabs--fill-height", detailTabs.ClassName, StringComparison.Ordinal);
        Assert.Contains("cad-tabs--panel-overflow-auto", detailTabs.ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workspace_replaces_inline_help_copy_with_compact_help_popovers()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Help popover workspace project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workspace-visible process", cut.Markup);
            Assert.NotNull(cut.Find("button[aria-label='Show help for Definitions']"));
            Assert.NotNull(cut.Find("button[aria-label='Show help for Process definitions']"));
            Assert.NotNull(cut.Find("button[aria-label='Show process workspace help']"));
        });

        Assert.DoesNotContain("Persisted process definitions.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Browse saved process contracts, filter the library, and keep selection in sync with the detail workspace without expanding the list header.", cut.Markup, StringComparison.Ordinal);

        var workspaceHelpButton = cut.Find("button[aria-label='Show process workspace help']");
        Assert.NotNull(workspaceHelpButton.ParentElement);

        workspaceHelpButton.ParentElement.TriggerEvent("onmouseenter", new MouseEventArgs());

        cut.WaitForAssertion(() =>
            Assert.Contains("Change the durable process contract, then validate the same definition through runtime runs, deviations, and captured evidence.", cut.Markup));
    }

    [Fact]
    public async Task Process_workspace_exposes_feed_defaults_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='processes-feed-defaults-button']"));
        });
    }

    [Fact]
    public async Task Steps_canvas_toolbar_switches_between_authoring_and_delete_modes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var process = BuildCanvasAuthoringDefinition(await CreateProjectAsync(projectsService, "Canvas toolbar project"));
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));

        await ActivateStepsTabAsync(cut);

        cut.WaitForAssertion(() =>
        {
            var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
            Assert.Equal("authoring", canvasWorkbench.Instance.Surface.Mode);
        });

        cut.Find("[data-testid='processes-canvas-tool-delete']").Click();
        cut.WaitForAssertion(() =>
        {
            var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
            Assert.Equal("delete", canvasWorkbench.Instance.Surface.Mode);
        });

        cut.Find("[data-testid='processes-canvas-tool-select']").Click();
        cut.WaitForAssertion(() =>
        {
            var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
            Assert.Equal("authoring", canvasWorkbench.Instance.Surface.Mode);
        });
    }

    [Fact]
    public async Task Steps_canvas_connection_actions_create_and_delete_branch_and_step_dependencies()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var process = BuildCanvasAuthoringDefinition(await CreateProjectAsync(projectsService, "Canvas connection project"));
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var role = Assert.Single(initialEditor.Roles, item => item.Key == process.RoleKey);
        var decisionStep = Assert.Single(initialEditor.Steps, step => step.Key == process.DecisionStepKey);
        var fixStepModel = Assert.Single(initialEditor.Steps, step => step.Key == process.FixStepKey);
        var implementationStep = Assert.Single(initialEditor.Steps, step => step.Key == process.ImplementationStepKey);
        var mergeStep = Assert.Single(initialEditor.Steps, step => step.Key == process.MergeStepKey);
        var repairsOutcome = Assert.Single(decisionStep.BranchOutcomes, outcome => outcome.Key == "repairs-required");

        var roleNodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(role);
        var branchNodeId = ProcessCanvasBranching.BuildDefinitionBranchNodeId(decisionStep);
        var fixNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(fixStepModel);
        var implementationNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(implementationStep);
        var mergeNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(mergeStep);
        var routedOutcomePortId = ProcessCanvasBranching.BuildOutcomePortId(repairsOutcome);

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentDecisionStep = Assert.Single(editor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Equal(role.Id, currentDecisionStep.DecisionRoleRequirementId);
            var currentFixStep = Assert.Single(editor.Steps, step => step.Key == process.FixStepKey);
            var dependency = Assert.Single(ProcessCanvasBranching.GetOrderedDependencies(currentFixStep));
            Assert.Equal(currentDecisionStep.Id, dependency.DependsOnStepId);
            Assert.Equal(repairsOutcome.Id, dependency.DependsOnBranchOutcomeId);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                fixNodeId,
                "delete-link",
                0,
                0,
                "link",
                branchNodeId,
                fixNodeId,
                "flow",
                routedOutcomePortId,
                CanvasWorkbenchAnchorPorts.Left))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentFixStep = Assert.Single(editor.Steps, step => step.Key == process.FixStepKey);
            Assert.Empty(ProcessCanvasBranching.GetOrderedDependencies(currentFixStep));
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                branchNodeId,
                "delete-link",
                0,
                0,
                "link",
                roleNodeId,
                branchNodeId,
                "flow",
                ProcessCanvasBranching.RoleDecisionOutputPortId,
                ProcessCanvasBranching.DecisionRoleInputPortId))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentDecisionStep = Assert.Single(editor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Null(currentDecisionStep.DecisionRoleRequirementId);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                branchNodeId,
                "connection:create",
                0,
                0,
                "link",
                roleNodeId,
                branchNodeId,
                "flow",
                ProcessCanvasBranching.RoleDecisionOutputPortId,
                ProcessCanvasBranching.DecisionRoleInputPortId))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentDecisionStep = Assert.Single(editor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Equal(role.Id, currentDecisionStep.DecisionRoleRequirementId);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                fixNodeId,
                "connection:create",
                0,
                0,
                "link",
                branchNodeId,
                fixNodeId,
                "flow",
                routedOutcomePortId,
                CanvasWorkbenchAnchorPorts.Left))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentDecisionStep = Assert.Single(editor.Steps, step => step.Key == process.DecisionStepKey);
            var currentFixStep = Assert.Single(editor.Steps, step => step.Key == process.FixStepKey);
            var dependency = Assert.Single(ProcessCanvasBranching.GetOrderedDependencies(currentFixStep));
            Assert.Equal(currentDecisionStep.Id, dependency.DependsOnStepId);
            Assert.Equal(repairsOutcome.Id, dependency.DependsOnBranchOutcomeId);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                mergeNodeId,
                "connection:create",
                0,
                0,
                "link",
                implementationNodeId,
                mergeNodeId,
                "flow",
                CanvasWorkbenchAnchorPorts.Right,
                CanvasWorkbenchAnchorPorts.Left))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentImplementationStep = Assert.Single(editor.Steps, step => step.Key == process.ImplementationStepKey);
            var currentMergeStep = Assert.Single(editor.Steps, step => step.Key == process.MergeStepKey);
            var dependency = Assert.Single(ProcessCanvasBranching.GetOrderedDependencies(currentMergeStep));
            Assert.Equal(currentImplementationStep.Id, dependency.DependsOnStepId);
            Assert.Null(dependency.DependsOnBranchOutcomeId);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                mergeNodeId,
                "delete-link",
                0,
                0,
                "link",
                implementationNodeId,
                mergeNodeId,
                "flow",
                CanvasWorkbenchAnchorPorts.Right,
                CanvasWorkbenchAnchorPorts.Left))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentMergeStep = Assert.Single(editor.Steps, step => step.Key == process.MergeStepKey);
            Assert.Empty(ProcessCanvasBranching.GetOrderedDependencies(currentMergeStep));
        });

    }

    [Fact]
    public async Task Steps_canvas_connection_actions_create_and_delete_role_participation_and_artifact_links_and_persist()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Canvas role artifact project");
        var process = BuildCanvasAuthoringDefinition(projectId, assignDecisionRole: true, connectFixStep: false);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var role = Assert.Single(initialEditor.Roles, item => item.Key == process.RoleKey);
        var implementationStep = Assert.Single(initialEditor.Steps, step => step.Key == process.ImplementationStepKey);
        var mergeStep = Assert.Single(initialEditor.Steps, step => step.Key == process.MergeStepKey);
        var implementationArtifact = Assert.Single(implementationStep.ArtifactExpectations, artifact => artifact.Title == "Implementation package");

        var roleNodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(role);
        var implementationNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(implementationStep);
        var mergeNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(mergeStep);
        var responsibleRolePortId = ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(ProcessResponsibilityKind.Responsible);
        var responsibleStepPortId = ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(ProcessResponsibilityKind.Responsible);
        var artifactOutputPortId = ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(implementationArtifact);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                mergeNodeId,
                "connection:create",
                0,
                0,
                "link",
                roleNodeId,
                mergeNodeId,
                "flow",
                responsibleRolePortId,
                responsibleStepPortId))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentMergeStep = Assert.Single(editor.Steps, step => step.Key == process.MergeStepKey);
            Assert.Contains(currentMergeStep.RoleAssignments, assignment =>
                assignment.RoleRequirementId == role.Id &&
                assignment.ResponsibilityKind == ProcessResponsibilityKind.Responsible);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                mergeNodeId,
                "connection:create",
                0,
                0,
                "link",
                implementationNodeId,
                mergeNodeId,
                "flow",
                artifactOutputPortId,
                ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentImplementationStep = Assert.Single(editor.Steps, step => step.Key == process.ImplementationStepKey);
            var currentMergeStep = Assert.Single(editor.Steps, step => step.Key == process.MergeStepKey);
            var currentArtifact = Assert.Single(currentImplementationStep.ArtifactExpectations, artifact => artifact.Title == "Implementation package");
            Assert.Contains(currentMergeStep.ArtifactInputs, input => input.ArtifactExpectationId == currentArtifact.Id);
            Assert.Contains(ProcessCanvasBranching.GetOrderedDependencies(currentMergeStep), dependency => dependency.DependsOnStepId == currentImplementationStep.Id);
        });

        var persistedEditor = await WaitForPersistedEditorAsync(
            processesService,
            saveResult.Value,
            projectId,
            persisted =>
            {
                var persistedImplementationStep = persisted.Steps.FirstOrDefault(step => step.Key == process.ImplementationStepKey);
                var persistedMergeStep = persisted.Steps.FirstOrDefault(step => step.Key == process.MergeStepKey);
                var persistedArtifactId = persistedImplementationStep?.ArtifactExpectations
                    .FirstOrDefault(artifact => artifact.Title == "Implementation package")?
                    .Id;
                return persistedMergeStep is not null &&
                    persistedMergeStep.RoleAssignments.Any(assignment => assignment.ResponsibilityKind == ProcessResponsibilityKind.Responsible) &&
                    persistedArtifactId.HasValue &&
                    persistedMergeStep.ArtifactInputs.Any(input => input.ArtifactExpectationId == persistedArtifactId.Value);
            });

        var persistedImplementationStep = Assert.Single(persistedEditor.Steps, step => step.Key == process.ImplementationStepKey);
        var persistedMergeStep = Assert.Single(persistedEditor.Steps, step => step.Key == process.MergeStepKey);
        var persistedArtifact = Assert.Single(persistedImplementationStep.ArtifactExpectations, artifact => artifact.Title == "Implementation package");
        Assert.Contains(persistedMergeStep.RoleAssignments, assignment => assignment.ResponsibilityKind == ProcessResponsibilityKind.Responsible);
        Assert.Contains(persistedMergeStep.ArtifactInputs, input => input.ArtifactExpectationId == persistedArtifact.Id);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                mergeNodeId,
                "delete-link",
                0,
                0,
                "link",
                implementationNodeId,
                mergeNodeId,
                "flow",
                ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput,
                ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentImplementationStep = Assert.Single(editor.Steps, step => step.Key == process.ImplementationStepKey);
            var currentMergeStep = Assert.Single(editor.Steps, step => step.Key == process.MergeStepKey);
            var currentArtifact = Assert.Single(currentImplementationStep.ArtifactExpectations, artifact => artifact.Title == "Implementation package");
            Assert.Contains(currentMergeStep.ArtifactInputs, input => input.ArtifactExpectationId == currentArtifact.Id);
            Assert.Contains(ProcessCanvasBranching.GetOrderedDependencies(currentMergeStep), dependency => dependency.DependsOnStepId == currentImplementationStep.Id);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                mergeNodeId,
                "delete-link",
                0,
                0,
                "link",
                implementationNodeId,
                mergeNodeId,
                "flow",
                artifactOutputPortId,
                ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentMergeStep = Assert.Single(editor.Steps, step => step.Key == process.MergeStepKey);
            Assert.Empty(currentMergeStep.ArtifactInputs);
            Assert.Contains(ProcessCanvasBranching.GetOrderedDependencies(currentMergeStep), dependency => dependency.DependsOnStepId.HasValue);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                mergeNodeId,
                "delete-link",
                0,
                0,
                "link",
                implementationNodeId,
                mergeNodeId,
                "flow",
                ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput,
                ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentMergeStep = Assert.Single(editor.Steps, step => step.Key == process.MergeStepKey);
            Assert.Empty(ProcessCanvasBranching.GetOrderedDependencies(currentMergeStep));
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                mergeNodeId,
                "delete-link",
                0,
                0,
                "link",
                roleNodeId,
                mergeNodeId,
                "flow",
                responsibleRolePortId,
                responsibleStepPortId))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentMergeStep = Assert.Single(editor.Steps, step => step.Key == process.MergeStepKey);
            Assert.Empty(currentMergeStep.RoleAssignments);
        });

        cut.WaitForAssertion(() =>
        {
            var persistedEditor = processesService.GetEditorAsync(saveResult.Value, projectId).GetAwaiter().GetResult();
            var persistedMergeStep = Assert.Single(persistedEditor.Steps, step => step.Key == process.MergeStepKey);
            Assert.Empty(persistedMergeStep.RoleAssignments);
            Assert.Empty(persistedMergeStep.ArtifactInputs);
            Assert.Empty(ProcessCanvasBranching.GetOrderedDependencies(persistedMergeStep));
        });
    }

    [Fact]
    public async Task Steps_canvas_node_moves_update_role_and_branch_positions_in_editor_state()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Canvas movement project");
        var process = BuildCanvasAuthoringDefinition(projectId);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var role = Assert.Single(initialEditor.Roles, item => item.Key == process.RoleKey);
        var decisionStep = Assert.Single(initialEditor.Steps, step => step.Key == process.DecisionStepKey);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodesMoved(JsonSerializer.Serialize(new[]
        {
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionRoleNodeId(role), 320, 420),
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionBranchNodeId(decisionStep), 940, 260)
        })));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var currentRole = Assert.Single(editor.Roles, item => item.Key == process.RoleKey);
            var currentDecisionStep = Assert.Single(editor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Equal(320, currentRole.CanvasX);
            Assert.Equal(420, currentRole.CanvasY);
            Assert.Equal(940, currentDecisionStep.BranchCanvasX);
            Assert.Equal(260, currentDecisionStep.BranchCanvasY);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodeOpened(ProcessCanvasBranching.BuildDefinitionRoleNodeId(role)));

        cut.WaitForAssertion(() =>
        {
            var currentWorkbench = cut.FindComponent<CanvasWorkbench>();
            var roleNode = Assert.Single(currentWorkbench.Instance.Surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildDefinitionRoleNodeId(role));
            var branchNode = Assert.Single(currentWorkbench.Instance.Surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildDefinitionBranchNodeId(decisionStep));
            Assert.Equal(320, roleNode.X);
            Assert.Equal(420, roleNode.Y);
            Assert.Equal(940, branchNode.X);
            Assert.Equal(260, branchNode.Y);
        });

        cut.WaitForAssertion(() =>
        {
            var persistedEditor = processesService.GetEditorAsync(saveResult.Value, projectId).GetAwaiter().GetResult();
            var persistedRole = Assert.Single(persistedEditor.Roles, item => item.Key == process.RoleKey);
            var persistedDecisionStep = Assert.Single(persistedEditor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Equal(320, persistedRole.CanvasX);
            Assert.Equal(420, persistedRole.CanvasY);
            Assert.Equal(940, persistedDecisionStep.BranchCanvasX);
            Assert.Equal(260, persistedDecisionStep.BranchCanvasY);
        });
    }

    [Fact]
    public async Task Steps_canvas_node_moves_coalesce_rapid_updates_into_one_persisted_definition_update()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var activityService = harness.Context.Services.GetRequiredService<ActivityService>();
        var projectId = await CreateProjectAsync(projectsService, "Canvas movement coalescing project");
        var process = BuildCanvasAuthoringDefinition(projectId);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var baselineUpdateCount = (await activityService.ListRecentAsync(200))
            .Count(item =>
                item.Category == "processes" &&
                item.Action == "update-definition" &&
                item.Description == process.Editor.Name);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var role = Assert.Single(initialEditor.Roles, item => item.Key == process.RoleKey);
        var decisionStep = Assert.Single(initialEditor.Steps, step => step.Key == process.DecisionStepKey);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodesMoved(JsonSerializer.Serialize(new[]
        {
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionRoleNodeId(role), 320, 420),
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionBranchNodeId(decisionStep), 940, 260)
        })));

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodesMoved(JsonSerializer.Serialize(new[]
        {
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionRoleNodeId(role), 360, 460),
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionBranchNodeId(decisionStep), 1000, 300)
        })));

        cut.WaitForAssertion(() =>
        {
            var persistedEditor = processesService.GetEditorAsync(saveResult.Value, projectId).GetAwaiter().GetResult();
            var persistedRole = Assert.Single(persistedEditor.Roles, item => item.Key == process.RoleKey);
            var persistedDecisionStep = Assert.Single(persistedEditor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Equal(360, persistedRole.CanvasX);
            Assert.Equal(460, persistedRole.CanvasY);
            Assert.Equal(1000, persistedDecisionStep.BranchCanvasX);
            Assert.Equal(300, persistedDecisionStep.BranchCanvasY);
        });

        cut.WaitForAssertion(() =>
        {
            var currentUpdateCount = activityService.ListRecentAsync(200).GetAwaiter().GetResult()
                .Count(item =>
                    item.Category == "processes" &&
                    item.Action == "update-definition" &&
                    item.Description == process.Editor.Name);
            Assert.Equal(baselineUpdateCount + 1, currentUpdateCount);
        });
    }

    [Fact]
    public async Task Publish_action_flushes_pending_canvas_persistence_before_publishing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Workspace publish quiescence project");
        var process = BuildCanvasAuthoringDefinition(projectId);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var role = Assert.Single(initialEditor.Roles, item => item.Key == process.RoleKey);
        var decisionStep = Assert.Single(initialEditor.Steps, step => step.Key == process.DecisionStepKey);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodesMoved(JsonSerializer.Serialize(new[]
        {
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionRoleNodeId(role), 360, 460),
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionBranchNodeId(decisionStep), 1000, 300)
        })));

        Assert.NotNull(GetPrivateFieldValue<CancellationTokenSource>(cut.Instance, "pendingDefinitionCanvasPersistCts"));

        await InvokeWorkspaceMethodAsync(cut, "PublishAsync");

        cut.WaitForAssertion(() =>
        {
            var currentEditor = GetEditor(cut.Instance);
            var currentRole = Assert.Single(currentEditor.Roles, item => item.Key == process.RoleKey);
            var currentDecisionStep = Assert.Single(currentEditor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Equal(360, currentRole.CanvasX);
            Assert.Equal(460, currentRole.CanvasY);
            Assert.Equal(1000, currentDecisionStep.BranchCanvasX);
            Assert.Equal(300, currentDecisionStep.BranchCanvasY);
        });

        Assert.Null(GetPrivateFieldValue<CancellationTokenSource>(cut.Instance, "pendingDefinitionCanvasPersistCts"));
    }

    [Fact]
    public async Task Delete_action_cancels_pending_canvas_persistence_before_removing_the_definition()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Workspace delete quiescence project");
        var process = BuildCanvasAuthoringDefinition(projectId);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var role = Assert.Single(initialEditor.Roles, item => item.Key == process.RoleKey);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodesMoved(JsonSerializer.Serialize(new[]
        {
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionRoleNodeId(role), 360, 460)
        })));

        Assert.NotNull(GetPrivateFieldValue<CancellationTokenSource>(cut.Instance, "pendingDefinitionCanvasPersistCts"));

        await InvokeWorkspaceMethodAsync(cut, "DeleteAsync");

        Assert.Null(GetPrivateFieldValue<CancellationTokenSource>(cut.Instance, "pendingDefinitionCanvasPersistCts"));
        Assert.Null(GetPrivateFieldValue<Guid?>(cut.Instance, "selectedProcessId"));

        await Task.Delay(500);

        var definitions = await processesService.ListDefinitionsAsync(projectId);
        Assert.Empty(definitions);
    }

    [Fact]
    public async Task Export_action_flushes_pending_canvas_persistence_before_serializing_the_definition()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Workspace export quiescence project");
        var process = BuildCanvasAuthoringDefinition(projectId);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var role = Assert.Single(initialEditor.Roles, item => item.Key == process.RoleKey);
        var decisionStep = Assert.Single(initialEditor.Steps, step => step.Key == process.DecisionStepKey);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodesMoved(JsonSerializer.Serialize(new[]
        {
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionRoleNodeId(role), 360, 460),
            new CanvasWorkbenchNodePositionChange(ProcessCanvasBranching.BuildDefinitionBranchNodeId(decisionStep), 1000, 300)
        })));

        Assert.NotNull(GetPrivateFieldValue<CancellationTokenSource>(cut.Instance, "pendingDefinitionCanvasPersistCts"));

        await InvokeWorkspaceMethodAsync(cut, "ExportAsync");

        var exportJson = GetPrivateFieldValue<string>(cut.Instance, "exportJson");
        Assert.NotNull(exportJson);
        Assert.False(string.IsNullOrWhiteSpace(exportJson));
        var envelope = JsonSerializer.Deserialize<ProcessImportExportEnvelope>(exportJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Definition);

        var exportedRole = Assert.Single(envelope.Definition.Roles, item => item.Key == process.RoleKey);
        var exportedDecisionStep = Assert.Single(envelope.Definition.Steps, step => step.Key == process.DecisionStepKey);
        Assert.Equal(360, exportedRole.CanvasX);
        Assert.Equal(460, exportedRole.CanvasY);
        Assert.Equal(1000, exportedDecisionStep.BranchCanvasX);
        Assert.Equal(300, exportedDecisionStep.BranchCanvasY);
        Assert.Null(GetPrivateFieldValue<CancellationTokenSource>(cut.Instance, "pendingDefinitionCanvasPersistCts"));
    }

    [Fact]
    public async Task Steps_canvas_delete_action_removes_roles_and_steps()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var process = BuildCanvasAuthoringDefinition(await CreateProjectAsync(projectsService, "Canvas delete project"), assignDecisionRole: true, connectFixStep: true);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var role = Assert.Single(initialEditor.Roles, item => item.Key == process.RoleKey);
        var decisionStep = Assert.Single(initialEditor.Steps, step => step.Key == process.DecisionStepKey);
        var roleNodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(role);
        var decisionNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(decisionStep);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                roleNodeId,
                "delete",
                0,
                0))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            Assert.DoesNotContain(editor.Roles, item => item.Key == process.RoleKey);
            var currentDecisionStep = Assert.Single(editor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Null(currentDecisionStep.DecisionRoleRequirementId);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                decisionNodeId,
                "delete",
                0,
                0))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            Assert.DoesNotContain(editor.Steps, step => step.Key == process.DecisionStepKey);
            var currentFixStep = Assert.Single(editor.Steps, step => step.Key == process.FixStepKey);
            Assert.Empty(ProcessCanvasBranching.GetOrderedDependencies(currentFixStep));
        });
    }

    [Fact]
    public async Task Templates_dialog_shows_process_template_preview_with_add_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Template process import project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>(parameters => parameters
            .Add(component => component.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Workspace-visible process", cut.Markup));
        cut.Find("[data-testid='processes-templates-button']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='processes-template-library-dialog']"));
            Assert.NotNull(cut.Find("[data-testid='processes-template-library-item-ai-assisted-change-delivery']"));
        });

        cut.Find("[data-testid='processes-template-library-item-ai-assisted-change-delivery']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("AI-assisted change delivery with guarded delegation", cut.Markup);
            Assert.Contains("Add to my processes", cut.Markup);
            Assert.NotNull(cut.Find("[data-testid='processes-template-library-dialog']"));
        });
    }

    [Fact]
    public async Task Templates_dialog_uses_internal_scroll_regions_and_bounded_mermaid_viewport_markup()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Template containment project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>(parameters => parameters
            .Add(component => component.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Workspace-visible process", cut.Markup));
        cut.Find("[data-testid='processes-templates-button']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='processes-template-library-list-scroll']"));
            Assert.NotNull(cut.Find("[data-testid='processes-template-library-detail-scroll']"));
        });

        var listScroll = cut.Find("[data-testid='processes-template-library-list-scroll']");
        Assert.Contains("h-full", listScroll.ClassName, StringComparison.Ordinal);
        Assert.Contains("overflow-y-auto", listScroll.ClassName, StringComparison.Ordinal);

        var detailScroll = cut.Find("[data-testid='processes-template-library-detail-scroll']");
        Assert.Contains("h-full", detailScroll.ClassName, StringComparison.Ordinal);
        Assert.Contains("overflow-y-auto", detailScroll.ClassName, StringComparison.Ordinal);

        await SetTemplateLibraryPreviewTabAsync(cut, "diagrams");
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='processes-template-library-diagram-flowchart-clip']"));
            Assert.NotNull(cut.Find("[data-testid='processes-template-library-diagram-flowchart-viewport']"));
        });
    }

    [Fact]
    public async Task Templates_dialog_adds_role_templates_into_the_current_definition_without_closing_the_modal()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Template role import project");
        var process = BuildCanvasAuthoringDefinition(projectId);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        cut.Find("[data-testid='processes-templates-button']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-template-library-dialog']")));

        await SetTemplateLibraryCategoryAsync(cut, "roles");
        cut.Find("[data-testid='processes-template-library-add-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            Assert.Equal(2, editor.Roles.Count);
        });
        Assert.NotNull(cut.Find("[data-testid='processes-template-library-dialog']"));
    }

    [Fact]
    public async Task Templates_dialog_adds_artifact_templates_into_the_selected_definition_step_without_closing_the_modal()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Template artifact import project");
        var process = BuildCanvasAuthoringDefinition(projectId);
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        cut.Find("[data-testid='processes-templates-button']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='processes-template-library-dialog']")));

        await SetTemplateLibraryCategoryAsync(cut, "artifacts");
        cut.Find("[data-testid='processes-template-library-add-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            var targetStep = Assert.Single(editor.Steps, step => step.Key == process.DecisionStepKey);
            Assert.Single(targetStep.ArtifactExpectations);
        });
        Assert.NotNull(cut.Find("[data-testid='processes-template-library-dialog']"));
    }

    private static ProcessDefinitionEditorModel BuildDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Workspace-visible process",
            Summary = "Ensures the global processes workspace loads on first render.",
            ValueStatement = "Show persisted definitions without query-string routing.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Persist the current workspace model without token dependencies.",
            ChangeSummary = "Initial component-test definition.",
            ConstitutionRuleSummary = "The first render must hydrate persisted definitions.",
            OperatingModeSummary = "Authoring-first workspace validation.",
            SimulationReadinessSummary = "Safe for component validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "workspace-owner",
                    DisplayName = "Workspace owner",
                    Purpose = "Own the workspace verification flow.",
                    StaffingIntent = "Single accountable owner for the smoke definition.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Workspace owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "workspace-intake",
                    Title = "Capture workspace intake",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Definition metadata.",
                    OutputContractSummary = "Loaded workspace definition.",
                    EvidenceContractSummary = "Visible definition list entry.",
                    DecisionRightsSummary = "Workspace owner confirms the first render.",
                    ExceptionPolicySummary = "Escalate when the list remains empty.",
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Keep the workspace owner assigned."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "workspace-review",
                    Title = "Review rendered workspace",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Loaded workspace definition.",
                    OutputContractSummary = "Rendered process workspace state.",
                    EvidenceContractSummary = "Process name visible in the first render.",
                    DecisionRightsSummary = "Workspace owner confirms visibility.",
                    ExceptionPolicySummary = "Fail when the page does not hydrate.",
                    TargetLeadHours = 1,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 420,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Keep the workspace owner assigned."
                        }
                    ]
                }
            ]
        };
    }


    private static CanvasAuthoringFixture BuildCanvasAuthoringDefinition(
        Guid projectId,
        bool assignDecisionRole = true,
        bool connectFixStep = true)
    {
        var roleId = Guid.NewGuid();
        var decisionStepId = Guid.NewGuid();
        var repairsOutcomeId = Guid.NewGuid();
        var fixStepId = Guid.NewGuid();
        var implementationStepId = Guid.NewGuid();
        var mergeStepId = Guid.NewGuid();

        var editor = new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Canvas authoring process",
            Summary = "Exercise process-canvas delete and connection authoring flows.",
            ValueStatement = "Keep process branching editable on the canvas.",
            CustomerName = "Acme Customer",
            OwnerName = "Canvas owner",
            GovernancePolicySummary = "Canvas links must map back to strongly-typed process state.",
            ChangeSummary = "Component-test coverage for process canvas authoring tools.",
            ConstitutionRuleSummary = "Connections must remain explicit and reversible.",
            OperatingModeSummary = "Authoring-first validation.",
            SimulationReadinessSummary = "Safe for component validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "qa-lead",
                    DisplayName = "QA lead",
                    Purpose = "Own review routing decisions.",
                    StaffingIntent = "Single quality authority.",
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 40
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = decisionStepId,
                    Key = "route-review",
                    Title = "Route review outcome",
                    StepKind = ProcessStepKind.Decision,
                    DecisionRoleRequirementId = assignDecisionRole ? roleId : null,
                    OutputContractSummary = "Choose what happens after review.",
                    DecisionRightsSummary = "Decide whether the change returns for repair or keeps moving.",
                    CanvasX = 140,
                    CanvasY = 180,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = repairsOutcomeId,
                            Key = "repairs-required",
                            Title = "Repairs required",
                            Description = "Return the change to implementation."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = fixStepId,
                    Key = "repair-change",
                    Title = "Repair change",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = connectFixStep
                        ? CreateDependencies((decisionStepId, repairsOutcomeId))
                        : [],
                    OutputContractSummary = "Updated implementation ready for another review.",
                    CanvasX = 760,
                    CanvasY = 60
                },
                new ProcessStepEditorModel
                {
                    Id = implementationStepId,
                    Key = "implement-approved-change",
                    Title = "Implement approved change",
                    StepKind = ProcessStepKind.Work,
                    OutputContractSummary = "Ready to merge.",
                    CanvasX = 420,
                    CanvasY = 360,
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = Guid.NewGuid(),
                            ArtifactKind = ProcessArtifactKind.Deliverable,
                            Title = "Implementation package",
                            IsRequired = true,
                            ValidationRequirementSummary = "Package must remain reviewable and linked to the merge path."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = mergeStepId,
                    Key = "merge-change",
                    Title = "Merge change",
                    StepKind = ProcessStepKind.End,
                    OutputContractSummary = "Merged code.",
                    CanvasX = 760,
                    CanvasY = 360
                }
            ]
        };

        return new CanvasAuthoringFixture(
            editor,
            "qa-lead",
            "route-review",
            "repair-change",
            "implement-approved-change",
            "merge-change");
    }

    [Fact]
    public async Task Steps_canvas_connection_actions_create_and_delete_messaging_links_and_classify_them_visually()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var process = BuildMessagingCanvasDefinition(await CreateProjectAsync(projectsService, "Canvas messaging project"));
        var saveResult = await processesService.SaveAsync(process.Editor);

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();
        cut.WaitForAssertion(() => Assert.Contains(process.Editor.Name, cut.Markup));
        await ActivateStepsTabAsync(cut);

        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();
        var initialEditor = GetEditor(cut.Instance);
        var sourceRole = Assert.Single(initialEditor.Roles, item => item.Key == process.SourceRoleKey);
        var targetRole = Assert.Single(initialEditor.Roles, item => item.Key == process.TargetRoleKey);
        var sourceNodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(sourceRole);
        var targetNodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(targetRole);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                targetNodeId,
                "connection:create",
                0,
                0,
                "link",
                sourceNodeId,
                targetNodeId,
                "messaging",
                ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput,
                ProcessCanvasCatalog.DefinitionPorts.RoleMessagingInput))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            Assert.Contains(editor.MessagingPolicies, item =>
                item.SourceRoleRequirementId == sourceRole.Id &&
                item.TargetRoleRequirementId == targetRole.Id);

            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            var currentSourceNode = Assert.Single(surface.Nodes, item => item.Id == sourceNodeId);
            var currentTargetNode = Assert.Single(surface.Nodes, item => item.Id == targetNodeId);
            Assert.Contains(currentSourceNode.OutputPorts, port =>
                port.Id == ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput &&
                port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.Messaging);
            Assert.Contains(currentTargetNode.InputPorts, port =>
                port.Id == ProcessCanvasCatalog.DefinitionPorts.RoleMessagingInput &&
                port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.Messaging);
            Assert.Contains(surface.Links, link =>
                link.SourceId == sourceNodeId &&
                link.TargetId == targetNodeId &&
                link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput &&
                link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.RoleMessagingInput &&
                string.Equals(link.Kind, "messaging", StringComparison.Ordinal));
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                targetNodeId,
                "delete-link",
                0,
                0,
                "link",
                sourceNodeId,
                targetNodeId,
                "messaging",
                ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput,
                ProcessCanvasCatalog.DefinitionPorts.RoleMessagingInput))));

        cut.WaitForAssertion(() =>
        {
            var editor = GetEditor(cut.Instance);
            Assert.Empty(editor.MessagingPolicies);

            var surface = cut.FindComponent<CanvasWorkbench>().Instance.Surface;
            Assert.DoesNotContain(surface.Links, link =>
                link.SourceId == sourceNodeId &&
                link.TargetId == targetNodeId &&
                link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput &&
                link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.RoleMessagingInput &&
                string.Equals(link.Kind, "messaging", StringComparison.Ordinal));
        });
    }

    private static MessagingCanvasFixture BuildMessagingCanvasDefinition(Guid projectId)
    {
        var sourceRoleId = Guid.NewGuid();
        var targetRoleId = Guid.NewGuid();

        var editor = new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Canvas messaging policy process",
            Summary = "Exercise process-owned Messaging links on the authoring canvas.",
            ValueStatement = "Messaging links must stay explicit, directional, and visually distinct.",
            CustomerName = "Acme Customer",
            OwnerName = "Messaging owner",
            GovernancePolicySummary = "Direct role messaging requires an explicit process-owned policy link.",
            ChangeSummary = "Component-test coverage for Messaging canvas links.",
            ConstitutionRuleSummary = "No role may bypass the Messaging graph in authoring or runtime.",
            OperatingModeSummary = "Authoring-first validation.",
            SimulationReadinessSummary = "Safe for component validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = sourceRoleId,
                    Key = "delivery-lead",
                    DisplayName = "Delivery lead",
                    Purpose = "Initiate direct role-to-role delivery handoffs.",
                    StaffingIntent = "Primary delivery contact.",
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 60
                },
                new ProcessRoleEditorModel
                {
                    Id = targetRoleId,
                    Key = "review-lead",
                    DisplayName = "Review lead",
                    Purpose = "Receive delivery-side direct role messaging.",
                    StaffingIntent = "Primary review contact.",
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 40
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Key = "capture-handoff",
                    Title = "Capture delivery handoff",
                    StepKind = ProcessStepKind.Start,
                    OutputContractSummary = "Visible role nodes with a valid process definition.",
                    CanvasX = 360,
                    CanvasY = 200
                }
            ]
        };

        return new MessagingCanvasFixture(editor, "delivery-lead", "review-lead");
    }

    private static async Task ActivateStepsTabAsync(IRenderedComponent<ProcessWorkspace> cut)
    {
        var method = typeof(ProcessWorkspace).GetMethod("HandleDetailTabChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        await cut.InvokeAsync(async () =>
        {
            var task = method!.Invoke(cut.Instance, [2]) as Task;
            Assert.NotNull(task);
            await task!;
        });

        cut.Render();
        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<CanvasWorkbench>()));
    }

    private static async Task SetTemplateLibraryCategoryAsync(IRenderedComponent<ProcessWorkspace> cut, string key)
    {
        var dialog = cut.FindComponent<ProcessTemplateLibraryDialog>();
        var method = typeof(ProcessTemplateLibraryDialog).GetMethod("HandleCategoryChangedAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        await cut.InvokeAsync(async () =>
        {
            var task = method!.Invoke(dialog.Instance, [key]) as Task;
            Assert.NotNull(task);
            await task!;
        });

        cut.Render();
        cut.WaitForAssertion(() =>
        {
            var refreshedDialog = cut.FindComponent<ProcessTemplateLibraryDialog>();
            Assert.Contains(key switch
            {
                "roles" => "Add to my roles",
                "artifacts" => "Add to my artifacts",
                _ => "Add to my processes"
            }, refreshedDialog.Markup);
        });
    }

    private static async Task SetTemplateLibraryPreviewTabAsync(IRenderedComponent<ProcessWorkspace> cut, string key)
    {
        var dialog = cut.FindComponent<ProcessTemplateLibraryDialog>();
        var method = typeof(ProcessTemplateLibraryDialog).GetMethod("HandlePreviewTabChangedAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        await cut.InvokeAsync(async () =>
        {
            var task = method!.Invoke(dialog.Instance, [key]) as Task;
            Assert.NotNull(task);
            await task!;
        });

        cut.Render();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(key switch
            {
                "diagrams" => "Mermaid",
                "markdown" => "Markdown",
                "json" => "JSON",
                _ => "Overview"
            }, cut.Markup);
        });
    }

    private static ProcessDefinitionEditorModel GetEditor(ProcessWorkspace component)
    {
        var field = typeof(ProcessWorkspace).GetField("editor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ProcessDefinitionEditorModel>(field!.GetValue(component));
    }

    private static T? GetPrivateFieldValue<T>(ProcessWorkspace component, string fieldName)
    {
        var field = typeof(ProcessWorkspace).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T?)field!.GetValue(component);
    }

    private static async Task InvokeWorkspaceMethodAsync(IRenderedComponent<ProcessWorkspace> cut, string methodName)
    {
        var method = typeof(ProcessWorkspace).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        await cut.InvokeAsync(async () =>
        {
            var task = method!.Invoke(cut.Instance, []) as Task;
            Assert.NotNull(task);
            await task!;
        });

        cut.Render();
    }

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

    private static async Task<ProcessDefinitionEditorModel> WaitForPersistedEditorAsync(
        ProcessesService processesService,
        Guid definitionId,
        Guid projectId,
        Func<ProcessDefinitionEditorModel, bool> predicate)
    {
        ProcessDefinitionEditorModel? lastObserved = null;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            lastObserved = await processesService.GetEditorAsync(definitionId, projectId);
            if (predicate(lastObserved))
            {
                return lastObserved;
            }

            await Task.Delay(100);
        }

        return lastObserved ?? await processesService.GetEditorAsync(definitionId, projectId);
    }

    private static List<ProcessStepDependencyEditorModel> CreateDependencies(params (Guid StepId, Guid? BranchOutcomeId)[] items)
    {
        return items
            .Select(item => new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = item.StepId,
                DependsOnBranchOutcomeId = item.BranchOutcomeId
            })
            .ToList();
    }

    private sealed record CanvasAuthoringFixture(
        ProcessDefinitionEditorModel Editor,
        string RoleKey,
        string DecisionStepKey,
        string FixStepKey,
        string ImplementationStepKey,
        string MergeStepKey);

    private sealed record MessagingCanvasFixture(
        ProcessDefinitionEditorModel Editor,
        string SourceRoleKey,
        string TargetRoleKey);
}
