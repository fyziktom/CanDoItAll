# Project Structure Process Role Assignment Modal

This bundle coordinates the implementation of the redesigned AI-agent role assignment flow that opens when a project-structure process node is started.

## Profile

- `feedback`

## Mission

Replace the current stacked staffing review dialog with a full-screen process assignment modal matching the supplied design: role sidebar, assignment progress, role cards, selected-agent detail rail, and manual agent selection using the existing chat agent switcher experience.

## Outcome Contract

- Requested outcome: Starting a process from a project-structure process node opens a full-screen staffing modal where process roles can be reviewed and assigned before the process starts.
- Hard constraints: preserve launch-plan state and existing start validation; reuse the existing agent switcher/card filtering and favorite behavior for manual agent selection; do not start the process until required roles are resolved and reviewed.
- Evidence required before closure: component or integration tests, successful build or targeted test run, real browser proof with large-screen and narrower screenshots, and bundle execution report rows that cite the screenshots.
- Known blockers or explicit scope exceptions: none at preparation time. If manual agent selection cannot safely add an agent absent from the launch-plan candidate matrix, the bundle must be repaired before execution continues.

## Bundle Layout

- `inputs/` raw request, screenshot, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-fullscreen-assignment-layout`
2. `subbundles/02-02-manual-agent-picker-reuse`
3. `subbundles/03-03-browser-proof-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready after prepared-stage validation`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`
