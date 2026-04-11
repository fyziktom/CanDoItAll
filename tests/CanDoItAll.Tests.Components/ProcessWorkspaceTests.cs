using System.Reflection;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

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
            Assert.Contains("Process management", cut.Markup);
            Assert.Contains("Workspace-visible process", cut.Markup);
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
    public async Task Steps_canvas_node_moves_update_role_and_branch_positions_in_editor_state()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var process = BuildCanvasAuthoringDefinition(await CreateProjectAsync(projectsService, "Canvas movement project"));
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
                    DependsOnStepId = intakeStepId,
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
                    DependsOnStepId = connectFixStep ? decisionStepId : null,
                    DependsOnBranchOutcomeId = connectFixStep ? repairsOutcomeId : null,
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
                    CanvasY = 360
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

    private static ProcessDefinitionEditorModel GetEditor(ProcessWorkspace component)
    {
        var field = typeof(ProcessWorkspace).GetField("editor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ProcessDefinitionEditorModel>(field!.GetValue(component));
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

    private sealed record CanvasAuthoringFixture(
        ProcessDefinitionEditorModel Editor,
        string RoleKey,
        string DecisionStepKey,
        string FixStepKey,
        string ImplementationStepKey,
        string MergeStepKey);
}
