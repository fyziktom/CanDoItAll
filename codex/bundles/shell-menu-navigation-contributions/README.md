# Shell Menu Navigation Contributions

This bundle is a coordination and execution package for `shell-menu-navigation-contributions`.

## Profile

- `feedback`

## Mission

- Tune the desktop shell menu so tooltip behavior is calm across menu items, and introduce a generic module-owned navigation contribution path that lets modules expose selected subpages as normal top-level menu rows for now.

## Outcome Contract

- Requested outcome: every shell menu tooltip waits a few seconds before showing, and the AgentFramework module contributes a visible `Workflows` menu item immediately after `Agents`.
- Hard constraints: `More`, `Opened`, and `Switch Database` trigger tooltips remain removed; contributed module subpages render visually like normal menu items for now; the contribution model records parent/subitem intent so a later subitem design can use it.
- Evidence required before closure: prepared and completed bundle validators, targeted component/unit tests or build proof, and Playwright MCP proof on a desktop viewport showing the `Agents` to `Workflows` menu order and delayed tooltip behavior.
- Known blockers or explicit scope exceptions: no visual nested-subitem styling will be shipped in this bundle; it is intentionally only prepared in metadata for a later menu design.

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

1. `subbundles/01-tooltip-delay-coverage`
2. `subbundles/02-module-navigation-contributions`
3. `subbundles/03-validation-and-closure`

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
