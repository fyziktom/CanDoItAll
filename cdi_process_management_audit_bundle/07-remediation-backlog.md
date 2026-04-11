# Remediation backlog

| Task ID | Phase | Priority | Title | Outcome | Dependencies | Primary touchpoints |
| --- | --- | --- | --- | --- | --- | --- |
| COD-PRM-001 | Phase 0 | Critical | Refactor the module before adding more orchestration logic | Break oversized services and UI files into coherent authoring, runtime, projection, policy, and integration slices. | None | src/CanDoItAll.Modules.Processes/ProcessesService*.cs; src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace*; src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs |
| COD-PRM-002 | Phase 1 | Critical | Complete the canonical definition model | Add structured transitions, governance profile, interface contracts, decision-right rules, input-quality rules, and exception/variant entities. | COD-PRM-001 | ProcessDefinitionModels.cs; new domain files under Definitions/ and Governance/; migrations in both providers |
| COD-PRM-003 | Phase 1 | High | Replace destructive child persistence with stable diff-based saves | Process child rows keep stable identities and only changed records mutate. | COD-PRM-002 | ProcessesService.cs or replacement definition persistence services; migrations if needed |
| COD-PRM-004 | Phase 2 | Critical | Rebuild runtime orchestration over canonical transitions | Runtime activation, branching, and completion are driven by ProcessTransition and policy evaluation instead of linear sequence. | COD-PRM-002, COD-PRM-003 | ProcessesService.Runtime.cs or new Runtime/ orchestration services; ProcessRuntimeModels.cs; ProcessCanvasSurfaceFactory.cs |
| COD-PRM-005 | Phase 2 | Critical | Add baton handoffs, governed triage, and normalized work brief snapshots | Responsibility transfer and dispatch decisions become first-class runtime artifacts with full audit context. | COD-PRM-004 | ProcessRuntimeModels.cs; new Runtime/Handoffs and Runtime/Briefs services; MCP run detail DTOs |
| COD-PRM-006 | Phase 2 | Critical | Implement approval and escalation policy engine | Approvals, supervisory escalation, conflict prevention, override records, and timeout handling are enforced in code. | COD-PRM-002, COD-PRM-004 | new Policy/ services and entities; runtime orchestration; project structure projection |
| COD-PRM-007 | Phase 3 | Critical | Add hard agent guardrails and external executor correlations | Future agents can be governed by capability checks, permission narrowing, evaluation records, and durable external runtime correlations. | COD-PRM-004, COD-PRM-006 | ProcessesModuleServiceCollectionExtensions.cs; new Bridges/ and Runtime/Correlations files; CRM-HR and Workspace integration bridges |
| COD-PRM-008 | Phase 4 | Critical | Propagate interventions into project structure | Blocked, waiting-approval, refused, failed, and human-decision-required states create actionable project structure nodes. | COD-PRM-005, COD-PRM-006 | ProjectStructureAssemblyService.cs; SharedKernel project object contracts if needed; runtime projection services |
| COD-PRM-009 | Phase 4 | High | Expose journal, replay, and live overlay from the same canonical runtime | Operators can inspect journal chronology, replay context, and live canvas overlays without alternate mutation paths. | COD-PRM-004, COD-PRM-005 | ProcessesService.Reads.cs or new Query/ services; MCP coordinator/tools; ProcessCanvasSurfaceFactory.cs |
| COD-PRM-010 | Phase 5 | High | Expand telemetry, capacity, conformance, and feedback | The module can measure lead time, queue time, approval wait, blocked time, rework, capacity, bottlenecks, and outcome/feedback signals meaningfully. | COD-PRM-004, COD-PRM-005, COD-PRM-009 | ProcessRuntimeModels.cs; new Telemetry/ and Analytics/ services; query models and projections |
| COD-PRM-011 | Phase 5 | High | Add conformance clustering and improvement governance | Repeated unofficial loops, bypasses, and friction patterns become structured improvement and governance signals. | COD-PRM-010 | ProcessRuntimeModels.cs; new Conformance/ and Improvement/ services |
| COD-PRM-012 | Phase 6 | Medium | Converge role/agent templates with CRM-HR and add staffing gap workflows | Reusable templates and staffing gap resolution are governed canonically outside the process module while process versions snapshot references safely. | COD-PRM-007 | CRM-HR models/services; process definition role references; seed data |
| COD-PRM-013 | Phase 6 | Medium | Add Mermaid import/export and controlled seeding adapters | Processes can be imported/exported more portably without making external formats canonical. | COD-PRM-002 | Import/export services; ProcessWorkspace exchange tab; MCP export/import surface |
| COD-PRM-014 | Phase 7 | High | Add change governance, communication, and portfolio guidance | Process portfolio changes become governed, acknowledged, and tiered by criticality and impact. | COD-PRM-002, COD-PRM-006 | new Governance/Change/ services and models; UI help/glossary surfaces |
| COD-PRM-015 | Phase 7 | High | Strengthen test matrix and runtime hardening for production control-plane use | The module is defensible under concurrency, external executor integration, and projection-heavy operational use. | All prior phases | tests/CanDoItAll.Tests.*; runtime models; migrations |

## COD-PRM-001 — Refactor the module before adding more orchestration logic

- **Phase:** Phase 0
- **Priority:** Critical
- **Dependencies:** None

**Desired outcome**  
Break oversized services and UI files into coherent authoring, runtime, projection, policy, and integration slices.

**Primary touchpoints**  
src/CanDoItAll.Modules.Processes/ProcessesService*.cs; src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace*; src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs

**Acceptance**  
No single service or component file that owns core orchestration exceeds a maintainability threshold; runtime, definition authoring, projection, and policy concerns are separated; behavior remains covered by tests.

**Tests to add/update**  
Refactor-safety regression tests around save/publish/start/transition/workbench projection.

**Risk notes**  
Do not change canonical behavior during the split beyond bug fixes already covered by tests.

## COD-PRM-002 — Complete the canonical definition model

- **Phase:** Phase 1
- **Priority:** Critical
- **Dependencies:** COD-PRM-001

**Desired outcome**  
Add structured transitions, governance profile, interface contracts, decision-right rules, input-quality rules, and exception/variant entities.

**Primary touchpoints**  
ProcessDefinitionModels.cs; new domain files under Definitions/ and Governance/; migrations in both providers

**Acceptance**  
Published processes can express transitions with conditions/defaults/priorities; governance profile and interface contracts are structured; graph validation rejects unreachable or orphaned states.

**Tests to add/update**  
Unit tests for graph validation and publish validation; migration tests.

**Risk notes**  
Preserve existing data with additive migrations and compatibility mapping from legacy text fields.

## COD-PRM-003 — Replace destructive child persistence with stable diff-based saves

- **Phase:** Phase 1
- **Priority:** High
- **Dependencies:** COD-PRM-002

**Desired outcome**  
Process child rows keep stable identities and only changed records mutate.

**Primary touchpoints**  
ProcessesService.cs or replacement definition persistence services; migrations if needed

**Acceptance**  
Editing a definition no longer recreates all roles/steps/assignments/artifact expectations; external references remain stable across saves.

**Tests to add/update**  
Integration tests verifying child IDs remain stable across non-structural edits.

**Risk notes**  
Backward compatibility with existing editor payloads must be retained.

## COD-PRM-004 — Rebuild runtime orchestration over canonical transitions

- **Phase:** Phase 2
- **Priority:** Critical
- **Dependencies:** COD-PRM-002, COD-PRM-003

**Desired outcome**  
Runtime activation, branching, and completion are driven by ProcessTransition and policy evaluation instead of linear sequence.

**Primary touchpoints**  
ProcessesService.Runtime.cs or new Runtime/ orchestration services; ProcessRuntimeModels.cs; ProcessCanvasSurfaceFactory.cs

**Acceptance**  
Parallel/conditional/default flows can be activated correctly; invalid transitions are rejected deterministically; runtime status reflects graph semantics.

**Tests to add/update**  
Unit tests for branching and guarded transitions; integration tests for multi-path process runs.

**Risk notes**  
Avoid changing shell/MCP contracts until orchestration behavior is stable behind service boundaries.

## COD-PRM-005 — Add baton handoffs, governed triage, and normalized work brief snapshots

- **Phase:** Phase 2
- **Priority:** Critical
- **Dependencies:** COD-PRM-004

**Desired outcome**  
Responsibility transfer and dispatch decisions become first-class runtime artifacts with full audit context.

**Primary touchpoints**  
ProcessRuntimeModels.cs; new Runtime/Handoffs and Runtime/Briefs services; MCP run detail DTOs

**Acceptance**  
Each handoff records source role, target role, brief snapshot, completion context, and journal link; direct agent-to-agent bypass requires explicit override evidence.

**Tests to add/update**  
Integration tests for human-to-agent, agent-to-human, and triage-controlled routing scenarios.

**Risk notes**  
Keep work briefs immutable once attached to a handoff or activation.

## COD-PRM-006 — Implement approval and escalation policy engine

- **Phase:** Phase 2
- **Priority:** Critical
- **Dependencies:** COD-PRM-002, COD-PRM-004

**Desired outcome**  
Approvals, supervisory escalation, conflict prevention, override records, and timeout handling are enforced in code.

**Primary touchpoints**  
new Policy/ services and entities; runtime orchestration; project structure projection

**Acceptance**  
A step can pause for approval, route to a target human or supervisory role, prevent self-approval/conflicts, and resume with auditable evidence.

**Tests to add/update**  
Approval conflict tests, escalation route tests, explicit override tests.

**Risk notes**  
Do not leave policy hidden in free-text summaries once this task lands.

## COD-PRM-007 — Add hard agent guardrails and external executor correlations

- **Phase:** Phase 3
- **Priority:** Critical
- **Dependencies:** COD-PRM-004, COD-PRM-006

**Desired outcome**  
Future agents can be governed by capability checks, permission narrowing, evaluation records, and durable external runtime correlations.

**Primary touchpoints**  
ProcessesModuleServiceCollectionExtensions.cs; new Bridges/ and Runtime/Correlations files; CRM-HR and Workspace integration bridges

**Acceptance**  
External executor/session/log/metric IDs can be linked back to ProcessRun and ProcessStepRun; permission/approval policy is enforced in code; no direct compile-time AgentFramework contamination is introduced.

**Tests to add/update**  
Integration tests for bridge adapters, permission narrowing, and approval-required external actions.

**Risk notes**  
CRM-HR and Workspace must remain canonical for identities/templates/providers.

## COD-PRM-008 — Propagate interventions into project structure

- **Phase:** Phase 4
- **Priority:** Critical
- **Dependencies:** COD-PRM-005, COD-PRM-006

**Desired outcome**  
Blocked, waiting-approval, refused, failed, and human-decision-required states create actionable project structure nodes.

**Primary touchpoints**  
ProjectStructureAssemblyService.cs; SharedKernel project object contracts if needed; runtime projection services

**Acceptance**  
Decision nodes and WorkItem nodes appear under the process run when human intervention is required and are updated as process state changes.

**Tests to add/update**  
Workbench integration tests for intervention node creation, update, and closure.

**Risk notes**  
Keep projection one-way; project structure must not become a second canonical process store.

## COD-PRM-009 — Expose journal, replay, and live overlay from the same canonical runtime

- **Phase:** Phase 4
- **Priority:** High
- **Dependencies:** COD-PRM-004, COD-PRM-005

**Desired outcome**  
Operators can inspect journal chronology, replay context, and live canvas overlays without alternate mutation paths.

**Primary touchpoints**  
ProcessesService.Reads.cs or new Query/ services; MCP coordinator/tools; ProcessCanvasSurfaceFactory.cs

**Acceptance**  
Run detail includes journal timeline; MCP exposes journal access; canvas overlay shows wait reason, approval state, and last baton movement from journal-backed projections.

**Tests to add/update**  
Integration tests for journal retrieval and overlay consistency.

**Risk notes**  
Overlay must remain projection-only.

## COD-PRM-010 — Expand telemetry, capacity, conformance, and feedback

- **Phase:** Phase 5
- **Priority:** High
- **Dependencies:** COD-PRM-004, COD-PRM-005, COD-PRM-009

**Desired outcome**  
The module can measure lead time, queue time, approval wait, blocked time, rework, capacity, bottlenecks, and outcome/feedback signals meaningfully.

**Primary touchpoints**  
ProcessRuntimeModels.cs; new Telemetry/ and Analytics/ services; query models and projections

**Acceptance**  
Dashboards segment by process, owner, customer, project, interface, and criticality; customer/internal-customer feedback can be attached to completed runs.

**Tests to add/update**  
Analytics query tests and feedback persistence tests.

**Risk notes**  
Do not present raw activity counts as success KPIs without outcome context.

## COD-PRM-011 — Add conformance clustering and improvement governance

- **Phase:** Phase 5
- **Priority:** High
- **Dependencies:** COD-PRM-010

**Desired outcome**  
Repeated unofficial loops, bypasses, and friction patterns become structured improvement and governance signals.

**Primary touchpoints**  
ProcessRuntimeModels.cs; new Conformance/ and Improvement/ services

**Acceptance**  
Deviation clusters can be reviewed and converted into approved variants, fixes, or policy-breach investigations with privacy controls.

**Tests to add/update**  
Cluster-detection tests and governance routing tests.

**Risk notes**  
Avoid creating an unmanaged rumor registry.

## COD-PRM-012 — Converge role/agent templates with CRM-HR and add staffing gap workflows

- **Phase:** Phase 6
- **Priority:** Medium
- **Dependencies:** COD-PRM-007

**Desired outcome**  
Reusable templates and staffing gap resolution are governed canonically outside the process module while process versions snapshot references safely.

**Primary touchpoints**  
CRM-HR models/services; process definition role references; seed data

**Acceptance**  
Process roles can reference governed template versions; unresolved gaps can open staffing/recruiting/agent-sourcing work without losing process context.

**Tests to add/update**  
Cross-module integration tests for template reference resolution and staffing gap links.

**Risk notes**  
Do not duplicate durable identity or template ownership in Processes.

## COD-PRM-013 — Add Mermaid import/export and controlled seeding adapters

- **Phase:** Phase 6
- **Priority:** Medium
- **Dependencies:** COD-PRM-002

**Desired outcome**  
Processes can be imported/exported more portably without making external formats canonical.

**Primary touchpoints**  
Import/export services; ProcessWorkspace exchange tab; MCP export/import surface

**Acceptance**  
Published processes can export JSON and Mermaid; imports record explicit warnings for non-round-trippable semantics.

**Tests to add/update**  
Round-trip tests with warning assertions.

**Risk notes**  
Maintain process tables as the single source of truth.

## COD-PRM-014 — Add change governance, communication, and portfolio guidance

- **Phase:** Phase 7
- **Priority:** High
- **Dependencies:** COD-PRM-002, COD-PRM-006

**Desired outcome**  
Process portfolio changes become governed, acknowledged, and tiered by criticality and impact.

**Primary touchpoints**  
new Governance/Change/ services and models; UI help/glossary surfaces

**Acceptance**  
Change proposals capture reason, impact, risk, rollout, approvals, and acknowledgements; publish/retire operations can require governance approval based on criticality.

**Tests to add/update**  
Governed publish/retire tests and acknowledgement workflow tests.

**Risk notes**  
Keep business governance explicit and reviewable, not hidden in operational notes.

## COD-PRM-015 — Strengthen test matrix and runtime hardening for production control-plane use

- **Phase:** Phase 7
- **Priority:** High
- **Dependencies:** All prior phases

**Desired outcome**  
The module is defensible under concurrency, external executor integration, and projection-heavy operational use.

**Primary touchpoints**  
tests/CanDoItAll.Tests.*; runtime models; migrations

**Acceptance**  
Unit, integration, component, MCP, and concurrency tests cover the final canonical graph, approvals, handoffs, projections, and bridge behavior; runtime rows use concurrency protection.

**Tests to add/update**  
This task is itself the test and hardening expansion.

**Risk notes**  
Do not declare agent readiness before this phase is green.

