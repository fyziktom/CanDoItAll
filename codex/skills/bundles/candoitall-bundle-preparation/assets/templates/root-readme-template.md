# {{BUNDLE_TITLE}}

This bundle is a coordination and execution package for `{{BUNDLE_NAME}}`.

## Profile

- `{{PROFILE_NAME}}`

## Mission

- Describe the desired end state in one short paragraph.

## Outcome Contract

- Requested outcome:
- Hard constraints:
- Evidence required before closure:
- Known blockers or explicit scope exceptions:

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries when architecture decisions are material
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts when repeated handoff needs them
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-...`
2. `subbundles/02-...`
3. Continue until the final validation subbundle is complete.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## UI Target Policy

- CanDoItAll applications target large-screen desktop use; do not add small/medium/mobile tuning unless explicitly requested.
- Reusable basic `CanDoItAll.Components.BaseLib` components remain responsible for small, medium, and large viewport behavior.

## Validation Summary

- Bundle preparation status: `Draft`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Not started`
