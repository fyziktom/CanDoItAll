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
                []),
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
        };

        var factory = new ProcessCanvasSurfaceFactory();
        var surface = factory.BuildRunSurface(run, stepRuns);

        var branchNode = Assert.Single(surface.Nodes, node => node.Id == ProcessCanvasBranching.BuildRuntimeBranchNodeId(stepRuns[0].Id));
        Assert.Equal(3, branchNode.OutputPorts.Count);
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
