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
        var evidenceArtifactId = Guid.NewGuid();

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
            ],
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Id = evidenceArtifactId,
                    ArtifactKind = ProcessArtifactKind.Evidence,
                    Title = "Review evidence pack"
                }
            ]
        };
        var currentStep = new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Review selected path",
            Dependencies =
            [
                new ProcessStepDependencyEditorModel
                {
                    Id = Guid.NewGuid(),
                    DependsOnStepId = decisionStepId,
                    DependsOnBranchOutcomeId = uiOutcomeId
                }
            ],
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
            ],
            ArtifactInputs =
            [
                new ProcessStepArtifactInputEditorModel
                {
                    ArtifactExpectationId = evidenceArtifactId
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
        Assert.Contains("Canvas-managed dependencies", cut.Markup);
        Assert.Contains("Canvas-managed artifact inputs", cut.Markup);
        Assert.Contains("Route requested revision", cut.Markup);
        Assert.Contains("Decision maker role", cut.Markup);
        Assert.Contains("UI architect revision", cut.Markup);
        Assert.Contains("Human approval required", cut.Markup);
        Assert.Contains("Review evidence pack", cut.Markup);
    }

    [Fact]
    public void Render_SB08_INV_001_operation_contract_controls_update_model()
    {
        using var context = new TestContext();
        var currentStep = new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Create report"
        };

        var cut = context.RenderComponent<ProcessStepEditorForm>(
            ComponentParameter.CreateParameter(nameof(ProcessStepEditorForm.Model), currentStep));

        cut.Find("[data-testid='processes-operation-target-scope-select']")
            .Change(ProcessStepTargetScope.ExternalArtifactDestination.ToString());
        cut.Find("[data-testid='processes-operation-WriteExternalArtifactDestination']")
            .Change(true);

        Assert.Equal(ProcessStepTargetScope.ExternalArtifactDestination, currentStep.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.WriteExternalArtifactDestination, currentStep.AllowedOperations);

        cut.Find("[data-testid='processes-operation-WriteExternalArtifactDestination']")
            .Change(false);

        Assert.DoesNotContain(ProcessStepOperation.WriteExternalArtifactDestination, currentStep.AllowedOperations);
    }
}
