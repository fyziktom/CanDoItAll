# App Components Project Rename

This bundle is a coordination and execution package for `app-components-rename-v1`.

## Profile

- `initiative`

## Mission

- Rename the in-repo app-shell component facade from `CanDoItAll.Components` to `CanDoItAll.AppComponents`, repair solution and project consumers, and keep the sibling `CanDoItAll.Components` component-library repository untouched.

## Outcome Contract

- Requested outcome: `repo://src/CanDoItAll.Components` becomes `repo://src/CanDoItAll.AppComponents`, with project identity, namespaces, solution entries, project references, tests, and local docs repaired.
- Hard constraints: do not edit the sibling `C:\repositories\CanDoItAll.Components` repository; do not rename or replace external package references such as `CanDoItAll.Components.BaseLib`; keep the change to the app-specific facade and its direct consumers.
- Evidence required before closure: prepared and completed bundle validators, targeted project build, targeted component tests, stale-reference search, changed-file hashes, source assertion notes, and anti-stub audit.
- Known blockers or explicit scope exceptions: browser proof is not required because this is a compile-time project/namespace rename with no intended rendered UI change.

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

1. `subbundles/01-project-rename-and-reference-repair`
2. Final proof and completed-stage validation.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed prepared-stage validator on 2026-05-30`
- Execution status: `Completed`
- Subbundle gate review: `SB01 passed`
- Final closure gate: `Passed completed-stage validator on 2026-05-30`
- Browser validation analytics: `N/A - no browser-visible behavior change`
