using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasSurfaceFactoryTests
{
    private const string DecisionActionId = "process-step.decision";
    private const string AddBranchOutcomeActionId = "process-definition.add-branch-outcome";

    [Fact]
    public void Definition_surface_exposes_decision_creation_and_branch_dependency_chips()
    {
        var branchOutcomeId = Guid.NewGuid();
        var decisionStepId = Guid.NewGuid();
        var editor = new ProcessDefinitionEditorModel
        {
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = decisionStepId,
                    Key = "route-change",
                    Title = "Route change",
                    StepKind = ProcessStepKind.Decision,
                    OutputContractSummary = "Choose the next lane.",
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = branchOutcomeId,
                            Key = "db-review",
                            Title = "DB review"
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = Guid.NewGuid(),
                    Key = "db-review-step",
                    Title = "Review database impact",
                    StepKind = ProcessStepKind.Review,
                    DependsOnStepId = decisionStepId,
                    DependsOnBranchOutcomeId = branchOutcomeId,
                    OutputContractSummary = "Data review completed."
                }
            ]
        };

        var factory = new ProcessCanvasSurfaceFactory();
        var surface = factory.BuildDefinitionSurface(editor);

        Assert.Contains(surface.Chrome.QuickCreateActions, action => action.ActionId == DecisionActionId);
        Assert.Contains(surface.Chrome.GroupContextActions, action => action.ActionId == DecisionActionId);

        var decisionNode = Assert.Single(surface.Nodes, node => node.Title == "Route change");
        Assert.Contains(decisionNode.ContextActions, action => action.ActionId == AddBranchOutcomeActionId);

        var routedNode = Assert.Single(surface.Nodes, node => node.Title == "Review database impact");
        Assert.Contains(routedNode.FooterChips, chip => chip.Text == "DB review");
        Assert.Contains("DB review", routedNode.LeadText, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_surface_projects_branch_router_ports_and_decision_role_links()
    {
        var roleId = Guid.NewGuid();
        var decisionStepId = Guid.NewGuid();
        var fixOutcomeId = Guid.NewGuid();
        var repairStepId = Guid.NewGuid();
        var editor = new ProcessDefinitionEditorModel
        {
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "peer-reviewer",
                    DisplayName = "Peer reviewer",
                    Purpose = "Own the code review routing decision."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = decisionStepId,
                    Key = "route-review",
                    Title = "Route code review outcome",
                    StepKind = ProcessStepKind.Decision,
                    DecisionRoleRequirementId = roleId,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = fixOutcomeId,
                            Key = "repairs-required",
                            Title = "Repairs required"
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = repairStepId,
                    Key = "repair-change",
                    Title = "Repair change set",
                    StepKind = ProcessStepKind.Work,
                    DependsOnStepId = decisionStepId,
                    DependsOnBranchOutcomeId = fixOutcomeId
                },
                new ProcessStepEditorModel
                {
                    Id = Guid.NewGuid(),
                    Key = "qa-lane",
                    Title = "Validate QA lane",
                    StepKind = ProcessStepKind.Review,
                    DependsOnStepId = decisionStepId
                }
            ]
        };

        var factory = new ProcessCanvasSurfaceFactory();
        var surface = factory.BuildDefinitionSurface(editor);

        var branchNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildDefinitionBranchNodeId(editor.Steps[0]));
        Assert.Equal(2, branchNode.InputPorts.Count);
        Assert.Equal(3, branchNode.OutputPorts.Count);
        Assert.Contains(branchNode.OutputPorts, port => port.Label == ProcessCanvasBranching.DefaultRouteTitle);
        Assert.Contains(branchNode.OutputPorts, port => port.Label == ProcessCanvasBranching.ErrorRouteTitle);

        var roleNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildDefinitionRoleNodeId(editor.Roles[0]));
        Assert.Contains(surface.Links, link =>
            link.SourceId == roleNode.Id &&
            link.TargetId == branchNode.Id &&
            link.SourcePortId == ProcessCanvasBranching.RoleDecisionOutputPortId &&
            link.TargetPortId == ProcessCanvasBranching.DecisionRoleInputPortId);

        var repairLink = Assert.Single(surface.Links, link => link.TargetId == ProcessCanvasBranching.BuildDefinitionStepNodeId(editor.Steps[1]));
        Assert.Equal(branchNode.Id, repairLink.SourceId);
        Assert.Equal(ProcessCanvasBranching.BuildOutcomePortId(editor.Steps[0].BranchOutcomes.Single(outcome => outcome.Id == fixOutcomeId)), repairLink.SourcePortId);

        var defaultLink = Assert.Single(surface.Links, link => link.TargetId == ProcessCanvasBranching.BuildDefinitionStepNodeId(editor.Steps[2]));
        Assert.Equal(branchNode.Id, defaultLink.SourceId);
        Assert.Equal(
            ProcessCanvasBranching.BuildOutcomePortId(editor.Steps[0].BranchOutcomes.Single(outcome => outcome.Title == ProcessCanvasBranching.DefaultRouteTitle)),
            defaultLink.SourcePortId);
    }

    [Fact]
    public void Definition_surface_keeps_decision_role_input_available_without_a_bound_role_and_honors_delete_mode()
    {
        var editor = new ProcessDefinitionEditorModel
        {
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = Guid.NewGuid(),
                    Key = "route-review",
                    Title = "Route review",
                    StepKind = ProcessStepKind.Decision,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = Guid.NewGuid(),
                            Key = "repairs-required",
                            Title = "Repairs required"
                        }
                    ]
                }
            ]
        };

        var factory = new ProcessCanvasSurfaceFactory();
        var surface = factory.BuildDefinitionSurface(editor, mode: "delete");

        Assert.Equal("delete", surface.Mode);

        var branchNode = Assert.Single(surface.Nodes, node => node.Kind == "process-branch-router");
        Assert.Contains(branchNode.InputPorts, port => port.Id == ProcessCanvasBranching.StepInputPortId);
        var decisionRolePort = Assert.Single(branchNode.InputPorts, port => port.Id == ProcessCanvasBranching.DecisionRoleInputPortId);
        Assert.Equal("Decision authority", decisionRolePort.Label);
        Assert.False(decisionRolePort.IsRequired);
    }

    [Fact]
    public void Definition_surface_projects_step_participant_ports_artifact_ports_and_explicit_links()
    {
        var reviewerRoleId = Guid.NewGuid();
        var responsibleRoleId = Guid.NewGuid();
        var implementationStepId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();
        var implementationArtifactId = Guid.NewGuid();
        var editor = new ProcessDefinitionEditorModel
        {
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = reviewerRoleId,
                    Key = "review-lead",
                    DisplayName = "Review lead"
                },
                new ProcessRoleEditorModel
                {
                    Id = responsibleRoleId,
                    Key = "developer",
                    DisplayName = "Developer"
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = implementationStepId,
                    Key = "implementation",
                    Title = "Prepare implementation package",
                    StepKind = ProcessStepKind.Work,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            Id = Guid.NewGuid(),
                            RoleRequirementId = responsibleRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            IsRequired = true
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = implementationArtifactId,
                            ArtifactKind = ProcessArtifactKind.Deliverable,
                            Title = "Implementation package",
                            IsRequired = true
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = reviewStepId,
                    Key = "peer-review",
                    Title = "Complete peer review",
                    StepKind = ProcessStepKind.Review,
                    DependsOnStepId = implementationStepId,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = implementationStepId
                        }
                    ],
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            Id = Guid.NewGuid(),
                            RoleRequirementId = reviewerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Reviewer,
                            IsRequired = true
                        }
                    ],
                    ArtifactInputs =
                    [
                        new ProcessStepArtifactInputEditorModel
                        {
                            Id = Guid.NewGuid(),
                            ArtifactExpectationId = implementationArtifactId
                        }
                    ]
                }
            ]
        };

        var factory = new ProcessCanvasSurfaceFactory();
        var surface = factory.BuildDefinitionSurface(editor);

        var implementationNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildDefinitionStepNodeId(editor.Steps[0]));
        Assert.Contains(implementationNode.OutputPorts, port => port.Id == ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput);
        Assert.Contains(implementationNode.OutputPorts, port => port.Label == "Implementation package");
        Assert.Contains(implementationNode.InputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(ProcessResponsibilityKind.Responsible) &&
            port.IsRequired);
        Assert.Contains(implementationNode.InputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(ProcessResponsibilityKind.Responsible) &&
            port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.ResponsibilityResponsible &&
            port.AccentColor == "#0ea5e9");

        var reviewNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildDefinitionStepNodeId(editor.Steps[1]));
        Assert.Contains(reviewNode.InputPorts, port => port.Id == ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput);
        Assert.Contains(reviewNode.InputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(ProcessResponsibilityKind.Reviewer) &&
            port.IsRequired);
        Assert.Contains(reviewNode.InputPorts, port => port.Id == ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs);
        Assert.Contains(reviewNode.InputPorts, port => port.Id == ProcessCanvasCatalog.DefinitionPorts.StepDecisionAuthorityInput);
        Assert.Contains(reviewNode.InputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput &&
            port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.Structural &&
            port.AccentColor == "#2563eb");
        Assert.Contains(reviewNode.InputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs &&
            port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.Artifact &&
            port.AccentColor == "#db2777");

        var reviewerRoleNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildDefinitionRoleNodeId(editor.Roles[0]));
        Assert.Equal(ProcessCanvasCatalog.OrderedResponsibilities.Count + 1, reviewerRoleNode.OutputPorts.Count);
        Assert.Contains(reviewerRoleNode.OutputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(ProcessResponsibilityKind.Reviewer) &&
            port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.ResponsibilityReviewer &&
            port.AccentColor == "#6366f1");

        Assert.Contains(surface.Links, link =>
            link.SourceId == ProcessCanvasBranching.BuildDefinitionRoleNodeId(editor.Roles[1]) &&
            link.TargetId == implementationNode.Id &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(ProcessResponsibilityKind.Responsible) &&
            link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(ProcessResponsibilityKind.Responsible));
        Assert.Contains(surface.Links, link =>
            link.SourceId == reviewerRoleNode.Id &&
            link.TargetId == reviewNode.Id &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(ProcessResponsibilityKind.Reviewer) &&
            link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(ProcessResponsibilityKind.Reviewer));
        Assert.Contains(surface.Links, link =>
            link.SourceId == implementationNode.Id &&
            link.TargetId == reviewNode.Id &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput &&
            link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput);
        Assert.Contains(surface.Links, link =>
            link.SourceId == implementationNode.Id &&
            link.TargetId == reviewNode.Id &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(editor.Steps[0].ArtifactExpectations[0]) &&
            link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs);
    }

    [Fact]
    public void Runtime_surface_projects_branch_router_for_routed_steps()
    {
        var run = new ProcessRunListItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Branching review run",
            ProcessRunStatus.Active,
            ProcessOperatingMode.GovernedLive,
            0,
            0,
            0,
            0,
            0m,
            0m,
            DateTimeOffset.UtcNow);
        var stepDefinitionId = Guid.NewGuid();
        var repairOutcomeId = Guid.NewGuid();
        var stepRuns = new List<ProcessStepRunViewModel>
        {
            new(
                Guid.NewGuid(),
                stepDefinitionId,
                null,
                null,
                null,
                1,
                "Route review outcome",
                ProcessStepKind.Decision,
                ProcessStepRunStatus.InProgress,
                "Peer reviewer",
                "Routing the review outcome.",
                string.Empty,
                string.Empty,
                null,
                string.Empty,
                10,
                5,
                0,
                0,
                ProcessCapabilityGapSeverity.None,
                [
                    new ProcessStepBranchOutcomeOptionViewModel(repairOutcomeId, "Repairs required", string.Empty),
                    new ProcessStepBranchOutcomeOptionViewModel(Guid.NewGuid(), ProcessCanvasBranching.DefaultRouteTitle, string.Empty),
                    new ProcessStepBranchOutcomeOptionViewModel(Guid.NewGuid(), ProcessCanvasBranching.ErrorRouteTitle, string.Empty)
                ]),
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                stepDefinitionId,
                repairOutcomeId,
                null,
                2,
                "Repair change set",
                ProcessStepKind.Work,
                ProcessStepRunStatus.Pending,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                string.Empty,
                0,
                0,
                0,
                0,
                ProcessCapabilityGapSeverity.None,
                [])
            {
                Dependencies =
                [
                    new ProcessStepDependencyViewModel(stepDefinitionId, repairOutcomeId)
                ],
                ResponsibilityPorts =
                [
                    new ProcessStepRunResponsibilityPortViewModel(ProcessResponsibilityKind.Responsible, true, 1),
                    new ProcessStepRunResponsibilityPortViewModel(ProcessResponsibilityKind.Reviewer, false, 2)
                ],
                ArtifactInputCount = 2,
                ArtifactOutputs =
                [
                    new ProcessStepRunArtifactPortViewModel(Guid.NewGuid(), "QA validation evidence pack", true)
                ]
            },
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                stepDefinitionId,
                null,
                null,
                3,
                "Validate QA lane",
                ProcessStepKind.Review,
                ProcessStepRunStatus.Pending,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                string.Empty,
                0,
                0,
                0,
                0,
                ProcessCapabilityGapSeverity.None,
                [])
            {
                Dependencies =
                [
                    new ProcessStepDependencyViewModel(stepDefinitionId, null)
                ],
                DecisionRoleTitle = "Review lead"
            }
        };

        var factory = new ProcessCanvasSurfaceFactory();
        var surface = factory.BuildRunSurface(run, stepRuns);

        var branchNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildRuntimeBranchNodeId(stepRuns[0].Id));
        Assert.Equal(3, branchNode.OutputPorts.Count);
        var repairNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildRuntimeStepNodeId(stepRuns[1].Id));
        Assert.Contains(repairNode.InputPorts, port => port.Id == ProcessCanvasCatalog.RuntimePorts.GetStepResponsibilityInputPortId(ProcessResponsibilityKind.Responsible));
        Assert.Contains(repairNode.InputPorts, port => port.Id == ProcessCanvasCatalog.RuntimePorts.GetStepResponsibilityInputPortId(ProcessResponsibilityKind.Reviewer));
        Assert.Contains(repairNode.InputPorts, port => port.Id == ProcessCanvasCatalog.RuntimePorts.StepArtifactInputs);
        Assert.Contains(repairNode.OutputPorts, port => port.Label == "QA validation evidence pack");
        var qaNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildRuntimeStepNodeId(stepRuns[2].Id));
        Assert.Contains(qaNode.InputPorts, port => port.Id == ProcessCanvasCatalog.RuntimePorts.StepDecisionAuthorityInput);
        Assert.Contains(surface.Links, link =>
            link.SourceId == branchNode.Id &&
            link.TargetId == ProcessCanvasBranching.BuildRuntimeStepNodeId(stepRuns[1].Id) &&
            link.SourcePortId == ProcessCanvasBranching.BuildOutcomePortId(stepRuns[0].AvailableBranchOutcomes[0]));
        Assert.Contains(surface.Links, link =>
            link.SourceId == branchNode.Id &&
            link.TargetId == ProcessCanvasBranching.BuildRuntimeStepNodeId(stepRuns[2].Id) &&
            link.SourcePortId == ProcessCanvasBranching.BuildOutcomePortId(stepRuns[0].AvailableBranchOutcomes[1]));
    }
}
