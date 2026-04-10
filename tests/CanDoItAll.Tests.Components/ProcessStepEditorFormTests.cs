using Bunit;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessStepEditorFormTests
{
    [Fact]
    public void Render_shows_branch_outcomes_and_dependency_outcome_options()
    {
        using var context = new TestContext();
        var decisionStepId = Guid.NewGuid();
        var uiOutcomeId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var dependencyStep = new ProcessStepEditorModel
        {
            Id = decisionStepId,
            Title = "Route requested revision",
            BranchOutcomes =
            [
                new ProcessStepBranchOutcomeEditorModel
                {
                    Id = uiOutcomeId,
                    Key = "ui-review",
                    Title = "UI architect revision",
                    Description = "Send the change through UI review."
                }
            ]
        };
        var currentStep = new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Review selected path",
            DependsOnStepId = decisionStepId,
            DependsOnBranchOutcomeId = uiOutcomeId,
            DecisionRoleRequirementId = roleId,
            BranchOutcomes =
            [
                new ProcessStepBranchOutcomeEditorModel
                {
                    Id = Guid.NewGuid(),
                    Key = "human-approval",
                    Title = "Human approval required",
                    Description = "Escalate the decision to a human approver."
                }
            ]
        };

        var cut = context.RenderComponent<ProcessStepEditorForm>(
            ComponentParameter.CreateParameter(nameof(ProcessStepEditorForm.Model), currentStep),
            ComponentParameter.CreateParameter(nameof(ProcessStepEditorForm.AvailableRoles), new List<ProcessRoleEditorModel>
            {
                new()
                {
                    Id = roleId,
                    DisplayName = "Routing owner"
                }
            }),
            ComponentParameter.CreateParameter(nameof(ProcessStepEditorForm.DependencyOptions), new List<ProcessStepEditorModel>
            {
                dependencyStep
            }));

        Assert.Contains("Branch outcomes", cut.Markup);
        Assert.Contains("Depends on outcome", cut.Markup);
        Assert.Contains("Any dependency outcome", cut.Markup);
        Assert.Contains("Decision maker role", cut.Markup);
        Assert.Contains("UI architect revision", cut.Markup);
        Assert.Contains("Human approval required", cut.Markup);
    }
}
