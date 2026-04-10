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
}
