using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessCanvasActionIds
{
    public const string CreateRoleBlank = "process-role.blank";
    public const string CreateRoleProductOwner = "process-role.product-owner";
    public const string CreateRoleDeliveryManager = "process-role.delivery-manager";
    public const string CreateRoleSolutionArchitect = "process-role.solution-architect";
    public const string CreateRoleSoftwareEngineer = "process-role.software-engineer";
    public const string CreateRoleQaLead = "process-role.qa-lead";
    public const string CreateRoleSecurityReviewer = "process-role.security-reviewer";

    public const string CreateStepIntake = "process-step.intake";
    public const string CreateStepDecision = "process-step.decision";
    public const string CreateStepArchitecture = "process-step.architecture";
    public const string CreateStepImplementation = "process-step.implementation";
    public const string CreateStepQa = "process-step.qa";
    public const string CreateStepSecurityReview = "process-step.security-review";
    public const string CreateStepReleaseApproval = "process-step.release-approval";
    public const string CreateStepDeployment = "process-step.deployment";
    public const string CreateStepRetrospective = "process-step.retrospective";

    public const string EditDefinitionStep = "process-definition.edit-step";
    public const string EditDefinitionRole = "process-definition.edit-role";
    public const string AddDependentStep = "process-definition.add-dependent-step";
    public const string AddBranchOutcome = "process-definition.add-branch-outcome";
    public const string AddRoleBinding = "process-definition.add-role-binding";
    public const string AddArtifactExpectation = "process-definition.add-artifact-expectation";
    public const string RemoveDefinitionStep = "process-definition.remove-step";
    public const string OpenDefinitionToolbox = "process-definition.open-toolbox";

    public const string RuntimeStart = "process-runtime.start";
    public const string RuntimeComplete = "process-runtime.complete";
    public const string RuntimeBlock = "process-runtime.block";
    public const string RuntimeApproval = "process-runtime.approval";
    public const string RuntimeRefuse = "process-runtime.refuse";
    public const string RuntimeFail = "process-runtime.fail";
    public const string RuntimeRecordArtifact = "process-runtime.record-artifact";
}

public sealed record ProcessCanvasToolboxGroup(
    string Key,
    string Title,
    string Summary,
    IReadOnlyList<ProcessCanvasToolboxAction> Actions);

public sealed record ProcessCanvasToolboxAction(
    string ActionId,
    string Label,
    string Summary,
    string Tone);

public sealed record ProcessCanvasRoleTemplate(
    string ActionId,
    string Label,
    string Summary,
    Func<int, ProcessRoleEditorModel> Factory);

public sealed record ProcessCanvasStepTemplate(
    string ActionId,
    string Label,
    string Summary,
    Func<int, Guid?, double, double, ProcessStepEditorModel> Factory);

internal static class ProcessCanvasTemplateCatalog
{
    public static IReadOnlyList<ProcessCanvasRoleTemplate> RoleTemplates { get; } =
    [
        new(
            ProcessCanvasActionIds.CreateRoleBlank,
            "Blank role",
            "Start with an empty role contract and define the staffing semantics yourself.",
            ordinal => new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                Key = $"role-{ordinal}",
                DisplayName = $"Role {ordinal}",
                DefaultAllocationPercent = 100,
                PreferredExecutorKind = "person"
            }),
        new(
            ProcessCanvasActionIds.CreateRoleProductOwner,
            "Product owner",
            "Own value trade-offs, backlog priority, and acceptance boundaries.",
            ordinal => new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                Key = $"product-owner-{ordinal}",
                DisplayName = "Product owner",
                Purpose = "Own outcome clarity, acceptance boundaries, and decision timing for the requested change.",
                StaffingIntent = "A customer-facing owner with authority to clarify scope, sequence delivery, and accept or reject the outcome.",
                PreferredExecutorKind = "person",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.CustomerContact,
                IsRequired = true,
                AllowsFallback = false,
                RequiresExplicitApproval = true,
                DefaultAllocationPercent = 35,
                RoleTemplateSourceKey = "process-role-template/product-owner",
                RoleTemplateSnapshotName = "Product owner / v1",
                SnapshotSummary = "Value owner for scope, priority, and acceptance."
            }),
        new(
            ProcessCanvasActionIds.CreateRoleDeliveryManager,
            "Delivery manager",
            "Coordinate sequencing, staffing, and release commitments across the execution path.",
            ordinal => new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                Key = $"delivery-manager-{ordinal}",
                DisplayName = "Delivery manager",
                Purpose = "Own delivery flow, staffing trade-offs, release commitments, and escalation timing.",
                StaffingIntent = "A delivery-side accountable owner who keeps the plan realistic and coordinates across engineering, QA, and release.",
                PreferredExecutorKind = "person",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                IsRequired = true,
                AllowsFallback = true,
                RequiresExplicitApproval = false,
                DefaultAllocationPercent = 50,
                RoleTemplateSourceKey = "process-role-template/delivery-manager",
                RoleTemplateSnapshotName = "Delivery manager / v1",
                SnapshotSummary = "Flow owner for staffing, sequencing, and delivery risk."
            }),
        new(
            ProcessCanvasActionIds.CreateRoleSolutionArchitect,
            "Solution architect",
            "Own the target architecture, integration boundaries, and design guardrails.",
            ordinal => new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                Key = $"solution-architect-{ordinal}",
                DisplayName = "Solution architect",
                Purpose = "Define the target architecture, critical integration seams, and irreversible design decisions.",
                StaffingIntent = "A senior engineering or platform owner who can review cross-module boundaries and preserve maintainability.",
                PreferredExecutorKind = "person",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Reviewer,
                IsRequired = true,
                AllowsFallback = true,
                RequiresExplicitApproval = true,
                DefaultAllocationPercent = 30,
                RoleTemplateSourceKey = "process-role-template/solution-architect",
                RoleTemplateSnapshotName = "Solution architect / v1",
                SnapshotSummary = "Architecture authority for design, integration, and guardrails."
            }),
        new(
            ProcessCanvasActionIds.CreateRoleSoftwareEngineer,
            "Software engineer",
            "Own implementation details, code quality, and executable delivery artifacts.",
            ordinal => new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                Key = $"software-engineer-{ordinal}",
                DisplayName = "Software engineer",
                Purpose = "Implement the change, produce code and tests, and surface execution risks early.",
                StaffingIntent = "A build-capable engineer who can own the working implementation and adjacent proof.",
                PreferredExecutorKind = "person-or-agent",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.TeamMember,
                IsRequired = true,
                AllowsFallback = true,
                RequiresExplicitApproval = false,
                DefaultAllocationPercent = 100,
                RoleTemplateSourceKey = "process-role-template/software-engineer",
                RoleTemplateSnapshotName = "Software engineer / v1",
                SnapshotSummary = "Implementation owner for code, tests, and delivery artifacts."
            }),
        new(
            ProcessCanvasActionIds.CreateRoleQaLead,
            "QA lead",
            "Own test strategy, regression depth, and release confidence.",
            ordinal => new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                Key = $"qa-lead-{ordinal}",
                DisplayName = "QA lead",
                Purpose = "Design coverage depth, verify risk hotspots, and sign off the release evidence quality.",
                StaffingIntent = "A test lead or quality owner who can challenge weak proof before release.",
                PreferredExecutorKind = "person",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Reviewer,
                IsRequired = true,
                AllowsFallback = true,
                RequiresExplicitApproval = true,
                DefaultAllocationPercent = 40,
                RoleTemplateSourceKey = "process-role-template/qa-lead",
                RoleTemplateSnapshotName = "QA lead / v1",
                SnapshotSummary = "Quality owner for coverage, evidence, and release confidence."
            }),
        new(
            ProcessCanvasActionIds.CreateRoleSecurityReviewer,
            "Security reviewer",
            "Own security sign-off, threat assessment, and exception handling.",
            ordinal => new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                Key = $"security-reviewer-{ordinal}",
                DisplayName = "Security reviewer",
                Purpose = "Review threat exposure, secrets/data handling, and approve or reject security exceptions.",
                StaffingIntent = "A governance-capable security specialist who can block unsafe release paths.",
                PreferredExecutorKind = "person",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Reviewer,
                IsRequired = false,
                AllowsFallback = true,
                RequiresExplicitApproval = true,
                DefaultAllocationPercent = 15,
                RoleTemplateSourceKey = "process-role-template/security-reviewer",
                RoleTemplateSnapshotName = "Security reviewer / v1",
                SnapshotSummary = "Security gate owner for threat review and exceptions."
            })
    ];

    public static IReadOnlyList<ProcessCanvasStepTemplate> StepTemplates { get; } =
    [
        new(
            ProcessCanvasActionIds.CreateStepIntake,
            "Intake and scope",
            "Capture the delivery ask, constraints, and acceptance boundary before engineering starts.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "intake-and-scope",
                "Clarify intake and scope boundary",
                "Demand, scope, and value framing",
                ProcessStepKind.Start,
                dependsOnStepId,
                x,
                y,
                "Confirmed demand, stakeholder list, time pressure, and no-go constraints.",
                "Decision-ready scope packet with explicit acceptance boundary.",
                "Discovery notes, dependency map, and open-question register.",
                "Product and delivery owners can clarify demand but cannot silently drop constraints.",
                "Escalate when timing, cost, or quality expectations conflict.",
                false,
                true,
                false,
                false,
                8,
                [])),
        new(
            ProcessCanvasActionIds.CreateStepDecision,
            "Decision and routing",
            "Add a switch-style decision gate with explicit branch outcomes and a named decision-maker role.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "decision-and-routing",
                "Route the next path",
                "Switch-style branching gate",
                ProcessStepKind.Decision,
                dependsOnStepId,
                x,
                y,
                "Decision input, proof, and role context needed to choose the next path.",
                "Chosen branch outcome with explicit downstream path ownership.",
                "Decision evidence, supporting analysis, and the selected branch rationale.",
                "A named decision-maker role must choose the branch instead of letting routing hide inside notes.",
                "Escalate when no defined outcome safely covers the observed case.",
                false,
                true,
                false,
                true,
                4,
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Decision,
                        Title = "Branch routing decision record",
                        TrustRequirement = ProcessArtifactTrustRequirement.HumanApproved,
                        AllowedFutureUsageSummary = "Reusable for later execution review, audit, and process tuning.",
                        ValidationRequirementSummary = "Decision record must name the selected outcome and why the other paths were not chosen."
                    }
                ],
                [
                    new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = Guid.NewGuid(),
                        Key = "outcome-1",
                        Title = "Outcome 1"
                    },
                    new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = Guid.NewGuid(),
                        Key = "outcome-2",
                        Title = "Outcome 2"
                    }
                ])),
        new(
            ProcessCanvasActionIds.CreateStepArchitecture,
            "Architecture review",
            "Review system boundaries, canonical model impact, and irreversible technical choices.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "architecture-review",
                "Review architecture and dependency impact",
                "Cross-module design review",
                ProcessStepKind.Review,
                dependsOnStepId,
                x,
                y,
                "Scope packet, integration touchpoints, and affected modules.",
                "Reviewed architecture path with accepted trade-offs and explicit risks.",
                "Architecture notes, source-of-truth decisions, and integration risk register.",
                "Architect can recommend; accountable delivery owner approves the chosen path.",
                "Stop implementation when canonical-model or ownership boundaries stay unclear.",
                false,
                false,
                false,
                true,
                12,
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Decision,
                        Title = "Architecture decision record",
                        TrustRequirement = ProcessArtifactTrustRequirement.HumanApproved,
                        AllowedFutureUsageSummary = "Reusable during implementation, review, and future change analysis.",
                        ValidationRequirementSummary = "Decision rationale must capture source of truth and rejected alternatives."
                    }
                ])),
        new(
            ProcessCanvasActionIds.CreateStepImplementation,
            "Implementation",
            "Build the code and adjacent proof without losing contract, evidence, or review context.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "implementation",
                "Implement the approved change",
                "Code, tests, and reviewable proof",
                ProcessStepKind.Work,
                dependsOnStepId,
                x,
                y,
                "Accepted architecture path, delivery plan, and unresolved questions list.",
                "Working implementation with test coverage, migration notes, and reviewable deltas.",
                "PR notes, test evidence, migration scripts, and operational deltas.",
                "Assigned engineers can implement; irreversible scope changes require escalation.",
                "Pause when proof, dependencies, or data migration assumptions become unsafe.",
                false,
                true,
                false,
                true,
                24,
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Deliverable,
                        Title = "Implementation change set",
                        TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
                        AllowedFutureUsageSummary = "Reusable for code review, rollout preparation, and forensic replay.",
                        ValidationRequirementSummary = "Change set must be linked to tests and deployment notes."
                    },
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Evidence,
                        Title = "Automated validation evidence",
                        TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
                        AllowedFutureUsageSummary = "Reusable for release and audit proof.",
                        ValidationRequirementSummary = "Evidence must name the exact validation scope and timestamp."
                    }
                ])),
        new(
            ProcessCanvasActionIds.CreateStepQa,
            "QA and regression",
            "Prove the change at the right depth for the actual release risk.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "qa-regression",
                "Verify QA and regression coverage",
                "Quality gate before release",
                ProcessStepKind.Review,
                dependsOnStepId,
                x,
                y,
                "Implementation artifacts, changed-surface map, and release-risk notes.",
                "Coverage verdict with explicit residual risk and follow-up actions.",
                "Test runs, screenshots, bug notes, and regression comparison evidence.",
                "QA lead can reject release readiness when proof is thin or regressions remain unresolved.",
                "Do not release on vague 'looks good' confidence.",
                false,
                false,
                false,
                true,
                10,
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Evidence,
                        Title = "Regression evidence pack",
                        TrustRequirement = ProcessArtifactTrustRequirement.HumanApproved,
                        AllowedFutureUsageSummary = "Reusable for release approval and later incident forensics.",
                        ValidationRequirementSummary = "Must cover changed flows, not just generic smoke checks."
                    }
                ])),
        new(
            ProcessCanvasActionIds.CreateStepSecurityReview,
            "Security review",
            "Review risky changes, data handling, and policy exceptions before rollout.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "security-review",
                "Complete security review",
                "Threats, exceptions, and controls",
                ProcessStepKind.Approval,
                dependsOnStepId,
                x,
                y,
                "Changed-surface map, secrets/data handling notes, and deployment plan.",
                "Approved security posture or explicit blocked exception path.",
                "Threat notes, exception approvals, and control validation evidence.",
                "Security reviewer owns approval for elevated risk or policy exceptions.",
                "Block rollout when secrets, data scope, or threat assumptions are unresolved.",
                false,
                false,
                true,
                true,
                6,
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Decision,
                        Title = "Security review record",
                        TrustRequirement = ProcessArtifactTrustRequirement.HumanApproved,
                        AllowedFutureUsageSummary = "Reusable for audit and post-incident investigation.",
                        ValidationRequirementSummary = "Decision must capture residual risk and approved exceptions."
                    }
                ])),
        new(
            ProcessCanvasActionIds.CreateStepReleaseApproval,
            "Release approval",
            "Approve or reject release readiness using explicit runtime proof.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "release-approval",
                "Approve release readiness",
                "Go / no-go decision",
                ProcessStepKind.Approval,
                dependsOnStepId,
                x,
                y,
                "QA evidence, open-risk summary, security posture, and rollback plan.",
                "Approved or rejected release decision with accountable rationale.",
                "Approval record, residual risk register, and rollback readiness notes.",
                "Delivery manager owns the decision and cannot waive missing proof silently.",
                "Reject release when rollback, support, or residual risk ownership stays vague.",
                false,
                false,
                true,
                true,
                3,
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Decision,
                        Title = "Release approval record",
                        TrustRequirement = ProcessArtifactTrustRequirement.HumanApproved,
                        AllowedFutureUsageSummary = "Reusable for incident review and release audit.",
                        ValidationRequirementSummary = "Approval must name the accountable approver and residual risk owner."
                    }
                ])),
        new(
            ProcessCanvasActionIds.CreateStepDeployment,
            "Deployment",
            "Roll the change out with controlled evidence, rollback readiness, and operator awareness.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "deployment",
                "Deploy and verify production rollout",
                "Controlled production execution",
                ProcessStepKind.Delivery,
                dependsOnStepId,
                x,
                y,
                "Approved release package, rollback plan, monitoring plan, and ownership rota.",
                "Deployed change with verified health, communication trail, and recovery path.",
                "Deployment logs, health checks, and stakeholder communications.",
                "Delivery owner can pause or roll back when health checks degrade.",
                "Rollback immediately on unsafe data or health indicators.",
                false,
                true,
                false,
                true,
                4,
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Checklist,
                        Title = "Deployment verification checklist",
                        TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
                        AllowedFutureUsageSummary = "Reusable for post-release review and future rollout rehearsal.",
                        ValidationRequirementSummary = "Checklist must capture actual health verification, not placeholders."
                    }
                ])),
        new(
            ProcessCanvasActionIds.CreateStepRetrospective,
            "Retrospective",
            "Capture what changed in the process itself before the next run repeats the same failures.",
            (ordinal, dependsOnStepId, x, y) => BuildStepDraft(
                ordinal,
                "retrospective",
                "Capture post-release learning",
                "Improvement and training signals",
                ProcessStepKind.End,
                dependsOnStepId,
                x,
                y,
                "Release outcome, support feedback, and residual risk observations.",
                "Improvement candidates, training actions, and governance follow-ups.",
                "Retro notes, follow-up actions, and process update candidates.",
                "Product, delivery, and QA owners agree what should change in the process design.",
                "Do not close the run without deciding what becomes durable process improvement.",
                true,
                false,
                false,
                true,
                6,
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Brief,
                        Title = "Retrospective summary",
                        TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
                        AllowedFutureUsageSummary = "Reusable for process improvement and training.",
                        ValidationRequirementSummary = "Summary must distinguish process defects from one-off execution mistakes."
                    }
                ]))
    ];

    public static IReadOnlyList<ProcessCanvasToolboxGroup> BuildDefinitionToolboxGroups()
    {
        return
        [
            new ProcessCanvasToolboxGroup(
                "role-templates",
                "Role templates",
                "Start from reusable staffing contracts so the process stays role-first and executor-agnostic.",
                RoleTemplates
                    .Select(template => new ProcessCanvasToolboxAction(template.ActionId, template.Label, template.Summary, "neutral"))
                    .ToList()),
                new ProcessCanvasToolboxGroup(
                "step-templates",
                "Step templates",
                "Seed explicit process steps with realistic governance, proof, and delivery expectations.",
                StepTemplates
                    .Select(template => new ProcessCanvasToolboxAction(template.ActionId, template.Label, template.Summary, "accent"))
                    .ToList())
        ];
    }

    public static bool TryCreateRoleDraft(string actionId, int ordinal, out ProcessRoleEditorModel role)
    {
        var template = RoleTemplates.FirstOrDefault(item => string.Equals(item.ActionId, actionId, StringComparison.Ordinal));
        if (template is null)
        {
            role = new ProcessRoleEditorModel();
            return false;
        }

        role = template.Factory(ordinal);
        return true;
    }

    public static bool TryCreateStepDraft(
        string actionId,
        int ordinal,
        Guid? dependsOnStepId,
        double x,
        double y,
        out ProcessStepEditorModel step)
    {
        var template = StepTemplates.FirstOrDefault(item => string.Equals(item.ActionId, actionId, StringComparison.Ordinal));
        if (template is null)
        {
            step = new ProcessStepEditorModel();
            return false;
        }

        step = template.Factory(ordinal, dependsOnStepId, x, y);
        return true;
    }

    private static ProcessStepEditorModel BuildStepDraft(
        int ordinal,
        string key,
        string title,
        string subtitle,
        ProcessStepKind stepKind,
        Guid? dependsOnStepId,
        double x,
        double y,
        string inputContractSummary,
        string outputContractSummary,
        string evidenceContractSummary,
        string decisionRightsSummary,
        string exceptionPolicySummary,
        bool allowsManualSkip,
        bool allowsSafeRefusal,
        bool requiresApproval,
        bool requiresDecisionRecord,
        int targetLeadHours,
        List<ProcessArtifactExpectationEditorModel> artifactExpectations,
        List<ProcessStepBranchOutcomeEditorModel>? branchOutcomes = null)
    {
        return new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Key = $"{key}-{ordinal}",
            Title = title,
            Subtitle = subtitle,
            StepKind = stepKind,
            DependsOnStepId = dependsOnStepId,
            CanvasX = x,
            CanvasY = y,
            InputContractSummary = inputContractSummary,
            OutputContractSummary = outputContractSummary,
            EvidenceContractSummary = evidenceContractSummary,
            DecisionRightsSummary = decisionRightsSummary,
            ExceptionPolicySummary = exceptionPolicySummary,
            AllowsManualSkip = allowsManualSkip,
            AllowsSafeRefusal = allowsSafeRefusal,
            RequiresApproval = requiresApproval,
            RequiresDecisionRecord = requiresDecisionRecord,
            TargetLeadHours = targetLeadHours,
            ArtifactExpectations = artifactExpectations,
            BranchOutcomes = branchOutcomes ?? []
        };
    }
}
