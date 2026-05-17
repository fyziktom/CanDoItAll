# Shell Tab And Menu Density

This bundle is a coordination and execution package for `shell-tab-menu-density`.

## Profile

- `feedback`

## Mission

- Reduce large-desktop shell chrome height by keeping tab controls, tab stats, and status badges on one row, and replace the height-locked sidebar's internal navigation scroll with a hover-open continuation menu for overflow pages.

## Outcome Contract

- Requested outcome: on large desktop, tab search, tab overflow count, recent-tab controls, and the compact shell status badges share the same row as the tabs; the primary sidebar no longer scrolls internally and uses a final `more_up` menu item to expose pages that do not fit.
- Hard constraints: keep mobile and non-large layouts conservative; continuation pages render as small square icon cards with one-word labels; the continuation panel uses the same dark menu background; the overflow grid has at most three rows and grows columns as needed.
- Evidence required before closure: prepared and completed bundle validators, targeted component tests or build proof, Tailwind output regeneration, and large-desktop browser proof with the continuation panel open plus a narrower-width layout check.
- Known blockers or explicit scope exceptions: imagegen mockup is planning evidence only and never replaces browser proof.

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

1. `subbundles/01-01-tab-header-density`
2. `subbundles/02-02-sidebar-overflow-continuation-menu`
3. `subbundles/03-03-validation-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Prepared-stage validator passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Completed-stage validator passed`
- Browser validation analytics: `Passed`
