using Bunit;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasSelectionPanelTests
{
    [Fact]
    public void Definition_selection_exposes_branch_actions_and_routed_step_tools()
    {
        using var context = new TestContext();
        var receiver = new object();
        var routedOutcomeId = Guid.NewGuid();
        var definitionStep = new ProcessStepEditorModel
        {
            Title = "Route the review lane",
            StepKind = ProcessStepKind.Decision,
            TargetLeadHours = 2,
            OutputContractSummary = "Select the next review path.",
            BranchOutcomes =
            [
                new ProcessStepBranchOutcomeEditorModel
                {
                    Id = routedOutcomeId,
                    Key = "db-review",
                    Title = "DB review",
                    Description = "Route work to the data architecture lane."
                },
                new ProcessStepBranchOutcomeEditorModel
                {
                    Id = Guid.NewGuid(),
                    Key = "ui-review",
                    Title = "UI review"
                }
            ],
            RoleAssignments =
            [
                new ProcessStepRoleRequirementEditorModel
                {
                    RoleRequirementId = Guid.NewGuid(),
                    ResponsibilityKind = ProcessResponsibilityKind.Responsible
                }
            ],
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Decision record"
                }
            ]
        };

        var branchOutcomeAdds = 0;
        Guid? selectedRoutedOutcomeId = null;
        var cut = context.RenderComponent<ProcessCanvasSelectionPanel>(
            parameters => parameters
                .Add(component => component.DefinitionStep, definitionStep)
                .Add(component => component.AddBranchOutcome, EventCallback.Factory.Create(receiver, () => branchOutcomeAdds++))
                .Add(component => component.AddRoutedStep, EventCallback.Factory.Create<Guid?>(receiver, outcomeId => selectedRoutedOutcomeId = outcomeId)));

        Assert.Contains("Add branch outcome", cut.Markup);
        Assert.Contains("Outcome paths", cut.Markup);

        cut.Find("[data-testid='processes-canvas-selection-add-branch-outcome']").Click();
        cut.FindAll("[data-testid='processes-canvas-selection-add-routed-step']")[0].Click();

        Assert.Equal(1, branchOutcomeAdds);
        Assert.Equal(routedOutcomeId, selectedRoutedOutcomeId);
    }

    [Fact]
    public void Definition_role_selection_exposes_edit_role_action()
    {
        using var context = new TestContext();
        var receiver = new object();
        var roleEdits = 0;
        var definitionRole = new ProcessRoleEditorModel
        {
            DisplayName = "Review lead",
            Purpose = "Own the explicit routing decision for review outcomes.",
            PreferredExecutorKind = "person",
            DefaultAllocationPercent = 40,
            IsRequired = true
        };

        var cut = context.RenderComponent<ProcessCanvasSelectionPanel>(
            parameters => parameters
                .Add(component => component.DefinitionRole, definitionRole)
                .Add(component => component.EditDefinitionRole, EventCallback.Factory.Create(receiver, () => roleEdits++)));

        Assert.Contains("Role definition", cut.Markup);
        Assert.Contains("Review lead", cut.Markup);

        cut.Find("[data-testid='processes-canvas-selection-edit-role']").Click();

        Assert.Equal(1, roleEdits);
    }

    [Fact]
    public void Runtime_actions_disable_invalid_transitions_for_ready_steps()
    {
        using var context = new TestContext();
        var runtimeStep = new ProcessStepRunViewModel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            1,
            "Route requested revision",
            ProcessStepKind.Decision,
            ProcessStepRunStatus.Ready,
            "Routing owner",
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
            []);

        var cut = context.RenderComponent<ProcessCanvasSelectionPanel>(
            parameters => parameters
                .Add(component => component.IsRuntime, true)
                .Add(component => component.RuntimeStep, runtimeStep));

        var buttons = cut.FindAll("button");
        var startButton = Assert.Single(buttons, button => button.TextContent.Trim() == "Start");
        var completeButton = Assert.Single(buttons, button => button.TextContent.Trim() == "Complete");
        var blockButton = Assert.Single(buttons, button => button.TextContent.Trim() == "Block");

        Assert.False(startButton.HasAttribute("disabled"));
        Assert.True(completeButton.HasAttribute("disabled"));
        Assert.False(blockButton.HasAttribute("disabled"));
    }
}
