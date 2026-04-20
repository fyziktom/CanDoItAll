using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AgentFrameworkAuditProofTests
{
    private static DirectMessagingDefinitionFixture BuildDirectMessagingDefinitionEditor(Guid projectId)
    {
        var sourceRoleId = Guid.NewGuid();
        var targetRoleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();

        return new DirectMessagingDefinitionFixture(
            new ProcessDefinitionEditorModel
            {
                ProjectId = projectId,
                Name = "Playwright direct messaging proof process",
                Summary = "Browser proof for process-owned direct role messaging.",
                ValueStatement = "Direct role messaging must stay process-owned and auditable.",
                CustomerName = "Playwright Customer",
                OwnerName = "Playwright Owner",
                GovernancePolicySummary = "Direct messaging is allowed only for explicit role links with explicit runtime permission.",
                ChangeSummary = "Playwright browser proof definition.",
                ConstitutionRuleSummary = "No role may bypass process-owned messaging policy or governance state.",
                OperatingModeSummary = "Assisted execution for browser proof.",
                SimulationReadinessSummary = "Safe for Playwright validation.",
                Roles =
                [
                    new ProcessRoleEditorModel
                    {
                        Id = sourceRoleId,
                        Key = "delivery-lead",
                        DisplayName = "Delivery lead",
                        Purpose = "Initiate delivery handoffs.",
                        StaffingIntent = "Primary delivery authority.",
                        PreferredExecutorKind = "person",
                        DefaultAllocationPercent = 60
                    },
                    new ProcessRoleEditorModel
                    {
                        Id = targetRoleId,
                        Key = "review-lead",
                        DisplayName = "Review lead",
                        Purpose = "Receive delivery review handoffs.",
                        StaffingIntent = "Primary review authority.",
                        PreferredExecutorKind = "person",
                        DefaultAllocationPercent = 40
                    }
                ],
                MessagingPolicies =
                [
                    new ProcessRoleMessagingPolicyEditorModel
                    {
                        SourceRoleRequirementId = sourceRoleId,
                        TargetRoleRequirementId = targetRoleId
                    }
                ],
                Steps =
                [
                    new ProcessStepEditorModel
                    {
                        Id = intakeStepId,
                        Key = "capture-delivery-handoff",
                        Title = "Capture delivery handoff",
                        StepKind = ProcessStepKind.Start,
                        InputContractSummary = "Delivery package ready for review.",
                        OutputContractSummary = "Structured handoff ready for reviewer.",
                        EvidenceContractSummary = "Visible run-scoped message evidence.",
                        DecisionRightsSummary = "Delivery lead confirms readiness.",
                        ExceptionPolicySummary = "Escalate when package evidence is incomplete.",
                        TargetLeadHours = 1,
                        CanvasX = 180,
                        CanvasY = 180,
                        RoleAssignments =
                        [
                            new ProcessStepRoleRequirementEditorModel
                            {
                                RoleRequirementId = sourceRoleId,
                                ResponsibilityKind = ProcessResponsibilityKind.Responsible
                            }
                        ]
                    },
                    new ProcessStepEditorModel
                    {
                        Key = "review-delivery-handoff",
                        Title = "Review delivery handoff",
                        StepKind = ProcessStepKind.Review,
                        InputContractSummary = "Structured delivery handoff package.",
                        OutputContractSummary = "Reviewed package ready for the next stage.",
                        EvidenceContractSummary = "Review note or direct-message evidence.",
                        DecisionRightsSummary = "Review lead confirms reviewability.",
                        ExceptionPolicySummary = "Block the run when evidence is missing.",
                        TargetLeadHours = 1,
                        Dependencies =
                        [
                            new ProcessStepDependencyEditorModel
                            {
                                Id = Guid.NewGuid(),
                                DependsOnStepId = intakeStepId
                            }
                        ],
                        CanvasX = 520,
                        CanvasY = 180,
                        RoleAssignments =
                        [
                            new ProcessStepRoleRequirementEditorModel
                            {
                                RoleRequirementId = targetRoleId,
                                ResponsibilityKind = ProcessResponsibilityKind.Responsible
                            }
                        ]
                    }
                ]
            },
            sourceRoleId,
            targetRoleId);
    }

    private static CalculatorDefinitionFixture BuildCalculatorDeliveryDefinitionEditor(Guid projectId)
    {
        var managerRoleId = Guid.NewGuid();
        var builderRoleId = Guid.NewGuid();
        var reviewerRoleId = Guid.NewGuid();
        var generationStepId = Guid.NewGuid();
        var handoffStepId = Guid.NewGuid();
        var generationArtifactId = Guid.NewGuid();

        const string builderRoleName = "Calculator builder agent";
        const string reviewerRoleName = "Calculator reviewer agent";
        const string generationStepTitle = "SC03 Generate Blazor calculator delivery";
        const string handoffStepTitle = "Approve generated calculator delivery";
        const string reviewStepTitle = "SC10 Review generated calculator delivery";

        return new CalculatorDefinitionFixture(
            new ProcessDefinitionEditorModel
            {
                ProjectId = projectId,
                Name = "SC11 calculator delivery process",
                Summary = "Launch, staff, execute, message, and close a simple Blazor calculator delivery through the integrated process runtime.",
                ValueStatement = "Prove that CanDoItAll can define and complete a multi-agent software delivery workflow end to end.",
                CustomerName = "Integrated proof customer",
                OwnerName = "Playwright process owner",
                GovernancePolicySummary = "AI delivery steps must stay bound to explicit launch approval, projected evidence, and process-owned messaging.",
                ChangeSummary = "Full AgentFramework integration closure proof for SC11.",
                ConstitutionRuleSummary = "All execution, messaging, approvals, and evidence must remain inside the process runtime.",
                OperatingModeSummary = "Assisted execution with human handoff between automated calculator generation and review.",
                SimulationReadinessSummary = "Safe for deterministic browser and service validation.",
                Roles =
                [
                    new ProcessRoleEditorModel
                    {
                        Id = managerRoleId,
                        Key = "delivery-manager",
                        DisplayName = "Delivery manager",
                        Purpose = "Approve the staffed launch and release the review handoff.",
                        StaffingIntent = "Human owner of the integrated SC11 workflow.",
                        PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                        PreferredExecutorKind = "person",
                        DefaultAllocationPercent = 100
                    },
                    new ProcessRoleEditorModel
                    {
                        Id = builderRoleId,
                        Key = "calculator-builder-ai",
                        DisplayName = builderRoleName,
                        Purpose = "Generate and build the calculator delivery through SC03.",
                        StaffingIntent = "Technical AI resource for deterministic calculator generation.",
                        PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                        PreferredExecutorKind = "AI agent",
                        DefaultAllocationPercent = 100
                    },
                    new ProcessRoleEditorModel
                    {
                        Id = reviewerRoleId,
                        Key = "calculator-reviewer-ai",
                        DisplayName = reviewerRoleName,
                        Purpose = "Inspect the generated calculator delivery through SC10.",
                        StaffingIntent = "Technical AI resource for deterministic delivery review.",
                        PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                        PreferredExecutorKind = "AI agent",
                        DefaultAllocationPercent = 100
                    }
                ],
                MessagingPolicies =
                [
                    new ProcessRoleMessagingPolicyEditorModel
                    {
                        SourceRoleRequirementId = builderRoleId,
                        TargetRoleRequirementId = reviewerRoleId
                    }
                ],
                Steps =
                [
                    new ProcessStepEditorModel
                    {
                        Id = generationStepId,
                        Key = "sc03-generate-calculator",
                        Title = generationStepTitle,
                        StepKind = ProcessStepKind.Start,
                        InputContractSummary = "A simple calculator specification and a ready AI builder role.",
                        OutputContractSummary = "A generated Blazor calculator project with a successful build receipt.",
                        EvidenceContractSummary = "SC03 must persist generation-report.md and the generated project files.",
                        DecisionRightsSummary = "The calculator builder agent completes the deterministic generation flow.",
                        ExceptionPolicySummary = "Fail the run when the calculator project or build receipt is missing.",
                        TargetLeadHours = 1,
                        CanvasX = 180,
                        CanvasY = 180,
                        RoleAssignments =
                        [
                            new ProcessStepRoleRequirementEditorModel
                            {
                                RoleRequirementId = builderRoleId,
                                ResponsibilityKind = ProcessResponsibilityKind.Responsible
                            }
                        ],
                        ArtifactExpectations =
                        [
                            new ProcessArtifactExpectationEditorModel
                            {
                                Id = generationArtifactId,
                                ArtifactKind = ProcessArtifactKind.Deliverable,
                                Title = "generation-report.md",
                                ValidationRequirementSummary = "SC03 must project the calculator generation evidence into the process run."
                            }
                        ]
                    },
                    new ProcessStepEditorModel
                    {
                        Id = handoffStepId,
                        Key = "approve-review-handoff",
                        Title = handoffStepTitle,
                        StepKind = ProcessStepKind.Work,
                        InputContractSummary = "The generated calculator delivery and builder evidence.",
                        OutputContractSummary = "A human-approved handoff that keeps the run active for direct builder-to-reviewer messaging.",
                        EvidenceContractSummary = "The manager confirms the delivery is ready for review and preserves the handoff trail.",
                        DecisionRightsSummary = "The delivery manager decides when the generated calculator is ready for reviewer pickup.",
                        ExceptionPolicySummary = "Do not release review until the generation evidence exists and messaging is captured.",
                        TargetLeadHours = 1,
                        Dependencies =
                        [
                            new ProcessStepDependencyEditorModel
                            {
                                Id = Guid.NewGuid(),
                                DependsOnStepId = generationStepId
                            }
                        ],
                        CanvasX = 520,
                        CanvasY = 180,
                        RoleAssignments =
                        [
                            new ProcessStepRoleRequirementEditorModel
                            {
                                RoleRequirementId = managerRoleId,
                                ResponsibilityKind = ProcessResponsibilityKind.Responsible
                            }
                        ]
                    },
                    new ProcessStepEditorModel
                    {
                        Key = "sc10-review-calculator",
                        Title = reviewStepTitle,
                        StepKind = ProcessStepKind.Review,
                        InputContractSummary = "The approved calculator delivery plus the direct message from builder to reviewer.",
                        OutputContractSummary = "A deterministic review report that confirms the calculator assets exist.",
                        EvidenceContractSummary = "SC10 must persist review-report.md and close the run with durable evidence.",
                        DecisionRightsSummary = "The calculator reviewer agent closes the delivery after the human handoff is complete.",
                        ExceptionPolicySummary = "Fail the run when the generated calculator is missing or the review cannot verify it.",
                        TargetLeadHours = 1,
                        Dependencies =
                        [
                            new ProcessStepDependencyEditorModel
                            {
                                Id = Guid.NewGuid(),
                                DependsOnStepId = generationStepId
                            },
                            new ProcessStepDependencyEditorModel
                            {
                                Id = Guid.NewGuid(),
                                DependsOnStepId = handoffStepId
                            }
                        ],
                        CanvasX = 860,
                        CanvasY = 180,
                        RoleAssignments =
                        [
                            new ProcessStepRoleRequirementEditorModel
                            {
                                RoleRequirementId = reviewerRoleId,
                                ResponsibilityKind = ProcessResponsibilityKind.Responsible
                            }
                        ],
                        ArtifactExpectations =
                        [
                            new ProcessArtifactExpectationEditorModel
                            {
                                ArtifactKind = ProcessArtifactKind.Evidence,
                                Title = "review-report.md",
                                ValidationRequirementSummary = "SC10 must project the calculator review evidence into the process run."
                            }
                        ],
                        ArtifactInputs =
                        [
                            new ProcessStepArtifactInputEditorModel
                            {
                                ArtifactExpectationId = generationArtifactId
                            }
                        ]
                    }
                ]
            },
            builderRoleId,
            reviewerRoleId,
            builderRoleName,
            reviewerRoleName,
            generationStepTitle,
            handoffStepTitle,
            reviewStepTitle);
    }

    private sealed record DirectMessagingDefinitionFixture(
        ProcessDefinitionEditorModel Editor,
        Guid SourceRoleRequirementId,
        Guid TargetRoleRequirementId);

    private sealed record CalculatorDefinitionFixture(
        ProcessDefinitionEditorModel Editor,
        Guid BuilderRoleRequirementId,
        Guid ReviewerRoleRequirementId,
        string BuilderRoleName,
        string ReviewerRoleName,
        string GenerationStepTitle,
        string HandoffStepTitle,
        string ReviewStepTitle);
}
