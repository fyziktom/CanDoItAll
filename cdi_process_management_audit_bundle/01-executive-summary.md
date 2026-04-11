# Executive summary

## Assessment

The starter bundle aimed for a **canonical business-owned process module** that would eventually supervise human and agent work through explicit roles, transitions, handoffs, approvals, governance, metrics, and projection-only overlays. The current codebase delivers the **foundation**, but not the **full control-plane semantics**.

### What is already good

- **The Processes module foundation is in place and follows existing composition patterns.** Routes, shell navigation, DI registration, module assembly discovery, and both SQLite/PostgreSQL migrations are already wired cleanly.
- **A real persisted domain and runtime baseline already exists.** Definitions, versions, roles, steps, runs, assignments, work briefs, decisions, artifacts, journal entries, conformance observations, and improvement candidates are stored in dedicated tables with useful indexes.
- **There is already a local MCP projection for process operations.** The MCP layer stays thin and delegates to canonical process services instead of introducing a second domain model.
- **Workbench projection discipline is mostly correct.** Definitions and runs are projected into project structure as projections, not alternate canonical records.
- **Seed scenarios and baseline integration tests provide a usable starting harness.** The module already contains seed data, runtime examples, and integration tests covering the happy-path flow.

### Why the module is not ready for agent integration yet

1. **Process topology is not canonical enough at runtime.**  
   The bundle expected explicit transitions, branch conditions, default paths, and handoff metadata. The current runtime still advances by step sequence, which means the authoring model and the execution model are not fully aligned.

2. **Governance is still too implicit.**  
   Approval and escalation exist only as booleans, statuses, and generic decision rows. That is not strong enough for future external executors or agent permissions.

3. **Escalations do not reach the project operating surface.**  
   The platform already has `Decision` and `WorkItem` node types in project structure, but the processes projection only emits definition and run nodes. That means interruptions and required human actions do not propagate into the place where operators already manage work.

4. **Canonical ownership boundaries are at risk.**  
   Static process-local role templates are useful for seeding, but they would become a shadow registry if left as the durable source for reusable human/AI staffing semantics.

5. **Code structure is already under strain.**  
   The process service and main workspace component family are already large enough that adding the missing governance and agent functionality directly into them would be risky.

## Headline numbers

- Intended entity inventory in the starter bundle: **44**
- Current persisted process tables in the module: **16**
- Bundle alignment score (heuristic): **48%**
- Agent orchestration readiness (heuristic): **15%**

## Executive verdict

- **Use what exists as the baseline.**
- **Do not integrate the agent module directly on top of the current runtime yet.**
- **Finish the canonical process model, governance model, escalation propagation, and hard guardrails first.**
- **Refactor the monoliths before or during those repairs, not after them.**

## Immediate must-fix set before agent-module integration

- No canonical transition graph or branch semantics. Runtime progression still uses sequence order instead of explicit transitions.
- No first-class baton handoffs, triage records, or governed direct agent-to-agent override path.
- No explicit approval/escalation policy engine with targets, conflict prevention, or override records.
- No hard runtime guardrails beyond prompt text: executor bridge is a noop and permission/capability/evaluation policy is not enforced in code.
- Escalations are not propagated into project structure as Decision or WorkItem nodes, despite the platform already supporting those node types.
- Role and agent templates are process-local static definitions, which risks creating a shadow registry that diverges from CRM-HR and Workspace.
- No external executor/session/log/metric correlation model, so future agent runs would not be attributable back to canonical process context.
- The core service and UI files are already too large; adding agent orchestration on top of the current structure would raise maintenance and safety risk.

## Safe recommendation for next step

Execute the remediation backlog in the order defined in `08-codex-execution-plan.md`, beginning with the structural split plus canonical graph completion. That sequence minimizes the risk of introducing a second source of truth or a half-governed agent runtime.
