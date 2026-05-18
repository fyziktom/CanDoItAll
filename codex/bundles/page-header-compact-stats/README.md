# Compact Page Header Stats

This bundle is a coordination and execution package for `page-header-compact-stats`.

## Profile

- `feedback`

## Mission

- Make page headers and tab summary stats compact across production surfaces by replacing large stat-card rows with badge-style stats, icon-only tooltip-backed header actions, and a shared BaseLib implementation modeled on the processes page.

## Outcome Contract

- Requested outcome: compact headers/stat badges with 2-second delayed tooltips and large-screen screenshot proof.
- Hard constraints: preserve existing workflows; focus on large-screen density; use shared BaseLib primitives before local CSS.
- Evidence required before closure: build/test command proof, route screenshots, delayed tooltip proof, and raw-note closure.
- Known blockers or explicit scope exceptions: medium/mobile tuning and dialog-only stat cards are outside the critical path unless they block large-screen page/tab proof.

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

1. `subbundles/01-01-shared-compact-header-primitives`
2. `subbundles/02-02-page-and-tab-stat-migration`
3. `subbundles/03-03-large-screen-browser-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
