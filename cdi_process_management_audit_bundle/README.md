# CanDoItAll process-management audit bundle

This bundle compares the starter `process-management-bundle` against the current `CanDoItAll-process-manag-modul` snapshot and turns the result into an implementation-grade package for Codex.

## Outcome

- **Bundle alignment score (heuristic): 48%**
- **Agent-orchestration readiness (heuristic): 15%**
- **Feature status mix:** 2 implemented, 18 partial, 2 stub-only, 2 missing
- **Critical findings:** 7
- **High findings:** 8

## Bottom line

The module has a **good foundation** and already contains a real persisted process domain, runtime baseline, MCP access layer, workbench projection, and seed/test scaffolding.

It is **not yet ready to become the control plane for agents**. The biggest blockers are:

- No canonical transition graph or branch semantics. Runtime progression still uses sequence order instead of explicit transitions.
- No first-class baton handoffs, triage records, or governed direct agent-to-agent override path.
- No explicit approval/escalation policy engine with targets, conflict prevention, or override records.
- No hard runtime guardrails beyond prompt text: executor bridge is a noop and permission/capability/evaluation policy is not enforced in code.
- Escalations are not propagated into project structure as Decision or WorkItem nodes, despite the platform already supporting those node types.
- Role and agent templates are process-local static definitions, which risks creating a shadow registry that diverges from CRM-HR and Workspace.
- No external executor/session/log/metric correlation model, so future agent runs would not be attributable back to canonical process context.
- The core service and UI files are already too large; adding agent orchestration on top of the current structure would raise maintenance and safety risk.

## Non-negotiable guardrails for every follow-up repair

- The process definition must remain the canonical collaboration and handoff graph.
- CRM-HR must remain canonical for durable human and AI identities plus reusable role/agent templates.
- Workspace must remain canonical for provider profiles and shared capability ownership.
- Workbench, shell, canvas overlays, activity stream, and MCP must remain projections or integrations, not alternate canonical stores.
- Future AgentFramework integration must stay behind an adapter seam; do not add compile-time contamination of the canonical module.
- Approval, escalation, permission, and autonomy controls must be enforced in code and persisted as policy/evidence records, not only written into prompts.

## What is included

- `01-executive-summary.md`
- `02-bundle-intent-and-target-direction.md`
- `03-current-implementation-audit.md`
- `04-detailed-findings.md`
- `05-agent-orchestration-readiness.md`
- `06-code-quality-and-refactor-plan.md`
- `07-remediation-backlog.md`
- `08-codex-execution-plan.md`
- `09-analysis-method.md`
- `artifacts/CanDoItAll-process-management-audit.xlsx`
- `artifacts/feature-status.json`
- `artifacts/large-files.csv`
- `artifacts/evidence-map.csv`
- `codex/TASKS.json`
- `codex/REVIEW_CHECKLIST.md`

## Recommended reading order

1. Executive summary
2. Feature coverage workbook
3. Agent orchestration readiness
4. Remediation backlog
5. Codex execution plan

## Scope note

This audit is based on **static code review and artifact comparison**. The container used for this work did **not** have a `dotnet` SDK available, so build/test execution could not be re-run inside this environment. That means all findings should be treated as **high-confidence static analysis**, not as a replacement for a final compile/test gate.
