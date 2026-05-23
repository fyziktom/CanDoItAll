# Process Missing Artifact Manager Recovery Hardening

This bundle is a coordination and execution package for `process-missing-artifact-manager-recovery-hardening`.

## Profile

- `feedback`

## Mission

Harden process automation so a completed step that still lacks required handoff artifacts is routed to the process manager for evidence recovery from step history, upstream artifacts, and prior execution records instead of blocking indefinitely or rerunning the same executor loop.

## Outcome Contract

- Requested outcome: missing required artifacts after a completed process-step execution trigger manager-mediated recovery with durable audit evidence and no self-rerun loop.
- Hard constraints: preserve existing process artifact projection semantics; use explicit manager recovery evidence; fail predictably when no manager can be resolved; avoid silent fallback.
- Evidence required before closure: targeted tests proving manager directive content and manager-agent handoff, plus a build/test result for the changed projects.
- Known blockers or explicit scope exceptions: the live run may still have pending tool approvals; this bundle hardens the runtime path and does not manually fabricate artifacts.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-manager-artifact-recovery`
2. `subbundles/02-validation-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `N/A`
