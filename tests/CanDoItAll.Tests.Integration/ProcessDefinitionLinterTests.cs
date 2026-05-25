using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessDefinitionLinterTests
{
    [Fact]
    public void Analyze_warns_when_architecture_step_demands_product_implementation()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Architecture review and Blazor implementation",
            StepKind = ProcessStepKind.Review,
            OutputContractSummary = "Review architecture and implement the Blazor component.",
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Implementation change set",
                    ArtifactKind = ProcessArtifactKind.Deliverable,
                    ValidationRequirementSummary = "Must list product files changed."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model);

        Assert.Contains(result.Issues, issue => issue.Code == "processes.lint.step-boundary-ambiguous");
        Assert.Contains("step-boundary-ambiguous", result.BuildDryRunSummary(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_warns_when_workflow_step_has_weak_required_artifact_mapping()
    {
        var workflowRoleId = Guid.NewGuid();
        var model = CreateBaseDefinition();
        model.Roles.Add(new ProcessRoleEditorModel
        {
            Id = workflowRoleId,
            DisplayName = "Workflow executor",
            PreferredExecutorKind = ProcessExecutorKindNames.Workflow,
            PreferredWorkflowDefinitionId = Guid.NewGuid()
        });
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Run finance approval workflow",
            StepKind = ProcessStepKind.Work,
            RoleAssignments =
            [
                new ProcessStepRoleRequirementEditorModel
                {
                    RoleRequirementId = workflowRoleId
                }
            ],
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Finance approval packet",
                    ArtifactKind = ProcessArtifactKind.Deliverable,
                    ValidationRequirementSummary = string.Empty
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model);

        Assert.Contains(result.Issues, issue => issue.Code == "processes.lint.workflow-artifact-validation-weak");
    }

    [Fact]
    public void Analyze_warns_when_subprocess_parent_declares_required_artifacts()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Run operations incident subprocess",
            StepKind = ProcessStepKind.Subprocess,
            SubprocessDefinitionId = Guid.NewGuid(),
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Operations incident review",
                    ArtifactKind = ProcessArtifactKind.Deliverable,
                    ValidationRequirementSummary = "Child process must produce matching incident review evidence."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model);

        Assert.Contains(result.Issues, issue => issue.Code == "processes.lint.subprocess-parent-artifact-mapping-review");
    }

    [Fact]
    public void Analyze_warns_when_finance_approval_has_no_disposition_branches()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Finance approval no-go decision",
            StepKind = ProcessStepKind.Approval,
            DecisionRightsSummary = "Approve, reject, or no-go the spend request."
        });

        var result = ProcessDefinitionLinter.Analyze(model);

        Assert.Contains(result.Issues, issue => issue.Code == "processes.lint.branch-outcome-missing");
    }

    [Fact]
    public void Analyze_accepts_legal_decision_log_without_runtime_proof_warning()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Record legal decision log",
            StepKind = ProcessStepKind.Decision,
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Legal decision log",
                    ArtifactKind = ProcessArtifactKind.Decision,
                    ValidationRequirementSummary = "Record approval, unavailable findings, and legal rationale."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.decision-log-runtime-proof-conflict");
    }

    private static ProcessDefinitionEditorModel CreateBaseDefinition()
    {
        return new ProcessDefinitionEditorModel
        {
            Name = "Governed process",
            ValueStatement = "Deliver governed outcomes.",
            OwnerName = "Owner",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Owner",
                    PreferredExecutorKind = ProcessExecutorKindNames.AiAgent
                }
            ]
        };
    }
}
