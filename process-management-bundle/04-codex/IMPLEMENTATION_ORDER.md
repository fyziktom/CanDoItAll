# Codex implementation order

This order keeps the **process-first** intent intact: ship the canonical process model, then canonical runtime handoffs, then live supervision and future AI convergence seams.

## Wave order

### Wave 0 — Design freeze and repo scaffolding

**Entry**: Bundle approved

**Exit**: Process module project exists; composition wired; architecture guardrails checked in.

- No feature bundle; establish scaffolding and approvals.

### Wave 1 — Canonical process modeling foundation

**Entry**: Wave 0 complete

**Exit**: Users can create/publish draft processes, bind actors from CRM-HR, select reusable role templates, capture owner/customer/interface metadata, and edit diagrams on canvas.

- `PRM-F01` — Process module foundation and shell integration
- `PRM-F02` — Process definition language and versioning
- `PRM-F03` — Actor roles, responsibilities, and CRM-HR bindings
- `PRM-F16` — Role and agent templates, staffing briefs, and sourcing handoffs
- `PRM-F04` — Step contracts, inputs, outputs, and evidence
- `PRM-F17` — Process ownership, interfaces, customer, and value alignment
- `PRM-F09` — Canvas modeler and interactive diagrams

### Wave 2 — Runtime handoffs, baton orchestration, and governance core

**Entry**: Wave 1 complete

**Exit**: Users can start runs, enforce approval/decision-right rules, route governed baton handoffs and triage decisions, handle bad inputs and exceptions, and inspect journals.

- `PRM-F05` — Transition rules, decisions, and explicit handoffs
- `PRM-F06` — Approval policies, escalations, and governance gates
- `PRM-F07` — Runtime execution state machine and assignments
- `PRM-F08` — Execution timeline, audit journal, and replay
- `PRM-F18` — Variants, exceptions, input quality, and decision rights
- `PRM-F22` — Process-native work briefs, baton handoffs, and governed triage routing
- `PRM-F12` — Import/export, templates, Mermaid, and prompt-flow seeding

### Wave 3 — Cross-module integration, live supervision, and future AI convergence seams

**Entry**: Wave 2 complete

**Exit**: Project UX, activity/automation hooks, live runtime canvas overlays, future AI bridge seams, and cross-repo convergence rules are integrated without surrendering canonical ownership.

- `PRM-F10` — Project, Workbench, and shell projections
- `PRM-F11` — Activity, Automation, Validation, and TestLab hooks
- `PRM-F13` — Future AgentFramework adapter and AI executor seam
- `PRM-F24` — Live process execution canvas overlays and baton visibility
- `PRM-F23` — AgentFramework convergence: process-bound context, rights, and shared registries
- `PRM-F20` — Change governance, prioritization, literacy, and management adoption

### Wave 4 — Operational intelligence and conformance loop

**Entry**: Wave 3 complete

**Exit**: Outcome telemetry, bottleneck insights, improvement candidates, and paper-versus-reality reviews exist without requiring the intelligence lake.

- `PRM-F19` — Outcome metrics, capacity, wait-state telemetry, and customer-value measures
- `PRM-F14` — Operational intelligence, improvement backlog, and training-opportunity loop
- `PRM-F21` — Conformance, field observation, and reality-alignment reviews

## Cross-cutting guardrails

- `PRM-F15` should be treated as cross-cutting from the start.
- Do not turn Workbench or live canvas overlays into canonical process state.
- Do not create a second canonical registry for templates, providers, or capabilities.
- Do not wire production human/agent collaboration outside the modeled process unless a break-glass path is explicitly journaled.
- Do not add a compile-time dependency on the external AgentFramework repo in the first CanDoItAll process-module merge.
