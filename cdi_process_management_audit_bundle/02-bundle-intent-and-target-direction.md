# Bundle intent and target direction

## What the starter bundle actually wanted

The starter bundle was not just a request for a designer and a runtime. It described a **canonical process-management module** that should eventually become the governed collaboration layer for humans and future AI executors.

### Core architectural intent

- The process definition must remain the canonical collaboration and handoff graph.
- CRM-HR must remain canonical for durable human and AI identities plus reusable role/agent templates.
- Workspace must remain canonical for provider profiles and shared capability ownership.
- Workbench, shell, canvas overlays, activity stream, and MCP must remain projections or integrations, not alternate canonical stores.
- Future AgentFramework integration must stay behind an adapter seam; do not add compile-time contamination of the canonical module.
- Approval, escalation, permission, and autonomy controls must be enforced in code and persisted as policy/evidence records, not only written into prompts.

### Intended feature inventory

| Feature ID | Title | Status | Gap severity |
| --- | --- | --- | --- |
| PRM-F01 | Process module foundation and shell integration | Implemented | Low |
| PRM-F02 | Process definition language and versioning | Partial | High |
| PRM-F03 | Actor roles, responsibilities, and CRM-HR bindings | Partial | High |
| PRM-F16 | Role and agent templates, staffing briefs, and sourcing handoffs | Partial | Critical |
| PRM-F04 | Step contracts, inputs, outputs, and evidence | Partial | High |
| PRM-F17 | Process ownership, interfaces, customer, and value alignment | Partial | Critical |
| PRM-F09 | Canvas modeler and interactive diagrams | Partial | High |
| PRM-F15 | Storage, migrations, and performance hardening | Partial | High |
| PRM-F05 | Transition rules, decisions, and explicit handoffs | Missing | Critical |
| PRM-F06 | Approval policies, escalations, and governance gates | Partial | Critical |
| PRM-F07 | Runtime execution state machine and assignments | Partial | High |
| PRM-F08 | Execution timeline, audit journal, and replay | Partial | High |
| PRM-F18 | Variants, exceptions, input quality, and decision rights | Partial | High |
| PRM-F22 | Process-native work briefs, baton handoffs, and governed triage routing | Partial | Critical |
| PRM-F12 | Import/export, templates, Mermaid, and prompt-flow seeding | Partial | Medium |
| PRM-F10 | Project, Workbench, and shell projections | Implemented | Medium |
| PRM-F11 | Activity, Automation, Validation, and TestLab hooks | Partial | High |
| PRM-F13 | Future AgentFramework adapter and AI executor seam | Stub | Critical |
| PRM-F24 | Live process execution canvas overlays and baton visibility | Partial | High |
| PRM-F23 | AgentFramework convergence: process-bound context, rights, and shared registries | Stub | Critical |
| PRM-F20 | Change governance, prioritization, literacy, and management adoption | Missing | Critical |
| PRM-F19 | Outcome metrics, capacity, wait-state telemetry, and customer-value measures | Partial | High |
| PRM-F14 | Operational intelligence, improvement backlog, and training-opportunity loop | Partial | Medium |
| PRM-F21 | Conformance, field observation, and reality-alignment reviews | Partial | High |

## The strategic direction behind the bundle

### 1. Processes own the collaboration graph

The bundle repeatedly states that the process definition is the **canonical collaboration and handoff graph**. That means:
- roles are stable and canonical,
- runtime assignees are replaceable fulfillments of those roles,
- approvals, escalations, and baton transfers are not hidden inside another runtime topology,
- project/workbench overlays remain projections.

### 2. CRM-HR and Workspace stay canonical

The bundle does **not** want the process module to become the owner of:
- durable human identities,
- durable AI identities,
- reusable staffing/agent templates,
- provider profiles and shared capabilities.

Instead, Processes should reference and snapshot those sources as needed.

### 3. AgentFramework is a future adapter seam

The bundle explicitly pushed for a future adapter instead of a hard merge. The process module should remain stable, while a later external executor bridge can translate process-owned context into runtime session execution.

### 4. Runtime supervision must still be business-legible

The bundle wanted:
- normalized work briefs,
- baton handoffs,
- auditable approval and escalation flows,
- live overlay visibility on the same process canvas,
- operational metrics and conformance review,
- governance and change management for process changes.

### 5. Escalation and interruption must be operationally visible

The bundle's direction matches your concern directly: when a process hits a block, requires a decision, or needs human intervention, that state must not stay buried inside the process runtime only. It needs a **project-structure-visible operational representation**.

## Features that matter most for the future agent-control target

### Critical for safe agent management
- PRM-F05 Transition rules, decisions, and explicit handoffs
- PRM-F06 Approval policies, escalations, and governance gates
- PRM-F13 Future AgentFramework adapter and AI executor seam
- PRM-F22 Process-native work briefs, baton handoffs, and governed triage routing
- PRM-F23 AgentFramework convergence: process-bound context, rights, and shared registries
- PRM-F24 Live process execution canvas overlays and baton visibility

### Critical for canonical ownership
- PRM-F03 Actor roles, responsibilities, and CRM-HR bindings
- PRM-F16 Role and agent templates, staffing briefs, and sourcing handoffs
- PRM-F17 Process ownership, interfaces, customer, and value alignment

### Critical for operational control
- PRM-F10 Project, Workbench, and shell projections
- PRM-F19 Outcome metrics, capacity, wait-state telemetry, and customer-value measures
- PRM-F21 Conformance, field observation, and reality-alignment reviews

## My reading of the intended target state

The intended destination is a system where:

1. a published process definition expresses the authoritative graph of work and responsibility;
2. runtime uses that graph to orchestrate humans and later agents safely;
3. approvals and escalations are explicit, attributable, and visible;
4. project structure receives projection nodes for interruption and decision management;
5. agent execution is constrained by code-level policy, not only by prompt text;
6. every external runtime action can be traced back to a process run, step, handoff, and policy decision.
