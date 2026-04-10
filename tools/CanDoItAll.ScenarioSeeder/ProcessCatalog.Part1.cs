using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.ScenarioSeeder;

internal static partial class ProcessCatalog
{
    public static IReadOnlyList<AgentFrameworkIntegrationSimulationSeeder.ProcessSpec> BuildProcessSpecs()
    {
        return
        [
            BuildOperatingModelProcess(),
            BuildCanonicalConvergenceProcess(),
            BuildLocalSliceProcess(),
            BuildOpenAiComplexLaneProcess(),
            BuildValidationAndLearningProcess()
        ];
    }

    private static AgentFrameworkIntegrationSimulationSeeder.ProcessSpec BuildOperatingModelProcess()
    {
        return new AgentFrameworkIntegrationSimulationSeeder.ProcessSpec(
            "AgentFramework integration / role-first operating model baseline",
            "Role-first operating model baseline / active run",
            ProcessOperatingMode.Development,
            ProcessCriticality.High,
            ProcessAutonomyLevel.Guarded,
            "Define the reusable role system, approval rights, execution lanes, and meeting triggers before implementation binds to specific people or agents.",
            "Preserve role-first process stability when participants, models, or providers change later.",
            "Process defines accountable roles before staffing and enforces explicit boundaries between human-only, local-LLM, and OpenAI-assisted work.",
            "The sponsor approves irreversible governance changes, architects define boundaries, and AI lanes stay subordinate to human review.",
            "No participant or agent may self-authorize a change to canonical ownership, provider governance, or release risk acceptance.",
            "Manual and guarded autonomy coexist: small slices may be prepared automatically, but governance remains human-accountable.",
            "Simulation includes a blocked governance step so the project graph can demonstrate meeting-driven escalation.",
            [
                new("program-sponsor", "Program sponsor", "Approve scope, budget posture, and irreversible risk acceptance. Cannot author technical design details or bypass security review.", "Human-only sponsor role. Must stay stable even if the sponsor changes later.", "person", ProjectPartyAssignmentRole.Stakeholder, true, true, 10, "Role exists to approve the operating model and force explicit escalation when business risk changes.", "AFINT-SPONSOR", "Bound to the current program sponsor for release-risk and budget decisions."),
                new("delivery-manager", "Delivery manager", "Sequence work, coordinate staffing, and trigger decision cadences. Cannot self-approve architecture or security exceptions.", "Human delivery orchestration role with authority to call meetings when slices stop being safely independent.", "person", ProjectPartyAssignmentRole.Manager, false, true, 40, "Maintains delivery flow and ensures blocked work becomes an explicit decision instead of silent drift.", "AFINT-DELIVERY-MGR", "Bound to the active delivery manager for cadence and dependency control."),
                new("solution-architect", "Solution architect", "Define module boundaries, integration contracts, and acceptable merge paths. Cannot waive evidence or release validation.", "Human architecture role responsible for explicit boundary proposals.", "person", ProjectPartyAssignmentRole.TechnicalContact, false, true, 35, "Owns the shape of the integration, but not the final business-risk decision.", "AFINT-ARCH", "Bound to the current solution architect for boundary design."),
                new("canonical-steward", "Canonical model steward", "Approve source-of-truth mapping and reject shadow-model duplication. Cannot change staffing or schedule unilaterally.", "Human reviewer role for ownership decisions that must survive personnel changes.", "person", ProjectPartyAssignmentRole.Reviewer, true, true, 30, "Protects long-term model integrity by requiring explicit ownership and migration decisions.", "AFINT-CANONICAL", "Bound to the canonical model steward."),
                new("local-lane-analyst", "Local lane analyst", "Prepare small-slice automation options. Cannot approve cross-boundary work, handle secrets, or decide provider governance.", "AI-assisted helper role limited to bounded preparation work.", "local-llm-agent", ProjectPartyAssignmentRole.AiAgent, false, true, 15, "Used only to draft lane options and highlight risky work that must stay human-reviewed.", "AFINT-LOCAL-LLM", "Bound to the local slice worker for bounded option generation.")
            ],
            [
                new("collect-constraints", "Collect integration objectives and constraints", "Capture the real target and non-negotiable constraints before staffing or coding begins.", "Start with the existing bundle goals, repo boundaries, process expectations, and validation obligations.", ProcessStepKind.Start, false, false, false, "Bundle goals, repo inventory, prior process-management findings, and validation rules.", "Typed constraint packet with decision-worthy scope and exclusions.", "Constraint packet, repo map, and dependency summary.", "Delivery manager prepares; sponsor and architect may challenge unclear business goals.", "Raise a sponsor meeting if constraints imply scope, budget, or rollout changes beyond the current mandate.", 6, 140, 120, null,
                    [
                        new("delivery-manager", ProcessResponsibilityKind.Responsible, 0, "Role remains delivery-owned even if the current manager changes."),
                        new("solution-architect", ProcessResponsibilityKind.Reviewer, 1, "Architect review is required before lane design.")
                    ],
                    [
                        new(ProcessArtifactKind.Brief, "Integration constraint packet", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for architecture, staffing, and release planning until a new decision supersedes it.", "Packet must explicitly list repo boundaries, excluded work, and validation obligations.")
                    ]),
                new("draft-role-catalog", "Draft role catalog and execution-lane policy", "Define roles before naming individuals so the process survives participant changes.", "Document allowed actions, prohibited actions, local-LLM-safe work, OpenAI escalation triggers, and mandatory meetings.", ProcessStepKind.Work, false, false, true, "Constraint packet and current delivery/team capabilities.", "Role-first operating model draft with explicit lane policy.", "Role matrix, lane policy, and meeting trigger list.", "Architect and canonical steward define boundaries; delivery manager ensures staffing realism.", "Raise an architecture meeting if any role needs both canonical-ownership power and release-approval power.", 10, 420, 120, "collect-constraints",
                    [
                        new("solution-architect", ProcessResponsibilityKind.Responsible, 0, "Architect owns boundary draft."),
                        new("canonical-steward", ProcessResponsibilityKind.Reviewer, 1, "Canonical steward checks for ownership drift."),
                        new("local-lane-analyst", ProcessResponsibilityKind.Backup, 2, "Local AI may help compare options but cannot finalize them.")
                    ],
                    [
                        new(ProcessArtifactKind.Decision, "Role and execution-lane matrix", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for future bundle preparation and staffing decisions.", "Matrix must include explicit allowed, prohibited, and escalation-trigger statements for each role.")
                    ]),
                new("review-lane-risks", "Review lane risks and blocked responsibilities", "Challenge any role or lane that would hide escalation or duplicate authority.", "Review whether local and external AI lanes remain bounded and whether sponsor/architect powers are separated correctly.", ProcessStepKind.Decision, true, false, true, "Role matrix and example work slices.", "Accepted risk posture or explicit blockers requiring a meeting.", "Risk review memo and blocked-role list.", "Canonical steward may block if ownership is unclear; sponsor decides only after architecture review.", "Raise a convergence meeting when provider-profile ownership or participant identity would live in more than one canonical place.", 6, 720, 120, "draft-role-catalog",
                    [
                        new("canonical-steward", ProcessResponsibilityKind.Responsible, 0, "Canonical steward leads this challenge step."),
                        new("program-sponsor", ProcessResponsibilityKind.Reviewer, 1, "Sponsor reviews business impact of blocked responsibilities.")
                    ],
                    []),
                new("convergence-workshop", "Run role charter convergence workshop", "Human meeting to resolve blocked authority, unclear escalation rules, or duplicated ownership.", "Meeting is mandatory whenever text review cannot settle authority or lane problems.", ProcessStepKind.Review, true, false, true, "Blocked-role list, lane matrix, and example slices.", "Converged role charter with explicit meeting outcomes.", "Meeting record and decision log.", "Delivery manager convenes; sponsor, architect, and canonical steward decide together.", "If the meeting cannot converge, do not proceed to approval. Create a follow-up decision bundle first.", 4, 1040, 120, "review-lane-risks",
                    [
                        new("delivery-manager", ProcessResponsibilityKind.Responsible, 0, "Delivery manager convenes the workshop."),
                        new("program-sponsor", ProcessResponsibilityKind.Approver, 1, "Sponsor participates in final human decision."),
                        new("solution-architect", ProcessResponsibilityKind.Reviewer, 2, "Architect confirms the technical consequences.")
                    ],
                    [
                        new(ProcessArtifactKind.Transcript, "Role charter convergence meeting record", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for later audits and follow-up bundle generation.", "Meeting record must capture which role may do what, what stays prohibited, and which triggers force another meeting.")
                    ]),
                new("approve-role-baseline", "Approve reusable role baseline", "Freeze the role-first operating model for downstream bundle work.", "Approval must happen after all blocked ownership issues are either resolved or explicitly deferred.", ProcessStepKind.Approval, false, true, true, "Converged role charter and meeting outputs.", "Approved role baseline usable by future bundles and project assignments.", "Approval record and versioned operating-model reference.", "Sponsor approves. Architect and steward cannot self-approve on behalf of the sponsor.", "If unresolved authority remains, block approval and return to the workshop with a tighter question set.", 2, 1360, 120, "convergence-workshop",
                    [
                        new("program-sponsor", ProcessResponsibilityKind.Approver, 0, "Sponsor approval is mandatory."),
                        new("solution-architect", ProcessResponsibilityKind.Reviewer, 1, "Architect confirms implementability.")
                    ],
                    []),
                new("publish-role-handbook", "Publish role handbook for implementation teams", "Convert the approved role baseline into a reusable reference for bundle execution.", "This is the delivery output consumed by the rest of the project graph.", ProcessStepKind.Delivery, false, false, false, "Approved role baseline.", "Published handbook and lane policy used by subsequent processes.", "Reusable handbook and route links.", "Delivery manager publishes; steward verifies the published reference still matches the approved decision.", "If publication would remove prohibited actions or meeting triggers from the text, reopen approval instead.", 3, 1680, 120, "approve-role-baseline",
                    [
                        new("delivery-manager", ProcessResponsibilityKind.Responsible, 0, "Delivery manager publishes and distributes the handbook."),
                        new("canonical-steward", ProcessResponsibilityKind.Reviewer, 1, "Steward validates fidelity to the approved baseline.")
                    ],
                    [])
            ],
            [
                new(0, ProcessStepRunStatus.Completed, "Integration objectives, repo boundaries, and validation constraints were captured from the current platform state.", "scenario-seeder/operating-model"),
                new(1, ProcessStepRunStatus.Completed, "The role matrix now distinguishes human-only approvals, local-LLM-safe slices, and OpenAI escalation triggers.", "scenario-seeder/operating-model"),
                new(2, ProcessStepRunStatus.Blocked, "Blocked because provider-profile ownership between CRM/HR, CanDoItAll, and AgentFramework is still disputed and requires a human convergence meeting.", "scenario-seeder/operating-model")
            ],
            [
                new("draft-role-catalog", ProcessArtifactKind.Decision, "Role and execution-lane matrix", ProcessArtifactTrustStatus.Approved, ProcessSensitivityLevel.Internal, "Drafted from the current process bundle, repo inventory, and execution-lane constraints in the live profile.", "Reusable for future bundles, staffing decisions, and simulation review until a new approved baseline supersedes it.", "Reviewed by architecture and canonical governance during scenario seeding.")
            ]);
    }
}
