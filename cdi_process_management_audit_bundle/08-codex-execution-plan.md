# Codex execution plan

## Guiding principle

Treat this as a **controlled modernization of the canonical process module**, not as a quick patch cycle.

## Batch order

### Batch A — structural preparation
1. Split the oversized services/components without changing behavior.
2. Add missing test scaffolding around current behavior.
3. Document compatibility mapping for legacy text fields.

### Batch B — canonical definition completion
1. Add transition, governance profile, interface contract, and rule entities.
2. Add publish validators for graph soundness and governance completeness.
3. Replace destructive child rewrite behavior.

### Batch C — runtime engine replacement
1. Move runtime activation from linear sequence to transition-driven orchestration.
2. Add baton handoff and triage records.
3. Add approval/escalation policy enforcement.
4. Add override and human-intervention semantics.

### Batch D — operational projection and replay
1. Expose journal/replay query surfaces and MCP tools.
2. Drive live overlay projection from runtime + journal.
3. Project intervention nodes into project structure.

### Batch E — agent-governance bridge
1. Add external executor correlation entities.
2. Add capability/permission/evaluation bridge contracts.
3. Keep implementation behind adapter seams and existing canonical boundaries.

### Batch F — intelligence and governance completion
1. Expand telemetry and feedback.
2. Add conformance clustering and improvement governance.
3. Add change governance and communication workflows.
4. Add Mermaid import/export and controlled seeding adapters.

## Review checklist for each batch

- Does the change preserve Processes as the canonical collaboration graph?
- Does CRM-HR remain the owner of durable human/AI identity and reusable templates?
- Does Workspace remain the owner of provider/capability truth?
- Is every new projection still projection-only?
- Are approvals/escalations/permissions enforced in code rather than only documented in text?
- Are there tests for the newly introduced semantics?
- Has any file become a new monolith?

## Suggested stop conditions

Stop the batch and re-review if any of these appear:
- a new process-local durable template registry,
- direct hidden agent-to-agent topology outside process semantics,
- project structure becoming writable canonical process state,
- prompt-only governance for permissions/approvals,
- a new monolith replacing the old monolith.
