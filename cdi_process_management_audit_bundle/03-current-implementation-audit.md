# Current implementation audit

### PRM-F01 — Process module foundation and shell integration

- **Status:** Implemented
- **Implementation quality:** Good
- **Gap severity:** Low

**Bundle expectation**  
Introduce a new canonical CanDoItAll module for process management, register it in composition, expose shell and project routes, and keep the module isolated from Workbench canonical state.

**Current state**  
The module exists, is registered in composition, exposes both global and project-scoped routes, appears in shell navigation, and has SQLite/PostgreSQL migrations. This is a solid foundation that matches the bundle's first-wave intent.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs; src/CanDoItAll.Web/Program.cs; src/CanDoItAll.Web/Composition/ShellNavigation.cs; src/CanDoItAll.Composition/ModuleAssemblies.cs; src/CanDoItAll.Modules.Processes/Pages/ProcessesPage.razor; src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor; src/CanDoItAll.Migrations.Sqlite/Migrations/20260409104531_AddProcessesFoundation.cs; src/CanDoItAll.Migrations.PostgreSql/Migrations/20260409104612_AddProcessesFoundation.cs

**Recommended next move**  
Keep this foundation stable and avoid leaking future runtime logic into Workbench, MCP, or shell layers.

### PRM-F02 — Process definition language and versioning

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Create the canonical process DSL with definitions, versions, nodes, transitions, lifecycle status, publication rules, and template cloning semantics.

**Current state**  
Draft/published/superseded/archived versioning exists and publish clones the active version into a new draft. The definition language is still too flat: steps are mostly free-text, graph semantics are limited, and child persistence rewrites records instead of preserving stable lineage.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs; src/CanDoItAll.Modules.Processes/ProcessesService.cs:326-578, 580-740

**Recommended next move**  
Introduce explicit transition, contract, policy, and governance entities and replace destructive child rewrites with stable diff-based persistence.

### PRM-F03 — Actor roles, responsibilities, and CRM-HR bindings

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Bind process actors to CRM-HR parties and AI-agent profiles, model role responsibilities, and prevent creation of a second durable agent registry inside the process module.

**Current state**  
Roles, responsibility kinds, required skills, and project-party prebinding are implemented. Durable CRM-HR identity and AI profile binding is still indirect, and runtime rebinding history is only partially represented by current assignment rows and generic decision records.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:60-102, 325-399; src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs

**Recommended next move**  
Bind process roles to CRM-HR-owned party and AI identity references explicitly and preserve assignment history independently from current assignment state.

### PRM-F16 — Role and agent templates, staffing briefs, and sourcing handoffs

- **Status:** Partial
- **Implementation quality:** Weak
- **Gap severity:** Critical

**Bundle expectation**  
Let managers define reusable human/AI/hybrid role templates in CRM-HR, let process designers reference them instead of ad-hoc free-text roles, and let HR fulfill gaps through staffing, recruiting, or agent-sourcing workflows with durable snapshots.

**Current state**  
Role templates exist only as a static module-local catalog and published versions snapshot a few template strings. The governed lifecycle, sourcing handoff, fallback pool semantics, and HR-driven staffing gap workflow described by the bundle are not implemented.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessCanvasTemplateCatalog.cs; src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs:168-172; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:67-84

**Recommended next move**  
Move reusable role/agent templates to CRM-HR-owned canonical records, then reference versioned snapshots from process definitions and runtime staffing gaps.

### PRM-F04 — Step contracts, inputs, outputs, and evidence

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Define typed step contracts for what a step consumes, produces, validates, and hands off so responsibilities are explicit and auditable.

**Current state**  
Step contracts and artifact expectations are represented, but mostly as summary text fields. Contracts are not normalized into typed inputs, outputs, evidence, done definitions, or reusable validation rules.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs:208-226, 245-268; src/CanDoItAll.Modules.Processes/ProcessesService.cs:654-736

**Recommended next move**  
Introduce structured step contracts and typed artifact/input-output records so runtime, metrics, and approval policy can reason over them safely.

### PRM-F17 — Process ownership, interfaces, customer, and value alignment

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** Critical

**Bundle expectation**  
Make process owner, sponsor, customer, strategic objective, criticality, and upstream/downstream interfaces first-class so processes are governed end-to-end by value flow rather than org-chart convenience.

**Current state**  
Owner, customer, value statement, interface summary, governance notes, criticality, and autonomy fields exist. Publish validation only enforces owner, customer, value statement, and governance summary; criticality and structured interface ownership required by the bundle are not enforced.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs:77-109; src/CanDoItAll.Modules.Processes/ProcessesService.cs:781-805

**Recommended next move**  
Add structured governance profile and interface contract entities and make them mandatory for publication where required by criticality.

### PRM-F09 — Canvas modeler and interactive diagrams

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Deliver a dedicated process designer on top of CanvasLib with reusable node rendering, quick-create affordances, saved layout, template-aware role editing, and a path to labeled transitions and swimlane-like grouping.

**Current state**  
The authoring canvas, runtime canvas, toolbox, and process workspace are implemented. The modeler cannot express branch conditions, default paths, or richer node/edge semantics, so the canvas is still visually useful but semantically underpowered relative to the bundle.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs; src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor; src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.cs

**Recommended next move**  
Upgrade the canvas to edit and visualize canonical graph transitions, policy badges, and first-class handoffs.

### PRM-F15 — Storage, migrations, and performance hardening

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Use the shared CanDoItAll app database first, add strong indexing and retention boundaries, and leave a clean seam for later extraction of hot append-only stores.

**Current state**  
The module has dedicated tables, indexes, and migrations in both database providers. Storage hardening is incomplete because there are no optimistic concurrency tokens, no stable child identities on save, and no deeper runtime/query performance strategy.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs:271-376; src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs:350-493; src/CanDoItAll.Migrations.Sqlite/Migrations/20260409104531_AddProcessesFoundation.cs; src/CanDoItAll.Migrations.PostgreSql/Migrations/20260409104612_AddProcessesFoundation.cs

**Recommended next move**  
Add concurrency protection, preserve child row identity, and benchmark the main read models before agent-scale runtime is introduced.

### PRM-F05 — Transition rules, decisions, and explicit handoffs

- **Status:** Missing
- **Implementation quality:** None
- **Gap severity:** Critical

**Bundle expectation**  
Model ordered responsibility changes between actors, decision branches, retries, default paths, and explicit handoff payloads for process execution.

**Current state**  
The bundle expected explicit transitions, conditions, default paths, branch priorities, handoff metadata, and graph validation. The current implementation only stores a single DependsOnStepId and runtime activation simply moves to the next step by sequence.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs:226; src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs:14-19; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:238-248

**Recommended next move**  
Introduce a canonical ProcessTransition model and drive runtime activation, validation, and canvas links from that graph.

### PRM-F06 — Approval policies, escalations, and governance gates

- **Status:** Partial
- **Implementation quality:** Weak
- **Gap severity:** Critical

**Bundle expectation**  
Add approval gates, escalation rules, separation-of-duties constraints, and policy boundaries that align with future agent-rights enforcement while working for human and manual flows now.

**Current state**  
Approval waiting and escalation-like statuses exist, and decisions are journaled. Explicit approval policy metadata, escalation routing to human or supervisory targets, self-approval conflict prevention, and override handling are absent.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs:164, 208; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:117-119, 260-287

**Recommended next move**  
Create explicit approval and escalation policy aggregates and enforce them in code before any agent can act autonomously.

### PRM-F07 — Runtime execution state machine and assignments

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Implement process runs, step runs, state transitions, actor claims, eligibility-aware assignments, and safe concurrency rules for manual, AI, and hybrid execution.

**Current state**  
There is a usable baseline state machine for runs, step states, assignments, and artifact recording. It is not yet a safe orchestration engine because it is sequence-driven, lacks concurrency protection, and does not fully model pause/resume, branching, or governed re-entry.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs; src/CanDoItAll.Modules.Processes/ProcessesService.cs:815-847

**Recommended next move**  
Extract a dedicated runtime orchestration service with transition graph semantics, optimistic concurrency, and deterministic re-entry rules.

### PRM-F08 — Execution timeline, audit journal, and replay

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Persist append-oriented events for every important runtime change so project, manager, QA, and future training flows can replay what happened.

**Current state**  
Decision records, artifacts, and journal entries are persisted, and replay context JSON exists. The journal is not exposed as a first-class read API or MCP tool, and replay context is too small for serious runtime reconstruction.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs:276-297; src/CanDoItAll.Modules.Processes/ProcessesService.cs:924-949; src/CanDoItAll.Mcp.Processes/ProcessesCoordinator.cs:132-140

**Recommended next move**  
Expose journal read/replay surfaces and enrich replay context with transition, handoff, approval, and external executor correlation data.

### PRM-F18 — Variants, exceptions, input quality, and decision rights

- **Status:** Partial
- **Implementation quality:** Weak
- **Gap severity:** High

**Bundle expectation**  
Model controlled variants, exception paths, input-quality requirements, explicit decision rights, and risk-based controls so the runtime handles real-world deviations without degenerating into bureaucracy.

**Current state**  
Exception policy, decision rights, and input quality are present only as summary text. There are no explicit variants, approved exception playbooks, or structured override records, so governance cannot reason over them safely.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs:212-220; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:221-233, 301-319

**Recommended next move**  
Model variants, exception playbooks, input quality rules, and decision-right rules as first-class canonical entities.

### PRM-F22 — Process-native work briefs, baton handoffs, and governed triage routing

- **Status:** Partial
- **Implementation quality:** Weak
- **Gap severity:** Critical

**Bundle expectation**  
Make the modeled process the canonical collaboration/orchestration graph by issuing normalized work briefs from step contracts and template snapshots, recording baton handoffs, and treating triage or routing as governed process decisions rather than hidden direct agent wiring.

**Current state**  
Executable steps do materialize work briefs, which is valuable. The bundle's core baton-handoff, triage-routing, and governed direct agent-to-agent override semantics are missing.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs:202-222; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:145-159

**Recommended next move**  
Persist baton handoffs and triage decisions as their own runtime artifacts and tie them to journal, approvals, and project structure projections.

### PRM-F12 — Import/export, templates, Mermaid, and prompt-flow seeding

- **Status:** Partial
- **Implementation quality:** Weak
- **Gap severity:** Medium

**Bundle expectation**  
Allow process definitions to be imported from Mermaid and exported back to Mermaid/JSON packages, and seed starter templates from existing prompt-flow patterns where it fits.

**Current state**  
JSON import/export works for the local CanDoItAll envelope and warnings are stored. Mermaid import/export and prompt-flow seeding described by the bundle are absent.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:443-473; src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs:477-519

**Recommended next move**  
Add format adapters for Mermaid and seeding helpers while preserving the process module as the canonical store.

### PRM-F10 — Project, Workbench, and shell projections

- **Status:** Implemented
- **Implementation quality:** Good
- **Gap severity:** Medium

**Bundle expectation**  
Expose process navigation in project UX and optional Workbench projection surfaces while keeping canonical process data outside Workbench.

**Current state**  
Definitions and runs are projected into project/workbench structure and shell routes are in place. The projection is intentionally one-way, which aligns with the bundle, but intervention-specific nodes are not yet emitted.

**Primary evidence**  
src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:1007-1059; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:104-132

**Recommended next move**  
Keep projection-only discipline and extend it with decision/work-item projections for escalations and human intervention.

### PRM-F11 — Activity, Automation, Validation, and TestLab hooks

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Connect process runs to timeline visibility, overdue-signal generation, validation gates, and testing evidence so process execution becomes a first-class operational surface.

**Current state**  
Activity stream and search indexing hooks exist, and the seed service creates realistic scenarios. Dedicated automation, validation, and TestLab integration hooks are not first-class process concepts yet.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessesService.cs:300-322, 379-390; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:175-186

**Recommended next move**  
Add explicit emitted process events and typed integration references for automation, validation, and testing systems.

### PRM-F13 — Future AgentFramework adapter and AI executor seam

- **Status:** Stub
- **Implementation quality:** Weak
- **Gap severity:** Critical

**Bundle expectation**  
Prepare the process module for later Microsoft Agent Framework integration without forcing that runtime into the first process-management implementation.

**Current state**  
A future executor registry seam exists, but only as an interface with a noop implementation that returns no options. There is no actual AgentFramework adapter, permission narrowing, or execution policy enforcement.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs:10, 15-28; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:475-476

**Recommended next move**  
Keep the seam adapter-based, but implement real bridge contracts, correlations, and policy guards before enabling external executors.

### PRM-F24 — Live process execution canvas overlays and baton visibility

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** High

**Bundle expectation**  
Project live run state back onto the authored process diagram so operators can see active steps, current assignee, waits, approvals, and baton movement on the same canvas without turning the overlay into canonical state.

**Current state**  
The runtime canvas overlays status, capability gaps, and executor assignments, which is already operationally useful. It does not show last baton movement, explicit wait reasons, or approval-route context, and runtime links are still linear.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs:37-78, 412-434

**Recommended next move**  
Drive the live overlay from canonical transitions, handoffs, approval routes, and the journal rather than from sequence-only runtime state.

### PRM-F23 — AgentFramework convergence: process-bound context, rights, and shared registries

- **Status:** Stub
- **Implementation quality:** Weak
- **Gap severity:** Critical

**Bundle expectation**  
Prepare future AgentFramework integration so sessions, logs, metrics, permissions, templates, providers, and capabilities remain subordinate to CanDoItAll process, CRM-HR, and Workspace truth rather than creating parallel registries or orphaned runtime context.

**Current state**  
The bundle expected executor/session/log/metric correlations and explicit canonical registry convergence with CRM-HR and Workspace. The current code has no such persisted model and only exposes an empty executor-options seam.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs; src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs; src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs; src/CanDoItAll.Modules.Workspace/WorkspaceStorageModels.cs

**Recommended next move**  
Add external executor correlation entities and bridge rules that consume CRM-HR and Workspace truth rather than duplicating it.

### PRM-F20 — Change governance, prioritization, literacy, and management adoption

- **Status:** Missing
- **Implementation quality:** None
- **Gap severity:** Critical

**Bundle expectation**  
Govern process changes through impact analysis, prioritization, communications, role-specific guidance, and management sponsorship so process management becomes an operating discipline rather than a document library.

**Current state**  
There is no implemented change request workflow, governance approval path for portfolio changes, acknowledgement tasks, prioritization tiers, or operator guidance surface matching the bundle.

**Primary evidence**  
No matching process-side models or services were found in src/CanDoItAll.Modules.Processes for change proposals, acknowledgements, or governed publish/retire workflows.

**Recommended next move**  
Introduce change governance entities and workflows before the module becomes a control plane for multiple agents or teams.

### PRM-F19 — Outcome metrics, capacity, wait-state telemetry, and customer-value measures

- **Status:** Partial
- **Implementation quality:** Weak
- **Gap severity:** High

**Bundle expectation**  
Track lead time, touch time, queue time, wait reasons, first-time-right, rework, capacity, bottlenecks, SLA attainment, and customer-facing value measures instead of only activity counts.

**Current state**  
The runtime stores wait/touch/blocked/rework/cost/FTR/SLA fields and analytics summary returns basic aggregates. Capacity load, bottleneck analysis, segmentation, customer feedback, and outcome-context KPIs remain largely unimplemented.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs:121-125, 167-174; src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs:196-235

**Recommended next move**  
Create telemetry and capacity facts, calculate richer KPIs, and attach outcome/customer feedback to completed runs.

### PRM-F14 — Operational intelligence, improvement backlog, and training-opportunity loop

- **Status:** Partial
- **Implementation quality:** Fair
- **Gap severity:** Medium

**Bundle expectation**  
Turn runtime telemetry and repeated deviations into governed improvement requests, training-opportunity markers, and curator-ready signals without contaminating live execution.

**Current state**  
Improvement candidates and training-opportunity flags are generated from friction events. Governance routing, deduplication, prioritization, and compatibility with later intelligence-lake style analysis remain thin.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs:324-346; src/CanDoItAll.Modules.Processes/ProcessesService.cs:952-992

**Recommended next move**  
Separate improvement workflow from execution queries and add governance state, deduplication, and clustering.

### PRM-F21 — Conformance, field observation, and reality-alignment reviews

- **Status:** Partial
- **Implementation quality:** Weak
- **Gap severity:** High

**Bundle expectation**  
Compare modeled processes with actual execution and field observations, capture deviations and unofficial workarounds, and turn them into governed improvement work so the system reflects reality instead of only diagrams.

**Current state**  
Conformance observations are recorded against runs and steps. The bundle's repeated-deviation clustering, privacy-scoped access controls, and paper-versus-reality reporting dimensions are not implemented.

**Primary evidence**  
src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs:302-324; src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:301-319

**Recommended next move**  
Add deviation clustering, restricted-visibility controls, and portfolio reporting by step, interface, owner, and customer segment.
