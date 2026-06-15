using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasSurfaceFactoryTests
{
    private const string AddBranchOutcomeActionId = "process-definition.add-branch-outcome";
    private static readonly string[] ExpectedQuickCreateActionIds =
    [
        "process-definition.open-toolbox",
        "process-role.product-owner",
        "process-role.solution-architect",
        "process-step.intake",
        "process-step.architecture",
        "process-step.implementation",
        "process-step.subprocess",
        "process-step.qa",
        "process-step.release-approval"
    ];
    private static readonly string[] ExpectedGroupContextActionIds =
    [
        "process-definition.edit-step",
        "process-definition.add-dependent-step",
        "process-definition.add-subprocess-step",
        "process-definition.add-role-binding",
        "process-definition.add-artifact-expectation",
        "process-definition.remove-step"
    ];

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
                    Dependencies = CreateDependencies((decisionStepId, branchOutcomeId)),
                    OutputContractSummary = "Data review completed."
                }
            ]
        };

        var factory = CreateFactory();
        var surface = factory.BuildDefinitionSurface(editor);

        Assert.Equal(ExpectedQuickCreateActionIds, surface.Chrome.QuickCreateActions.Select(action => action.ActionId));
        Assert.Equal(ExpectedGroupContextActionIds, surface.Chrome.GroupContextActions.Select(action => action.ActionId));

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
                    Dependencies = CreateDependencies((decisionStepId, fixOutcomeId))
                },
                new ProcessStepEditorModel
                {
                    Id = Guid.NewGuid(),
                    Key = "qa-lane",
                    Title = "Validate QA lane",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((decisionStepId, null))
                }
            ]
        };

        var factory = CreateFactory();
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

        var factory = CreateFactory();
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
                    Dependencies = CreateDependencies((implementationStepId, null)),
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

        var factory = CreateFactory();
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
        Assert.Equal(ProcessCanvasCatalog.OrderedResponsibilities.Count + 2, reviewerRoleNode.OutputPorts.Count);
        Assert.Contains(reviewerRoleNode.OutputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(ProcessResponsibilityKind.Reviewer) &&
            port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.ResponsibilityReviewer &&
            port.AccentColor == "#6366f1");
        Assert.Contains(reviewerRoleNode.OutputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput &&
            port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.Messaging);
        Assert.Contains(reviewerRoleNode.OutputPorts, port =>
            port.Id == ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput &&
            port.CategoryKey == ProcessCanvasCatalog.ConnectionCategories.DecisionAuthority);

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

        var artifactNodeId = ProcessCanvasBranching.BuildDefinitionArtifactNodeId(editor.Steps[0].ArtifactExpectations[0]);
        var artifactCloneNodeId = ProcessCanvasBranching.BuildDefinitionArtifactCloneNodeId(
            editor.Steps[0].ArtifactExpectations[0],
            editor.Steps[1].ArtifactInputs[0],
            editor.Steps[1]);
        var artifactNode = Assert.Single(surface.Nodes, node => node.Id == artifactNodeId);
        var artifactCloneNode = Assert.Single(surface.Nodes, node => node.Id == artifactCloneNodeId);
        Assert.Equal(ProcessCanvasCatalog.NodeKinds.DefinitionArtifact, artifactNode.Kind);
        Assert.Equal(ProcessCanvasCatalog.NodeKinds.DefinitionArtifact, artifactCloneNode.Kind);
        Assert.Equal("artifact", artifactNode.PaletteKey);
        Assert.Equal("artifact", artifactCloneNode.PaletteKey);
        Assert.Equal(artifactNode.AccentColor, artifactCloneNode.AccentColor);
        Assert.NotEqual(implementationNode.AccentColor, artifactNode.AccentColor);
        Assert.Contains(artifactNode.ContextActions, action => action.ActionId == "process-definition.create-artifact-clone");
        Assert.Contains(artifactCloneNode.ContextActions, action => action.ActionId == "process-definition.highlight-artifact-clones");
        Assert.Contains(surface.Links, link =>
            link.SourceId == implementationNode.Id &&
            link.TargetId == artifactNode.Id &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(editor.Steps[0].ArtifactExpectations[0]) &&
            link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.ArtifactSourceInput);
        Assert.Contains(surface.Links, link =>
            link.SourceId == artifactCloneNode.Id &&
            link.TargetId == reviewNode.Id &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.ArtifactUsageOutput &&
            link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs);
        Assert.DoesNotContain(surface.Links, link =>
            link.SourceId == implementationNode.Id &&
            link.TargetId == reviewNode.Id &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(editor.Steps[0].ArtifactExpectations[0]) &&
            link.TargetPortId == ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs);
    }

    [Fact]
    public void Definition_surface_renders_repeated_role_as_step_role_instances()
    {
        var roleId = Guid.NewGuid();
        var firstStepId = Guid.NewGuid();
        var secondStepId = Guid.NewGuid();
        var editor = new ProcessDefinitionEditorModel
        {
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "delivery-lead",
                    DisplayName = "Delivery lead"
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = firstStepId,
                    Key = "scope",
                    Title = "Clarify scope",
                    StepKind = ProcessStepKind.Start,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            Id = Guid.NewGuid(),
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            IsRequired = true
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = secondStepId,
                    Key = "release",
                    Title = "Approve release",
                    StepKind = ProcessStepKind.Approval,
                    Dependencies = CreateDependencies((firstStepId, null)),
                    DecisionRoleRequirementId = roleId,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            Id = Guid.NewGuid(),
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Approver,
                            IsRequired = true
                        }
                    ]
                }
            ]
        };

        var factory = CreateFactory();
        var surface = factory.BuildDefinitionSurface(editor);

        var canonicalRoleNodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(editor.Roles[0]);
        var scopeRoleNodeId = ProcessCanvasBranching.BuildDefinitionRoleInstanceNodeId(editor.Roles[0], editor.Steps[0]);
        var releaseRoleNodeId = ProcessCanvasBranching.BuildDefinitionRoleInstanceNodeId(editor.Roles[0], editor.Steps[1]);
        var roleNodes = surface.Nodes
            .Where(node => node.Kind == ProcessCanvasCatalog.NodeKinds.DefinitionRole)
            .ToList();

        Assert.DoesNotContain(roleNodes, node => node.Id == canonicalRoleNodeId);
        Assert.Contains(roleNodes, node => node.Id == scopeRoleNodeId);
        Assert.Contains(roleNodes, node => node.Id == releaseRoleNodeId);
        Assert.All(roleNodes, roleNode =>
            Assert.Contains(roleNode.ContextActions, action => action.ActionId == "process-definition.highlight-role-clones"));
        Assert.True(ProcessCanvasBranching.TryResolveDefinitionRoleToken(scopeRoleNodeId, out var roleToken));
        Assert.Equal(roleId.ToString("D"), roleToken);

        Assert.Contains(surface.Links, link =>
            link.SourceId == scopeRoleNodeId &&
            link.TargetId == ProcessCanvasBranching.BuildDefinitionStepNodeId(editor.Steps[0]) &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(ProcessResponsibilityKind.Responsible));
        Assert.Contains(surface.Links, link =>
            link.SourceId == releaseRoleNodeId &&
            link.TargetId == ProcessCanvasBranching.BuildDefinitionStepNodeId(editor.Steps[1]) &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(ProcessResponsibilityKind.Approver));
        Assert.Contains(surface.Links, link =>
            link.SourceId == releaseRoleNodeId &&
            link.TargetId == ProcessCanvasBranching.BuildDefinitionBranchNodeId(editor.Steps[1]) &&
            link.SourcePortId == ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput);
    }

    [Fact]
    public void Runtime_surface_projects_branch_router_for_routed_steps()
    {
        var runId = Guid.NewGuid();
        var run = new ProcessRunListItem(
            Id: runId,
            ProcessDefinitionId: Guid.NewGuid(),
            ProcessDefinitionVersionId: Guid.NewGuid(),
            ParentRunId: null,
            ParentStepRunId: null,
            RootRunId: runId,
            HierarchyDepth: 0,
            ProjectId: null,
            Name: "Branching review run",
            Status: ProcessRunStatus.Active,
            OperatingMode: ProcessOperatingMode.GovernedLive,
            ManagerAgentId: null,
            ManagerAgentName: string.Empty,
            CompletedStepCount: 0,
            TotalStepCount: 0,
            BlockedStepCount: 0,
            CapabilityGapCount: 0,
            EstimatedCost: 0m,
            ActualCost: 0m,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
        var stepDefinitionId = Guid.NewGuid();
        var repairOutcomeId = Guid.NewGuid();
        var stepRuns = new List<ProcessStepRunViewModel>
        {
            new(
                Guid.NewGuid(),
                stepDefinitionId,
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

        var factory = CreateFactory();
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

    private static ProcessCanvasSurfaceFactory CreateFactory(string? packRoot = null)
    {
        return new ProcessCanvasSurfaceFactory(
            new ProcessCanvasChromeCatalogService(
                new ProcessTemplatePackLoader(packRoot)));
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
}
