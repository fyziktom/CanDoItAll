# Decision log

Locked bundle decisions:

## ADR-PROC-001 — Create a new canonical Processes module

**Status**: Accepted for bundle

**Decision**: Process management is implemented as a new `CanDoItAll.Modules.Processes` module rather than extending Workbench or Prompt Factory as the canonical store.

**Rationale**: The repo already has ADR guardrails against turning Workbench into a hidden canonical model.

**Related features**: PRM-F01, PRM-F10

## ADR-PROC-002 — Use the shared application database first

**Status**: Accepted for bundle

**Decision**: The first process-management release uses the main AppDbContext and both SQLite/PostgreSQL migration projects with `Processes_*` tables, not a separate per-project database.

**Rationale**: Direct module integrations (Projects, CRM-HR, Activity, Automation, Validation, TestLab) are simpler and safer with the current CanDoItAll storage pattern. Extraction remains a future seam.

**Related features**: PRM-F01, PRM-F15

## ADR-PROC-003 — CRM-HR owns durable actor identity

**Status**: Accepted for bundle

**Decision**: The process module binds actor roles to CRM-HR parties and AI-agent profiles instead of maintaining a second durable actor registry.

**Rationale**: The uploaded CRM-HR module already models AI-agent profiles and project assignments as first-class business identity.

**Related features**: PRM-F03, PRM-F13

## ADR-PROC-004 — Workbench remains projection-only for process data

**Status**: Accepted for bundle

**Decision**: Any Workbench support for processes is projection, navigation, or summary only; canonical process state stays in Processes tables.

**Rationale**: This follows the repo ADRs about projection-only metadata and node extension guardrails.

**Related features**: PRM-F10

## ADR-PROC-005 — Reuse CanvasLib and Prompt Factory patterns with minimal shared extensions

**Status**: Accepted for bundle

**Decision**: The designer reuses CanvasLib and Prompt Factory adapter patterns first, and only introduces the smallest shared canvas changes needed for process diagrams.

**Rationale**: CanvasLib is already a shared owned foundation, but broad changes risk destabilizing existing consumers.

**Related features**: PRM-F09

## ADR-PROC-006 — Process runtime is AgentFramework-ready but not AgentFramework-dependent in v1

**Status**: Accepted for bundle

**Decision**: The first process runtime supports manual, human, and AI-capable roles through an execution bridge interface, but it does not take a direct dependency on the current AgentFramework repo.

**Rationale**: The user wants process management first and AgentFramework integration after/refactored later.

**Related features**: PRM-F07, PRM-F13

## ADR-PROC-007 — Store canonical graph semantics separately from diagram layout

**Status**: Accepted for bundle

**Decision**: Nodes, transitions, policies, and contracts are normalized canonical data, while diagram coordinates, group frames, and UI state are stored separately as layout/projection data.

**Rationale**: This protects semantics from layout churn and supports multiple future visualizations.

**Related features**: PRM-F02, PRM-F09

## ADR-PROC-008 — Make runtime journaling explicit and append-oriented

**Status**: Accepted for bundle

**Decision**: Important runtime changes are recorded as explicit journal events instead of inferred later from mutable current-state rows.

**Rationale**: Replay, supervision, and later training analysis depend on durable event history.

**Related features**: PRM-F08, PRM-F14

## ADR-PROC-009 — Support Mermaid and JSON package interop before BPMN

**Status**: Accepted for bundle

**Decision**: The first interop target is Mermaid mindmap/flowchart plus a lossless JSON package; BPMN is deferred.

**Rationale**: The repo already contains Mermaid import/export patterns, while BPMN would significantly expand the semantic surface.

**Related features**: PRM-F12

## ADR-PROC-010 — Defer intelligence-lake implementation but preserve extension seams

**Status**: Accepted for bundle

**Decision**: Process management ships first; training snapshots, curator loops, and future knowledge-lake integration are modeled as deferred hooks and insights outputs, not as first-wave dependencies.

**Rationale**: This matches the user's reprioritization and avoids blocking process delivery on the lake.

**Related features**: PRM-F14

## ADR-PROC-011 — Stage advanced parallel orchestration after sequential handoff core

**Status**: Accepted for bundle

**Decision**: Sequential handoffs, decisions, approvals, and escalations ship before complex parallel joins, quorums, or group-chat-like runtime semantics.

**Rationale**: The current immediate need is explicit responsibilities and ordering for handoff collaboration, not maximum orchestration complexity.

**Related features**: PRM-F05, PRM-F07

## ADR-PROC-012 — CRM-HR owns reusable role and agent templates

**Status**: Accepted for bundle

**Decision**: Reusable human, AI, and hybrid role templates plus staffing briefs live in CRM-HR; Processes stores references and snapshots rather than owning a second staffing catalog.

**Rationale**: The uploaded CRM-HR module already owns staffing requests, workforce data, recruiting, and AI-agent profiles, so template ownership belongs with staffing/governance rather than the process graph.

**Related features**: PRM-F03, PRM-F16, PRM-F13

## ADR-PROC-013 — Snapshot template selections into published definitions and runs

**Status**: Accepted for bundle

**Decision**: When a process version is published and when a run starts, the selected role-template version and key requirement summary are snapshotted into process-owned data and journal events.

**Rationale**: Template evolution must not rewrite prior process history, staffing decisions, audit review, or future training replay.

**Related features**: PRM-F02, PRM-F07, PRM-F16

## ADR-PROC-014 — Keep Wave 1 designer core independent from Wave 2 handoff chrome

**Status**: Accepted for bundle

**Decision**: The Wave 1 process designer depends on definition, actor, template, and contract features only; richer handoff visuals and edge-label chrome deepen later without blocking first authoring delivery.

**Rationale**: The previous bundle revision had an inverted dependency where the Wave 1 designer depended on a Wave 2 handoff feature. Splitting core authoring from later chrome restores a sane delivery path.

**Related features**: PRM-F09, PRM-F05, PRM-F16

## ADR-PROC-015 — Model process ownership, customer, and interfaces as first-class metadata

**Status**: Accepted for bundle

**Decision**: Every governed process version must carry process owner, primary customer, value statement, criticality tier, and explicit interface contracts rather than leaving them to external documents.

**Rationale**: Without owner/customer/interface metadata, process maps optimize local boxes and hide the real boundaries where value and delay occur.

**Related features**: PRM-F17, PRM-F10

## ADR-PROC-016 — Separate process value flow from organizational hierarchy

**Status**: Accepted for bundle

**Decision**: The process graph and actor responsibilities are modeled independently from reporting lines or org-chart structure, even when role bindings resolve through CRM-HR.

**Rationale**: Processes should follow value creation and decision flow, not mirror who reports to whom.

**Related features**: PRM-F03, PRM-F17

## ADR-PROC-017 — Treat exceptions, decision rights, and input quality as canonical semantics

**Status**: Accepted for bundle

**Decision**: Input-quality rules, decision rights, approved variants, exception paths, and control tiers are stored as canonical process semantics rather than comments or free-text operating notes.

**Rationale**: Happy-path-only models fail in real operations and leave critical authority questions implicit.

**Related features**: PRM-F18, PRM-F04, PRM-F06

## ADR-PROC-018 — Measure outcome flow, not only activity counts

**Status**: Accepted for bundle

**Decision**: The telemetry model must distinguish work time, wait time, blocked time, rework, and customer-facing outcomes; dashboards should not use raw activity counts as the primary success signal.

**Rationale**: What is measured wrong gets optimized wrong; process management needs flow and outcome visibility.

**Related features**: PRM-F19, PRM-F14

## ADR-PROC-019 — Govern process change as an operating model, not just version storage

**Status**: Accepted for bundle

**Decision**: Critical process changes require impact analysis, prioritization, communication, and acknowledgement workflows in addition to immutable version records.

**Rationale**: Versioning alone does not prevent unofficial forks or middle-management bypasses.

**Related features**: PRM-F20, PRM-F02

## ADR-PROC-020 — Conformance review must use observed execution and privacy-safe observations

**Status**: Accepted for bundle

**Decision**: Paper-versus-reality review uses runtime evidence, structured deviations, and access-controlled observation notes; the system must not become an unmanaged registry of rumors about people.

**Rationale**: Reality alignment matters, but governance and privacy discipline matter too.

**Related features**: PRM-F21, PRM-F19, PRM-F20

## ADR-PROC-021 — Treat the process definition as the canonical collaboration and handoff graph

**Status**: Accepted for bundle

**Decision**: Human and AI collaboration topology is defined by the modeled process. Runtime triage, routing, and baton transfers must remain anchored to process steps, transitions, and governed routing policies rather than hidden direct agent-to-agent wiring.

**Rationale**: The user explicitly wants agent binding to be visible and governable through the process canvas and runtime journal.

**Related features**: PRM-F05, PRM-F07, PRM-F22, PRM-F24

## ADR-PROC-022 — Keep CRM-HR canonical for business role and agent templates

**Status**: Accepted for bundle

**Decision**: CRM-HR remains the canonical business owner of reusable human and AI role templates plus durable AI identities. Any runtime-side template fields remain derivative and adapter-scoped.

**Rationale**: The uploaded CanDoItAll repo already binds AI profiles to governance and Workspace provider profiles, and the AgentFramework repo explicitly treats its registry as temporary research infrastructure.

**Related features**: PRM-F16, PRM-F23

## ADR-PROC-023 — Correlate future external executor sessions and metrics back to process context

**Status**: Accepted for bundle

**Decision**: Future external executor sessions, logs, metrics, and approvals must carry correlation links to ProcessRun, ProcessStepRun, assignment, and actor-binding context.

**Rationale**: Without process-bound correlation, AI runtime evidence cannot support business audit, replay, or quality review.

**Related features**: PRM-F08, PRM-F13, PRM-F23

## ADR-PROC-024 — Converge provider and capability ownership to shared CanDoItAll registries

**Status**: Accepted for bundle

**Decision**: Workspace provider profiles and shared capability proof ownership remain canonical for future AI execution. Process and AgentFramework adapters may consume them but must not create a second permanent registry.

**Rationale**: The CanDoItAll repo already exposes a neutral provider contract in Workspace, while the AgentFramework repo describes its provider and capability stores as research seams.

**Related features**: PRM-F23

## ADR-PROC-025 — Treat live process canvas overlays as projections, not state

**Status**: Accepted for bundle

**Decision**: Live run overlays on the process canvas are derived projections over canonical definition, runtime, and journal state. They may visualize and navigate, but they do not own or mutate canonical state directly.

**Rationale**: The same diagram should supervise execution, but projection convenience must not collapse semantic boundaries between model, runtime, and UI layout.

**Related features**: PRM-F09, PRM-F24

## ADR-PROC-026 — Separate project work breakdown from process orchestration while allowing typed links

**Status**: Accepted for bundle

**Decision**: Projects remains the owner of project scope, hierarchy, and delivery context; Processes owns collaboration and handoff orchestration. Typed references may link runs or steps to project objects, but one model must not silently replace the other.

**Rationale**: The user wants process-modeled handoffs for actors, while the existing repo already uses Projects for broader execution context. These concerns must stay aligned without becoming a second hidden scheduling language.

**Related features**: PRM-F10, PRM-F22, PRM-F23
