using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessSeedScenarioKeys
{
    public const string SoftwareDelivery = "software-delivery";
    public const string BranchingCodeReview = "branching-code-review";
    public const string HotfixRollout = "hotfix-rollout";
    public const string CustomerOnboarding = "customer-onboarding";
    public const string IncidentResponse = "incident-response";
}

public sealed partial class ProcessDevelopmentSeedService
{
    private static IReadOnlyList<ProcessSeedScenario> BuildBaselineScenarios()
    {
        return
        [
            BuildSoftwareDeliveryScenario(),
            BuildBranchingCodeReviewScenario(),
            BuildHotfixRolloutScenario(),
            BuildOnboardingScenario(),
            BuildIncidentScenario()
        ];
    }

    private static ProcessSeedScenario BuildSoftwareDeliveryScenario()
    {
        var productOwnerId = Guid.NewGuid();
        var deliveryManagerId = Guid.NewGuid();
        var architectId = Guid.NewGuid();
        var leadEngineerId = Guid.NewGuid();
        var qaLeadId = Guid.NewGuid();
        var securityReviewerId = Guid.NewGuid();
        var releaseManagerId = Guid.NewGuid();

        var roles = new List<ProcessRoleEditorModel>
        {
            BuildRole(productOwnerId, "product-owner", "Product owner", "Owns value framing, acceptance boundaries, and scope timing for the platform feature.", "Customer-side value owner who can confirm business priority but cannot silently waive architecture or security guardrails.", ProjectPartyAssignmentRole.CustomerContact, "person", "Value owner for scope, acceptance, and sequencing.", true, false, true, 35, "process-role-template/product-owner", "Product owner / v1"),
            BuildRole(deliveryManagerId, "delivery-manager", "Delivery manager", "Owns sequencing, dependency coordination, release timing, and escalation policy for the delivery lane.", "Delivery-side accountable owner who balances staffing, release timing, and governance readiness.", ProjectPartyAssignmentRole.Manager, "person", "Delivery authority for sequencing, cost, and release readiness.", true, true, true, 55, "process-role-template/delivery-manager", "Delivery manager / v1"),
            BuildRole(architectId, "solution-architect", "Solution architect", "Owns architecture boundaries, canonical-model impact review, and irreversible technical decisions.", "Architecture authority who can review cross-module consequences and block unsafe shortcuts.", ProjectPartyAssignmentRole.Reviewer, "person-or-agent", "Architecture authority for design guardrails and source-of-truth choices.", true, true, true, 30, "process-role-template/solution-architect", "Solution architect / v1"),
            BuildRole(leadEngineerId, "lead-engineer", "Lead engineer", "Owns implementation quality, migration preparation, and executable delivery proof.", "Build-capable engineering owner for the working change set and adjacent test evidence.", ProjectPartyAssignmentRole.TeamMember, "person", "Implementation owner for code, tests, and migration notes.", true, true, false, 100, "process-role-template/software-engineer", "Software engineer / v1"),
            BuildRole(qaLeadId, "qa-lead", "QA lead", "Owns regression depth, release evidence strength, and defect triage quality.", "Quality authority who can reject thin proof before release.", ProjectPartyAssignmentRole.Reviewer, "person", "Quality owner for test depth, browser proof, and release confidence.", true, true, true, 45, "process-role-template/qa-lead", "QA lead / v1"),
            BuildRole(securityReviewerId, "security-reviewer", "Security reviewer", "Owns data-handling review, exception approval, and security sign-off.", "Governance-capable reviewer for tenant-data, secrets, and policy exceptions.", ProjectPartyAssignmentRole.Reviewer, "person", "Security gate owner for exception review and sign-off.", true, true, true, 20, "process-role-template/security-reviewer", "Security reviewer / v1"),
            BuildRole(releaseManagerId, "release-manager", "Release manager", "Owns controlled rollout, rollback readiness, and operational verification.", "Operational owner for production execution, release windows, and rollback decisions.", ProjectPartyAssignmentRole.Manager, "person", "Release owner for rollout control, rollback readiness, and telemetry review.", true, true, true, 40, "process-role-template/release-manager", "Release manager / v1")
        };

        var intakeId = Guid.NewGuid();
        var architectureId = Guid.NewGuid();
        var implementationId = Guid.NewGuid();
        var peerReviewId = Guid.NewGuid();
        var qaId = Guid.NewGuid();
        var securityId = Guid.NewGuid();
        var releaseApprovalId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();

        var steps = new List<ProcessStepEditorModel>
        {
            new()
            {
                Id = intakeId,
                Key = "feature-intake",
                Title = "Clarify scope and release boundary",
                Subtitle = "Value framing and no-go constraints",
                Notes = "Capture the commercial ask, tenant impact, release deadline, and explicit exclusions before engineering commits.",
                StepKind = ProcessStepKind.Start,
                InputContractSummary = "Feature request, tenant-impact notes, target release window, and customer-facing constraints.",
                OutputContractSummary = "Decision-ready scope packet with acceptance boundary and dependency map.",
                EvidenceContractSummary = "Intake notes, acceptance criteria, known exclusions, and unresolved dependency register.",
                DecisionRightsSummary = "Product owner can refine the ask but cannot waive architecture, data, or release-governance requirements.",
                ExceptionPolicySummary = "Escalate immediately when timeline pressure conflicts with data-safety or release constraints.",
                TargetLeadHours = 8,
                CanvasX = 120,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(productOwnerId, ProcessResponsibilityKind.Responsible, "If the original product owner changes, ownership transfers to the next accountable value owner without changing the process contract."), BuildRoleAssignment(deliveryManagerId, ProcessResponsibilityKind.Reviewer, "Delivery review remains explicit even if staffing changes mid-stream.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Brief, "Scope boundary packet", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 365, "Reusable during architecture review, implementation planning, and future scope-drift analysis.", "Must capture no-go constraints, tenant impact, and acceptance boundary in typed form.")]
            },
            new()
            {
                Id = architectureId,
                Key = "architecture-review",
                Title = "Review architecture and canonical-model impact",
                Subtitle = "Cross-module and source-of-truth decision",
                Notes = "Validate process/workspace/billing seams, canonical model implications, and migration ownership before implementation starts.",
                StepKind = ProcessStepKind.Review,
                RequiresDecisionRecord = true,
                InputContractSummary = "Scope packet, touched modules, data-flow map, and integration concerns.",
                OutputContractSummary = "Approved architecture path with explicit trade-offs and rejected alternatives.",
                EvidenceContractSummary = "Architecture notes, canonical-model decision, and source-of-truth rationale.",
                DecisionRightsSummary = "Architecture authority recommends the path; delivery manager remains accountable for choosing the approved option.",
                ExceptionPolicySummary = "Do not continue while source-of-truth ownership or migration responsibility remains ambiguous.",
                TargetLeadHours = 12,
                DependsOnStepId = intakeId,
                CanvasX = 430,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(architectId, ProcessResponsibilityKind.Responsible, "Architecture authority may be reassigned between vetted humans or approved architecture agents."), BuildRoleAssignment(leadEngineerId, ProcessResponsibilityKind.Reviewer, "Implementation owner must confirm the design is buildable before approval.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Architecture decision record", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 730, "Reusable for implementation, review, and later forensic replay.", "Must capture selected option, rejected options, source-of-truth choice, and migration ownership.")]
            },
            new()
            {
                Id = implementationId,
                Key = "implementation",
                Title = "Implement feature, tests, and migration notes",
                Subtitle = "Code, tests, and reviewable proof",
                Notes = "Produce the change set, migration guidance, and targeted validation without losing traceability to the approved design.",
                StepKind = ProcessStepKind.Work,
                RequiresDecisionRecord = true,
                InputContractSummary = "Approved architecture path, scope packet, and unresolved technical questions.",
                OutputContractSummary = "Review-ready implementation with tests, migration notes, and rollout checklist inputs.",
                EvidenceContractSummary = "Change set, test outputs, migration steps, and touched-surface inventory.",
                DecisionRightsSummary = "Lead engineer can implement but cannot silently alter the approved architecture or reduce proof depth.",
                ExceptionPolicySummary = "Pause when migration impact, performance risk, or dependency scope grows beyond the approved path.",
                TargetLeadHours = 36,
                DependsOnStepId = architectureId,
                CanvasX = 740,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(leadEngineerId, ProcessResponsibilityKind.Responsible, "Engineering ownership moves between qualified engineers without changing the role contract."), BuildRoleAssignment(productOwnerId, ProcessResponsibilityKind.Reviewer, "Value owner reviews only acceptance drift, not technical implementation details.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Deliverable, "Implementation change set", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 365, "Reusable for peer review, release approval, and later defect forensics.", "Must be linked to tests, migration notes, and touched-surface inventory."), BuildArtifactExpectation(ProcessArtifactKind.Checklist, "Migration and rollout preparation checklist", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 365, "Reusable for release rehearsal and rollback planning.", "Must name data changes, operational preconditions, and rollback steps.")]
            },
            new()
            {
                Id = peerReviewId,
                Key = "peer-review",
                Title = "Complete peer review and integration readiness",
                Subtitle = "Design and implementation challenge",
                Notes = "Review the change set against the approved design, integration consequences, and release assumptions.",
                StepKind = ProcessStepKind.Review,
                InputContractSummary = "Implementation package, architecture decision record, and changed-surface inventory.",
                OutputContractSummary = "Peer-reviewed change set with explicit residual risk and follow-up items.",
                EvidenceContractSummary = "Review notes, unresolved issues list, and approved follow-up actions.",
                DecisionRightsSummary = "Reviewers may block unsafe merge or release progression until the change set meets the approved guardrails.",
                ExceptionPolicySummary = "Do not defer critical design or migration concerns into QA or production rollout.",
                TargetLeadHours = 10,
                DependsOnStepId = implementationId,
                CanvasX = 1050,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(architectId, ProcessResponsibilityKind.Reviewer, "Architecture review remains explicit even when the architecture authority changes."), BuildRoleAssignment(deliveryManagerId, ProcessResponsibilityKind.Reviewer, "Delivery manager verifies readiness and unresolved-risk ownership before QA.")]
            },
            new()
            {
                Id = qaId,
                Key = "qa-regression",
                Title = "Validate regression depth and release evidence",
                Subtitle = "Quality gate before release",
                Notes = "Run targeted tests and browser proof against the changed surfaces and confirm evidence quality before security and release gates.",
                StepKind = ProcessStepKind.Review,
                InputContractSummary = "Peer-reviewed implementation package and release-risk notes.",
                OutputContractSummary = "Accepted regression evidence with residual risks and follow-up obligations.",
                EvidenceContractSummary = "Targeted tests, large-screen browser proof, and defect triage notes.",
                DecisionRightsSummary = "QA lead may reject release readiness when proof is weak or changed-surface coverage is missing.",
                ExceptionPolicySummary = "Do not collapse coverage into generic smoke results when the changed surfaces are materially deeper.",
                TargetLeadHours = 16,
                DependsOnStepId = peerReviewId,
                CanvasX = 1360,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(qaLeadId, ProcessResponsibilityKind.Responsible, "QA ownership remains explicit even if a different quality lead takes the gate."), BuildRoleAssignment(leadEngineerId, ProcessResponsibilityKind.Reviewer, "Implementation owner reviews failures and residual risk before release progression.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Evidence, "Regression evidence pack", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for release approval, incident review, and future regression comparison.", "Must name changed flows, assertion scope, screenshots, and unresolved risks.")]
            },
            new()
            {
                Id = securityId,
                Key = "security-review",
                Title = "Review data handling and security exceptions",
                Subtitle = "Trust and policy gate",
                Notes = "Validate secrets, data exposure, tenant export controls, and any exception requests before release can proceed.",
                StepKind = ProcessStepKind.Approval,
                RequiresApproval = true,
                RequiresDecisionRecord = true,
                InputContractSummary = "QA evidence pack, data-flow notes, and exception request package if any.",
                OutputContractSummary = "Approved, blocked, or rejected security posture for release.",
                EvidenceContractSummary = "Security review notes, exception rationale, and approved controls.",
                DecisionRightsSummary = "Security reviewer owns the sign-off for tenant-data and policy exceptions.",
                ExceptionPolicySummary = "Block release when data-handling review capacity is missing or exception rationale is incomplete.",
                TargetLeadHours = 6,
                DependsOnStepId = peerReviewId,
                CanvasX = 1670,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(securityReviewerId, ProcessResponsibilityKind.Approver, "Security approval remains attached to the role even if the reviewer changes.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Security exception assessment", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Confidential, 1095, "Reusable for release governance, audit, and post-incident review.", "Must capture controls, residual risk owner, and approval or block rationale.")]
            },
            new()
            {
                Id = releaseApprovalId,
                Key = "release-approval",
                Title = "Approve release readiness",
                Subtitle = "Go / no-go board decision",
                Notes = "Approve or reject release using QA proof, security posture, rollback readiness, and support coverage.",
                StepKind = ProcessStepKind.Approval,
                RequiresApproval = true,
                RequiresDecisionRecord = true,
                InputContractSummary = "QA evidence, security outcome, rollback plan, and support ownership.",
                OutputContractSummary = "Approved or rejected release readiness with accountable rationale.",
                EvidenceContractSummary = "Approval note, residual risk register, and rollback ownership record.",
                DecisionRightsSummary = "Delivery manager owns the decision and cannot waive missing proof or missing rollback readiness silently.",
                ExceptionPolicySummary = "Reject release when security review, rollback ownership, or support readiness remains incomplete.",
                TargetLeadHours = 3,
                Dependencies =
                [
                    new ProcessStepDependencyEditorModel
                    {
                        Id = Guid.NewGuid(),
                        DependsOnStepId = qaId
                    },
                    new ProcessStepDependencyEditorModel
                    {
                        Id = Guid.NewGuid(),
                        DependsOnStepId = securityId
                    }
                ],
                CanvasX = 1980,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(deliveryManagerId, ProcessResponsibilityKind.Approver, "Release-governance ownership stays attached to the delivery manager role."), BuildRoleAssignment(releaseManagerId, ProcessResponsibilityKind.Reviewer, "Release operations must confirm readiness before approval can be accepted.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Release approval record", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 1095, "Reusable for release audit, incident response, and later process tuning.", "Must name the approver, residual-risk owner, and rollback trigger.")]
            },
            new()
            {
                Id = rolloutId,
                Key = "rollout",
                Title = "Roll out, verify telemetry, and confirm rollback readiness",
                Subtitle = "Controlled production execution",
                Notes = "Execute rollout under the approved window, verify telemetry, and keep rollback readiness explicit.",
                StepKind = ProcessStepKind.Delivery,
                InputContractSummary = "Approved release package, rollback trigger, support rota, and telemetry checks.",
                OutputContractSummary = "Controlled rollout with verified telemetry and explicit rollback status.",
                EvidenceContractSummary = "Rollout notes, telemetry checkpoint results, and rollback state confirmation.",
                DecisionRightsSummary = "Release manager may execute only within the approved window and rollback policy.",
                ExceptionPolicySummary = "Escalate immediately when telemetry deviates or rollback readiness degrades.",
                TargetLeadHours = 6,
                DependsOnStepId = releaseApprovalId,
                CanvasX = 2290,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(releaseManagerId, ProcessResponsibilityKind.Responsible, "Release manager owns rollout execution until the stage completes."), BuildRoleAssignment(leadEngineerId, ProcessResponsibilityKind.Backup, "Engineering backup remains explicit for operational rollback support.", false, 1)]
            },
            new()
            {
                Key = "post-release-review",
                Title = "Capture post-release learning and follow-up actions",
                Subtitle = "Operational learning and anti-fragility",
                Notes = "Capture what was learned, which gates were expensive, and whether the orchestration decisions were appropriate.",
                StepKind = ProcessStepKind.End,
                RequiresDecisionRecord = true,
                InputContractSummary = "Rollout result, incident notes if any, and quality or security follow-up actions.",
                OutputContractSummary = "Learning record with concrete improvement candidates and ownership.",
                EvidenceContractSummary = "Retrospective notes, improvement backlog, and orchestration-quality observations.",
                DecisionRightsSummary = "Delivery manager records learning, but affected role owners must accept their follow-up obligations.",
                ExceptionPolicySummary = "Do not close the process without naming follow-up owners for repeated failure patterns or role gaps.",
                TargetLeadHours = 4,
                DependsOnStepId = rolloutId,
                CanvasX = 2600,
                CanvasY = 120,
                RoleAssignments = [BuildRoleAssignment(deliveryManagerId, ProcessResponsibilityKind.Responsible, "Learning-capture ownership stays with delivery governance."), BuildRoleAssignment(productOwnerId, ProcessResponsibilityKind.Reviewer, "Value owner reviews whether the delivered scope matched the intended outcome.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Brief, "Post-release learning review", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 730, "Reusable for process improvement and future simulation baselines.", "Must capture orchestration-quality observations, not only technical defects.")]
            }
        };

        return new ProcessSeedScenario(
            ProcessSeedScenarioKeys.SoftwareDelivery,
            "Multi-team software delivery and release governance",
            "Multi-team software delivery and release governance / Q3 billing capability",
            "Coordinate architecture, implementation, QA, security, release, and learning for a high-signal software delivery change without losing role-first governance.",
            "Provide a realistic cross-functional software-delivery process that surfaces architecture, trust, staffing, and release-governance concerns on one durable model.",
            "Enterprise Billing Platform",
            "Digital delivery governance board",
            "Feature delivery crosses process authoring, workspace, billing, and release-management boundaries and therefore needs explicit design, validation, and release gates.",
            "No role or approval decision may be collapsed into an implicit chat or tribal handoff; every trust-sensitive transition needs explicit accountability and evidence.",
            "Seeded from the software-delivery governance pack with realistic enterprise release constraints and tenant-data considerations.",
            "Release readiness requires architecture proof, QA evidence, security posture, rollback readiness, and explicit residual-risk ownership.",
            "Role contracts, decision rights, and source-of-truth ownership outlive the specific person or agent assigned at runtime.",
            "Governed live execution is allowed only after explicit quality, security, and release gates succeed.",
            "This scenario is intentionally rich enough for simulation, canvas authoring, analytics, and large-screen browser walkthroughs.",
            ProcessOperatingMode.GovernedLive,
            roles,
            steps);
    }

    private static ProcessSeedScenario BuildBranchingCodeReviewScenario()
    {
        var authorId = Guid.NewGuid();
        var reviewLeadId = Guid.NewGuid();
        var qaLeadId = Guid.NewGuid();
        var securityReviewerId = Guid.NewGuid();
        var architectId = Guid.NewGuid();
        var mergeApproverId = Guid.NewGuid();

        var roles = new List<ProcessRoleEditorModel>
        {
            BuildRole(authorId, "author", "Change author", "Owns the pull request, repair pass, and final delivery notes for the requested change.", "Primary implementation owner for the authored change set and any rework requested by reviewers.", ProjectPartyAssignmentRole.TeamMember, "person", "Implementation owner for authored changes and repair work.", true, true, false, 100, "process-role-template/software-engineer", "Software engineer / v1"),
            BuildRole(reviewLeadId, "review-lead", "Review lead", "Owns the explicit switch-style decision that routes the change into repair, QA, security, architecture, or merge lanes.", "Senior reviewer who can choose the next governed lane but cannot silently collapse the routing decision into chat.", ProjectPartyAssignmentRole.Reviewer, "person", "Branch-routing authority for review outcomes.", true, true, true, 45, "process-role-template/reviewer", "Review lead / v1"),
            BuildRole(qaLeadId, "qa-lead", "QA lead", "Owns regression depth, browser-proof strength, and release-quality confidence for the change.", "Quality gate owner for targeted regression and browser validation.", ProjectPartyAssignmentRole.Reviewer, "person", "Quality owner for QA proof and release confidence.", true, true, true, 40, "process-role-template/qa-lead", "QA lead / v1"),
            BuildRole(securityReviewerId, "security-reviewer", "Security reviewer", "Owns tenant-data, secrets, and policy-exception review for changes that need trust validation before merge.", "Security gate owner for data-handling and exception review.", ProjectPartyAssignmentRole.Reviewer, "person", "Security owner for trust-sensitive merge decisions.", true, true, true, 25, "process-role-template/security-reviewer", "Security reviewer / v1"),
            BuildRole(architectId, "solution-architect", "Solution architect", "Owns architecture escalation when a review outcome exposes canonical-model, module-boundary, or integration-risk concerns.", "Architecture authority for non-local design consequences discovered during review.", ProjectPartyAssignmentRole.Reviewer, "person-or-agent", "Architecture owner for design escalation during review.", true, true, true, 25, "process-role-template/solution-architect", "Solution architect / v1"),
            BuildRole(mergeApproverId, "merge-approver", "Merge approver", "Owns the explicit merge gate after review routing decides the change is ready for the default or accelerated merge lane.", "Delivery-side approver for merge timing, residual risk, and release-note completeness.", ProjectPartyAssignmentRole.Manager, "person", "Merge gate owner for ready-to-merge outcomes.", true, true, true, 20, "process-role-template/release-manager", "Merge approver / v1")
        };

        var preparePullRequestId = Guid.NewGuid();
        var routeReviewDispositionId = Guid.NewGuid();
        var repairsRequiredOutcomeId = Guid.NewGuid();
        var qaValidationOutcomeId = Guid.NewGuid();
        var securityReviewOutcomeId = Guid.NewGuid();
        var architectureReviewOutcomeId = Guid.NewGuid();
        var readyForMergeOutcomeId = Guid.NewGuid();
        var errorOutcomeId = Guid.NewGuid();

        var steps = new List<ProcessStepEditorModel>
        {
            new()
            {
                Id = preparePullRequestId,
                Key = "prepare-pull-request",
                Title = "Prepare pull request and reviewer brief",
                Subtitle = "Implementation packet",
                Notes = "Package the code change, screenshots, impacted modules, and release-note draft before the review board decides what lane comes next.",
                StepKind = ProcessStepKind.Start,
                InputContractSummary = "Accepted work item, implementation diff, test evidence, and draft release notes.",
                OutputContractSummary = "Review-ready pull request packet with typed reviewer context and proof references.",
                EvidenceContractSummary = "Diff summary, screenshots, changed-surface list, and rollback notes.",
                DecisionRightsSummary = "The author may prepare the review packet but cannot declare the change ready for merge without explicit routing.",
                ExceptionPolicySummary = "Pause immediately if proof, rollback notes, or touched-surface inventory is incomplete.",
                TargetLeadHours = 6,
                CanvasX = 120,
                CanvasY = 220,
                RoleAssignments = [BuildRoleAssignment(authorId, ProcessResponsibilityKind.Responsible, "Authorship stays attached to the implementation owner even if the engineer changes before review.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Deliverable, "Pull request readiness packet", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 365, "Reusable for code review, QA planning, and release-note preparation.", "Must name touched modules, screenshots, rollback notes, and explicit reviewer asks.")]
            },
            new()
            {
                Id = routeReviewDispositionId,
                Key = "route-review-disposition",
                Title = "Route code review disposition",
                Subtitle = "Switch-style review router",
                Notes = "Use an explicit branch router node to decide whether the authored change goes to repairs, QA, security, architecture, merge approval, the default normalization lane, or an error lane.",
                StepKind = ProcessStepKind.Decision,
                DependsOnStepId = preparePullRequestId,
                DecisionRoleRequirementId = reviewLeadId,
                RequiresDecisionRecord = true,
                InputContractSummary = "Review-ready pull request packet, reviewer comments, and changed-surface risk notes.",
                OutputContractSummary = "Explicit next-lane selection for the reviewed change.",
                EvidenceContractSummary = "Review notes, chosen route, and reasons for the selected lane.",
                DecisionRightsSummary = "Review lead owns the switch outcome and must keep the route explicit on the canvas.",
                ExceptionPolicySummary = "Do not bury routing logic inside comments or verbal agreements; the chosen lane must stay modeled and replayable.",
                TargetLeadHours = 4,
                CanvasX = 520,
                CanvasY = 220,
                BranchOutcomes =
                [
                    new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = repairsRequiredOutcomeId,
                        Key = "repairs-required",
                        Title = "Repairs required",
                        Description = "Route the change back to the author for a focused repair pass."
                    },
                    new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = qaValidationOutcomeId,
                        Key = "qa-validation",
                        Title = "QA validation",
                        Description = "Route the change into targeted QA and browser proof."
                    },
                    new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = securityReviewOutcomeId,
                        Key = "security-review",
                        Title = "Security review",
                        Description = "Route the change into security and data-handling review."
                    },
                    new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = architectureReviewOutcomeId,
                        Key = "architecture-review",
                        Title = "Architecture review",
                        Description = "Escalate architectural consequences that exceed local code review authority."
                    },
                    new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = readyForMergeOutcomeId,
                        Key = "ready-for-merge",
                        Title = "Ready for merge",
                        Description = "Send the change directly to the merge approval lane."
                    },
                    new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = errorOutcomeId,
                        Key = ProcessCanvasBranching.ErrorRouteKey,
                        Title = ProcessCanvasBranching.ErrorRouteTitle,
                        Description = "Escalate canvas or runtime authoring failures that prevent a safe routing decision."
                    }
                ],
                RoleAssignments =
                [
                    BuildRoleAssignment(reviewLeadId, ProcessResponsibilityKind.Responsible, "The review lead owns the branch-selection decision and its recorded rationale."),
                    BuildRoleAssignment(authorId, ProcessResponsibilityKind.Reviewer, "The author reviews whether requested follow-up work is clear before the route is accepted.")
                ],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Review routing decision record", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for rework analysis, merge audits, and process-tuning retrospectives.", "Must record the selected route, reviewer rationale, and any unresolved risk transferred to the next lane.")]
            },
            new()
            {
                Key = "repair-change-set",
                Title = "Repair change set",
                Subtitle = "Author repair lane",
                Notes = "Apply the requested code-review repairs and prepare a follow-up reviewer brief for the next pass.",
                StepKind = ProcessStepKind.Work,
                DependsOnStepId = routeReviewDispositionId,
                DependsOnBranchOutcomeId = repairsRequiredOutcomeId,
                InputContractSummary = "Repair-ready review notes and the original pull request packet.",
                OutputContractSummary = "Updated change set with explicit repair notes and follow-up reviewer context.",
                EvidenceContractSummary = "Repair summary, re-tested evidence, and updated reviewer brief.",
                DecisionRightsSummary = "The author may repair the change but cannot mark it merged or approved.",
                ExceptionPolicySummary = "Escalate if the requested repair expands scope beyond the original review boundary.",
                TargetLeadHours = 8,
                CanvasX = 1240,
                CanvasY = 20,
                RoleAssignments = [BuildRoleAssignment(authorId, ProcessResponsibilityKind.Responsible, "Repair ownership remains with the authoring role until the new revision is ready.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Deliverable, "Repair pass summary", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 180, "Reusable for the next reviewer pass and later rework analysis.", "Must link the repaired areas to the original review comments and updated tests.")]
            },
            new()
            {
                Key = "validate-qa-lane",
                Title = "Validate QA lane",
                Subtitle = "Quality regression lane",
                Notes = "Run the changed surfaces through focused QA and browser proof before the change can advance.",
                StepKind = ProcessStepKind.Review,
                DependsOnStepId = routeReviewDispositionId,
                DependsOnBranchOutcomeId = qaValidationOutcomeId,
                InputContractSummary = "Reviewed pull request packet with the QA-validation route selected.",
                OutputContractSummary = "Accepted or rejected QA evidence with explicit residual risks.",
                EvidenceContractSummary = "Targeted tests, browser screenshots, and unresolved defect notes.",
                DecisionRightsSummary = "QA lead may block weak proof and require deeper changed-surface coverage.",
                ExceptionPolicySummary = "Do not substitute generic smoke coverage for the explicitly routed QA lane.",
                TargetLeadHours = 10,
                CanvasX = 1240,
                CanvasY = 180,
                RoleAssignments = [BuildRoleAssignment(qaLeadId, ProcessResponsibilityKind.Responsible, "The QA lead owns the explicit regression gate for the routed change.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Evidence, "QA validation evidence pack", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for merge approval, release-note review, and later regression audits.", "Must capture the changed flows, screenshots, and unresolved defects or risks.")]
            },
            new()
            {
                Key = "review-security-impact",
                Title = "Review security impact",
                Subtitle = "Trust-sensitive lane",
                Notes = "Validate data-handling, secrets, and exception implications before the change can merge.",
                StepKind = ProcessStepKind.Approval,
                DependsOnStepId = routeReviewDispositionId,
                DependsOnBranchOutcomeId = securityReviewOutcomeId,
                RequiresApproval = true,
                RequiresDecisionRecord = true,
                InputContractSummary = "Reviewed pull request packet plus any tenant-data or secret-handling concerns.",
                OutputContractSummary = "Approved, blocked, or rejected security posture for merge.",
                EvidenceContractSummary = "Security notes, exception rationale, and approved controls.",
                DecisionRightsSummary = "Security reviewer owns the trust gate and cannot be bypassed by delivery pressure.",
                ExceptionPolicySummary = "Block the change when exception rationale, controls, or data-handling facts are incomplete.",
                TargetLeadHours = 6,
                CanvasX = 1240,
                CanvasY = 340,
                RoleAssignments = [BuildRoleAssignment(securityReviewerId, ProcessResponsibilityKind.Approver, "Security approval remains explicit and reviewable even when the reviewer pool changes.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Security merge assessment", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Confidential, 730, "Reusable for merge audit, release governance, and later security replay.", "Must capture approved controls, blocked concerns, and the residual-risk owner.")]
            },
            new()
            {
                Key = "review-architecture-escalation",
                Title = "Review architecture escalation",
                Subtitle = "Cross-module lane",
                Notes = "Escalate design consequences that affect canonical model boundaries, module seams, or long-term platform shape.",
                StepKind = ProcessStepKind.Review,
                DependsOnStepId = routeReviewDispositionId,
                DependsOnBranchOutcomeId = architectureReviewOutcomeId,
                RequiresDecisionRecord = true,
                InputContractSummary = "Reviewed pull request packet with the architecture-escalation route selected.",
                OutputContractSummary = "Architecture guidance with explicit accepted and rejected design paths.",
                EvidenceContractSummary = "Architecture review notes, source-of-truth decision, and rejected alternatives.",
                DecisionRightsSummary = "The architect may block unsafe shortcuts and require a clearer source-of-truth boundary.",
                ExceptionPolicySummary = "Do not hide cross-module consequences behind a generic review-complete state.",
                TargetLeadHours = 8,
                CanvasX = 1240,
                CanvasY = 500,
                RoleAssignments = [BuildRoleAssignment(architectId, ProcessResponsibilityKind.Responsible, "Architecture escalation stays attached to the solution-architect role even if a different reviewer takes the lane.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Architecture escalation record", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 730, "Reusable for merge review, later refactoring, and forensic replay of design decisions.", "Must name the selected design path, rejected options, and affected module boundaries.")]
            },
            new()
            {
                Key = "approve-merge-window",
                Title = "Approve merge window",
                Subtitle = "Ready-to-merge lane",
                Notes = "Make the final merge decision once the review lead routes the change directly into the merge lane.",
                StepKind = ProcessStepKind.Approval,
                DependsOnStepId = routeReviewDispositionId,
                DependsOnBranchOutcomeId = readyForMergeOutcomeId,
                RequiresApproval = true,
                InputContractSummary = "Reviewed pull request packet and explicit ready-for-merge route.",
                OutputContractSummary = "Approved or rejected merge timing and residual risk posture.",
                EvidenceContractSummary = "Merge approval note, release-note quality, and residual-risk owner.",
                DecisionRightsSummary = "Merge approver owns timing, merge readiness, and residual-risk acceptance.",
                ExceptionPolicySummary = "Reject the lane if release notes, rollback notes, or follow-up ownership are weak.",
                TargetLeadHours = 3,
                CanvasX = 1240,
                CanvasY = 660,
                RoleAssignments = [BuildRoleAssignment(mergeApproverId, ProcessResponsibilityKind.Approver, "The merge gate stays explicit even when release ownership rotates.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Merge approval note", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for release audit, incident replay, and merge-governance tuning.", "Must capture the approver, merge timing, and residual-risk owner.")]
            },
            new()
            {
                Key = "normalize-unclassified-review-disposition",
                Title = "Normalize unclassified review disposition",
                Subtitle = "Default lane",
                Notes = "Handle reviewer outcomes that do not match a named route yet, while keeping the next action explicit.",
                StepKind = ProcessStepKind.Review,
                DependsOnStepId = routeReviewDispositionId,
                InputContractSummary = "Reviewed pull request packet without an explicitly selected named branch outcome.",
                OutputContractSummary = "Normalized follow-up recommendation that can be converted into a typed route later.",
                EvidenceContractSummary = "Reviewer notes explaining why no named route fit the current case.",
                DecisionRightsSummary = "Review lead owns the default lane and must convert ambiguity into an explicit next action.",
                ExceptionPolicySummary = "Do not leave the review state ambiguous after landing in the default lane.",
                TargetLeadHours = 2,
                CanvasX = 1240,
                CanvasY = 820,
                RoleAssignments = [BuildRoleAssignment(reviewLeadId, ProcessResponsibilityKind.Responsible, "The default lane remains owned by the same review authority that selected the route.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Brief, "Default routing normalization note", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 180, "Reusable for process tuning and later route-taxonomy cleanup.", "Must describe why the route landed on the default lane and what explicit action should follow.")]
            },
            new()
            {
                Key = "escalate-review-workflow-failure",
                Title = "Escalate review workflow failure",
                Subtitle = "Error lane",
                Notes = "Capture routing failures caused by malformed review state, runtime issues, or missing authoring context before the process continues.",
                StepKind = ProcessStepKind.End,
                DependsOnStepId = routeReviewDispositionId,
                DependsOnBranchOutcomeId = errorOutcomeId,
                RequiresDecisionRecord = true,
                InputContractSummary = "Failed routing attempt, malformed review payload, or missing authoring state.",
                OutputContractSummary = "Escalated workflow defect with explicit owner and recovery recommendation.",
                EvidenceContractSummary = "Failure details, missing context, and recovery notes.",
                DecisionRightsSummary = "The review lead records the failure, but recovery ownership moves to the platform or process owner outside this lane.",
                ExceptionPolicySummary = "Do not silently recover from a broken routing state; capture the failure explicitly.",
                TargetLeadHours = 2,
                CanvasX = 1240,
                CanvasY = 980,
                RoleAssignments = [BuildRoleAssignment(reviewLeadId, ProcessResponsibilityKind.Responsible, "The same role that owns routing must record routing failures for traceability.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Evidence, "Review workflow failure packet", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 365, "Reusable for process defect repair, platform follow-up, and architecture trouble logs.", "Must capture the failing state, user-visible symptom, and the recommended recovery path.")]
            }
        };

        return new ProcessSeedScenario(
            ProcessSeedScenarioKeys.BranchingCodeReview,
            "Branching code review and merge governance",
            "Branching code review and merge governance / pull request routing rehearsal",
            "Exercise the additive branch-router canvas model with an explicit review decision node, multiple routed outcomes, a default lane, and an error lane around a realistic software-development change.",
            "Provide a concrete software-development branching scenario that makes review routing, QA, security, architecture escalation, merge approval, default handling, and error handling visible on the canvas.",
            "Developer productivity platform",
            "Engineering governance board",
            "Code review is not a single linear gate; it needs an explicit routing surface that shows who decides the next lane and what each routed outcome means.",
            "Review routing must stay explicit, strongly typed, and replayable so later merge or governance questions can be answered from the model instead of chat fragments.",
            "Seeded specifically to validate branch-router authoring, role-to-router inputs, multi-output canvases, and software-delivery branching examples.",
            "Merge readiness depends on the selected routed lane, and the process still needs visible default and error handling when a named route is missing or the workflow state is broken.",
            "Role contracts for authoring, routing, QA, security, architecture, and merge approval stay explicit even when individuals change.",
            "Assisted execution is acceptable because the branching goal is to make routing explicit before deeper automation is attempted.",
            "This scenario is intentionally branch-heavy so canvas screenshots, authoring validation, and architecture-gap review can use one realistic software-development example.",
            ProcessOperatingMode.AssistedExecution,
            roles,
            steps);
    }

    private static ProcessSeedScenario BuildHotfixRolloutScenario()
    {
        var incidentCommanderId = Guid.NewGuid();
        var platformEngineerId = Guid.NewGuid();
        var databaseEngineerId = Guid.NewGuid();
        var qaResponderId = Guid.NewGuid();
        var releaseApproverId = Guid.NewGuid();
        var customerLiaisonId = Guid.NewGuid();

        var roles = new List<ProcessRoleEditorModel>
        {
            BuildRole(incidentCommanderId, "incident-commander", "Incident commander", "Owns classification, bridge leadership, and decision pacing for emergency response.", "Primary accountable coordinator for the hotfix lane who can escalate but cannot waive evidence or rollback discipline.", ProjectPartyAssignmentRole.Manager, "person", "Emergency response owner for pace, escalation, and cross-team alignment.", true, true, true, 60, "process-role-template/incident-commander", "Incident commander / v1"),
            BuildRole(platformEngineerId, "platform-engineer", "Platform engineer", "Owns hotfix package assembly, deployment automation, and runtime telemetry interpretation.", "Senior production-capable engineer for packaging, rollout execution, and rollback automation.", ProjectPartyAssignmentRole.TeamMember, "person", "Platform delivery owner for the emergency patch and deployment lane.", true, true, false, 100, "process-role-template/platform-engineer", "Platform engineer / v1"),
            BuildRole(databaseEngineerId, "database-engineer", "Database engineer", "Owns blast-radius analysis, shard-state inspection, and rollback script validation.", "Specialist engineer for schema drift, shard locks, and data-safe rollback planning.", ProjectPartyAssignmentRole.TeamMember, "person", "Database reliability owner for high-risk migration and rollback paths.", true, true, false, 80, "process-role-template/database-engineer", "Database engineer / v1"),
            BuildRole(qaResponderId, "qa-responder", "QA responder", "Owns emergency validation depth and decides whether the hotfix evidence is credible enough for release.", "Fast-response quality owner for shadow validation and focused regression coverage.", ProjectPartyAssignmentRole.Reviewer, "person", "Quality gate owner for emergency regression evidence.", true, true, true, 50, "process-role-template/qa-responder", "QA responder / v1"),
            BuildRole(releaseApproverId, "release-approver", "Release approver", "Owns the formal go / no-go decision for the emergency release window.", "Governance-capable approver for exceptional release timing and rollback triggers.", ProjectPartyAssignmentRole.Reviewer, "person", "Emergency release authority for go / no-go and rollback policy.", true, true, true, 30, "process-role-template/release-approver", "Release approver / v1"),
            BuildRole(customerLiaisonId, "customer-liaison", "Customer liaison", "Owns external communications, status cadence, and expectation management for impacted customers.", "Customer-facing owner for incident updates, recovery expectation setting, and follow-up messaging.", ProjectPartyAssignmentRole.CustomerContact, "person", "Customer communication owner for impacted accounts during emergency execution.", true, true, false, 40, "process-role-template/customer-liaison", "Customer liaison / v1")
        };

        var acknowledgeId = Guid.NewGuid();
        var blastRadiusId = Guid.NewGuid();
        var packageHotfixId = Guid.NewGuid();
        var validateHotfixId = Guid.NewGuid();
        var approveEmergencyReleaseId = Guid.NewGuid();
        var executeRolloutId = Guid.NewGuid();

        var steps = new List<ProcessStepEditorModel>
        {
            new()
            {
                Id = acknowledgeId,
                Key = "acknowledge-production-failure",
                Title = "Acknowledge production failure and activate emergency bridge",
                Subtitle = "Classify impact and establish command",
                Notes = "Confirm tenant impact, activate the bridge, and fix a single accountable commander before engineering changes begin.",
                StepKind = ProcessStepKind.Start,
                InputContractSummary = "Customer-impact alerts, telemetry anomalies, and on-call observations.",
                OutputContractSummary = "Emergency bridge with severity, bridge cadence, and accountable commander.",
                EvidenceContractSummary = "Initial timeline, impacted tenants or regions, and command ownership.",
                DecisionRightsSummary = "Incident commander may classify severity and mobilize teams but cannot authorize production changes alone.",
                ExceptionPolicySummary = "Escalate immediately when customer impact is uncertain or multiple services show correlated failure.",
                TargetLeadHours = 1,
                CanvasX = 120,
                CanvasY = 360,
                RoleAssignments = [BuildRoleAssignment(incidentCommanderId, ProcessResponsibilityKind.Responsible, "Command authority transfers to the active incident commander without changing the emergency process contract."), BuildRoleAssignment(customerLiaisonId, ProcessResponsibilityKind.Reviewer, "Customer communication review remains explicit even under time pressure.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Brief, "Emergency bridge activation note", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 365, "Reusable for the live incident timeline and later post-incident reconstruction.", "Must capture severity, time opened, and impacted customer segments.")]
            },
            new()
            {
                Id = blastRadiusId,
                Key = "analyze-blast-radius",
                Title = "Analyze blast radius, shard state, and rollback options",
                Subtitle = "Data-safe diagnosis before code changes",
                Notes = "Confirm which tenants, shards, or regions are affected and whether rollback is possible without amplifying the outage.",
                StepKind = ProcessStepKind.Review,
                RequiresDecisionRecord = true,
                InputContractSummary = "Bridge timeline, telemetry snapshots, shard health, and current deployment metadata.",
                OutputContractSummary = "Typed blast-radius assessment with rollback feasibility and recovery constraints.",
                EvidenceContractSummary = "Shard lock state, suspected root cause, rollback script readiness, and unsafe paths.",
                DecisionRightsSummary = "Database engineer recommends the safe data path; incident commander decides whether packaging can begin.",
                ExceptionPolicySummary = "Do not authorize a hotfix package while rollback feasibility or shard safety remains ambiguous.",
                TargetLeadHours = 3,
                DependsOnStepId = acknowledgeId,
                CanvasX = 430,
                CanvasY = 360,
                RoleAssignments = [BuildRoleAssignment(databaseEngineerId, ProcessResponsibilityKind.Responsible, "Database ownership may move to another qualified specialist without invalidating the evidence trail."), BuildRoleAssignment(platformEngineerId, ProcessResponsibilityKind.Reviewer, "Platform engineer reviews blast-radius findings before package assembly.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Blast-radius and rollback assessment", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Confidential, 1095, "Reusable for emergency release approval, rollback execution, and audit.", "Must capture affected shards, rollback feasibility, and explicitly rejected unsafe remediation paths.")]
            },
            new()
            {
                Id = packageHotfixId,
                Key = "package-hotfix",
                Title = "Package emergency hotfix and rollback scripts",
                Subtitle = "Controlled change assembly",
                Notes = "Assemble the emergency patch, deployment instructions, and rollback scripts under one accountable engineer.",
                StepKind = ProcessStepKind.Work,
                RequiresDecisionRecord = true,
                InputContractSummary = "Blast-radius assessment, target change scope, and deployment constraints.",
                OutputContractSummary = "Hotfix package with rollout steps, rollback scripts, and changed-surface inventory.",
                EvidenceContractSummary = "Patch diff, deployment bundle, schema scripts, and operator checklist.",
                DecisionRightsSummary = "Platform engineer owns assembly but cannot expand scope beyond the approved emergency boundary.",
                ExceptionPolicySummary = "Pause immediately when the required fix grows into an unreviewable multi-area release.",
                TargetLeadHours = 4,
                DependsOnStepId = blastRadiusId,
                CanvasX = 740,
                CanvasY = 360,
                RoleAssignments = [BuildRoleAssignment(platformEngineerId, ProcessResponsibilityKind.Responsible, "Emergency packaging remains attached to the platform-engineer role even if the individual changes."), BuildRoleAssignment(databaseEngineerId, ProcessResponsibilityKind.Reviewer, "Database review is mandatory for scripts that touch shard state or tenant data.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Deliverable, "Emergency patch and rollback bundle", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Confidential, 365, "Reusable for validation, release approval, and post-incident engineering review.", "Must link the exact patch, database scripts, rollback path, and operator checklist.")]
            },
            new()
            {
                Id = validateHotfixId,
                Key = "validate-hotfix",
                Title = "Validate emergency fix in shadow environment",
                Subtitle = "Fast but explicit regression gate",
                Notes = "Run the emergency checklist against the hotfix bundle in a shadow or representative environment before release approval.",
                StepKind = ProcessStepKind.Review,
                InputContractSummary = "Emergency patch bundle, known blast radius, and incident reproduction notes.",
                OutputContractSummary = "Focused validation result with residual risks and unsupported cases.",
                EvidenceContractSummary = "Checklist output, shadow-environment notes, and residual-risk annotations.",
                DecisionRightsSummary = "QA responder may block the rollout if the emergency evidence is too thin for the risk profile.",
                ExceptionPolicySummary = "Do not convert the gate into a verbal approval; evidence still needs typed reviewable form.",
                TargetLeadHours = 2,
                DependsOnStepId = packageHotfixId,
                CanvasX = 1050,
                CanvasY = 360,
                RoleAssignments = [BuildRoleAssignment(qaResponderId, ProcessResponsibilityKind.Responsible, "Emergency QA ownership may move across the responder rota but the gate remains explicit."), BuildRoleAssignment(platformEngineerId, ProcessResponsibilityKind.Reviewer, "Package owner reviews failures and unsupported coverage before approval.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Evidence, "Emergency validation evidence pack", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for emergency release approval and later correction of the emergency checklist.", "Must name validated flows, skipped checks, and residual risk that the approver must accept explicitly.")]
            },
            new()
            {
                Id = approveEmergencyReleaseId,
                Key = "approve-emergency-release",
                Title = "Approve emergency release window",
                Subtitle = "Explicit go / no-go and rollback trigger",
                Notes = "Review the emergency evidence, rollback trigger, and customer communication plan before production rollout.",
                StepKind = ProcessStepKind.Approval,
                RequiresApproval = true,
                RequiresDecisionRecord = true,
                InputContractSummary = "Validation evidence, rollback trigger, customer-impact status, and operator readiness.",
                OutputContractSummary = "Go / no-go decision with explicit rollback trigger and accountable owners.",
                EvidenceContractSummary = "Approval note, release window, fallback trigger, and outward-communication owner.",
                DecisionRightsSummary = "Release approver owns the emergency release decision and cannot waive missing evidence or unclear rollback control.",
                ExceptionPolicySummary = "Reject the rollout when rollback conditions or customer-facing obligations are not explicit.",
                TargetLeadHours = 1,
                DependsOnStepId = validateHotfixId,
                CanvasX = 1360,
                CanvasY = 360,
                RoleAssignments = [BuildRoleAssignment(releaseApproverId, ProcessResponsibilityKind.Approver, "Emergency release approval remains attached to the role, not a specific operator."), BuildRoleAssignment(customerLiaisonId, ProcessResponsibilityKind.Reviewer, "Customer communication owner must review timing and outbound commitments before approval.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Decision, "Emergency release approval record", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 1095, "Reusable for audit, customer follow-up review, and future emergency policy tuning.", "Must name the approver, rollback trigger, communication owner, and residual risk owner.")]
            },
            new()
            {
                Id = executeRolloutId,
                Key = "execute-emergency-rollout",
                Title = "Execute emergency rollout and watch telemetry",
                Subtitle = "Controlled production action",
                Notes = "Roll out the patch inside the approved window while telemetry, shard locks, and customer communication remain actively managed.",
                StepKind = ProcessStepKind.Delivery,
                InputContractSummary = "Approved release record, deployment bundle, telemetry checkpoints, and customer message cadence.",
                OutputContractSummary = "Executed rollout with explicit telemetry outcome and rollback state.",
                EvidenceContractSummary = "Operator notes, telemetry checkpoints, rollback invocation if needed, and customer update timeline.",
                DecisionRightsSummary = "Platform engineer may execute only inside the approved window and rollback trigger boundaries.",
                ExceptionPolicySummary = "Trigger rollback immediately when shard lock duration, tenant impact, or telemetry drift breaches the approved threshold.",
                TargetLeadHours = 2,
                DependsOnStepId = approveEmergencyReleaseId,
                CanvasX = 1670,
                CanvasY = 360,
                RoleAssignments = [BuildRoleAssignment(platformEngineerId, ProcessResponsibilityKind.Responsible, "Execution ownership remains with the platform-engineer role until rollout completes or fails."), BuildRoleAssignment(incidentCommanderId, ProcessResponsibilityKind.Reviewer, "Incident commander reviews telemetry and escalation timing throughout the rollout."), BuildRoleAssignment(databaseEngineerId, ProcessResponsibilityKind.Backup, "Database specialist remains an explicit fallback for rollback execution.", false, 1)]
            },
            new()
            {
                Key = "post-incident-review",
                Title = "Capture post-incident learning and corrective actions",
                Subtitle = "Forensic replay and systemic follow-up",
                Notes = "Turn the failed or recovered emergency path into explicit learning about detection, coordination, rollback, and architecture weaknesses.",
                StepKind = ProcessStepKind.End,
                RequiresDecisionRecord = true,
                InputContractSummary = "Rollout outcome, telemetry record, customer communications, and command timeline.",
                OutputContractSummary = "Post-incident review with corrective actions, owner assignments, and simulation updates.",
                EvidenceContractSummary = "Timeline, contributing factors, missing controls, and next corrective actions.",
                DecisionRightsSummary = "Incident commander records learning, but role owners remain accountable for their corrective actions.",
                ExceptionPolicySummary = "Do not close the incident without turning repeated failure signals into explicit corrective work.",
                TargetLeadHours = 4,
                DependsOnStepId = executeRolloutId,
                CanvasX = 1980,
                CanvasY = 360,
                RoleAssignments = [BuildRoleAssignment(incidentCommanderId, ProcessResponsibilityKind.Responsible, "Learning capture stays with the command role for emergency-process tuning."), BuildRoleAssignment(customerLiaisonId, ProcessResponsibilityKind.Reviewer, "Customer communication review remains explicit when follow-up obligations affect trust.")],
                ArtifactExpectations = [BuildArtifactExpectation(ProcessArtifactKind.Brief, "Post-incident corrective action review", ProcessArtifactTrustRequirement.ReviewRequired, ProcessSensitivityLevel.Internal, 1095, "Reusable for simulation packs, response playbook tuning, and recurring-incident analysis.", "Must capture missing controls, decision latency, and concrete corrective actions with owners.")]
            }
        };

        return new ProcessSeedScenario(
            ProcessSeedScenarioKeys.HotfixRollout,
            "Emergency hotfix rollout with shard-risk governance",
            "Emergency hotfix rollout with shard-risk governance / tenant billing outage",
            "Coordinate emergency diagnosis, packaging, validation, release approval, rollout, and learning for a realistic production hotfix with data-sensitive rollback constraints.",
            "Provide a realistic software-operations emergency process that preserves role-first accountability, rollback discipline, and customer-trust obligations under time pressure.",
            "Enterprise Billing Platform",
            "Production response command",
            "Emergency software delivery spans production operations, customer communication, database safety, and release governance, so the process must keep those boundaries explicit.",
            "Time pressure never removes the need for explicit rollback ownership, communication ownership, and typed emergency evidence.",
            "Seeded from the emergency hotfix governance pack with realistic shard-lock, rollout-window, and customer-communication constraints.",
            "Emergency release requires typed blast-radius analysis, focused QA proof, release approval, and explicit rollback trigger ownership.",
            "Command, communication, database safety, and rollout execution remain durable role contracts even if people on the rota change.",
            "Emergency execution is allowed only inside a bounded release window with explicit rollback and communication obligations.",
            "This scenario is intentionally rich enough for emergency simulation, incident replay, and canvas authoring validation.",
            ProcessOperatingMode.Emergency,
            roles,
            steps);
    }

    private static ProcessRoleEditorModel BuildRole(
        Guid id,
        string key,
        string displayName,
        string purpose,
        string staffingIntent,
        ProjectPartyAssignmentRole? preferredProjectAssignmentRole,
        string preferredExecutorKind,
        string snapshotSummary,
        bool isRequired,
        bool allowsFallback,
        bool requiresExplicitApproval,
        int defaultAllocationPercent,
        string roleTemplateSourceKey,
        string roleTemplateSnapshotName)
    {
        return new ProcessRoleEditorModel
        {
            Id = id,
            Key = key,
            DisplayName = displayName,
            Purpose = purpose,
            StaffingIntent = staffingIntent,
            PreferredProjectAssignmentRole = preferredProjectAssignmentRole,
            PreferredExecutorKind = preferredExecutorKind,
            SnapshotSummary = snapshotSummary,
            IsRequired = isRequired,
            AllowsFallback = allowsFallback,
            RequiresExplicitApproval = requiresExplicitApproval,
            DefaultAllocationPercent = defaultAllocationPercent,
            RoleTemplateSourceKey = roleTemplateSourceKey,
            RoleTemplateSnapshotName = roleTemplateSnapshotName
        };
    }

    private static ProcessStepRoleRequirementEditorModel BuildRoleAssignment(
        Guid roleRequirementId,
        ProcessResponsibilityKind responsibilityKind,
        string rebindPolicySummary,
        bool isRequired = true,
        int fallbackOrder = 0)
    {
        return new ProcessStepRoleRequirementEditorModel
        {
            RoleRequirementId = roleRequirementId,
            ResponsibilityKind = responsibilityKind,
            IsRequired = isRequired,
            FallbackOrder = fallbackOrder,
            RebindPolicySummary = rebindPolicySummary
        };
    }

    private static ProcessArtifactExpectationEditorModel BuildArtifactExpectation(
        ProcessArtifactKind artifactKind,
        string title,
        ProcessArtifactTrustRequirement trustRequirement,
        ProcessSensitivityLevel sensitivityLevel,
        int retentionDays,
        string allowedFutureUsageSummary,
        string validationRequirementSummary)
    {
        return new ProcessArtifactExpectationEditorModel
        {
            ArtifactKind = artifactKind,
            Title = title,
            TrustRequirement = trustRequirement,
            SensitivityLevel = sensitivityLevel,
            RetentionDays = retentionDays,
            AllowedFutureUsageSummary = allowedFutureUsageSummary,
            ValidationRequirementSummary = validationRequirementSummary
        };
    }
}
