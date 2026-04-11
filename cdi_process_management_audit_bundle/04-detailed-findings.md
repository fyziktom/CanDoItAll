# Detailed findings

## FND-01 — No explicit transition graph or branch semantics exist in the persisted definition model.

- **Severity:** Critical
- **Area:** Canonical graph semantics
- **Linked features:** PRM-F02, PRM-F05, PRM-F09, PRM-F24
- **Codex phase:** Phase 1

**Why this matters**  
Without canonical transitions, the authored process cannot safely control runtime branching, validation, or handoff intent.

**Evidence**  
ProcessStepDefinition stores only DependsOnStepId; canvas definition links are dependency-only; runtime activates the next step by sequence. See ProcessDefinitionModels.cs:226 and ProcessCanvasSurfaceFactory.cs:14-19 and ProcessesService.Runtime.cs:238-248.

**Recommended fix direction**  
Introduce ProcessTransition, branch rule, and graph validation services, then make runtime activation and canvas rendering depend on that graph.

## FND-02 — Runtime progression is sequence-driven and does not execute the authored dependency graph.

- **Severity:** Critical
- **Area:** Runtime orchestration
- **Linked features:** PRM-F05, PRM-F07, PRM-F24
- **Codex phase:** Phase 2

**Why this matters**  
This makes published process topology non-authoritative and undermines future agent execution correctness.

**Evidence**  
ProcessesService.Runtime.cs activates the next step by Sequence > current.Sequence and ignores richer graph semantics.

**Recommended fix direction**  
Refactor runtime orchestration into a deterministic state machine driven by explicit transitions, approvals, and handoff outcomes.

## FND-03 — There are work briefs but no first-class baton handoff or triage routing records.

- **Severity:** Critical
- **Area:** Handoffs and baton flow
- **Linked features:** PRM-F05, PRM-F22, PRM-F24
- **Codex phase:** Phase 2

**Why this matters**  
Agent and human collaboration cannot be audited or supervised safely without explicit responsibility transfer events.

**Evidence**  
ProcessWorkBrief is persisted, but no ProcessRunHandoff or TriageDecision-like entity exists. Work briefs are created at run start only. See ProcessRuntimeModels.cs:202-222 and ProcessesService.Runtime.cs:145-159.

**Recommended fix direction**  
Add ProcessRunHandoff, ProcessWorkBriefSnapshot, and TriageDecision entities and journal every baton transfer or governed routing decision.

## FND-04 — Approval and escalation policy is modeled only through booleans, statuses, and generic decision rows.

- **Severity:** Critical
- **Area:** Governance and approvals
- **Linked features:** PRM-F06, PRM-F18, PRM-F23
- **Codex phase:** Phase 2

**Why this matters**  
Self-approval prevention, supervisory escalation, human intervention, and agent permission narrowing cannot be implemented safely on top of implicit policy.

**Evidence**  
ProcessRoleRequirement.RequiresExplicitApproval and ProcessStepDefinition.RequiresApproval exist, but there are no approval route or escalation target entities. TransitionStepAsync writes generic decision records only.

**Recommended fix direction**  
Create explicit approval and escalation policy aggregates with targets, conflict rules, timeout rules, and override evidence.

## FND-05 — The future executor seam is only a noop registry bridge; there are no hard runtime guardrails beyond prompt wording.

- **Severity:** Critical
- **Area:** Agent governance
- **Linked features:** PRM-F13, PRM-F23
- **Codex phase:** Phase 3

**Why this matters**  
Agents cannot be governed safely without capability checks, permission policy, external-call approval, evaluation records, and durable correlations.

**Evidence**  
ProcessesModuleServiceCollectionExtensions registers NoopProcessExecutorRegistryBridge and ListExecutorOptionsAsync delegates to it. No permission or evaluation service exists in the process module.

**Recommended fix direction**  
Implement bridge contracts for capability, permission, evaluation, and external executor correlation, still behind an adapter seam.

## FND-06 — Escalations, human decisions, and interventions are not projected into project structure.

- **Severity:** Critical
- **Area:** Project structure propagation
- **Linked features:** PRM-F10, PRM-F22, PRM-F24
- **Codex phase:** Phase 4

**Why this matters**  
The user’s intended operational control path through project structure cannot work until blocked/approval-needed states create actionable nodes.

**Evidence**  
ProjectStructureAssemblyService projects only ProcessDefinition and ProcessRun nodes, while ProjectObjectContracts already supports Decision and WorkItem nodes.

**Recommended fix direction**  
Upsert Decision and WorkItem projection nodes for blocked, waiting-approval, refused, or failed steps that need human action.

## FND-07 — Role and agent templates are process-local static templates, creating a shadow registry risk.

- **Severity:** Critical
- **Area:** Canonicality / source of truth
- **Linked features:** PRM-F03, PRM-F16, PRM-F23
- **Codex phase:** Phase 3

**Why this matters**  
The bundle explicitly wanted CRM-HR and Workspace to stay canonical for durable identities and templates.

**Evidence**  
ProcessCanvasTemplateCatalog defines static role templates and snapshots them into process rows; no CRM-HR-governed template lifecycle is referenced.

**Recommended fix direction**  
Move durable role/agent templates to CRM-HR-owned entities and keep process-side references versioned and read-only.

## FND-08 — Publish validation is too shallow for a module that will govern agents and escalations.

- **Severity:** High
- **Area:** Publication integrity
- **Linked features:** PRM-F02, PRM-F06, PRM-F17
- **Codex phase:** Phase 1

**Why this matters**  
Missing validation of criticality, interface completeness, graph reachability, and policy completeness allows underspecified processes into production runtime.

**Evidence**  
ValidatePublish only requires owner, customer, value statement, governance policy summary, roles, steps, and at least one role per step.

**Recommended fix direction**  
Add layered publish validators for governance profile completeness, graph soundness, approval coverage, and interface contracts.

## FND-09 — Saving a definition deletes and recreates most child records.

- **Severity:** High
- **Area:** Persistence strategy
- **Linked features:** PRM-F02, PRM-F15
- **Codex phase:** Phase 1

**Why this matters**  
This causes ID churn, weakens audit lineage, complicates diffing, and will make future graph references and external correlations fragile.

**Evidence**  
SaveDefinitionChildrenAsync removes existing roles, steps, assignments, and artifact expectations before recreating them.

**Recommended fix direction**  
Switch to diff-based child persistence with stable IDs and explicit add/update/remove handling.

## FND-10 — The transition helper allows same-state transitions and there is no optimistic concurrency protection.

- **Severity:** High
- **Area:** Concurrency and determinism
- **Linked features:** PRM-F07, PRM-F15
- **Codex phase:** Phase 2

**Why this matters**  
Double completion, conflicting assignment claims, or repeated approval actions may be accepted silently under contention.

**Evidence**  
IsTransitionAllowed returns true when currentStatus == targetStatus; no RowVersion or concurrency token is defined for process runtime rows.

**Recommended fix direction**  
Reject idempotence-breaking same-state transitions and add row-version concurrency handling on run and step runtime rows.

## FND-11 — Work briefs are concatenated strings rather than normalized governed execution packets.

- **Severity:** High
- **Area:** Work execution packets
- **Linked features:** PRM-F04, PRM-F22
- **Codex phase:** Phase 2

**Why this matters**  
They cannot safely carry baton snapshots, policy context, or structured downstream evidence expectations for agents and humans.

**Evidence**  
BuildWorkBrief concatenates definition and step text into a single string; ProcessWorkBrief stores summary fields only.

**Recommended fix direction**  
Create template-driven work brief snapshots with structured inputs, outputs, restrictions, and governance context.

## FND-12 — The journal exists internally but is not exposed as a first-class operational surface.

- **Severity:** High
- **Area:** Journal and replay
- **Linked features:** PRM-F08, PRM-F24
- **Codex phase:** Phase 4

**Why this matters**  
Replay, forensics, audit review, and process-mining style analysis cannot mature without direct access to journal chronology and context.

**Evidence**  
ProcessJournalEntry is persisted, but the coordinator’s run detail omits journal entries and there are no journal MCP tools.

**Recommended fix direction**  
Add journal list/read APIs, journal MCP tools, and replay-oriented views tied to the same canonical journal.

## FND-13 — Analytics are limited to shallow aggregates and do not support outcome-oriented operational control.

- **Severity:** High
- **Area:** Metrics and telemetry
- **Linked features:** PRM-F19, PRM-F14, PRM-F21
- **Codex phase:** Phase 5

**Why this matters**  
Agent management requires segmentation, bottleneck visibility, capacity signals, and feedback-linked success measures, not just activity counts.

**Evidence**  
GetAnalyticsAsync returns totals, averages, cost sums, and basic counts only.

**Recommended fix direction**  
Introduce telemetry snapshots, capacity signals, bottleneck computation, and customer/internal-customer feedback capture.

## FND-14 — Conformance signals are recorded, but deviation clustering and privacy-scoped governance are missing.

- **Severity:** High
- **Area:** Conformance and governance
- **Linked features:** PRM-F14, PRM-F21
- **Codex phase:** Phase 5

**Why this matters**  
Repeated unofficial loops and bypasses will stay hidden as isolated text notes instead of becoming actionable process governance signals.

**Evidence**  
Only ProcessConformanceObservation and ProcessImprovementCandidate exist; the bundle intended ProcessDeviationCluster and richer governance handling.

**Recommended fix direction**  
Add deviation clustering, privacy levels, conversion to approved variants, and breach investigation workflow.

## FND-15 — The module already has multiple god files and oversized UI components.

- **Severity:** High
- **Area:** Code structure
- **Linked features:** Cross-cutting
- **Codex phase:** Phase 0 / Ongoing

**Why this matters**  
Adding the missing governance and agent features into the current monoliths will sharply increase change risk and regression cost.

**Evidence**  
ProcessesService.cs (~994 lines), ProcessWorkspace.razor (~974), ProcessWorkspace.razor.cs (~924), ProcessWorkspace.Canvas.cs (~788), ProcessRuntimeModels.cs (~677), ProcessCanvasTemplateCatalog.cs (~573), ProcessDefinitionModels.cs (~559).

**Recommended fix direction**  
Split domain, orchestration, projection, import/export, policy, and UI concerns before or while implementing the missing critical features.

## FND-16 — Only the local JSON envelope is supported.

- **Severity:** Medium
- **Area:** Import / export
- **Linked features:** PRM-F12
- **Codex phase:** Phase 6

**Why this matters**  
The starter bundle explicitly included Mermaid import/export and template seeding to improve portability and controlled onboarding.

**Evidence**  
ExportAsync and ImportAsync only handle CanDoItAll.ProcessDefinition/v1; no Mermaid references exist in the process module.

**Recommended fix direction**  
Add Mermaid flowchart and mindmap adapters with explicit warning capture for lossy semantics.

## FND-17 — Tests mainly cover happy-path CRUD/runtime/projection scenarios and do not protect the missing advanced semantics.

- **Severity:** Medium
- **Area:** Test coverage
- **Linked features:** Cross-cutting
- **Codex phase:** Phase 0 / Ongoing

**Why this matters**  
Critical refactors need regression protection for branching, approvals, escalations, concurrency, external executor correlation, and project-structure propagation.

**Evidence**  
Existing integration tests validate save/publish/start/transition/assignment/artifact/analytics and workbench projection of definition/run nodes only.

**Recommended fix direction**  
Add unit and integration tests for graph validation, approval conflicts, escalation projection, baton handoffs, and concurrent claims/transitions.

## FND-18 — Indexes and migrations are present, but runtime hardening is incomplete for agent-scale orchestration.

- **Severity:** Medium
- **Area:** Storage hardening
- **Linked features:** PRM-F15, PRM-F19
- **Codex phase:** Phase 5

**Why this matters**  
Agent-driven volume and concurrent state mutation will expose weak spots quickly if the runtime keeps using simple tables without concurrency and projected query models.

**Evidence**  
Useful indexes exist in definition/runtime configurations, but there are no concurrency tokens and no separate overlay or telemetry query models.

**Recommended fix direction**  
Add concurrency tokens, projection tables where justified, and benchmark-driven query optimization after the canonical model is completed.

