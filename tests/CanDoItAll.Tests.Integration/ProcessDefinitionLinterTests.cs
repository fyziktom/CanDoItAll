using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessDefinitionLinterTests
{
    [Fact]
    public void ProcessStepOperationContractState_SB06_INV_001_adds_target_scope_implied_operations_and_validation_issues()
    {
        var normalized = ProcessStepOperationContractState.NormalizeDeclaredContract(
            ProcessStepKind.Review,
            [
                ProcessStepOperation.RunValidation,
                ProcessStepOperation.WriteManagedProcessArtifacts,
                ProcessStepOperation.RunValidation
            ],
            ProcessStepTargetScope.ExternalProductTargetReadOnly);

        Assert.Equal(ProcessStepTargetScope.ExternalProductTargetReadOnly, normalized.OperationTargetScope);
        Assert.Equal(
            [
                ProcessStepOperation.ReadProcessContext,
                ProcessStepOperation.ReadProjectStructure,
                ProcessStepOperation.WriteManagedProcessArtifacts,
                ProcessStepOperation.RunValidation
            ],
            normalized.AllowedOperations);
        Assert.DoesNotContain(normalized.Issues, issue =>
            issue.Code == ProcessStepOperationContractState.InvalidCombinationCode);

        var invalid = ProcessStepOperationContractState.NormalizeDeclaredContract(
            ProcessStepKind.Review,
            [ProcessStepOperation.MutateProductTarget],
            ProcessStepTargetScope.ExternalProductTargetReadOnly);

        Assert.Contains(invalid.Issues, issue =>
            issue.Code == ProcessStepOperationContractState.InvalidCombinationCode &&
            issue.Message.Contains(nameof(ProcessStepOperation.MutateProductTarget), StringComparison.Ordinal));
    }

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
    public void Analyze_strict_marks_missing_operation_contract_as_error()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Implement Blazor product component",
            StepKind = ProcessStepKind.Work,
            OutputContractSummary = "Implement the Blazor component in the product root.",
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

        var advisory = ProcessDefinitionLinter.Analyze(model);
        var strict = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.Contains(advisory.Issues, issue =>
            issue.Code == "processes.lint.step-operation-contract-missing" &&
            issue.Severity == ProcessDefinitionLintSeverity.Warning);
        Assert.Contains(strict.Issues, issue =>
            issue.Code == "processes.lint.step-operation-contract-missing" &&
            issue.Severity == ProcessDefinitionLintSeverity.Error &&
            !string.IsNullOrWhiteSpace(issue.Suggestion));
        Assert.True(strict.HasErrors);
    }

    [Fact]
    public void Analyze_SB08_INV_001_warns_when_operation_contract_is_text_inferred()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Implement Blazor product component",
            StepKind = ProcessStepKind.Work,
            OutputContractSummary = "Operation contract: allowed operations MutateProductTarget; target scope ExternalProductTargetMutable. Implement the Blazor component in the product root.",
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

        Assert.Contains(result.Issues, issue =>
            issue.Code == "processes.lint.step-operation-contract-inferred" &&
            issue.Severity == ProcessDefinitionLintSeverity.Warning);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-operation-contract-missing");
    }

    [Fact]
    public void Analyze_SB12_INV_001_strict_rejects_text_inferred_operation_contract()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Implement Blazor product component",
            StepKind = ProcessStepKind.Work,
            OutputContractSummary = "Operation contract: allowed operations MutateProductTarget; target scope ExternalProductTargetMutable. Implement the Blazor component in the product root.",
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

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "processes.lint.step-operation-contract-inferred" &&
            issue.Severity == ProcessDefinitionLintSeverity.Error);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Analyze_SB08_INV_001_accepts_typed_operation_contract_without_text_markers()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Implement Blazor product component",
            StepKind = ProcessStepKind.Work,
            AllowedOperations =
            [
                ProcessStepOperation.MutateProductTarget,
                ProcessStepOperation.WriteManagedProcessArtifacts
            ],
            OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetMutable,
            OutputContractSummary = "Implement the Blazor component in the product root.",
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

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-operation-contract-missing");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-operation-contract-inferred");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-operation-contract-partial");
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Analyze_SB08_INV_001_rejects_partial_typed_operation_contract()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Implement Blazor product component",
            StepKind = ProcessStepKind.Work,
            AllowedOperations =
            [
                ProcessStepOperation.MutateProductTarget
            ],
            OutputContractSummary = "Implement the Blazor component in the product root.",
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

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "processes.lint.step-operation-contract-partial" &&
            issue.Severity == ProcessDefinitionLintSeverity.Error);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Analyze_SB06_INV_001_strict_rejects_invalid_typed_operation_contract_combination()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Validate Blazor product component",
            StepKind = ProcessStepKind.Review,
            AllowedOperations =
            [
                ProcessStepOperation.MutateProductTarget,
                ProcessStepOperation.RunValidation
            ],
            OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetReadOnly,
            OutputContractSummary = "Validate the Blazor component in the product root.",
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Runtime validation evidence",
                    ArtifactKind = ProcessArtifactKind.Evidence,
                    ValidationRequirementSummary = "Must include browser proof and console status."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "processes.lint.step-operation-contract-invalid" &&
            issue.Severity == ProcessDefinitionLintSeverity.Error &&
            issue.Message.Contains("MutateProductTarget", StringComparison.Ordinal));
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Analyze_strict_marks_missing_artifact_recovery_policy_as_error()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Review implementation and route repair",
            StepKind = ProcessStepKind.Review,
            DecisionRightsSummary = "Approve or reject the implementation.",
            BranchOutcomes =
            [
                new ProcessStepBranchOutcomeEditorModel
                {
                    Key = "repair-required",
                    Title = "Repair required"
                }
            ],
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "QA finding",
                    ArtifactKind = ProcessArtifactKind.Evidence,
                    ValidationRequirementSummary = "Must record the failing acceptance criteria."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "processes.lint.artifact-recovery-policy-missing" &&
            issue.Severity == ProcessDefinitionLintSeverity.Error &&
            !string.IsNullOrWhiteSpace(issue.Suggestion));
    }

    [Fact]
    public void Analyze_accepts_business_plan_artifact_destination_without_product_mutation_warning()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Create market expansion business plan",
            StepKind = ProcessStepKind.Work,
            OutputContractSummary = "Create the business plan report and budget appendix as governed process deliverables.",
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Business plan report",
                    ArtifactKind = ProcessArtifactKind.Deliverable,
                    ValidationRequirementSummary = "Must include market assumptions, budget, owners, and approval criteria."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-operation-contract-missing");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-boundary-ambiguous");
    }

    [Fact]
    public void Analyze_SB10_INV_001_accepts_architecture_report_without_product_mutation_contract()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Create target operating model architecture report",
            StepKind = ProcessStepKind.Work,
            OutputContractSummary = "Create architecture options, decision criteria, tradeoffs, and a recommendation.",
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Architecture recommendation report",
                    ArtifactKind = ProcessArtifactKind.Deliverable,
                    ValidationRequirementSummary = "Must include context, options, decision criteria, risks, and recommended next action."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-operation-contract-missing");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-boundary-ambiguous");
        Assert.False(result.HasErrors);
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
    public void Analyze_accepts_workflow_step_with_required_validated_artifact_mapping()
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
                    ValidationRequirementSummary = "Must include approval id, approver, decision, timestamp, and routed next action."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.workflow-artifact-validation-weak");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.workflow-artifact-contract-missing");
    }

    [Fact]
    public void Analyze_SB09_INV_001_strict_rejects_required_workflow_artifact_without_explicit_output_mapping()
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
                    ValidationRequirementSummary = "Must include approval id, approver, decision, timestamp, and routed next action."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "processes.lint.workflow-artifact-output-mapping-missing" &&
            issue.Severity == ProcessDefinitionLintSeverity.Error);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Analyze_SB09_INV_001_accepts_required_workflow_artifact_with_explicit_output_mapping()
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
                    ValidationRequirementSummary = "Must include approval id, approver, decision, timestamp, and routed next action.",
                    WorkflowOutputId = "finance-approval-packet",
                    WorkflowOutputKind = WorkflowArtifactKind.Json
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.workflow-artifact-output-mapping-missing");
        Assert.False(result.HasErrors);
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
    public void Analyze_SB09_INV_001_strict_rejects_required_subprocess_artifact_without_child_mapping()
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

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "processes.lint.subprocess-child-artifact-mapping-missing" &&
            issue.Severity == ProcessDefinitionLintSeverity.Error);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Analyze_SB09_INV_001_accepts_required_subprocess_artifact_with_child_mapping()
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
                    ValidationRequirementSummary = "Child process must produce matching incident review evidence.",
                    SubprocessChildArtifactExpectationId = Guid.NewGuid()
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.subprocess-child-artifact-mapping-missing");
        Assert.False(result.HasErrors);
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

    [Fact]
    public void Analyze_accepts_legal_no_go_review_without_runtime_proof_requirement()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Review legal approval and no-go decision",
            StepKind = ProcessStepKind.Approval,
            DecisionRightsSummary = "Approve or no-go the contract.",
            ExceptionPolicySummary = "Artifact recovery: block when the legal decision log cannot be recorded; no runtime proof is required.",
            BranchOutcomes =
            [
                new ProcessStepBranchOutcomeEditorModel
                {
                    Key = "no-go",
                    Title = "No-go"
                }
            ],
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Legal decision log",
                    ArtifactKind = ProcessArtifactKind.Decision,
                    ValidationRequirementSummary = "Record legal rationale, decision owner, approval or no-go outcome, and required next action."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.decision-log-runtime-proof-conflict");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.artifact-recovery-policy-missing");
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Analyze_accepts_manufacturing_inspection_artifacts_without_software_assumptions()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Complete manufacturing line inspection",
            StepKind = ProcessStepKind.Review,
            OutputContractSummary = "Record inspection checklist, measurements, and nonconformance log.",
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Inspection checklist",
                    ArtifactKind = ProcessArtifactKind.Checklist,
                    ValidationRequirementSummary = "Must include station id, inspected controls, pass/fail marks, and inspector."
                },
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Inspection evidence log",
                    ArtifactKind = ProcessArtifactKind.Evidence,
                    ValidationRequirementSummary = "Must include measurements, sampled lot, nonconformance ids, and containment action."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-operation-contract-missing");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.decision-log-runtime-proof-conflict");
    }

    [Fact]
    public void Analyze_accepts_research_dataset_and_report_artifacts_without_product_mutation_warning()
    {
        var model = CreateBaseDefinition();
        model.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Create research dataset and findings report",
            StepKind = ProcessStepKind.Work,
            OutputContractSummary = "Create a dataset extract and research findings report.",
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Research dataset",
                    ArtifactKind = ProcessArtifactKind.Dataset,
                    ValidationRequirementSummary = "Must include source, sampling window, schema, and limitations."
                },
                new ProcessArtifactExpectationEditorModel
                {
                    Title = "Findings report",
                    ArtifactKind = ProcessArtifactKind.Deliverable,
                    ValidationRequirementSummary = "Must include findings, confidence, caveats, and cited dataset rows."
                }
            ]
        });

        var result = ProcessDefinitionLinter.Analyze(model, ProcessDefinitionLintMode.Strict);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-operation-contract-missing");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "processes.lint.step-boundary-ambiguous");
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
