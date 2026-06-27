# Process Manager Audio Reply And Run Metrics

This bundle is a coordination and execution package for `process-manager-audio-reply-and-run-metrics`.

## Profile

- `feedback`

## Mission

- Repair the Process Manager chat so voice-mode prompts behave like other voice-enabled chat surfaces, and so the manager has enough selected-run usage context to answer cost and token questions directly when those metrics are available.

## Outcome Contract

- Requested outcome: Manager chat auto-reads an assistant response after a voice-mode send, and manager prompts include selected-run cost/token usage when loaded for the Manager tab.
- Hard constraints: keep changes scoped to Process Manager chat/runtime projection loading and focused tests; do not add silent fallbacks or broad provider rewrites.
- Evidence required before closure: component/unit tests for both regressions, successful build, restarted `http://localhost:5032`, and browser proof for the Manager tab.
- Known blockers or explicit scope exceptions: real microphone/browser speech capture depends on local device permissions, so automated proof may validate UI/DOM behavior while component tests prove service calls.

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

1. `subbundles/01-...`
2. `subbundles/02-...`
3. Continue until the final validation subbundle is complete.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
