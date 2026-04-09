using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.ScenarioSeeder;

internal static partial class ProcessCatalog
{
    private static AgentFrameworkIntegrationSimulationSeeder.ProcessSpec BuildOpenAiComplexLaneProcess()
    {
        return new AgentFrameworkIntegrationSimulationSeeder.ProcessSpec(
            "AgentFramework integration / OpenAI-assisted complex integration lane",
            "OpenAI-assisted complex integration lane / blocked run",
            ProcessOperatingMode.GovernedLive,
            ProcessCriticality.MissionCritical,
            ProcessAutonomyLevel.Guarded,
            "Handle the hard cross-boundary questions that exceed the local lane by using sanitized OpenAI analysis under explicit human security, cost, and approval control.",
            "Use external-model depth only where necessary and never hide provider or governance risk behind generic automation.",
            "The lane begins only after local work declares complexity overflow and prepares a sanitized context package.",
            "OpenAI usage is allowed only on sanitized material, with explicit budget and security review before follow-up execution.",
            "The external model cannot receive secrets, own release decisions, or change canonical ownership without human approval.",
            "Guarded autonomy only: the external model may analyze and propose, but humans decide and own execution.",
            "Simulation leaves this run blocked on cost and security review so the project graph can show a realistic escalation choke point.",
            [
                new("solution-architect", "Solution architect", "Frame the complex problem and judge whether the external lane is actually required. Cannot self-approve release or security exceptions.", "Human architecture owner for the complex lane.", "person", ProjectPartyAssignmentRole.TechnicalContact, false, true, 35, "Controls whether the problem is legitimate for the complex lane.", "AFINT-ARCH", "Bound to the solution architect."),
                new("openai-analysis-agent", "OpenAI analysis agent", "Analyze sanitized context, compare options, and expose hidden tradeoffs. Cannot receive secrets, decide ownership, or execute risky changes.", "External-model role used only after sanitization and human lane approval.", "openai-agent", ProjectPartyAssignmentRole.AiAgent, true, true, 20, "Provides depth on complex tradeoffs without becoming the final authority.", "AFINT-OPENAI", "Bound to the OpenAI deep analysis agent."),
                new("security-reviewer", "Security reviewer", "Approve sanitization, data boundaries, and risky provider usage. Cannot set business priority alone.", "Human security gate for the external lane.", "person", ProjectPartyAssignmentRole.Reviewer, true, true, 20, "Protects against leaking secrets or regulated context into the external lane.", "AFINT-SECURITY", "Bound to the security and governance reviewer."),
                new("cost-steward", "Cost steward", "Check token budgets, recurring spend, and vendor constraints. Cannot approve architecture or release alone.", "Human budget and vendor control role.", "person", ProjectPartyAssignmentRole.BillingContact, true, true, 10, "Protects against unbounded or hidden provider spend.", "AFINT-COST", "Bound to the cost and vendor steward."),
                new("human-approver", "Human approval board", "Decide whether the external-model recommendation becomes a real implementation plan. Cannot skip missing security or cost evidence.", "Human-only decision role for risky escalations.", "person", ProjectPartyAssignmentRole.Stakeholder, true, true, 10, "Ensures final accountability stays human and explicit.", "AFINT-SPONSOR", "Bound to the sponsor for final human approval.")
            ],
            [
                new("declare-overflow", "Declare complexity overflow from the local lane", "Explain why the problem exceeded bounded local execution.", "Overflow must be justified by cross-boundary risk, provider policy, or unresolved ownership complexity.", ProcessStepKind.Start, false, false, true, "Escalation from local-slice or canonical process.", "Accepted complex-lane intake or rejection back to a smaller lane.", "Overflow rationale and target question.", "Architect accepts only real complexity overflow, not lazy scope widening.", "If the issue can still be split locally, reject the escalation and send it back with a narrower question.", 3, 140, 660, null,
                    [
                        new("solution-architect", ProcessResponsibilityKind.Responsible, 0, "Architect decides if overflow is real.")
                    ],
                    []),
                new("prepare-sanitized-context", "Prepare sanitized context package", "Strip secrets, customer data, and unnecessary noise before the external model sees anything.", "The package must contain only what the external lane needs to reason effectively.", ProcessStepKind.Work, false, false, true, "Accepted overflow intake and raw supporting material.", "Sanitized packet ready for OpenAI analysis.", "Sanitized packet and excluded-data statement.", "Architect prepares; security reviewer must approve the sanitization boundary.", "If the required context cannot be sanitized without losing the actual question, raise a human-only design meeting instead of calling the external model.", 6, 460, 660, "declare-overflow",
                    [
                        new("solution-architect", ProcessResponsibilityKind.Responsible, 0, "Architect frames the question and context."),
                        new("security-reviewer", ProcessResponsibilityKind.Reviewer, 1, "Security reviewer validates sanitization.")
                    ],
                    [
                        new(ProcessArtifactKind.Prompt, "Sanitized architecture packet", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 90, "Reusable for the external analysis run and immediate follow-up review only.", "Packet must prove that secrets, credentials, and customer identifiers were removed or replaced.")
                    ]),
                new("run-openai-analysis", "Run OpenAI-assisted analysis", "Generate and compare options on the sanitized packet without treating the answer as final truth.", "The output must remain a proposal subject to human challenge.", ProcessStepKind.Work, true, false, true, "Sanitized architecture packet.", "Option set with tradeoffs, risks, and recommended next step.", "Analysis note and alternative comparison.", "OpenAI analysis agent proposes; architect remains responsible for interpretation.", "If the output recommends changing autonomy, provider governance, or canonical ownership, schedule a human decision board.", 5, 780, 660, "prepare-sanitized-context",
                    [
                        new("openai-analysis-agent", ProcessResponsibilityKind.Responsible, 0, "OpenAI agent produces the comparative analysis."),
                        new("solution-architect", ProcessResponsibilityKind.Reviewer, 1, "Architect interprets and challenges the output.")
                    ],
                    []),
                new("security-and-budget-review", "Review security and budget posture", "Approve or reject the external-lane proposal based on risk, spend, and evidence quality.", "No follow-up execution may start before both security and cost posture are acceptable.", ProcessStepKind.Review, false, false, true, "OpenAI analysis note and sanitized context package.", "Accepted posture or explicit blocker list.", "Security and cost review memo.", "Security reviewer and cost steward each have veto power on unsafe or unbounded proposals.", "Raise a provider-governance board meeting if the proposal needs more model access, more spend, or new operational trust assumptions.", 6, 1120, 660, "run-openai-analysis",
                    [
                        new("security-reviewer", ProcessResponsibilityKind.Responsible, 0, "Security review is mandatory."),
                        new("cost-steward", ProcessResponsibilityKind.Reviewer, 1, "Cost steward validates spend posture.")
                    ],
                    [
                        new(ProcessArtifactKind.Decision, "Provider cost and risk review", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 180, "Reusable for the decision board and later audit of provider usage.", "Review must state token budget, redaction posture, and any residual risk that still needs human approval.")
                    ]),
                new("human-decision-board", "Run human decision board", "Turn the proposal and reviews into an explicit approve, reject, or defer decision.", "The board owns the human accountability that the external lane can never replace.", ProcessStepKind.Approval, false, true, true, "OpenAI analysis plus security and cost review.", "Approved follow-up plan or explicit deferral.", "Decision record and action list.", "Sponsor approves only after security and budget review are complete.", "If board members still disagree on ownership or autonomy, send the question back to convergence instead of forcing execution.", 3, 1460, 660, "security-and-budget-review",
                    [
                        new("human-approver", ProcessResponsibilityKind.Approver, 0, "Sponsor or board chair makes the final human decision."),
                        new("solution-architect", ProcessResponsibilityKind.Reviewer, 1, "Architect confirms technical interpretation.")
                    ],
                    []),
                new("prepare-follow-up-execution", "Prepare follow-up execution package", "Package the approved decision back into a bounded implementation lane.", "The external lane ends with a human-owned execution plan, not with direct code changes from the model.", ProcessStepKind.End, false, false, false, "Approved board decision.", "Execution package for downstream implementation.", "Follow-up package and guarded next steps.", "Architect and delivery manager convert the decision into bounded human-owned work.", "If no bounded next step exists, capture the gap and stop instead of smearing the decision into undefined work.", 3, 1780, 660, "human-decision-board",
                    [
                        new("solution-architect", ProcessResponsibilityKind.Responsible, 0, "Architect owns the technical next-step package."),
                        new("human-approver", ProcessResponsibilityKind.Reviewer, 1, "Human approver checks whether the follow-up package still matches the approved decision.")
                    ],
                    [])
            ],
            [
                new(0, ProcessStepRunStatus.Completed, "The provider-profile merge question was formally escalated out of the local lane because it combines canonical ownership, provider governance, and rollout risk.", "scenario-seeder/openai"),
                new(1, ProcessStepRunStatus.Completed, "A sanitized architecture packet was prepared without secrets, credentials, or customer-specific identifiers.", "scenario-seeder/openai"),
                new(2, ProcessStepRunStatus.Completed, "OpenAI-assisted analysis produced three tradeoff options and highlighted the hidden risk of duplicating provider profiles across repos.", "scenario-seeder/openai"),
                new(3, ProcessStepRunStatus.Blocked, "Blocked until security confirms redaction sufficiency and cost stewardship approves the expected recurring provider spend.", "scenario-seeder/openai")
            ],
            [
                new("prepare-sanitized-context", ProcessArtifactKind.Prompt, "Sanitized architecture packet", ProcessArtifactTrustStatus.Approved, ProcessSensitivityLevel.Internal, "Prepared from repository structure, process state, and architecture summaries after removing secrets and customer-specific identifiers.", "Reusable for this complex-lane decision and the immediate governance follow-up only.", "Security reviewer accepted the current redaction posture during scenario seeding."),
                new("security-and-budget-review", ProcessArtifactKind.Decision, "Provider cost and risk review", ProcessArtifactTrustStatus.ReviewRequired, ProcessSensitivityLevel.Internal, "Draft review assembled from the external analysis output, cost assumptions, and redaction notes.", "Reusable only for the pending human decision board and later audit of provider usage.", "Review remains blocked because the final spend ceiling and residual risk acceptance are still pending.")
            ]);
    }

    private static AgentFrameworkIntegrationSimulationSeeder.ProcessSpec BuildValidationAndLearningProcess()
    {
        return new AgentFrameworkIntegrationSimulationSeeder.ProcessSpec(
            "AgentFramework integration / validation, rollout, and learning loop",
            "Validation, rollout, and learning loop / active run",
            ProcessOperatingMode.AssistedExecution,
            ProcessCriticality.High,
            ProcessAutonomyLevel.Assisted,
            "Validate implementation and UX outcomes, decide rollout readiness, and capture the friction that must become new bundles or architecture repairs.",
            "Prevent hidden UI, data, or process drift from surviving past the simulation into real implementation.",
            "The process ties tests, browser proof, project-graph usability review, and follow-up bundle generation into one explicit learning loop.",
            "No release or bundle closure is valid without repeatable proof and explicit learning capture.",
            "Validation evidence must remain reviewable; no participant may silently treat a manual check as sufficient proof.",
            "Assisted automation may collect evidence, but humans still judge proof sufficiency and release readiness.",
            "Simulation keeps this run active with validation in progress so the seeded project looks like a real mid-flight program.",
            [
                new("qa-lead", "QA lead", "Own proof completeness, evidence quality, and repeatability. Cannot redefine architecture or risk appetite.", "Human validation role that can block closure when proof is weak.", "person", ProjectPartyAssignmentRole.TeamMember, true, true, 35, "Protects the simulation from optimistic closure without evidence.", "AFINT-QA", "Bound to the QA lead."),
                new("workbench-engineer", "Workbench engineer", "Validate process canvas and project structure parity, compactness, and reusable form composition. Cannot waive proof gaps.", "Human UI owner for process and project-graph experience.", "person", ProjectPartyAssignmentRole.TeamMember, false, true, 45, "Ensures the UX matches the intended operating model on large screens.", "AFINT-WORKBENCH", "Bound to the workbench UX engineer."),
                new("release-manager", "Release manager", "Decide whether the simulation evidence is sufficient for the next real bundle. Cannot bypass blocked architecture or security issues.", "Human closure role for moving from simulation to real implementation.", "person", ProjectPartyAssignmentRole.Manager, true, true, 25, "Turns evidence into a go, no-go, or reopen decision.", "AFINT-DELIVERY-MGR", "Bound to the delivery manager."),
                new("security-reviewer", "Security reviewer", "Confirm that provider usage, evidence handling, and process routes did not bypass governance.", "Human governance role for validation closure.", "person", ProjectPartyAssignmentRole.Reviewer, true, true, 15, "Checks that validation did not skip the hard governance questions.", "AFINT-SECURITY", "Bound to the security reviewer."),
                new("improvement-owner", "Improvement owner", "Capture friction and convert it into concrete bundle or architecture follow-up.", "Human learning role that ensures problems become trackable work instead of chat-only memory.", "person", ProjectPartyAssignmentRole.Reviewer, false, true, 20, "Protects the learning loop from getting lost after validation.", "AFINT-CANONICAL", "Bound to the canonical steward for follow-up bundle discipline.")
            ],
            [
                new("define-validation-matrix", "Define validation matrix and proof plan", "List what must be proven in services, UI, process state, and project-graph usability.", "Matrix should explicitly include process-to-graph binding, large-screen compactness, and follow-up gap capture.", ProcessStepKind.Start, false, false, true, "Current process runs, graph structure, and implementation slices.", "Validation matrix with explicit evidence owners.", "Matrix and proof plan.", "QA lead owns the matrix; workbench engineer contributes UX-specific proof points.", "Raise a release readiness meeting if any critical proof area has no accountable owner.", 4, 140, 840, null,
                    [
                        new("qa-lead", ProcessResponsibilityKind.Responsible, 0, "QA owns the proof matrix."),
                        new("workbench-engineer", ProcessResponsibilityKind.Reviewer, 1, "UI engineer adds large-screen and parity proof.")
                    ],
                    [
                        new(ProcessArtifactKind.Checklist, "Validation matrix", ProcessArtifactTrustRequirement.HumanApproved, ProcessSensitivityLevel.Internal, 180, "Reusable until the simulated bundle scope changes materially.", "Matrix must name service, UI, process, graph, and learning checks plus their owners.")
                    ]),
                new("run-proof", "Run integration, browser, and graph proof", "Execute the proof matrix across services, process runs, and the large-screen UI.", "Proof must include process pages, project structure routes, and the realism of the seeded scenario.", ProcessStepKind.Work, false, false, true, "Validation matrix and current seeded scenario.", "Collected proof package and unresolved gaps.", "Test results, screenshots, and route checks.", "QA lead and workbench engineer coordinate proof collection.", "If the project graph cannot open the right process context or the UI diverges from the intended workflow, block closure and capture a follow-up bundle.", 8, 500, 840, "define-validation-matrix",
                    [
                        new("qa-lead", ProcessResponsibilityKind.Responsible, 0, "QA collects the proof package."),
                        new("workbench-engineer", ProcessResponsibilityKind.Reviewer, 1, "UI engineer verifies the workflow parity.")
                    ],
                    []),
                new("review-friction", "Review friction and missing capabilities", "Convert what was painful in the simulation into explicit improvement work.", "This step is mandatory even if the technical proof passes.", ProcessStepKind.Review, false, false, true, "Proof package and observed friction notes.", "Prioritized gap list and candidate follow-up bundles.", "Gap review and proposed follow-up list.", "Improvement owner captures the problems; release manager checks impact on the next real bundle.", "If a gap threatens maintainability or safe execution, do not defer it silently. Create a follow-up bundle or reopen architecture work.", 5, 860, 840, "run-proof",
                    [
                        new("improvement-owner", ProcessResponsibilityKind.Responsible, 0, "Improvement owner records the learning."),
                        new("release-manager", ProcessResponsibilityKind.Reviewer, 1, "Release manager decides impact on next work.")
                    ],
                    []),
                new("decide-rollout-readiness", "Decide rollout readiness for the next real bundle", "Translate proof and friction into a go, no-go, or reopen decision.", "The decision is about whether the next bundle can safely build on the current implementation and simulation results.", ProcessStepKind.Approval, false, true, true, "Proof package and prioritized gap list.", "Explicit go/no-go/reopen decision.", "Decision record and rationale.", "Release manager approves only with QA and security concurrence.", "If critical architecture or process-binding gaps remain, reopen architecture work instead of forcing a green status.", 3, 1220, 840, "review-friction",
                    [
                        new("release-manager", ProcessResponsibilityKind.Approver, 0, "Release manager makes the final closure call."),
                        new("security-reviewer", ProcessResponsibilityKind.Reviewer, 1, "Security reviewer confirms governance sufficiency."),
                        new("qa-lead", ProcessResponsibilityKind.Reviewer, 2, "QA confirms proof sufficiency.")
                    ],
                    []),
                new("publish-follow-up-work", "Publish follow-up bundles and tuning backlog", "Persist the learning into future work so it survives beyond the current chat.", "The simulation is only successful if it leaves the next steps more explicit than before.", ProcessStepKind.Delivery, false, false, true, "Release readiness decision and prioritized gap list.", "Published follow-up work with accountable owners.", "Bundle proposals and tuning backlog.", "Improvement owner publishes; release manager confirms ownership.", "If no owner exists for an important gap, do not mark the learning loop complete.", 4, 1580, 840, "decide-rollout-readiness",
                    [
                        new("improvement-owner", ProcessResponsibilityKind.Responsible, 0, "Improvement owner materializes the backlog."),
                        new("release-manager", ProcessResponsibilityKind.Reviewer, 1, "Release manager confirms ownership and sequencing.")
                    ],
                    [])
            ],
            [
                new(0, ProcessStepRunStatus.Completed, "Validation matrix now covers service behavior, process routes, project-graph usability, and large-screen process/workbench UX.", "scenario-seeder/validation"),
                new(1, ProcessStepRunStatus.InProgress, "Integration and browser proof are currently running against the seeded simulation project.", "scenario-seeder/validation")
            ],
            [
                new("define-validation-matrix", ProcessArtifactKind.Checklist, "Validation matrix", ProcessArtifactTrustStatus.Approved, ProcessSensitivityLevel.Internal, "Prepared specifically for the AgentFramework integration simulation and its process/graph bindings.", "Reusable while the simulation scope stays materially the same.", "QA-approved proof matrix covering service, UI, graph, and learning checks.")
            ]);
    }
}
