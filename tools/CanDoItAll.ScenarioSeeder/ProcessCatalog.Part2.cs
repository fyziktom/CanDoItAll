using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.ScenarioSeeder;

internal static partial class ProcessCatalog
{
    private static AgentFrameworkIntegrationSimulationSeeder.ProcessSpec BuildCanonicalConvergenceProcess()
    {
        return new AgentFrameworkIntegrationSimulationSeeder.ProcessSpec(
            "AgentFramework integration / canonical model and boundary convergence",
            "Canonical model and boundary convergence / working run",
            ProcessOperatingMode.GovernedLive,
            ProcessCriticality.MissionCritical,
            ProcessAutonomyLevel.Guarded,
            "Resolve single sources of truth, projection boundaries, and migration direction across CanDoItAll, CRM/HR, and AgentFramework before implementation spreads duplication.",
            "Prevent permanent architectural drift by forcing explicit ownership, migration, and decision evidence.",
            "The process turns repo and module ambiguity into explicit ownership maps, migration decisions, and bounded follow-up work.",
            "Canonical ownership changes require steward and architecture review and may trigger a human convergence meeting.",
            "No repo may keep a shadow copy that behaves like a primary registry once a canonical owner is chosen.",
            "Automation may prepare inventories and diffs, but humans decide ownership and migration boundaries.",
            "Simulation keeps the run near approval so project-graph nodes can open both an active working process and the resulting decision backlog.",
            [
                new("solution-architect", "Solution architect", "Define system boundaries, migration shape, and acceptable integration seams. Cannot self-approve release or staffing impacts.", "Human architecture lead for cross-repo boundaries.", "person", ProjectPartyAssignmentRole.TechnicalContact, false, true, 35, "Owns the boundary proposal and integration constraints.", "AFINT-ARCH", "Bound to the current solution architect."),
                new("canonical-steward", "Canonical model steward", "Approve the source of truth for providers, participants, role definitions, and process artifacts. Cannot invent fallback shadow stores.", "Human reviewer ensuring model integrity across modules.", "person", ProjectPartyAssignmentRole.Reviewer, true, true, 30, "Acts as the source-of-truth gatekeeper.", "AFINT-CANONICAL", "Bound to the canonical model steward."),
                new("delivery-manager", "Delivery manager", "Coordinate the convergence work, run meetings, and keep the migration plan sliceable. Cannot decide canonical ownership alone.", "Human delivery role keeping convergence outputs executable.", "person", ProjectPartyAssignmentRole.Manager, false, true, 20, "Ensures the ownership decision becomes a real backlog instead of architecture prose.", "AFINT-DELIVERY-MGR", "Bound to the delivery manager for convergence sequencing."),
                new("crmhr-owner", "CRM/HR owner", "Describe current participant, staffing, and supplier obligations. Cannot keep legacy ownership without explicit approval.", "Human domain owner for current CRM/HR semantics.", "person", ProjectPartyAssignmentRole.TeamMember, false, true, 20, "Brings current-state constraints so migration does not silently break staffing or supplier flows.", "AFINT-CRMHR", "Bound to the CRM/HR owner for current-state knowledge."),
                new("integration-engineer", "AgentFramework bridge engineer", "Assess implementation impact across repos and service registrations. Cannot decide canonical ownership alone.", "Human implementation role for integration feasibility.", "person", ProjectPartyAssignmentRole.TeamMember, false, true, 45, "Provides the migration and runtime consequences of each ownership option.", "AFINT-INTEGRATION", "Bound to the primary integration engineer."),
                new("governance-reviewer", "Governance reviewer", "Reject ambiguous ownership, unsafe migration plans, or hidden fallback mechanisms. Cannot set business priority alone.", "Human governance role for risky cross-module changes.", "person", ProjectPartyAssignmentRole.Reviewer, true, true, 20, "Ensures the convergence plan stays reviewable and reversible.", "AFINT-SECURITY", "Bound to the security and governance reviewer.")
            ],
            [
                new("inventory-sources", "Inventory sources of truth and projections", "List every current owner, projection, and duplicated record involved in agent/provider/process identity.", "Use both repos and the running product state so the inventory matches reality.", ProcessStepKind.Start, false, false, false, "Repo map, runtime state, and existing bundle notes.", "Typed inventory of current owners, projections, and drift risks.", "Inventory sheet and drift notes.", "Architect and integration engineer gather facts; local AI may help compare code but cannot infer ownership alone.", "Raise a meeting if two modules both mutate the same conceptual entity.", 8, 140, 300, null,
                    [
                        new("solution-architect", ProcessResponsibilityKind.Responsible, 0, "Architect owns the inventory baseline."),
                        new("integration-engineer", ProcessResponsibilityKind.Reviewer, 1, "Bridge engineer checks runtime impact."),
                        new("crmhr-owner", ProcessResponsibilityKind.Reviewer, 2, "CRM/HR owner confirms current domain usage.")
                    ],
                    []),
                new("propose-ownership-map", "Propose canonical ownership map", "Convert the inventory into one explicit owner per concept with projections called out separately.", "This step must answer where provider profiles, participant records, agent registries, and process role templates truly live.", ProcessStepKind.Work, false, false, true, "Current-state inventory.", "Ownership map with explicit canonical entities and projections.", "Ownership map draft and rationale.", "Architect proposes; canonical steward can reject any shadow-model pattern.", "If the map would leave write authority in more than one place, stop and schedule a convergence meeting.", 10, 460, 300, "inventory-sources",
                    [
                        new("solution-architect", ProcessResponsibilityKind.Responsible, 0, "Architect authors the proposed ownership map."),
                        new("canonical-steward", ProcessResponsibilityKind.Reviewer, 1, "Steward challenges any ambiguous ownership.")
                    ],
                    [
                        new(ProcessArtifactKind.Decision, "Cross-repo ownership matrix", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for migration planning, future bundle validation, and onboarding new contributors.", "Matrix must list the canonical owner, projection owners, and forbidden duplicate write paths for each major concept.")
                    ]),
                new("reconcile-provider-and-agent-models", "Reconcile provider-profile and agent identity duplication", "Turn the ownership map into a concrete decision on what is merged, retired, wrapped, or projected.", "Focus on provider profiles, agent creation, participant identity, and any registry duplicated between repos.", ProcessStepKind.Decision, true, false, true, "Ownership map and current code capabilities.", "Concrete merge direction and rejected alternatives.", "Decision memo and migration implications.", "Canonical steward and architect decide together with explicit traceability.", "Raise a convergence meeting if the preferred option would force duplicated provider credentials, duplicated agent profile data, or incompatible staffing semantics.", 12, 800, 300, "propose-ownership-map",
                    [
                        new("canonical-steward", ProcessResponsibilityKind.Responsible, 0, "Steward leads the canonical decision."),
                        new("solution-architect", ProcessResponsibilityKind.Approver, 1, "Architect approves technical viability."),
                        new("crmhr-owner", ProcessResponsibilityKind.Reviewer, 2, "Domain owner checks staffing/identity impact.")
                    ],
                    []),
                new("architecture-convergence-meeting", "Hold architecture convergence meeting", "Resolve unresolved ownership or migration conflicts in a human meeting.", "This meeting is mandatory when text-only review cannot close the gap between repos or domain owners.", ProcessStepKind.Review, true, false, true, "Decision memo with unresolved conflicts called out explicitly.", "Resolved conflict list and committed migration direction.", "Meeting transcript and decision summary.", "Delivery manager convenes; architect, canonical steward, and domain owner participate.", "If the meeting still cannot name one canonical owner, block the bundle and create a dedicated follow-up repair bundle.", 4, 1140, 300, "reconcile-provider-and-agent-models",
                    [
                        new("integration-engineer", ProcessResponsibilityKind.Responsible, 0, "Bridge engineer brings concrete implementation constraints."),
                        new("solution-architect", ProcessResponsibilityKind.Reviewer, 1, "Architect validates the chosen path."),
                        new("canonical-steward", ProcessResponsibilityKind.Approver, 2, "Steward signs off on the canonical owner.")
                    ],
                    [
                        new(ProcessArtifactKind.Transcript, "Architecture convergence transcript", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 365, "Reusable for future architecture audits and post-implementation learning.", "Transcript must preserve the unresolved question, the considered options, and the final owner decision.")
                    ]),
                new("approve-boundary-plan", "Approve boundary and migration plan", "Freeze the technical plan so slice-level implementation can proceed without relitigating ownership.", "Approval binds the integration direction and the explicit backlog of cleanup or migration work.", ProcessStepKind.Approval, false, true, true, "Converged ownership decision and migration backlog.", "Approved boundary plan ready for execution slices.", "Approval record and backlog reference.", "Architect approves after steward review; governance reviewer may block unsafe rollback posture.", "If rollback or compatibility is unclear, block approval and capture the missing evidence explicitly.", 3, 1480, 300, "architecture-convergence-meeting",
                    [
                        new("solution-architect", ProcessResponsibilityKind.Approver, 0, "Architect approves the final technical plan."),
                        new("governance-reviewer", ProcessResponsibilityKind.Reviewer, 1, "Governance reviewer checks rollback and safety.")
                    ],
                    []),
                new("publish-convergence-backlog", "Publish convergence backlog for execution", "Turn the approved plan into concrete execution slices and deferred cleanup items.", "This delivery output becomes the bridge into local and complex implementation lanes.", ProcessStepKind.Delivery, false, false, false, "Approved boundary plan.", "Execution backlog and deferred cleanup map.", "Backlog cards and route bindings.", "Integration engineer publishes; delivery manager confirms slices are independently verifiable.", "If a slice still spans incompatible ownership decisions, return it to convergence instead of sending it to implementation.", 4, 1820, 300, "approve-boundary-plan",
                    [
                        new("integration-engineer", ProcessResponsibilityKind.Responsible, 0, "Bridge engineer materializes the backlog."),
                        new("delivery-manager", ProcessResponsibilityKind.Reviewer, 1, "Delivery manager checks slice granularity.")
                    ],
                    [])
            ],
            [
                new(0, ProcessStepRunStatus.Completed, "Inventory completed across CanDoItAll, CRM/HR, and AgentFramework with duplicated provider-profile and agent-registry ownership called out.", "scenario-seeder/canonical"),
                new(1, ProcessStepRunStatus.Completed, "A draft ownership matrix now distinguishes canonical owners from projections and adapters.", "scenario-seeder/canonical"),
                new(2, ProcessStepRunStatus.Completed, "Provider-profile and agent-identity merge options were reduced to one preferred path plus one rejected fallback.", "scenario-seeder/canonical"),
                new(3, ProcessStepRunStatus.Completed, "Architecture convergence meeting resolved that provider-profile creation must converge into a single canonical owner before broad integration work.", "scenario-seeder/canonical"),
                new(4, ProcessStepRunStatus.WaitingApproval, "Waiting for final governance confirmation on rollback posture and migration sequencing.", "scenario-seeder/canonical")
            ],
            [
                new("propose-ownership-map", ProcessArtifactKind.Decision, "Cross-repo ownership matrix", ProcessArtifactTrustStatus.Approved, ProcessSensitivityLevel.Internal, "Prepared from live repository review and service-boundary analysis in the simulation profile.", "Reusable for future bundle validation and implementation reviews until a new approved architecture supersedes it.", "Reviewed by the architect and canonical steward during scenario seeding."),
                new("architecture-convergence-meeting", ProcessArtifactKind.Transcript, "Architecture convergence transcript", ProcessArtifactTrustStatus.Approved, ProcessSensitivityLevel.Internal, "Human convergence record summarizing the dispute over provider-profile and agent identity ownership.", "Reusable for the follow-up migration backlog and future design audits.", "Approved human meeting record captured by the simulation.")
            ]);
    }

    private static AgentFrameworkIntegrationSimulationSeeder.ProcessSpec BuildLocalSliceProcess()
    {
        return new AgentFrameworkIntegrationSimulationSeeder.ProcessSpec(
            "AgentFramework integration / local-LLM-safe execution slices",
            "Local-LLM-safe execution slices / working run",
            ProcessOperatingMode.AssistedExecution,
            ProcessCriticality.High,
            ProcessAutonomyLevel.Guarded,
            "Deliver bounded implementation slices that a local model can assist with safely, while forcing human review before integration.",
            "Keep small work local, verifiable, and cheap without allowing slice work to quietly absorb larger architectural risk.",
            "The process formalizes how code is split, what the local model may touch, and when the work must escalate to a human or OpenAI-assisted lane.",
            "Only bounded, reversible, repository-local slices may enter the local AI lane. Cross-boundary, provider, or secret-related work must escalate immediately.",
            "Local AI may never decide canonical ownership, send external context, or merge risky changes without human review.",
            "Assisted autonomy is allowed only inside explicit slice boundaries and only with human integration review.",
            "Simulation keeps the slice run active so the user can inspect a partially advanced execution lane in the process UI.",
            [
                new("slice-coordinator", "Slice coordinator", "Split the plan into independently verifiable slices with explicit guardrails. Cannot waive review or validation.", "Human coordinator role that protects slice boundaries.", "person", ProjectPartyAssignmentRole.Manager, false, true, 25, "Keeps slice size compatible with local execution and verification.", "AFINT-DELIVERY-MGR", "Bound to the delivery manager for slice decomposition."),
                new("solution-architect", "Solution architect", "Review whether a slice is still bounded or has drifted into a canonical or provider-governance issue. Cannot silently widen the slice.", "Human architecture guardrail role for local execution.", "person", ProjectPartyAssignmentRole.TechnicalContact, false, true, 20, "Prevents bounded slice work from mutating into hidden architecture work.", "AFINT-ARCH", "Bound to the solution architect for escalation checks."),
                new("local-slice-worker", "Local slice worker", "Implement bounded code and analysis tasks inside the approved local lane. Cannot handle secrets, provider credentials, or canonical decisions.", "Local AI role for cheap, frequent, bounded work.", "local-llm-agent", ProjectPartyAssignmentRole.AiAgent, false, true, 60, "Acts only inside the guardrails defined by the slice coordinator.", "AFINT-LOCAL-LLM", "Bound to the local slice worker AI agent."),
                new("integration-engineer", "Integration engineer", "Review, refine, and integrate the bounded slice. Cannot treat the AI draft as self-validating proof.", "Human implementation role for actual code ownership.", "person", ProjectPartyAssignmentRole.TeamMember, false, true, 60, "Owns the human merge and follow-up adjustments.", "AFINT-INTEGRATION", "Bound to the integration engineer."),
                new("workbench-engineer", "Workbench engineer", "Own UI-specific slices, reusable component extraction, and canvas parity refinements. Cannot bypass shared component rules.", "Human UI engineer for shared-component-first implementations.", "person", ProjectPartyAssignmentRole.TeamMember, false, true, 45, "Ensures UI slices remain component-based and reusable in process floating windows.", "AFINT-WORKBENCH", "Bound to the workbench UX engineer."),
                new("qa-validator", "QA validator", "Verify that the slice has proof, test coverage, and browser evidence. Cannot sign off architecture or risk acceptance.", "Human validation role for slice readiness.", "person", ProjectPartyAssignmentRole.TeamMember, true, true, 35, "Prevents local AI work from bypassing actual proof.", "AFINT-QA", "Bound to the QA lead.")
            ],
            [
                new("split-slices", "Split implementation into bounded slices", "Choose slices small enough for local assistance and independent validation.", "Each slice must name its affected modules, excluded concerns, validation proof, and escalation triggers.", ProcessStepKind.Start, false, false, true, "Approved convergence backlog.", "Ranked slice backlog with explicit local-lane guardrails.", "Slice brief and exclusion list.", "Slice coordinator decides initial scope; architect may reject slices that hide cross-boundary work.", "If a slice touches more than one canonical owner or any external provider contract, move it to the complex lane immediately.", 5, 140, 480, null,
                    [
                        new("slice-coordinator", ProcessResponsibilityKind.Responsible, 0, "Coordinator owns slice granularity."),
                        new("integration-engineer", ProcessResponsibilityKind.Reviewer, 1, "Engineer confirms the slice is real.")
                    ],
                    [
                        new(ProcessArtifactKind.Brief, "Bounded slice brief", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 180, "Reusable until the slice is completed or escalated.", "Brief must state what the local model may do, what it may not do, and which proof closes the slice.")
                    ]),
                new("prepare-local-lane", "Prepare local-lane prompt and guardrails", "Create the exact bounded context the local model may use and name what is forbidden.", "Sanitize the slice context and keep it strictly local to the workspace.", ProcessStepKind.Work, false, false, false, "Bounded slice brief.", "Local-lane work brief and excluded-concern checklist.", "Prompt, checklist, and expected proof definition.", "Slice coordinator and workbench engineer define the lane; local AI cannot define its own scope.", "Raise a review if the prompt depends on ambiguous architecture or if the excluded-concern checklist is incomplete.", 4, 440, 480, "split-slices",
                    [
                        new("slice-coordinator", ProcessResponsibilityKind.Responsible, 0, "Coordinator authors the guardrails."),
                        new("workbench-engineer", ProcessResponsibilityKind.Reviewer, 1, "UI engineer checks component and UX constraints.")
                    ],
                    []),
                new("execute-local-slice", "Execute bounded local slice", "Use the local model only inside the pre-approved slice.", "The local model may draft code, tests, and refactors, but all work stays bounded and reversible.", ProcessStepKind.Work, true, false, false, "Local-lane work brief and excluded-concern checklist.", "Draft code changes and proof candidates inside slice boundaries.", "Patch set, notes, and self-reported uncertainty.", "Local AI may act only inside the prompt boundaries; the integration engineer remains accountable for final code.", "If uncertainty touches provider usage, source-of-truth ownership, or hidden UI duplication, stop and escalate.", 8, 760, 480, "prepare-local-lane",
                    [
                        new("local-slice-worker", ProcessResponsibilityKind.Responsible, 0, "Local AI executes the bounded slice."),
                        new("integration-engineer", ProcessResponsibilityKind.Backup, 1, "Engineer remains accountable if escalation is needed.")
                    ],
                    []),
                new("human-review-and-integration", "Review and integrate the slice", "Human review checks correctness, architecture fit, and code quality before the slice is accepted.", "This is where the human engineer rejects invalid automation and adjusts the patch as needed.", ProcessStepKind.Review, false, false, true, "Draft slice output and bounded brief.", "Integrated or rejected slice with explicit review rationale.", "Review note and integration summary.", "Integration engineer decides whether the slice remains valid. AI cannot approve itself.", "Raise a meeting if the review discovers hidden cross-boundary impact or reusable UI/form duplication.", 6, 1080, 480, "execute-local-slice",
                    [
                        new("integration-engineer", ProcessResponsibilityKind.Responsible, 0, "Engineer owns review and merge."),
                        new("workbench-engineer", ProcessResponsibilityKind.Reviewer, 1, "UI engineer joins when the slice affects shared process forms.")
                    ],
                    [
                        new(ProcessArtifactKind.Evidence, "Human slice review note", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 180, "Reusable for later validation and learning until the slice is superseded.", "Review note must call out whether the local lane stayed within approved bounds.")
                    ]),
                new("tests-and-browser-proof", "Run tests and browser proof", "Close the slice only after integration tests and browser checks succeed.", "Proof must include the relevant service behavior plus any large-screen UI or canvas change.", ProcessStepKind.Delivery, false, false, true, "Integrated slice candidate and proof expectations.", "Validated slice or explicit escalation to the complex lane.", "Test results, browser evidence, and remaining risk note.", "QA validator decides whether proof is sufficient for slice closure.", "If tests or browser proof reveal broader coupling than expected, escalate the slice instead of widening it silently.", 6, 1420, 480, "human-review-and-integration",
                    [
                        new("qa-validator", ProcessResponsibilityKind.Responsible, 0, "QA owns proof sufficiency."),
                        new("integration-engineer", ProcessResponsibilityKind.Reviewer, 1, "Engineer reviews failures and follow-up.")
                    ],
                    []),
                new("escalate-overflow", "Escalate overflow slice to complex lane", "Move work out of the local lane when proof or scope shows the slice was not actually local-safe.", "Escalation is success when it prevents unsafe silent widening.", ProcessStepKind.End, true, true, true, "Failed or widened local slice.", "Escalation package for the OpenAI-assisted complex lane.", "Escalation note and context package.", "Slice coordinator and architect decide the next lane.", "If escalation still lacks a clear next owner, raise a human review meeting before continuing.", 2, 1740, 480, "tests-and-browser-proof",
                    [
                        new("slice-coordinator", ProcessResponsibilityKind.Responsible, 0, "Coordinator closes the local lane and hands off."),
                        new("solution-architect", ProcessResponsibilityKind.Reviewer, 1, "Architect confirms the new lane.")
                    ],
                    [])
            ],
            [
                new(0, ProcessStepRunStatus.Completed, "The backlog was split into small slices including reusable process-form extraction, project-binding cleanup, and validation proof work.", "scenario-seeder/local-slice"),
                new(1, ProcessStepRunStatus.Completed, "Local-lane guardrails were prepared with explicit exclusions for provider credentials, canonical ownership, and irreversible runtime decisions.", "scenario-seeder/local-slice"),
                new(2, ProcessStepRunStatus.Completed, "The local slice worker completed a bounded draft for reusable process form extraction and nearby validation hooks.", "scenario-seeder/local-slice"),
                new(3, ProcessStepRunStatus.Completed, "Human review accepted the bounded slice and rejected any attempt to widen it into provider-governance concerns.", "scenario-seeder/local-slice"),
                new(4, ProcessStepRunStatus.InProgress, "Validation is running with integration tests and large-screen browser proof for the extracted form slice.", "scenario-seeder/local-slice")
            ],
            [
                new("split-slices", ProcessArtifactKind.Brief, "Bounded slice brief", ProcessArtifactTrustStatus.Approved, ProcessSensitivityLevel.Internal, "Prepared from the approved convergence backlog and current implementation hotspots.", "Reusable until the slice closes or moves to the complex lane.", "Human-approved slice brief defining allowed and prohibited work."),
                new("human-review-and-integration", ProcessArtifactKind.Evidence, "Human slice review note", ProcessArtifactTrustStatus.Approved, ProcessSensitivityLevel.Internal, "Review note captured after the bounded form-component extraction draft was integrated.", "Reusable for follow-up validation and operational learning.", "Confirms the local lane stayed inside shared-component and scope guardrails.")
            ]);
    }
}
